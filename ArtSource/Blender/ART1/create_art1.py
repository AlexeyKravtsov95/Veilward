"""Original ART.1 geometry, deterministic; Blender 5.3. No downloaded inputs.
Run: Blender --background --python ArtSource/Blender/ART1/create_art1.py
Editing saved .blend and running export_art1.py preserves mesh/material names.
"""
import bpy, math, random, json
from pathlib import Path
from mathutils import Vector
ROOT = Path(__file__).resolve().parents[3]
SOURCE = ROOT/'ArtSource/Blender/ART1'
MODELS = ROOT/'Assets/_Game/Art/Models/ART1'
TEX = ROOT/'Assets/_Game/Art/Textures/ART1'
for d in (SOURCE, MODELS, TEX): d.mkdir(parents=True, exist_ok=True)
# Small hand-authored palette atlas; all faces use UVs inside a swatch (no filtering bleed).
COLORS = ['EADCC0','C8B798','AA9476','F9EBCD','293D40','192C33','3D5652','526962',
          'BC8C50','E9B86A','795B43','4C3B34','DA7052','FFB251','FFE7A0','FA7E3E',
          '24253F','343552','51516F','77728F','8CE2DB','D2FFF0','C9B8BB','978B9B',
          '628E80','80A28C','A5B299','415F5C','62777A','93A1A0','BECEBD','FFFFFF']
