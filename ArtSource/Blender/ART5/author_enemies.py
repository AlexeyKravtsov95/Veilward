"""Оригинальные ART5 духи; procedural authoring редактируемой геометрии/rigid animation.
Input только собственная ART1 palette, внешних моделей/референсов нет.
"""
import bpy, math, json
from mathutils import Matrix
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
HERE=Path(__file__).resolve().parent
MODELS=ROOT/'Assets/_Game/Art/Models/ART5'
MODELS.mkdir(parents=True,exist_ok=True)
bpy.context.preferences.filepaths.save_version=0
CLIPS={'Idle':(1,31),'Move':(32,62),'Hit':(63,75),'Death':(76,94),'Leak':(95,113)}

def material(name):
 m=bpy.data.materials.get(name) or bpy.data.materials.new(name);m.use_nodes=True
 tex=m.node_tree.nodes.new('ShaderNodeTexImage')
 tex.image=bpy.data.images.load(str(ROOT/'Assets/_Game/Art/Textures/ART1/Art1Palette_BaseColor_128.png'),check_existing=True);tex.image.pack();tex.interpolation='Closest'
 bs=m.node_tree.nodes.get('Principled BSDF');m.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color']);bs.inputs['Roughness'].default_value=.85
 if name=='Art1Glow':m.node_tree.links.new(tex.outputs['Color'],bs.inputs['Emission Color']);bs.inputs['Emission Strength'].default_value=1
 return m

