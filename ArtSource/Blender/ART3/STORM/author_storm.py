"""Оригинальное mesh-authoring только STORM. После ручных правок использовать export_storm.py."""
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
    # Раздельная опора под хвостом сохраняет воздух вокруг изгиба тела.
    sweep('JadeWaterSupport',[(.04,-.03,.17),(.13,-.03,.31),(.12,-.03,.47)],[.10,.08,.055],24)
    if level==1:
        spine=[(.17,0,.42),(.22,-.02,.61),(.10,0,.80),(-.10,.03,.93)]
        head=(-.22,.04,1.00)
        tail=[(.14,.42),(.31,.29),(.39,.40),(.31,.53)]
    elif level==2:
        spine=[(.14,0,.40),(.24,-.03,.65),(-.08,-.04,.89),(-.18,0,1.14),(-.03,.03,1.32)]
        head=(.10,.04,1.39)
        tail=[(.10,.40),(.29,.27),(.40,.36),(.33,.55)]
    else:
        spine=[(.12,0,.41),(-.14,-.03,.67),(-.21,-.05,.94),(.14,-.04,1.18),(.20,0,1.42),(.03,.03,1.61)]
        head=(-.11,.04,1.68)
        tail=[(.12,.42),(.28,.25),(.40,.37),(.30,.57)]
    sweep('CurvedCarpBody',spine,[.09]+[.14]*(len(spine)-2)+[.13],6)
    hu,hd,hz=head
    ellipsoid('CarpHead',hu,hd,hz,(.16,.14,.15),24)
    direction=-1 if level!=2 else 1
    ellipsoid('RoundedSnout',hu+direction*.13,hd+.01,hz-.025,(.10,.13,.085),3)
    # Толстые губы и две открытые усовые нити отличают рыбу от лисицы/колокола.
    sweep('BronzeLips',[(hu+direction*.205,hd+.09,hz+.025),(hu+direction*.23,hd+.115,hz-.005),(hu+direction*.205,hd+.09,hz-.04)],[.019,.020,.019],8)
    ellipsoid('CarpEye',hu+direction*.035,hd+.135,hz+.045,(.033,.012,.031),0)
    ellipsoid('EyeGlint',hu+direction*.042,hd+.147,hz+.055,(.012,.005,.012),20,True)
    sweep('FrontBarbel',[(hu+direction*.18,hd+.10,hz-.035),(hu+direction*.18,hd+.15,hz-.14),(hu+direction*.07,hd+.17,hz-.19)],[.012,.012,.004],8)
    sweep('RearBarbel',[(hu+direction*.18,hd-.08,hz-.035),(hu+direction*.23,hd-.10,hz-.13),(hu+direction*.15,hd-.12,hz-.19)],[.010,.010,.004],8)
    # Раздвоенный хвост: две отдельные объёмные лопасти с заметным просветом.
    tu,tz=tail[0]
    plate('TailUpperLobe',[(tu,tz),tail[2],tail[3]],.055,24)
    plate('TailLowerLobe',[(tu,tz),tail[1],(tail[2][0],tail[2][1]-.06)],.055,6)
    sweep('TailRay',[(tu,.035,tz),(tail[3][0],.035,tail[3][1])],[.012,.005],8)
    mid=spine[len(spine)//2]
    mu,md,mz=mid
    plate('DorsalFin',[(mu-.08,mz-.15),(mu+.03,mz+.23),(mu+.18,mz+.04),(mu+.10,mz-.15)],.045,8,d=md-.07)
    plate('NearPectoralFin',[(mu-.04,mz),(mu-.18,mz-.05),(mu-.22,mz-.27),(mu+.06,mz-.12)],.04,24,d=.17)
    if level>=2:
        plate('OpenLeftFin',[(-.08,.88),(-.39,1.09),(-.42,.88),(-.25,.65),(-.13,.75)],.035,24,d=.06)
        plate('OpenRightFin',[(.08,.96),(.40,1.22),(.42,1.00),(.27,.78),(.13,.78)],.035,6,d=-.04)
        sweep('FinLightningLeft',[(-.16,.09,.91),(-.34,.09,1.04),(-.37,.09,.90)],[.014,.014,.007],20,True)
        sweep('FinLightningRight',[(.14,-.01,1.00),(.35,-.01,1.17),(.38,-.01,1.02)],[.014,.014,.007],20,True)
    # Крупные бронзовые чешуйки следуют изгибу, не заменяя различия поз.
    for i,(u,d,z) in enumerate(spine[1:-1]):
        ellipsoid('BronzeScale'+str(i),u,d+.135,z,(.052,.018,.073),8)
    ellipsoid('StormSeal',hu,hd+.143,hz-.055,(.045,.012,.04),20,True)
    if level==3:
        sweep('OpenStormArcLeft',[(-.20,-.12,1.10),(-.36,-.12,1.35),(-.32,-.10,1.62),(-.22,-.08,1.84),(-.10,-.05,1.96)],[.022,.022,.022,.019,.006],20,True)
        sweep('OpenStormArcRight',[(.24,-.10,1.03),(.37,-.10,1.30),(.34,-.08,1.56),(.22,-.06,1.76)],[.024,.024,.022,.006],3,True)
        plate('CrownFin',[(.03,1.56),(.20,1.91),(.26,1.78),(.19,1.54)],.04,8,d=-.06)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts: obj.select_set(True)
    bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();obj=parts[0]
    name=f'Tower_StormCarpL{level}';obj.name=name;obj.data.name=name+'Mesh'
    slots=list(obj.data.materials);assignments=[int(slots[p.material_index].name=='Art1Glow') for p in obj.data.polygons]
    obj.data.materials.clear()
    for m in ('Art1Surface','Art1Glow'):obj.data.materials.append(bpy.data.materials[m])
    for face,slot in zip(obj.data.polygons,assignments):face.material_index=slot
    bm=bmesh.new();bm.from_mesh(obj.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(obj.data);bm.free();obj.data.update()
    bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
    obj['art3f_level']=level;obj['art3f_provenance']='Original STORM carp / issue86 / 2026-11-23; shared ART1 palette only'
    for image in bpy.data.images:
        if image.has_data and not image.packed_file:image.pack()
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(FOLDER/(name+'.blend')),compress=True)
