"""Convert Blender 5.0 .blend meshes into a compact JSON that a Unity editor
script turns into Mesh/Material assets.

Blender is not installed here, so Unity's .blend -> FBX pipeline is unavailable.
This reads the .blend container directly (see blendparse.Blend).

Output is already in Unity space: Blender (x, y, z) Z-up right-handed
becomes (x, z, y) Y-up left-handed, with triangle winding reversed to keep
faces pointing outward.
"""
import json
import math
import os
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from blendparse import Blend


class Reader:
    def __init__(self, blend):
        self.b = blend

    # ------------------------------------------------------------ raw helpers

    def block(self, ptr):
        return self.b.by_ptr.get(ptr)

    def raw(self, ptr):
        bl = self.block(ptr)
        if bl is None:
            return None
        return self.b.data[bl["start"]:bl["start"] + bl["len"]]

    def cstr(self, ptr):
        r = self.raw(ptr)
        return None if r is None else r.split(b"\x00")[0].decode("utf-8", "replace")

    # ------------------------------------------------------------ attributes

    def mesh_attributes(self, si, so):
        """name -> (raw bytes, element count) for every array attribute."""
        b = self.b
        st_off = b.layout[si]["attribute_storage"][0]
        sl = b.layout[b.struct_by_name["AttributeStorage"]]
        arr_ptr = struct.unpack_from(b.e + "Q", b.data, so + st_off + sl["dna_attributes"][0])[0]
        num = struct.unpack_from(b.e + "i", b.data, so + st_off + sl["dna_attributes_num"][0])[0]

        blk = self.block(arr_ptr)
        if blk is None:
            return {}

        Al = b.layout[b.struct_by_name["Attribute"]]
        stride = sum(v[3] for v in Al.values())
        Arl = b.layout[b.struct_by_name["AttributeArray"]]

        out = {}
        for i in range(num):
            p = blk["start"] + i * stride
            name = self.cstr(struct.unpack_from(b.e + "Q", b.data, p + Al["name"][0])[0])
            storage = struct.unpack_from(b.e + "b", b.data, p + Al["storage_type"][0])[0]
            dptr = struct.unpack_from(b.e + "Q", b.data, p + Al["data"][0])[0]
            if storage != 0:
                continue  # AttributeSingle - a constant, none of them matter here
            db = self.block(dptr)
            if db is None:
                continue
            data_ptr = struct.unpack_from(b.e + "Q", b.data, db["start"] + Arl["data"][0])[0]
            size = struct.unpack_from(b.e + "q", b.data, db["start"] + Arl["size"][0])[0]
            payload = self.raw(data_ptr)
            if payload is not None:
                out[name] = (payload, size)
        return out

    # ------------------------------------------------------------ materials

    def material(self, ptr):
        bl = self.block(ptr)
        if bl is None:
            return None
        si, so = bl["sdna"], bl["start"]
        name = self.b.id_name(si, so)[2:]
        r, g, bb, a = self.b.field(si, so, "r", "4f")
        (metallic,) = self.b.field(si, so, "metallic", "f")
        (roughness,) = self.b.field(si, so, "roughness", "f")
        return {
            "name": name,
            "color": [r, g, bb, a],
            "metallic": metallic,
            "smoothness": max(0.0, 1.0 - roughness),
        }

    def mesh_materials(self, si, so):
        (totcol,) = self.b.field(si, so, "totcol", "h")
        matptr = self.b.field(si, so, "mat")
        if not totcol or not matptr:
            return []
        bl = self.block(matptr)
        if bl is None:
            return []
        ptrs = struct.unpack_from(self.b.e + f"{totcol}Q", self.b.data, bl["start"])
        return [self.material(p) for p in ptrs]


