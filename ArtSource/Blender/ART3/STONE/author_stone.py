"""Оригинальное mesh-authoring только STONE. После ручных правок использовать export_stone.py."""
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


def beam(name, a, b, width, tone):
    a, b = Vector(a), Vector(b)
    bpy.ops.mesh.primitive_cube_add(size=1, location=(a + b) / 2)
    obj = bpy.context.object
    obj.dimensions = (width, width, (b - a).length)
    obj.rotation_euler = (b - a).to_track_quat('Z', 'Y').to_euler()
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


def rune_beam(name,a,b,width,tone,glow=False,d=.175):
    obj=beam(name,local(a[0],d,a[1]),local(b[0],d,b[1]),width,tone)
    if glow:
        obj.data.materials.clear();obj.data.materials.append(bpy.data.materials['Art1Glow'])
    return obj

for level in (1,2,3):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    with bpy.data.libraries.load(str(BASE),link=False) as (available,imported):
        imported.materials=['Art1Surface','Art1Glow']
    parts=[]
    box('FoundationStone',(0,0,.075),(.90,.90,.15),2,.035)
    box('JadeStep',(0,0,.185),(.73,.73,.07),24,.02)
    # Две неровные половины монолита сохраняют видимый центральный раскол.
    plate('SplitMonolithLeft',[(-.27,.22),(-.29,1.12),(-.17,1.37),(-.035,1.30),
          (-.065,1.05),(-.02,.83),(-.065,.57),(-.03,.22)],.29,6)
    plate('SplitMonolithRight',[(.03,.22),(.06,.57),(.02,.83),(.07,1.06),
          (.04,1.30),(.22,1.22),(.29,.98),(.26,.22)],.29,24)
    # Крупная печать: бронзовая оправа и светлый угловой знак.
    outline=[(-.14,.75),(-.22,.91),(-.14,1.09),(.14,1.09),(.22,.91),(.14,.75)]
    for j in range(len(outline)):
        rune_beam('BronzeSealRim',outline[j],outline[(j+1)%len(outline)],.045,8,d=.17)
    for a,b in [((-.11,1.00),(.11,1.00)),((0,1.05),(0,.80)),((-.10,.88),(.10,.88)),
                ((-.10,.88),(-.10,.79)),((.10,.88),(.10,.79))]:
        rune_beam('IvoryStoneSeal',a,b,.036,3,True,d=.20)
    for a,b in [((-.04,.31),(-.06,.57)),((-.06,.57),(-.02,.82)),((-.02,.82),(-.06,1.05))]:
        rune_beam('FractureLight',a,b,.022,20,True,d=.15)
    for side in (-1,1):
        for z in (.44,1.15):
            rune_beam('BronzeStoneStaple',(side*.15,z),(side*.26,z-.04),.035,8,d=.16)
        rune_beam('CarvedBasePlate',(side*.29,.29),(side*.12,.29),.028,1,d=.21)
    if level>=2:
        # Наклонные плиты расширяют средний силуэт без изменения root scale.
        for side in (-1,1):
            outline=[(side*.22,.23),(side*.43,.28),(side*.43,.93),
                     (side*.36,1.58),(side*.21,1.39),(side*.30,.79)]
            plate('InclinedButtress',outline,.20,25,d=-.06)
            rune_beam('ButtressBronzeBinding',(side*.32,.54),(side*.40,.87),.043,8,d=.06)
            rune_beam('TabletCarving',(side*.34,.95),(side*.29,1.28),.035,1,d=.065)
    if level==3:
        # Раздельные верхние зубцы и внешние опоры; между ними остаются просветы.
        for side in (-1,1):
            plate('SplitCrownTooth',[(side*.10,1.23),(side*.25,1.25),(side*.22,1.98),
                                    (side*.09,2.15),(side*.055,1.72)],.22,6,d=-.10)
            plate('OuterAnchorSlab',[(side*.35,.22),(side*.53,.32),(side*.53,.68),
                                    (side*.46,1.09),(side*.36,.91)],.16,24,d=-.04)
            rune_beam('CrownSealLine',(side*.12,1.46),(side*.14,1.91),.028,3,True,d=.025)
            rune_beam('OuterBronzeTie',(side*.40,.63),(side*.49,.72),.04,8,d=.05)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    obj = parts[0]
    name = f'Tower_StoneSealL{level}'
    obj.name = name
    obj.data.name = name + 'Mesh'
    slots = list(obj.data.materials)
    assignments = [int(slots[p.material_index].name == 'Art1Glow') for p in obj.data.polygons]
    obj.data.materials.clear()
    for material in ('Art1Surface', 'Art1Glow'):
        obj.data.materials.append(bpy.data.materials[material])
    for face, slot in zip(obj.data.polygons, assignments):
        face.material_index = slot
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    bpy.context.scene.unit_settings.system = 'METRIC'
    bpy.context.scene.unit_settings.scale_length = 1
    obj['art3d_level'] = level
    obj['art3d_provenance'] = 'Original STONE geometry / issue82 / 2026-11-22; shared accepted ART1 palette only'
    for image in bpy.data.images:
        if image.has_data and not image.packed_file:
            image.pack()
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(FOLDER / (name + '.blend')), compress=True)
