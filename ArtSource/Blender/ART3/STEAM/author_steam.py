"""Оригинальное mesh-authoring только STEAM. После ручных правок использовать export_steam.py."""
import math
from pathlib import Path
import bpy
import bmesh
from mathutils import Vector

FOLDER = Path(__file__).resolve().parent
ROOT = FOLDER.parents[3]
BASE = ROOT / 'ArtSource/Blender/ART1/Tower_FlameLantern.blend'


def finish(obj, name, tone, glow=False):
    obj.name = name
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    obj.data.materials.clear()
    obj.data.materials.append(bpy.data.materials['Art1Glow' if glow else 'Art1Surface'])
    uv = obj.data.uv_layers.get('UVMap') or obj.data.uv_layers.new(name='UVMap')
    for face in obj.data.polygons:
        for index in face.loop_indices:
            uv.data[index].uv = ((tone % 8 + .5) / 8, (tone // 8 + .5) / 4)
    obj.vertex_groups.new(name=name).add(list(range(len(obj.data.vertices))), 1, 'REPLACE')
    parts.append(obj)
    return obj


def box(name, center, size, tone, bevel=.015):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center)
    obj = bpy.context.object
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        modifier = obj.modifiers.new('StoneEdge', 'BEVEL')
        modifier.width = bevel
        modifier.segments = 1
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    return finish(obj, name, tone)


def local(u, d, z):
    return ((u+d)/math.sqrt(2), (d-u)/math.sqrt(2), z)


def plate(name, outline, depth, tone, d=0):
    # Редактируемая каменная плита; лицевая нормаль направлена к диагонали Battle camera.
    n=len(outline)
    vertices=[local(u,d+side*depth/2,z) for side in (-1,1) for u,z in outline]
    faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    mesh=bpy.data.meshes.new(name+'Mesh');mesh.from_pydata(vertices,[],faces);mesh.update()
    obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj)
    return finish(obj,name,tone)


def ellipsoid(name,u,d,z,radii,tone,glow=False):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2,radius=1,location=local(u,d,z))
    obj=bpy.context.object
    # Оси формы совпадают с лицевой плоскостью, диагонально к Battle camera.
    for v in obj.data.vertices:
        x,y,h=v.co
        v.co=local(x*radii[0],y*radii[1],h*radii[2])
    return finish(obj,name,tone,glow)


def sweep(name,points,radii,tone,glow=False):
    centers=[Vector(local(*p)) for p in points]
    vertices=[];faces=[];sides=8
    for i,(c,r) in enumerate(zip(centers,radii)):
        tangent=(centers[min(i+1,len(centers)-1)]-centers[max(i-1,0)]).normalized()
        reference=Vector((0,0,1)) if abs(tangent.z)<.9 else Vector((1,0,0))
        across=tangent.cross(reference).normalized()
        other=tangent.cross(across).normalized()
        for j in range(sides):
            a=j*2*math.pi/sides
            vertices.append(c+r*(math.cos(a)*across+math.sin(a)*other))
    for i in range(len(centers)-1):
        for j in range(sides):
            a=i*sides+j;b=i*sides+(j+1)%sides
            faces.append((a,b,b+sides,a+sides))
    faces.extend([tuple(reversed(range(sides))),tuple(range((len(centers)-1)*sides,len(centers)*sides))])
    mesh=bpy.data.meshes.new(name+'Mesh');mesh.from_pydata(vertices,[],faces);mesh.update()
    obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj)
    return finish(obj,name,tone,glow)

