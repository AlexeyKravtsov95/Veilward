"""Оригинальный ART4 kit. Только геометрия и существующая ART1 palette, без внешних inputs."""
import bpy, math, random, json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
FOLDER=Path(__file__).resolve().parent
MODELS=ROOT/'Assets/_Game/Art/Models/ART4'
MODELS.mkdir(parents=True,exist_ok=True)
bpy.context.preferences.filepaths.save_version=0
palette=bpy.data.images.load(str(ROOT/'Assets/_Game/Art/Textures/ART1/Art1Palette_BaseColor_128.png'),check_existing=True)
palette.pack()
mats=[]
for name in ('Art1Surface','Art1Glow'):
 m=bpy.data.materials.get(name) or bpy.data.materials.new(name);m.use_nodes=True
 nodes=m.node_tree.nodes;nodes.clear();out=nodes.new('ShaderNodeOutputMaterial');bs=nodes.new('ShaderNodeBsdfPrincipled');tex=nodes.new('ShaderNodeTexImage');tex.image=palette;tex.interpolation='Closest'
 m.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color']);bs.inputs['Roughness'].default_value=.88
 if name.endswith('Glow'):m.node_tree.links.new(tex.outputs['Color'],bs.inputs['Emission Color']);bs.inputs['Emission Strength'].default_value=1
 m.node_tree.links.new(bs.outputs[0],out.inputs[0]);mats.append(m)
parts=[]
def start():
 global parts
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False);parts=[]
 bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1