def obj_matrix(b, si, so):
    """Local transform of an object, as (loc, quat_or_euler, scale, rotmode)."""
    loc = b.field(si, so, "loc", "3f")
    size = b.field(si, so, "size", "3f")
    (rotmode,) = b.field(si, so, "rotmode", "h")
    if rotmode == 0:  # quaternion (w, x, y, z)
        quat = b.field(si, so, "quat", "4f")
        w, x, y, z = quat
    else:  # euler XYZ (other orders are unused by these assets)
        ex, ey, ez = b.field(si, so, "rot", "3f")
        cx, sx = math.cos(ex / 2), math.sin(ex / 2)
        cy, sy = math.cos(ey / 2), math.sin(ey / 2)
        cz, sz = math.cos(ez / 2), math.sin(ez / 2)
        w = cx * cy * cz + sx * sy * sz
        x = sx * cy * cz - cx * sy * sz
        y = cx * sy * cz + sx * cy * sz
        z = cx * cy * sz - sx * sy * cz

    # rotation matrix from quaternion
    n = math.sqrt(w * w + x * x + y * y + z * z) or 1.0
    w, x, y, z = w / n, x / n, y / n, z / n
    rot = [
        [1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
        [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
        [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)],
    ]
    m = [[rot[r][c] * size[c] for c in range(3)] for r in range(3)]
    return m, loc


def apply(m, t, v):
    return (
        m[0][0] * v[0] + m[0][1] * v[1] + m[0][2] * v[2] + t[0],
        m[1][0] * v[0] + m[1][1] * v[1] + m[1][2] * v[2] + t[1],
        m[2][0] * v[0] + m[2][1] * v[1] + m[2][2] * v[2] + t[2],
    )


def to_unity(v):
    """Blender Z-up right-handed -> Unity Y-up left-handed."""
    return (v[0], v[2], v[1])


def normalize(v):
    L = math.sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2])
    if L < 1e-12:
        return (0.0, 1.0, 0.0)
    return (v[0] / L, v[1] / L, v[2] / L)


def convert_mesh(rd, me_blk, xform):
    b = rd.b
    si, so = me_blk["sdna"], me_blk["start"]
    (totvert,) = b.field(si, so, "totvert", "i")
    (totpoly,) = b.field(si, so, "totpoly", "i")
    (totloop,) = b.field(si, so, "totloop", "i")

    attrs = rd.mesh_attributes(si, so)
    if "position" not in attrs or ".corner_vert" not in attrs:
        return None

    pos_raw, _ = attrs["position"]
    positions = [struct.unpack_from(b.e + "3f", pos_raw, i * 12) for i in range(totvert)]

    cv_raw, _ = attrs[".corner_vert"]
    corner_vert = struct.unpack_from(b.e + f"{totloop}i", cv_raw, 0)

    offs_raw = rd.raw(b.field(si, so, "poly_offset_indices"))
    offsets = struct.unpack_from(b.e + f"{totpoly + 1}i", offs_raw, 0)

    mat_index = [0] * totpoly
    if "material_index" in attrs:
        mi_raw, _ = attrs["material_index"]
        mat_index = list(struct.unpack_from(b.e + f"{totpoly}i", mi_raw, 0))

    uvs = None
    for key in ("UVMap", "uv_map", "UVMap.001"):
        if key in attrs:
            uv_raw, _ = attrs[key]
            uvs = [struct.unpack_from(b.e + "2f", uv_raw, i * 8) for i in range(totloop)]
            break
    if uvs is None:
        for key, (raw, size) in attrs.items():
            if size == totloop and len(raw) == totloop * 8 and not key.startswith("."):
                uvs = [struct.unpack_from(b.e + "2f", raw, i * 8) for i in range(totloop)]
                break

    sharp = [False] * totpoly
    if "sharp_face" in attrs:
        sr, _ = attrs["sharp_face"]
        sharp = [sr[i] != 0 for i in range(totpoly)]

    m, t = xform
    world = [to_unity(apply(m, t, p)) for p in positions]

    # Face normals (in Unity space, using the reversed winding we will emit).
    face_normal = []
    for f in range(totpoly):
        a, bnd = offsets[f], offsets[f + 1]
        n = (0.0, 0.0, 0.0)
        cnt = bnd - a
        # Newell's method - robust for n-gons and slightly concave faces.
        for k in range(cnt):
            p0 = world[corner_vert[a + k]]
            p1 = world[corner_vert[a + (k + 1) % cnt]]
            n = (
                n[0] + (p0[1] - p1[1]) * (p0[2] + p1[2]),
                n[1] + (p0[2] - p1[2]) * (p0[0] + p1[0]),
                n[2] + (p0[0] - p1[0]) * (p0[1] + p1[1]),
            )
        # Winding is reversed on emit, so the normal flips too.
        face_normal.append(normalize((-n[0], -n[1], -n[2])))

    # Smooth normal per source vertex, averaged over the non-sharp faces using it.
    smooth = [(0.0, 0.0, 0.0)] * totvert
    for f in range(totpoly):
        if sharp[f]:
            continue
        fn = face_normal[f]
        for k in range(offsets[f], offsets[f + 1]):
            v = corner_vert[k]
            s = smooth[v]
            smooth[v] = (s[0] + fn[0], s[1] + fn[1], s[2] + fn[2])
    smooth = [normalize(s) for s in smooth]

    # Emit unwelded corners so flat/smooth shading and per-corner UVs both work.
    verts, norms, uv_out = [], [], []
    submeshes = {}
    for f in range(totpoly):
        a, bnd = offsets[f], offsets[f + 1]
        cnt = bnd - a
        if cnt < 3:
            continue
        base = len(verts)
        for k in range(cnt):
            c = a + k
            v = corner_vert[c]
            verts.append(world[v])
            norms.append(face_normal[f] if sharp[f] else smooth[v])
            uv_out.append(uvs[c] if uvs else (0.0, 0.0))

        tris = submeshes.setdefault(mat_index[f], [])
        for k in range(1, cnt - 1):
            # reversed winding: Blender CCW -> Unity front-facing
            tris.extend((base, base + k + 1, base + k))

    slots = rd.mesh_materials(si, so)
    max_slot = max(submeshes) if submeshes else 0
    while len(slots) <= max_slot:
        slots.append({"name": "Default", "color": [0.8, 0.8, 0.8, 1.0],
                      "metallic": 0.0, "smoothness": 0.25})

    # Unity's JsonUtility cannot read nested arrays, so each submesh is an object.
    return {
        "vertices": [round(c, 6) for v in verts for c in v],
        "normals": [round(c, 5) for v in norms for c in v],
        "uvs": [round(c, 5) for v in uv_out for c in v],
        "submeshes": [{"triangles": submeshes.get(i, [])} for i in range(len(slots))],
        "materials": slots,
    }