def finish(o,c,glow=False):
 bpy.context.view_layer.objects.active=o;bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
 o.data.materials.clear();o.data.materials.append(MATS[int(glow)])
 uv=o.data.uv_layers.get('UVMap') or o.data.uv_layers.new(name='UVMap')
 for p in o.data.polygons:
  for i in p.loop_indices:uv.data[i].uv=((c%8+.5)/8,(c//8+.5)/4)
 return o

def ell(name,pos,scale,c,glow=False):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=12,ring_count=6,radius=1,location=pos);o=bpy.context.object;o.name=name;o.scale=scale;return finish(o,c,glow)
def cone(name,pos,r1,r2,h,c,n=8):
 bpy.ops.mesh.primitive_cone_add(vertices=n,radius1=r1,radius2=r2,depth=h,location=pos);o=bpy.context.object;o.name=name;return finish(o,c)
def box(name,pos,scale,c,glow=False):
 bpy.ops.mesh.primitive_cube_add(size=1,location=pos);o=bpy.context.object;o.name=name;o.scale=scale;return finish(o,c,glow)
def hull(name,vs,fs,c):
 m=bpy.data.meshes.new(name);m.from_pydata(vs,[],fs);m.update();o=bpy.data.objects.new(name,m);bpy.context.collection.objects.link(o);finish(o,c)
 bpy.context.view_layer.objects.active=o;o.select_set(True);bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT');o.select_set(False);return o

def join(name,parts,pivot):
 bpy.ops.object.select_all(action='DESELECT')
 for o in parts:o.select_set(True)
 bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();o=parts[0];o.name=name;o.data.name=name+'Mesh';bpy.context.scene.cursor.location=pivot;bpy.ops.object.origin_set(type='ORIGIN_CURSOR');return o

def cloak(boss):
 # Closed, outward hull with angular fluttering lower hem, not a primitive capsule.
 n=12;vs=[];fs=[]
 rings=[(.16,.25,.32),(.54,.40,.29),(.94,.30,.24),(1.15,.16,.14)] if not boss else [(.18,.38,.30),(.62,.47,.31),(1.15,.42,.27),(1.38,.23,.21)]
 for k,(z,rx,ry) in enumerate(rings):
  for i in range(n):
   a=i*math.tau/n;vs.append((math.cos(a)*rx,math.sin(a)*ry,z+(.075*(i%2) if k==0 else 0)))
 fs.append(tuple(reversed(range(n))))
 for k in range(len(rings)-1):
  for i in range(n):fs.append((k*n+i,k*n+(i+1)%n,(k+1)*n+(i+1)%n,(k+1)*n+i))
 fs.append(tuple(range((len(rings)-1)*n,len(rings)*n)))
 o=hull('Mantle',vs,fs,5)
 uv=o.data.uv_layers.active
 for p in o.data.polygons:
  c=16 if p.center.z<.45 else 5
  for li in p.loop_indices:uv.data[li].uv=((c%8+.5)/8,(c//8+.5)/4)
 return o

records=[]
for boss in (False,True):
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
 bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
 bpy.context.scene.render.fps=30;bpy.context.scene.frame_start=1;bpy.context.scene.frame_end=113
 MATS=[material('Art1Surface'),material('Art1Glow')]
 # Remove duplicate materials so stable FBX remap names remain exact.
 for old,name in zip(MATS,('Art1Surface','Art1Glow')):
  previous=bpy.data.materials.get(name)
  if previous is not old and previous.users==0:bpy.data.materials.remove(previous)
  old.name=name
 parts=[];body=[cloak(boss)]
 if boss:
  for s in (-1,1):
   body += [ell('Pauldron',(.53*s,0,1.13),(.31,.28,.24),8),ell('PauldronInset',(.57*s,-.09,1.22),(.24,.24,.13),0),cone('ArmorSpike',(.71*s,0,1.38),.13,0,.40,8,5)]
  body += [ell('BreastSeal',(0,-.285,.95),(.29,.09,.31),8),ell('SealCore',(0,-.37,.95),(.13,.035,.16),20,True)]
 else:
  body += [ell('Cowl',(0,0,1.05),(.29,.23,.23),5)]
 body=join('Mantle',body,(0,0,.65));parts.append(body)
 z=1.39 if boss else 1.09
 mask=[ell('IvoryMask',(0,-.21,z),(.23 if boss else .16,.12,.28 if boss else .19),0)]
 for s in (-1,1):
  mask.append(box('RiftEye',(.085*s if boss else .057*s,-.325 if boss else -.323,z+.045),(.10 if boss else .055,.018,.033),20,True))
 mask.append(box('MaskSplit',(0,-.333,z-.09),(.025,.022,.16),5))
 if boss:
  # Freestanding split crown arms, separated from head and each other.
  for s in (-1,1):
   mask += [cone('CrownTine',(.29*s,-.03,1.65),.08,0,.47,8,5),ell('CrownSeal',(.25*s,-.08,1.48),(.11,.09,.12),0)]
 else:
  for s in (-1,1):mask.append(cone('CowlHorn',(.18*s,.02,1.25),.07,0,.24,5,5))
 parts.append(join('MaskCrown',mask,(0,0,z)))
 for s in (-1,1):
  if boss:
   ribbon=[box('PendantCord',(.58*s,-.22,.79),(.035,.035,.48),8),box('SealTablet',(.58*s,-.22,.48),(.19,.08,.25),0),box('TabletMark',(.58*s,-.265,.48),(.055,.018,.13),20,True)]
   pivot=(.58*s,-.22,1.03)
  else:
   ribbon=[hull('TrailingFold',[(s*.21,.09,.76),(s*.34,.05,.55),(s*.26,.13,.08),(s*.10,.13,.16),(s*.17,.18,.70),(s*.29,.15,.52)],[(0,1,2,3),(0,4,5,1),(1,5,2),(2,5,4,3),(3,4,0)],16)]
   pivot=(s*.21,.09,.76)
  parts.append(join('PendantLeft' if s<0 else 'PendantRight',ribbon,pivot))
 # Author independent full transform tracks in a single editable timeline.
 bases={o.name:(o.location.copy(),o.rotation_euler.copy()) for o in parts}
 for name,(lo,hi) in CLIPS.items():
  for frame in range(lo,hi+1):
   t=(frame-lo)/(hi-lo)
   for idx,o in enumerate(parts):
    loc,rot=bases[o.name];o.location=loc.copy();o.rotation_euler=rot.copy();o.scale=(1,1,1)
    if name in ('Idle','Move'):
     amp=.035 if name=='Idle' else .070;phase=t*math.tau
     o.location.z+=amp*math.sin(phase)
     o.rotation_euler.y=(.025 if idx<2 else .14)*math.sin(phase+idx*math.pi)
     if idx>=2:o.rotation_euler.x=(.08 if name=='Idle' else .25)*math.sin(phase+idx)
    elif name=='Hit':
     kick=math.sin(t*math.pi);o.location.y+=.12*kick;o.rotation_euler.x=-.23*kick;o.rotation_euler.z=(.08 if idx<2 else .25)*kick
    elif name=='Death':
     o.location.z-=.42*t;o.rotation_euler.y+=t*(.7 if idx<2 else 1.3);o.rotation_euler.x+=t*.7;o.scale=(max(.02,1-t),)*3
    elif name=='Leak':
     o.location.z+=.48*t;o.rotation_euler.z+=t*(1 if idx%2 else -1);o.scale=(max(.02,1-t),)*3
    for path in ('location','rotation_euler','scale'):o.keyframe_insert(data_path=path,frame=frame,group=name)
 # Canonical source forward +Y maps to Unity +Z with this FBX preset.
 # Proper180degree rotation of geometry AND articulated animation, without negative scale.
 for o in parts:
  o.data.transform(Matrix.Rotation(math.pi,4,'Z'));o.data.update()
  action=o.animation_data.action
  for layer in action.layers:
   for strip in layer.strips:
    for bag in strip.channelbags:
     for curve in bag.fcurves:
      if curve.data_path in ('location','rotation_euler') and curve.array_index in (0,1):
       for key in curve.keyframe_points:
        key.co.y=-key.co.y;key.handle_left.y=-key.handle_left.y;key.handle_right.y=-key.handle_right.y
       curve.update()
  o.update_tag()
 bpy.context.scene.frame_set(2);bpy.context.scene.frame_set(1)
 name='Enemy_RiftWarden' if boss else 'Enemy_Shade'
 bpy.ops.wm.save_as_mainfile(filepath=str(HERE/(name+'.blend')),compress=True)
 # Same exporter supports manual edits without rerunning this author script.
 exec(compile((HERE/'export_enemies.py').read_text(),str(HERE/'export_enemies.py'),'exec'),{'__file__':str(HERE/'export_enemies.py')})
 tri=0
 for o in parts:o.data.calc_loop_triangles();tri+=len(o.data.loop_triangles)
 records.append(dict(asset=name,triangles=tri,meshes=len(parts),renderers=len(parts),skinBones=0,weights=0,materialSlots=sum(len(o.data.materials) for o in parts),uniqueMaterials=sorted({m.name for o in parts for m in o.data.materials}),clips={n:dict(first=a,last=b,duration=(b-a)/30) for n,(a,b) in CLIPS.items()}))
(HERE/'geometry-manifest.json').write_text(json.dumps(records,indent=2)+'\n')
print(json.dumps(records))
