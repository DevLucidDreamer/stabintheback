"""Run with portable Blender: blender -b source.blend --python this.py -- output.mesh.json.
Evaluates modifiers and object transforms using Blender itself; runtime needs no Blender.
"""
import bpy
import json
import sys

output = sys.argv[sys.argv.index('--') + 1]
parts = []
depsgraph = bpy.context.evaluated_depsgraph_get()
for obj in bpy.context.scene.objects:
    if obj.type != 'MESH' or obj.hide_render:
        continue
    evaluated = obj.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    mesh.calc_loop_triangles()
    materials = []
    for material in mesh.materials:
        color = list(material.diffuse_color) if material else [0.7, 0.6, 0.3, 1]
        metallic, roughness = 0.0, 0.5
        if material and material.use_nodes:
            node = next((n for n in material.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
            if node:
                color = list(node.inputs['Base Color'].default_value)
                metallic = node.inputs['Metallic'].default_value
                roughness = node.inputs['Roughness'].default_value
        materials.append(dict(name='Chalice_' + (material.name if material else 'Default'),
                              color=color, metallic=metallic, smoothness=1 - roughness))
    if not materials:
        materials = [dict(name='Chalice_Default', color=[0.7, 0.6, 0.3, 1], metallic=0.2, smoothness=0.5)]
    part = dict(name=obj.name, vertices=[], normals=[], uvs=[], materials=materials,
                submeshes=[dict(triangles=[]) for _ in materials])
    normal_matrix = evaluated.matrix_world.to_3x3().inverted().transposed()
    uv = mesh.uv_layers.active
    for tri in mesh.loop_triangles:
        base = len(part['vertices']) // 3
        for loop_index in tri.loops:
            loop = mesh.loops[loop_index]
            position = evaluated.matrix_world @ mesh.vertices[loop.vertex_index].co
            normal = (normal_matrix @ mesh.corner_normals[loop_index].vector).normalized()
            part['vertices'].extend([position.x, position.z, position.y])
            part['normals'].extend([normal.x, normal.z, normal.y])
            part['uvs'].extend(list(uv.data[loop_index].uv) if uv else [0, 0])
        part['submeshes'][min(tri.material_index, len(materials) - 1)]['triangles'].extend([base, base + 2, base + 1])
    evaluated.to_mesh_clear()
    if part['vertices']:
        parts.append(part)
if not parts:
    raise RuntimeError('No renderable chalice meshes found')
with open(output, 'w', encoding='utf-8') as handle:
    json.dump(dict(name='ChaliceBottle', parts=parts), handle, separators=(',', ':'))
print('CHALICE_EXPORT_OK', len(parts), 'parts', sum(len(p['vertices']) // 9 for p in parts), 'triangles')
