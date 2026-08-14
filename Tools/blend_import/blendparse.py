"""Minimal .blend reader: zstd-decompress, parse SDNA, walk datablocks.

Blender is not installed on this machine, so Unity cannot import .blend directly.
This reads the file format itself well enough to pull meshes + material colors out.
"""
import struct
import sys
from compression import zstd


def read_blend_bytes(path):
    raw = open(path, "rb").read()
    if raw[:7] == b"BLENDER":
        return raw
    if raw[:4] == b"\x28\xb5\x2f\xfd":
        return zstd.decompress(raw)
    # gzip legacy
    if raw[:2] == b"\x1f\x8b":
        import gzip
        return gzip.decompress(raw)
    raise ValueError("unknown blend container: " + repr(raw[:8]))


class Blend:
    def __init__(self, path):
        data = read_blend_bytes(path)
        if data[:7] != b"BLENDER":
            raise ValueError("bad header " + repr(data[:12]))
        self.data = data

        if data[7:9].isdigit():
            # Blender 5.0 header: BLENDER <hdrsize:2> <ptr> <fileformat:2> <endian> <version:4>
            self.header_size = int(data[7:9])
            self.ptr_size = 8 if data[9:10] == b"-" else 4
            self.file_format = int(data[10:12])
            self.e = "<" if data[12:13] == b"v" else ">"
            self.version = data[13:self.header_size].decode()
            # code[4], SDNA_nr(i32), old(u64), len(u64), nr(u64)
            self.bhead = struct.Struct(self.e + "4siQQQ")
            self._bhead_order = "new"
        else:
            # Legacy header: BLENDER <ptr> <endian> <version:3>
            self.header_size = 12
            self.ptr_size = 8 if data[7:8] == b"-" else 4
            self.file_format = 0
            self.e = "<" if data[8:9] == b"v" else ">"
            self.version = data[9:12].decode()
            # code[4], len(i32), old(ptr), SDNA_nr(i32), nr(i32)
            self.bhead = struct.Struct(self.e + "4si" + ("Q" if self.ptr_size == 8 else "I") + "ii")
            self._bhead_order = "old"

        self.blocks = []          # list of dicts
        self.by_ptr = {}          # old pointer -> block
        self.by_code = {}         # b'OB' -> [blocks]
        self._scan_blocks()
        self._parse_dna()

    # ---------------------------------------------------------------- blocks

    def _scan_blocks(self):
        d = self.data
        off = self.header_size
        hdr = self.bhead
        n = len(d)
        while off + hdr.size <= n:
            a, b, c, e_, f = hdr.unpack_from(d, off)
            if self._bhead_order == "new":
                code, sdna, old, length, count = a, b, c, e_, f
            else:
                code, length, old, sdna, count = a, b, c, e_, f
            body = off + hdr.size
            if code == b"ENDB":
                break
            blk = {
                "code": code.rstrip(b"\x00"),
                "start": body,
                "len": length,
                "old": old,
                "sdna": sdna,
                "count": count,
            }
            self.blocks.append(blk)
            if old:
                self.by_ptr[old] = blk
            self.by_code.setdefault(blk["code"], []).append(blk)
            off = body + length

    # ---------------------------------------------------------------- DNA

    def _parse_dna(self):
        dna_blocks = self.by_code.get(b"DNA1")
        if not dna_blocks:
            raise ValueError("no DNA1 block")
        b = dna_blocks[0]
        d = self.data
        p = b["start"]
        e = self.e
        assert d[p:p + 4] == b"SDNA", d[p:p + 4]
        p += 4

        base = p  # alignment inside the DNA block is relative to the block start

        def align4(x):
            return base + (((x - base) + 3) & ~3)

        assert d[p:p + 4] == b"NAME"
        p += 4
        (cnt,) = struct.unpack_from(e + "i", d, p)
        p += 4
        names = []
        for _ in range(cnt):
            end = d.index(b"\x00", p)
            names.append(d[p:end].decode())
            p = end + 1
        p = align4(p)

        assert d[p:p + 4] == b"TYPE", d[p:p + 4]
        p += 4
        (cnt,) = struct.unpack_from(e + "i", d, p)
        p += 4
        types = []
        for _ in range(cnt):
            end = d.index(b"\x00", p)
            types.append(d[p:end].decode())
            p = end + 1
        p = align4(p)

        assert d[p:p + 4] == b"TLEN", d[p:p + 4]
        p += 4
        tlens = list(struct.unpack_from(e + str(len(types)) + "h", d, p))
        p += 2 * len(types)
        p = align4(p)

        assert d[p:p + 4] == b"STRC", d[p:p + 4]
        p += 4
        (cnt,) = struct.unpack_from(e + "i", d, p)
        p += 4
        structs = []
        for _ in range(cnt):
            tidx, nfields = struct.unpack_from(e + "hh", d, p)
            p += 4
            fields = struct.unpack_from(e + str(nfields * 2) + "h", d, p)
            p += 4 * nfields
            structs.append((tidx, [(fields[i * 2], fields[i * 2 + 1]) for i in range(nfields)]))

        self.names = names
        self.types = types
        self.tlens = tlens
        self.structs = structs
        self.struct_by_name = {}
        for i, (tidx, _f) in enumerate(structs):
            self.struct_by_name[types[tidx]] = i

        # Precompute field offsets per struct.
        self.layout = {}   # struct index -> {field_name: (offset, type_name, raw_name, size)}
        for i, (tidx, fields) in enumerate(structs):
            off = 0
            m = {}
            for ftype, fname in fields:
                raw = names[fname]
                tname = types[ftype]
                size = self._field_size(tname, raw)
                m[self._clean_name(raw)] = (off, tname, raw, size)
                off += size
            self.layout[i] = m

    def _clean_name(self, raw):
        n = raw.lstrip("*")
        if "[" in n:
            n = n[:n.index("[")]
        if n.startswith("(") and n.endswith(")()"):
            n = n[1:-3].lstrip("*")
        return n

    def _field_size(self, tname, raw):
        if raw.startswith("*") or raw.startswith("(*"):
            base = self.ptr_size
        else:
            base = self.tlens[self.types.index(tname)]
        n = 1
        rest = raw
        while "[" in rest:
            i = rest.index("[")
            j = rest.index("]")
            n *= int(rest[i + 1:j])
            rest = rest[j + 1:]
        return base * n

    # ---------------------------------------------------------------- access

    def field(self, sdna_index, base_off, name, fmt=None):
        """Read a field of the struct at data offset base_off."""
        lay = self.layout[sdna_index]
        if name not in lay:
            return None
        off, tname, raw, size = lay[name]
        p = base_off + off
        d = self.data
        e = self.e
        if raw.startswith("*") or raw.startswith("(*"):
            return struct.unpack_from(e + ("Q" if self.ptr_size == 8 else "I"), d, p)[0]
        if fmt:
            return struct.unpack_from(e + fmt, d, p)
        return (p, tname, raw, size)

    def field_offset(self, sdna_index, name):
        lay = self.layout[sdna_index]
        return lay[name][0] if name in lay else None

    def id_name(self, sdna_index, base_off):
        """Every ID-carrying struct begins with `ID id;` whose `name[..]` we want."""
        idx = self.struct_by_name["ID"]
        noff = self.field_offset(idx, "name")
        p = base_off + noff
        end = self.data.index(b"\x00", p)
        return self.data[p:end].decode("utf-8", "replace")


if __name__ == "__main__":
    b = Blend(sys.argv[1])
    print("version", b.version, "ptr", b.ptr_size)
    print("codes:", {k.decode(): len(v) for k, v in sorted(b.by_code.items())})
    for code in (b"OB", b"ME", b"MA"):
        for blk in b.by_code.get(code, []):
            print(code.decode(), b.id_name(blk["sdna"], blk["start"]), "sdna=", b.types[b.structs[blk["sdna"]][0]])
    mi = b.struct_by_name["Mesh"]
    print("\nMesh fields:")
    for k, v in b.layout[mi].items():
        print("  ", k, v)