def convert(path):
    b = Blend(path)
    rd = Reader(b)
    name = os.path.splitext(os.path.basename(path))[0]

    parts = []
    for ob in b.by_code.get(b"OB", []):
        si, so = ob["sdna"], ob["start"]
        data_ptr = b.field(si, so, "data")
        me = rd.block(data_ptr)
        if me is None or me["code"] != b"ME":
            continue
        part = convert_mesh(rd, me, obj_matrix(b, si, so))
        if part:
            part["name"] = b.id_name(si, so)[2:]
            parts.append(part)

    if not parts:  # objects may be absent in a data-only .blend
        for me in b.by_code.get(b"ME", []):
            part = convert_mesh(rd, me, ([[1, 0, 0], [0, 1, 0], [0, 0, 1]], (0, 0, 0)))
            if part:
                part["name"] = b.id_name(me["sdna"], me["start"])[2:]
                parts.append(part)

    return {"name": name, "blenderVersion": b.version, "parts": parts}


def main():
    src_dir, out_dir = sys.argv[1], sys.argv[2]
    os.makedirs(out_dir, exist_ok=True)
    summary = []
    for f in sorted(os.listdir(src_dir)):
        if not f.endswith(".blend"):
            continue
        path = os.path.join(src_dir, f)
        try:
            doc = convert(path)
        except Exception as ex:  # keep going - report at the end
            summary.append((f, "FAILED: %s" % ex))
            continue
        out = os.path.join(out_dir, os.path.splitext(f)[0] + ".mesh.json")
        with open(out, "w", encoding="utf-8") as fh:
            json.dump(doc, fh, separators=(",", ":"))
        tri = sum(len(s["triangles"]) // 3 for p in doc["parts"] for s in p["submeshes"])
        vt = sum(len(p["vertices"]) // 3 for p in doc["parts"])
        summary.append((f, f"{len(doc['parts'])} part(s), {vt} verts, {tri} tris, "
                           f"mats={[m['name'] for p in doc['parts'] for m in p['materials']]}"))
    for f, s in summary:
        print(f"{f:32s} {s}")


if __name__ == "__main__":
    main()
