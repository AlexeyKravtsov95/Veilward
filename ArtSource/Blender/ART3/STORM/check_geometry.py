"""Проверяет сохранённую, фактическую STORM geometry; не вызывает author/export."""
import bpy
import json
import math
from pathlib import Path

FOLDER = Path(__file__).resolve().parent
ROOT = FOLDER.parents[3]
records = []
required = {'FoundationStone','CurvedCarpBody','CarpHead','RoundedSnout','BronzeLips','CarpEye','FrontBarbel','RearBarbel','TailUpperLobe','TailLowerLobe','DorsalFin','NearPectoralFin','StormSeal'}
for level in (1, 2, 3):
    name = f'Tower_StormCarpL{level}'
    bpy.ops.wm.open_mainfile(filepath=str(FOLDER / (name + '.blend')))
    objects = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
    assert len(objects) == 1
    obj = objects[0]
    mesh = obj.data
    mesh.calc_loop_triangles()
    assert obj.name == name
    assert bpy.context.scene.unit_settings.system == 'METRIC'
    assert bpy.context.scene.unit_settings.scale_length == 1
    assert obj.location.length < 1e-6 and all(abs(v-1) < 1e-6 for v in obj.scale)
    assert all(abs(v) < 1e-6 for v in obj.rotation_euler)
    assert len(mesh.materials) == 2
    assert [m.name for m in mesh.materials] == ['Art1Surface', 'Art1Glow']
    assert 0 < len(mesh.loop_triangles) <= 5000
    assert len(mesh.uv_layers) == 1
    assert all(face.area > 1e-10 for face in mesh.polygons)
    assert all((mesh.vertices[t.vertices[1]].co-mesh.vertices[t.vertices[0]].co).cross(
        mesh.vertices[t.vertices[2]].co-mesh.vertices[t.vertices[0]].co).length > 1e-10 for t in mesh.loop_triangles)
    assert all(math.isfinite(c) for v in mesh.vertices for c in v.co)
    assert all(abs(v.normal.length - 1) < 1e-5 for v in mesh.vertices)
    assert all(0 < c < 1 for uv in mesh.uv_layers.active.data for c in uv.uv)
    for uv in mesh.uv_layers.active.data:
        assert abs(uv.uv.x*8 % 1 - .5) < 1e-5 and abs(uv.uv.y*4 % 1 - .5) < 1e-5
    bounds = [[min(v.co[i] for v in mesh.vertices), max(v.co[i] for v in mesh.vertices)] for i in range(3)]
    assert abs(bounds[2][0]) < 1e-6
    assert max(abs(c) for axis in bounds[:2] for c in axis) <= .47
    groups = {group.name for group in obj.vertex_groups}
    assert required <= groups
    if level >= 2:
        assert {'OpenLeftFin','OpenRightFin','FinLightningLeft','FinLightningRight'} <= groups
    if level == 3:
        assert {'OpenStormArcLeft','OpenStormArcRight','CrownFin'} <= groups
    def points(group):
        index=obj.vertex_groups[group].index
        return [v.co for v in mesh.vertices if any(g.group == index for g in v.groups)]
    body=points('CurvedCarpBody')
    assert max(v.z for v in body)-min(v.z for v in body)>.6
    assert max(v.z for v in points('CarpHead')) > (1.82 if level==3 else 1.53 if level==2 else 1.14)
    assert len(points('TailUpperLobe'))>=6 and len(points('TailLowerLobe'))>=6
    middle=[(v.co.x-v.co.y)/2**.5 for v in mesh.vertices if .75<=v.co.z<=1.60]
    glow_faces=sum(face.material_index==1 for face in mesh.polygons)
    assert glow_faces>0
    records.append(dict(level=level, source=str((FOLDER/(name+'.blend')).relative_to(ROOT)),
        fbx='Assets/_Game/Art/Models/ART3/STORM/'+name+'.fbx', vertices=len(mesh.vertices),
        triangles=len(mesh.loop_triangles), materialSlots=2, bounds=bounds,
        middleProjectedWidth=max(middle)-min(middle), semanticGroups=sorted(groups), headCenter=[sum(v[i] for v in points('CarpHead'))/len(points('CarpHead')) for i in range(3)],
        glowFaces=glow_faces, appliedTransforms=True, zeroAreaTriangles=0,
        packedPalette=any(i.packed_file for i in bpy.data.images)))
assert len(set(r['triangles'] for r in records)) == 3
assert records[1]['headCenter'][2] > records[0]['headCenter'][2] + .38
assert records[2]['headCenter'][2] > records[1]['headCenter'][2] + .28
assert records[2]['bounds'][2][1] > records[1]['bounds'][2][1] + .35
(FOLDER/'geometry-manifest.json').write_text(json.dumps(records, indent=2)+'\n')
print('ART3F source geometry PASS: 3 editable levels, original carp head/body/tail/fins/open storm arcs, budgets/pivot/UV/normals/groups')