def rgb(h): return tuple(int(h[i:i+2],16)/255 for i in (0,2,4))
img=bpy.data.images.new('Art1Palette', width=128, height=64)
pixels=[]
for y in range(64):
    for x in range(128): pixels.extend((*rgb(COLORS[(y//16)*8+x//16]),1))
img.pixels=pixels; img.filepath_raw=str(TEX/'Art1Palette_BaseColor_128.png'); img.file_format='PNG'; img.save(); img.pack()
# Sources remain readable in Blender with exactly the palette used in Unity.
def materials():
    result=[]
    for name, glow in [('Art1Surface',False),('Art1Glow',True)]:
        m=bpy.data.materials.get(name) or bpy.data.materials.new(name); m.use_nodes=True
        n=m.node_tree.nodes; n.clear(); out=n.new('ShaderNodeOutputMaterial'); bs=n.new('ShaderNodeBsdfPrincipled'); t=n.new('ShaderNodeTexImage'); t.image=img; t.interpolation='Closest'
        m.node_tree.links.new(t.outputs['Color'],bs.inputs['Base Color']); bs.inputs['Roughness'].default_value=.85
        if glow: m.node_tree.links.new(t.outputs['Color'],bs.inputs['Emission Color']); bs.inputs['Emission Strength'].default_value=1.3
        m.node_tree.links.new(bs.outputs[0],out.inputs[0]); result.append(m)
    return result
MATS=materials(); parts=[]
def finish(o, color, glow=False, bevel=0):
    bpy.context.view_layer.objects.active=o
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    if bevel:
        mod=o.modifiers.new('EditableEdgeBevel','BEVEL'); mod.width=bevel; mod.segments=1
        bpy.ops.object.modifier_apply(modifier=mod.name)
    o.data.materials.clear(); o.data.materials.append(MATS[int(glow)])
    uv=o.data.uv_layers.get('UVMap') or o.data.uv_layers.new(name='UVMap')
    for p in o.data.polygons:
        # Large tonal planes remain deliberate, no noisy procedural textures.
        c=color
        for li in p.loop_indices: uv.data[li].uv=((c%8+.5)/8,(c//8+.5)/4)
    parts.append(o); return o

def box(name, loc, dims, color, bevel=.015):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc); o=bpy.context.object; o.name=name; o.dimensions=dims
    return finish(o,color,bevel=bevel)
def cone(name,loc,r1,r2,depth,color,n=8,glow=False):
    bpy.ops.mesh.primitive_cone_add(vertices=n,radius1=r1,radius2=r2,depth=depth,location=loc); o=bpy.context.object; o.name=name
    return finish(o,color,glow)
def mesh(name, verts, faces, color, glow=False):
    if name in ('IvoryVoidMask','EyeSocket','ColdEye','MaskSplit','TornFold'):
        faces=[tuple(reversed(f)) if (Vector(verts[f[1]])-Vector(verts[f[0]])).cross(Vector(verts[f[2]])-Vector(verts[f[0]])).y>0 else f for f in faces]
    me=bpy.data.meshes.new(name); me.from_pydata(verts,[],faces); me.update(); o=bpy.data.objects.new(name,me); bpy.context.collection.objects.link(o)
    return finish(o,color,glow)
def beam(name,a,b,width,color):
    a,b=Vector(a),Vector(b); o=box(name,(a+b)*.5,(width,width,(b-a).length),color,0)
    o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler(); bpy.context.view_layer.objects.active=o; bpy.ops.object.transform_apply(location=False,rotation=True,scale=True); return o

def rings(name, rs, n, color, glow=False, phase=0):
    vs=[]
    for z,r,x,y in rs:
        for j in range(n):
            a=j*math.tau/n+phase; vs.append((x+math.cos(a)*r,y+math.sin(a)*r,z))
    fs=[tuple(reversed(range(n)))]
    for k in range(len(rs)-1):
        for j in range(n): fs.append((k*n+j,k*n+(j+1)%n,(k+1)*n+(j+1)%n,(k+1)*n+j))
    fs.append(tuple(range((len(rs)-1)*n,len(rs)*n)));return mesh(name,vs,fs,color,glow)

def start():
    global parts
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False); parts=[]
    sc=bpy.context.scene; sc.unit_settings.system='METRIC'; sc.unit_settings.scale_length=1

def save(name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in parts: o.select_set(True)
    bpy.context.view_layer.objects.active=parts[0]; bpy.ops.object.join(); o=bpy.context.object; o.name=name; o.data.name=name+'Mesh'
    bpy.context.scene.cursor.location=(0,0,0); bpy.ops.object.origin_set(type='ORIGIN_CURSOR'); bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    # FBX preset maps Blender +Y to Unity +Z. Face and pendants deliberately face +Y.
    if name in ('Enemy_Shade', 'Tower_FlameLantern'):
        for v in o.data.vertices: v.co.x *= -1; v.co.y *= -1
    # Consolidate duplicate material slots created by join; stable named submeshes.
    old=list(o.data.materials); used=[]
    for p in o.data.polygons:
        m=old[p.material_index]
        if m not in used: used.append(m)
        p.material_index=used.index(m)
    o.data.materials.clear()
    for m in used:o.data.materials.append(m)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(name+'.blend')),compress=True)
    export(name,o)
    o.data.calc_loop_triangles()
    return dict(asset=name,triangles=len(o.data.loop_triangles),materialSlots=len(o.data.materials),vertices=len(o.data.vertices),source='ArtSource/Blender/ART1/'+name+'.blend',fbx='Assets/_Game/Art/Models/ART1/'+name+'.fbx')

def export(name,o):
    bpy.ops.export_scene.fbx(filepath=str(MODELS/(name+'.fbx')),use_selection=True,object_types={'MESH'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',use_space_transform=True,bake_space_transform=True,use_mesh_modifiers=True,mesh_smooth_type='FACE',use_triangles=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP',use_custom_props=False)

records=[]
start()
# FLAME: a quiet stone plinth, four open timber columns, upswept double eave, bright visible core.
box('Foot',(0,0,.07),(.94,.94,.14),2,.045);box('StoneStep',(0,0,.18),(.80,.80,.12),0,.035)
box('BronzeSocket',(0,0,.27),(.65,.65,.06),8);box('Hearth',(0,0,.33),(.62,.62,.06),4)
for x in (-.27,.27):
    for y in (-.27,.27):
        box('TimberColumn',(x,y,.91),(.085,.085,1.14),5,.012)
        box('BronzeFoot',(x,y,.43),(.12,.12,.14),8)
        box('IvoryCapital',(x,y,1.4),(.135,.135,.12),0)
        beam('DiagonalBracket',(x,y,1.2),(x*1.65,y,1.49),.055,8)
# octagonal hearth collar, a carved flame, three licks with warm borders.
cone('FireBowl',(0,0,.43),.30,.23,.13,8)
rings('OuterFlame',[(.45,.13,0,0),(.73,.23,0,0),(1.04,.17,.04,0),(1.26,.09,-.03,0),(1.45,0,.09,0)],7,15,True)
rings('LightHeart',[(.45,.10,0,-.15),(.72,.14,0,-.15),(.97,.08,.03,-.12),(1.16,0,-.02,-.09)],6,14,True)
rings('SideFlame',[(.46,.07,-.14,.03),(.79,.12,-.16,.02),(1.06,0,-.25,0)],6,13,True)
# Roof profiles have upturned corners: eight points alternating corner/side midpoint.
def roof(name,z,scale,color):
    perimeter=[(-1,-1),(0,-1),(1,-1),(1,0),(1,1),(0,1),(-1,1),(-1,0)]
    vs=[]
    for w,h in [(scale,z),(scale*.94,z+.065),(scale*.46,z+.23),(.055,z+.39)]:
        for i,(x,y) in enumerate(perimeter):vs.append((x*w,y*w,h+(.08 if i%2==0 and w>scale*.8 else 0)))
    fs=[]
    for k in range(3):
        for i in range(8):fs.append((k*8+i,k*8+(i+1)%8,(k+1)*8+(i+1)%8,(k+1)*8+i))
    fs.extend([tuple(reversed(range(8))),tuple(range(24,32))]);mesh(name,vs,fs,color)
roof('BronzeEave',1.49,.59,8);roof('DarkSweptRoof',1.54,.56,5)
for x,y in [(-1,-1),(1,-1),(1,1),(-1,1)]:beam('RoofRib',(x*.54,y*.54,1.68),(x*.07,y*.07,1.94),.035,9)
cone('Crown',(0,0,2.01),.14,.04,.19,8);cone('FireFinial',(0,0,2.18),.075,0,.21,13,6,True)
for x in (-.45,.45):
    beam('PendantCord',(x,-.43,1.58),(x,-.43,1.24),.018,8)
    box('EmberVotive',(x,-.43,1.22),(.10,.035,.17),12,.01)
    box('VotiveInlay',(x,-.452,1.22),(.025,.008,.09),14,0)
records.append(save('Tower_FlameLantern'))

start()
# SHADE: jagged floating mantle; mask and paired fissures establish -Y Blender / +Z Unity forward.
n=10
vs=[]
for k,(z,r) in enumerate([(0,.18),(.23,.34),(.65,.29),(.91,.23),(1.04,.16)]):
    for j in range(n):
        a=math.tau*j/n; zz=z+(.12 if k==0 and j%2 else 0);vs.append((math.cos(a)*r,math.sin(a)*r*.8,zz))
fs=[]
for k in range(4):
    for j in range(n):fs.append((k*n+j,k*n+(j+1)%n,(k+1)*n+(j+1)%n,(k+1)*n+j))
fs.extend([tuple(reversed(range(n))),tuple(range(40,50))]);mesh('BrokenMantle',vs,fs,16)
# Broad shoulder blades and asymmetric hanging tails.
for s in (-1,1):
    mesh('Shoulder',[(s*.14,.03,.86),(s*.42,.05,.72),(s*.34,-.04,.44),(s*.22,-.15,.71),(s*.20,.14,.68)],[(0,1,3),(1,2,3),(0,4,1),(1,4,2),(0,3,4),(2,4,3)],17)
    mesh('CrownHorn',[(s*.08,.01,.99),(s*.20,.04,1.31),(s*.26,.08,1.35),(s*.22,.02,1.02),(s*.13,-.08,1.06)],[(0,1,4),(1,2,4),(2,3,4),(0,3,2,1),(0,4,3)],18)
    beam('ColdCrownEdge',(s*.12,-.07,1.04),(s*.21,-.015,1.27),.025,20)
# convex ivory mask, with directional nose point below.
mesh('IvoryVoidMask',[(-.18,-.176,1.03),(.18,-.176,1.03),(.13,-.239,.80),(0,-.30,.70),(-.13,-.239,.80),(0,-.28,.99)],[(0,1,5),(1,2,5),(2,3,5),(3,4,5),(4,0,5)],22)
for s in (-1,1):
    mesh('EyeSocket',[(s*.025,-.286,.97),(s*.15,-.219,.965),(s*.11,-.25,.88)],[(0,1,2)],5)
    mesh('ColdEye',[(s*.045,-.291,.95),(s*.135,-.237,.95),(s*.09,-.266,.914)],[(0,1,2)],21,True)
mesh('MaskSplit',[(-.014,-.286,1.02),(.014,-.286,1.02),(.018,-.304,.74),(-.018,-.304,.74)],[(0,1,2,3)],20,True)
for s in (-1,1):
    mesh('TornFold',[(s*.07,-.24,.63),(s*.23,-.22,.44),(s*.15,-.19,.12),(s*.12,-.27,.39)],[(0,1,3),(1,2,3)],18)
    beam('MantleSeam',(s*.09,-.247,.62),(s*.17,-.239,.38),.018,20)
records.append(save('Enemy_Shade'))

start()
# Island: irregular two-tier suspended stone, warm upper surface and cool underside planes.
rng=random.Random(101)
n=18;vs=[]
for k,(z,sx,sy) in enumerate([(-.08,6.5,4.5),(-.55,6.35,4.35),(-1.9,5.95,3.95),(-2.8,4.8,3.0)]):
    for j in range(n):
        a=math.tau*j/n; jitter=1+rng.uniform(-.045,.045);vs.append((math.cos(a)*sx*jitter,math.sin(a)*sy*jitter,z+rng.uniform(-.10,.03) if k>0 else z))
fs=[tuple(range(n))]
for k in range(3):
    for j in range(n):fs.append((k*n+j,(k+1)*n+j,(k+1)*n+(j+1)%n,k*n+(j+1)%n))
fs.append(tuple(reversed(range(3*n,4*n))))
o=mesh('SuspendedRock',vs,fs,28)
# assign intentional cliff facet variations within one surface material
uv=o.data.uv_layers.active
for p in o.data.polygons:
    c=26 if p.index==0 else [4,6,27,28,29][p.index%5]
    for li in p.loop_indices:uv.data[li].uv=((c%8+.5)/8,(c//8+.5)/4)
# upper stone rim punctuated by long clean ledges
for j in range(n):
    a=math.tau*j/n;b=math.tau*(j+1)/n
    p=Vector(vs[j]);q=Vector(vs[(j+1)%n]);mid=(p+q)*.5
    o=box('RimBlock',(mid.x,mid.y,-.05),((q-p).length*.96,.20,.25),1,.04);o.rotation_euler.z=math.atan2(q.y-p.y,q.x-p.x)
records.append(save('Environment_FloatingIsland'))

start()
box('Paver',(0,0,.005),(.94,.94,.13),0,.045)
box('WalkingSurface',(0,0,.078),(.78,.78,.025),3,.012)
# Broad asymmetrical seams; no tiny texture noise.
box('CrossSeam',(0,.22,.094),(.76,.014,.006),2,0)
box('ShortSeam',(.13,-.08,.094),(.014,.57,.006),1,0)
records.append(save('Environment_PathTile'))

start()
box('BuildFoot',(0,0,.03),(.92,.92,.12),2,.04)
box('BuildStone',(0,0,.11),(.82,.82,.08),0,.025)
# Empty slot has a low bronze frame, carved sunken cross and four substantial corner stones.
for s in (-1,1):
    box('BuildEdgeX',(0,s*.32,.158),(.69,.035,.024),8,.008)
    box('BuildEdgeY',(s*.32,0,.158),(.035,.61,.024),8,.008)
box('SocketX',(0,0,.156),(.29,.045,.012),6,.006);box('SocketY',(0,0,.156),(.045,.29,.012),6,.006)
records.append(save('Environment_BuildPad'))

start()
box('MarkerBase',(0,0,.06),(.48,.45,.12),2,.03)
box('MarkerStone',(0,0,.38),(.30,.26,.55),0,.035)
box('MarkerCap',(0,0,.70),(.48,.40,.10),5,.035)
box('Inlay',(0,-.138,.42),(.10,.018,.26),8,.008)
box('HeartInlay',(0,-.15,.43),(.035,.015,.13),20,.004)
records.append(save('Prop_Waystone'))

start()
# Rocks with cool jade moss caps, large enough to read as one motif.
cone('RockBase',(0,0,.12),.43,.31,.35,28,7)
cone('RockCrown',(.02,0,.30),.32,.14,.18,27,7)
cone('MossCap',(.02,0,.40),.15,.08,.045,25,7)
records.append(save('Prop_MossRock'))

start()
# Ritual wind-bent shrub: original polygonal leaves, no alpha planes / overdraw.
beam('Stem',(0,0,0),(.09,0,.45),.045,10)
for x,y,z,s in [(.15,0,.25,.25),(-.12,.02,.30,.23),(.05,.06,.48,.24)]:
    beam('Twig',(0,0,.14),(x,y,z),.025,10)
    cone('LeafMass',(x,y,z),s,s*.55,.12,24,6)
    cone('LeafTop',(x,y,z+.09),s*.55,0,.09,25,6)
records.append(save('Prop_RitualShrub'))
(SOURCE/'geometry-manifest.json').write_text(json.dumps(records,indent=2)+'\n')
print('ART1_GEOMETRY',json.dumps(records))
