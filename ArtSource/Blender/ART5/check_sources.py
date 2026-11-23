"""Audit saved editable sources, not author-generated manifest assertions."""
import bpy, bmesh, json, math, hashlib
from pathlib import Path
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[2];records=[]
expected={'Enemy_Shade.blend':400,'Enemy_RiftWarden.blend':1312}
assert {p.name for p in HERE.glob('Enemy_*.blend')}==set(expected)
for filename,triangles in expected.items():
 p=HERE/filename;bpy.ops.wm.open_mainfile(filepath=str(p));scene=bpy.context.scene;scene.frame_set(1)
 assert scene.unit_settings.system=='METRIC' and scene.unit_settings.scale_length==1
 meshes=[o for o in scene.objects if o.type=='MESH'];assert len(meshes)==4
 assert not any(o.type=='ARMATURE' for o in scene.objects)
 parts=[];materials=set();count=0
 for o in meshes:
  m=o.data;m.calc_loop_triangles();count+=len(m.loop_triangles)
  materials.update(x.name for x in m.materials)
  assert m.uv_layers.active and len(m.uv_layers.active.data)==len(m.loops)
  assert all(math.isfinite(c) for v in m.vertices for c in v.co)
  assert all(math.isfinite(c) for poly in m.polygons for c in poly.normal)
  assert all(abs(v-1)<1e-6 for v in o.scale),'base pose scale not applied'
  assert o.animation_data and o.animation_data.action
  action=o.animation_data.action
  curves=[curve for layer in action.layers for strip in layer.strips for bag in strip.channelbags for curve in bag.fcurves]
  assert len(curves)==9 and all(len(c.keyframe_points)==113 for c in curves)
  assert all(tuple(c.range())==(1.0,113.0) for c in curves)
  bm=bmesh.new();bm.from_mesh(m);volume=bm.calc_volume(signed=True);bm.free();assert volume>0
  parts.append(dict(name=o.name,triangles=len(m.loop_triangles),vertices=len(m.vertices),materialSlots=len(m.materials),materials=[x.name for x in m.materials],pivot=list(o.location),scale=list(o.scale),rotation=list(o.rotation_euler),positiveSignedVolume=volume,animationCurves=len(curves),keyframes=sum(len(c.keyframe_points) for c in curves),bones=0,weights=len(o.vertex_groups)))
 assert count==triangles and materials=={'Art1Surface','Art1Glow'}
 records.append(dict(source=str(p.relative_to(ROOT)),sha256=hashlib.sha256(p.read_bytes()).hexdigest(),units=scene.unit_settings.system,meters=scene.unit_settings.scale_length,frameRange=[scene.frame_start,scene.frame_end],fps=scene.render.fps,triangles=count,parts=parts,uniqueSharedMaterials=sorted(materials),externalModels=False,skinBones=0))
(ROOT/'Docs/review/ART.5/reports/source-audit.json').write_text(json.dumps(dict(status='PASS',actualSavedSourceReadback=True,sources=records,sourceCount=len(records)),indent=2)+'\n')
print('PASS2savededitable sources/8parts/8actions/72curves/8136keys; actual meters/UV/normals/volume/materials/animation audited')
