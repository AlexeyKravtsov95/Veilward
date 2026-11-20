"""Проверка сохранённых sources, не генерация. Blender --background --python ..."""
import bpy
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
FOLDER = Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'ArtSource/Blender/ART1/Tower_FlameLantern.blend'))
baseline = bpy.data.objects['Tower_FlameLantern']
vertices = [tuple(v.co) for v in baseline.data.vertices]
roof = [tuple(v.co) for v in baseline.data.vertices if v.co.z >= 1.69]
records = []
for level in (1, 2, 3):
    name = f'Tower_FlameLanternL{level}'
    bpy.ops.wm.open_mainfile(filepath=str(FOLDER/(name+'.blend')))
    obj = bpy.data.objects[name]
    mesh = obj.data
    mesh.calc_loop_triangles()
    assert bpy.context.scene.unit_settings.scale_length == 1
    assert obj.location.length < 1e-6 and all(abs(v-1) < 1e-6 for v in obj.scale)
    assert all(abs(v) < 1e-6 for v in obj.rotation_euler)
    assert len(mesh.materials) == 2
    assert 0 < len(mesh.loop_triangles) <= 5000
    # Все принятые вершины (в том числе крыша/опоры/огонь) сохранены точно.
    assert [tuple(v.co) for v in list(mesh.vertices)[:len(vertices)]] == vertices
    assert len(mesh.uv_layers) == 1
    assert all(p.area > 0 for p in mesh.polygons)
    bounds = [[min(v.co[i] for v in mesh.vertices), max(v.co[i] for v in mesh.vertices)] for i in range(3)]
    zero_triangles = sum((mesh.vertices[t.vertices[1]].co-mesh.vertices[t.vertices[0]].co).cross(
        mesh.vertices[t.vertices[2]].co-mesh.vertices[t.vertices[0]].co).length < 1e-10 for t in mesh.loop_triangles)
    assert abs(bounds[2][0]) < 1e-6
    assert max(abs(x) for axis in bounds[:2] for x in axis) <= .471
    middle = [(v.co.x-v.co.y)/2**.5 for v in mesh.vertices if .8 <= v.co.z <= 1.65]
    middle_width = max(middle)-min(middle)
    if level >= 2:
        assert middle_width >= 1.15, "L2 open frame must expand across the gameplay camera, not along its depth"
    records.append(dict(level=level, middleProjectedWidth=middle_width, source=str((FOLDER/(name+'.blend')).relative_to(ROOT)),
        fbx='Assets/_Game/Art/Models/ART3/'+name+'.fbx', vertices=len(mesh.vertices),
        triangles=len(mesh.loop_triangles), materialSlots=len(mesh.materials), bounds=bounds,
        degenerateTessellationTriangles=zero_triangles, nondegenerateTriangles=len(mesh.loop_triangles)-zero_triangles,
        preservedBaselineVertices=len(vertices), preservedBaselineRoofVertices=len(roof),
        appliedTransforms=True, packedPalette=any(i.packed_file for i in bpy.data.images)))
assert len(set(r['triangles'] for r in records)) == 3
(FOLDER/'geometry-manifest.json').write_text(json.dumps(records, indent=2)+'\n')
print('ART3A source geometry PASS: 3 levels, full baseline preserved, budgets/pivot/UV/transforms checked')