for level in (1,2,3):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    with bpy.data.libraries.load(str(BASE),link=False) as (available,imported):
        imported.materials=['Art1Surface','Art1Glow']
    parts=[]
    box('FoundationStone',(0,0,.065),(.90,.90,.13),2,.035)
    box('BronzePlinth',(0,0,.15),(.70,.70,.04),8,.015)
    box('JadeCushion',(0,0,.20),(.63,.63,.06),24,.02)
    ellipsoid('SeatedFoxBody',0,-.04,.58,(.20,.18,.34),6)
    for side in (-1,1):
        ellipsoid('HindHaunch',side*.17,-.09,.38,(.13,.15,.16),24)
        ellipsoid('FrontPaw',side*.09,.16,.30,(.065,.12,.07),3)
        sweep('Foreleg',[(side*.10,.09,.72),(side*.10,.12,.51),(side*.09,.15,.32)],[.065,.055,.045],24)
    ellipsoid('IvoryChest',0,.135,.60,(.105,.055,.23),3)
    ellipsoid('FoxHead',0,.10,1.02,(.225,.185,.225),24)
    for side in (-1,1):
        # Треугольные объёмные уши с отдельной лицевой вставкой.
        plate('PointedEar',[(side*.055,1.15),(side*.205,1.40),(side*.225,1.10)],.12,6,d=.075)
        plate('InnerEar',[(side*.105,1.16),(side*.194,1.33),(side*.195,1.15)],.015,1,d=.145)
        ellipsoid('IvoryCheek',side*.12,.21,.97,(.12,.075,.10),3)
        ellipsoid('IndigoEye',side*.11,.259,1.055,(.036,.016,.030),0)
        ellipsoid('EyeGlint',side*.107,.274,1.065,(.012,.005,.012),8,True)
    ellipsoid('LongMuzzle',0,.285,.97,(.09,.125,.065),3)
    ellipsoid('FoxNose',0,.402,.985,(.040,.022,.026),0)
    sweep('BronzeCollar',[(-.15,.15,.79),(0,.20,.77),(.15,.15,.79)],[.025,.025,.025],8)
    ellipsoid('SteamMedallion',0,.21,.75,(.045,.020,.060),20,True)
    # У хвостов самостоятельный объём и различная крупная поза, без широкой глухой крыши.
    sweep('RightTail',[(.10,-.17,.32),(.26,-.18,.42),(.35,-.13,.67),(.34,-.06,.91),(.28,-.02,1.04)],
          [.10,.13,.12,.095,.015],6)
    sweep('RightTailTip',[(.34,-.06,.91),(.30,-.03,1.02),(.28,-.02,1.065)],[.090,.06,.008],3)
    if level>=2:
        sweep('RaisedLeftTail',[(-.12,-.18,.32),(-.28,-.18,.56),(-.36,-.13,.87),(-.35,-.08,1.22),(-.28,-.04,1.52)],
              [.095,.125,.12,.095,.008],24)
        sweep('LeftTailTip',[(-.35,-.08,1.22),(-.32,-.06,1.40),(-.28,-.04,1.54)],[.09,.065,.008],3)
    if level==3:
        sweep('HighRearTail',[(0,-.23,.40),(.11,-.29,.79),(.23,-.27,1.18),(.25,-.20,1.53),(.16,-.15,1.77)],
              [.09,.11,.10,.075,.008],6)
        sweep('RearTailTip',[(.25,-.20,1.50),(.23,-.17,1.65),(.16,-.15,1.79)],[.072,.045,.008],3)
        sweep('OpenSteamCurl',[(-.29,-.04,1.53),(-.35,-.02,1.64),(-.33,.01,1.80),(-.23,.02,1.89),(-.13,.02,1.85),(-.12,.02,1.77)],
              [.023,.025,.025,.025,.020,.009],20,True)
        sweep('OpenSteamWisp',[(.16,-.15,1.78),(.25,-.12,1.88),(.22,-.08,2.02),(.12,-.05,2.08)],
              [.025,.025,.020,.008],3,True)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts: obj.select_set(True)
    bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();obj=parts[0]
    name=f'Tower_SteamFoxL{level}';obj.name=name;obj.data.name=name+'Mesh'
    slots=list(obj.data.materials);assignments=[int(slots[p.material_index].name=='Art1Glow') for p in obj.data.polygons]
    obj.data.materials.clear()
    for m in ('Art1Surface','Art1Glow'):obj.data.materials.append(bpy.data.materials[m])
    for face,slot in zip(obj.data.polygons,assignments):face.material_index=slot
    bm=bmesh.new();bm.from_mesh(obj.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(obj.data);bm.free();obj.data.update()
    bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
    obj['art3e_level']=level;obj['art3e_provenance']='Original STEAM fox / issue84 / 2026-11-22; shared ART1 palette only'
    for image in bpy.data.images:
        if image.has_data and not image.packed_file:image.pack()
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(FOLDER/(name+'.blend')),compress=True)