def finish(o,color,bevel=0,glow=False):
 bpy.context.view_layer.objects.active=o;bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
 if bevel:
  m=o.modifiers.new('Authored bevel','BEVEL');m.width=bevel;m.segments=1;bpy.ops.object.modifier_apply(modifier=m.name)
 o.data.materials.clear();o.data.materials.append(mats[int(glow)])
 uv=o.data.uv_layers.get('UVMap') or o.data.uv_layers.new(name='UVMap')
 for p in o.data.polygons:
  for i in p.loop_indices:uv.data[i].uv=((color%8+.5)/8,(color//8+.5)/4)
 group=o.vertex_groups.new(name=o.name);group.add(list(range(len(o.data.vertices))),1,'REPLACE');parts.append(o);return o

def box(name,loc,dims,c,bevel=.01):
 bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.name=name;o.dimensions=dims;return finish(o,c,bevel)
def cone(name,loc,r1,r2,depth,c,n=8,glow=False):
 bpy.ops.mesh.primitive_cone_add(vertices=n,radius1=r1,radius2=r2,depth=depth,location=loc);o=bpy.context.object;o.name=name;return finish(o,c,glow=glow)
def mesh(name,vertices,faces,c):
 data=bpy.data.meshes.new(name);data.from_pydata(vertices,[],faces);data.update();o=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(o);return finish(o,c)
def band(name,outer,inner,z,c,n=8):
 vs=[(math.cos(a*math.tau/n+math.pi/8)*r,math.sin(a*math.tau/n+math.pi/8)*r,z) for r in (outer,inner) for a in range(n)]
 fs=[(i,(i+1)%n,n+(i+1)%n,n+i) for i in range(n)];return mesh(name,vs,fs,c)
def save(name):
 bpy.ops.object.select_all(action='DESELECT')
 for o in parts:o.select_set(True)
 bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();o=bpy.context.object;o.name=name;o.data.name=name+'Mesh'
 bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR');bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
 old=list(o.data.materials);used=[];assign=[]
 for p in o.data.polygons:
  m=old[p.material_index]
  if m not in used:used.append(m)
  assign.append(used.index(m))
 o.data.materials.clear()
 for m in used:o.data.materials.append(m)
 for p,i in zip(o.data.polygons,assign):p.material_index=i
 # Независимая проверка exporter читает эти semantic groups из editable source.
 bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
 bpy.ops.wm.save_as_mainfile(filepath=str(FOLDER/(name+'.blend')),compress=True)
 bpy.ops.export_scene.fbx(filepath=str(MODELS/(name+'.fbx')),use_selection=True,object_types={'MESH'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',use_space_transform=True,bake_space_transform=True,use_mesh_modifiers=True,mesh_smooth_type='FACE',use_triangles=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP',use_custom_props=False)
 o.data.calc_loop_triangles()
 return dict(asset=name,triangles=len(o.data.loop_triangles),vertices=len(o.data.vertices),slots=len(o.data.materials),semanticGroups=[g.name for g in o.vertex_groups],source=str((FOLDER/(name+'.blend')).relative_to(ROOT)),fbx=str((MODELS/(name+'.fbx')).relative_to(ROOT)))
records=[]
start()
# Squircle contour covers every playable corner, with irregular cliffs outside the mask.
rng=random.Random(445);n=32;vs=[]
contour=[]
for i in range(n):
 a=i*math.tau/n
 x=math.copysign(abs(math.cos(a))**.48,math.cos(a))*7.35
 y=math.copysign(abs(math.sin(a))**.48,math.sin(a))*5.40
 contour.append((x*(1+rng.uniform(-.04,.04)),y*(1+rng.uniform(-.04,.04))))
for k,(z,scale,dx,dy) in enumerate([(-.065,1,0,0),(-.35,1.015,0,0),(-1.35,1.0,.06,-.03),(-2.70,.94,-.10,.08),(-4.1,.70,-.2,.12)]):
 for x,y in contour:vs.append((x*scale-.5+dx,y*scale-.5+dy,z+(rng.uniform(-.13,.13) if k>1 else 0)))
fs=[tuple(range(n))]
for k in range(4):
 for i in range(n):fs.append((k*n+i,(k+1)*n+i,(k+1)*n+(i+1)%n,k*n+(i+1)%n))
fs.append(tuple(reversed(range(4*n,5*n))))
o=mesh('TerracedSuspendedRock',vs,fs,6)
uv=o.data.uv_layers.active
for p in o.data.polygons:
 c=6 if p.index==0 else ([2,28,27,5][min(3,(p.index-1)//n)] if p.index<len(fs)-1 else 16)
 if 1<=p.index<=n and p.index%4==0:c=1
 elif n<p.index<len(fs)-1 and p.index%5==0:c=4
 for li in p.loop_indices:uv.data[li].uv=((c%8+.5)/8,(c//8+.5)/4)
# Insets on upper perimeter echo warm sanctuary masonry; no top clutter.
for i in range(0,n,2):
 x,y=contour[i];cone('CliffBronzeSeal%02d'%i,(x*.988-.5,y*.988-.5,-.31),.12,.095,.09,8,6)
for i,(x,y,r,z) in enumerate([(-4.7,-2.5,.9,-3.6),(3.8,1.9,1.15,-3.4),(-1.3,1.1,1.2,-4.2)]):
 cone('RootSpire%02d'%i,(x,y,z),.05,r,1.6,5,7)
# Exposed split cliff teeth on the visible rim, joined only at their upper shoulders.
for i,(x,y,z,r) in enumerate([(5.9,1.7,-2.85,.70),(3.2,4.05,-3.0,.82),(-.5,4.45,-2.8,.62),(6.25,-1.8,-2.7,.56)]):
 cone('ExposedCliffTooth%02d'%i,(x,y,z),.06,r,2.6,5 if i%2 else 28,6)
# Moss shelves remain strictly outside the build rectangle; low, irregular garden edge.
for i,(x,y,sx,sy) in enumerate([(-7.1,-3.2,.55,.9),(-6.2,-5.15,1.1,.5),(5.95,-3.4,.6,.7),(4.3,4.1,1.1,.55),(-3.7,4.3,1.0,.6)]):
 o=cone('MossLedge%02d'%i,(x,y,-.025),.6,.54,.07,24 if i%2 else 27,7);o.scale=(sx,sy,1);bpy.context.view_layer.objects.active=o;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
records.append(save('Environment_SanctuaryIsland'))
start()
# Continuous paver: top .035, max width .90, longitudinal overlap .02m.
box('SandstoneSlab',(0,0,-.045),(.90,1.02,.09),1,.035)
box('WalkSurface',(0,0,.016),(.78,.96,.038),0,.012)
for x in (-.405,.405):box('BronzeEdge',(x,0,.032),(.026,.94,.006),8,0)
box('JointInlay',(0,.46,.036),(.69,.014,.004),2,0)
records.append(save('Environment_SanctuaryPath'))
start()
# Buildable cells carry low octagonal sockets. They stay below selection y=.04.
cone('SocketStone',(0,0,-.04),.455,.455,.07,4)
band('SocketBronzeRim',.445,.415,.012,10)
band('SocketCarvedLip',.414,.38,.013,7)
cone('SocketJadeCenter',(0,0,-.003),.38,.38,.036,6)
for x,y in ((-.29,-.29),(.29,-.29),(-.29,.29),(.29,.29)):
 box('BuildCornerRune',(x,y,.015),(.065,.035,.006),8,0)
records.append(save('Environment_BuildSocket'))
start()
# Entry: open doorway, two posts and swept lintel. Open silhouette, no collider.
cone('Threshold',(0,0,-.02),.46,.46,.08,2)
for x in (-.36,.36):
 box('EntryPlinth',(x,0,.07),(.15,.25,.14),1,.02)
 box('EntryPillar',(x,0,.52),(.085,.13,.82),5)
 box('EntryBronzeCollar',(x,0,.84),(.13,.17,.055),8)
 box('EntryIvoryCap',(x,0,1.0),(.15,.21,.13),0,.018)
box('EntryLintel',(0,0,1.12),(.90,.28,.13),5,.025)
box('EntryBronzeCoping',(0,0,1.205),(.94,.30,.045),8,.012)
for x in (-.40,.40):box('UpturnedEave',(x,0,1.24),(.18,.32,.055),8,.015)
# Chevron points into +Y Blender / +Z Unity along the route.
mesh('IngressArrow',[(-.13,-.27,.025),(0,-.08,.025),(.13,-.27,.025),(0,-.20,.025)],[(0,1,3),(1,2,3)],20)
records.append(save('Environment_SpawnGate'))
start()
cone('HeartPedestal',(0,0,.035),.46,.40,.07,2)
cone('HeartBronzeSeat',(0,0,.09),.34,.32,.04,8)
cone('HeartIvoryCushion',(0,0,.14),.28,.26,.06,0)
# Small coral faceted heart, deliberately lower than an enemy's HP/status anchors.
cone('HeartLower',(0,0,.28),.03,.20,.22,12,6,True)
cone('HeartCrown',(0,0,.49),.20,.025,.20,14,6,True)
for x,y in ((-.32,0),(.32,0),(0,-.32),(0,.32)):
 cone('HeartWard',(x,y,.22),.045,.025,.34,8,5)
records.append(save('Environment_SealHeart'))
start()
# Restrained cloud-pine silhouette on the far rim: original faceted geometry, one shared slot.
cone('PineRoot',(0,0,.04),.20,.10,.08,10,6)
cone('PineTrunk',(0,0,.47),.065,.040,.90,10,7)
for i,(x,y,z,r) in enumerate([(-.12,.02,.67,.36),(.16,-.03,.88,.35),(-.04,.04,1.10,.29)]):
 cone('PineCanopyLower%02d'%i,(x,y,z-.045),r*.82,r,.14,27,7)
 cone('PineCanopyCrown%02d'%i,(x,y,z+.085),r,r*.25,.20,24 if i%2 else 25,7)
records.append(save('Environment_RimPine'))
(FOLDER/'geometry-manifest.json').write_text(json.dumps(records,indent=2)+'\n')
print(json.dumps(records,indent=2))
