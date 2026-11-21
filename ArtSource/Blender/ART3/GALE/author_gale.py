"""Оригинальное mesh-authoring только GALE. После ручных правок использовать export_gale.py."""
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


def rings(name, profile, tone, center=(0, 0), glow=False, n=12, caps=False):
    vertices = [(center[0] + radius * math.cos(2 * math.pi * j / n),
                 center[1] + radius * math.sin(2 * math.pi * j / n), z)
                for z, radius in profile for j in range(n)]
    faces = [(k*n+j, k*n+(j+1)%n, (k+1)*n+(j+1)%n, (k+1)*n+j)
             for k in range(len(profile)-1) for j in range(n)]
    if caps:
        faces.extend([tuple(reversed(range(n))), tuple(range((len(profile)-1)*n, len(profile)*n))])
    mesh = bpy.data.meshes.new(name + 'Mesh')
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return finish(obj, name, tone, glow)



def arc(name, radius, height, start, end, tone, width=.055, center=(0,0)):
    # Открытая лента в диагональной вертикальной плоскости: крупный отрицательный объём.
    points=[]
    for j in range(17):
        a=start+(end-start)*j/16
        x=radius*math.cos(a)
        points.append((center[0]+x/math.sqrt(2), center[1]-x/math.sqrt(2), height+radius*math.sin(a)))
    for j in range(len(points)-1):
        beam(name,points[j],points[j+1],width,tone)

for level in (1, 2, 3):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    with bpy.data.libraries.load(str(BASE), link=False) as (available, imported):
        imported.materials = ['Art1Surface', 'Art1Glow']
    parts=[]
    box('FoundationStone',(0,0,.075),(.90,.90,.15),1,.035)
    box('JadeStep',(0,0,.175),(.72,.72,.10),6,.025)
    rings('BronzeWindSeal',[(.225,.28),(.25,.28),(.25,.23),(.225,.23),(.225,.28)],9)
    for side in (-1,1):
        beam('JadeSplitPedestal',(side*.18,-side*.18,.22),(side*.25,-side*.25,.55),.105,24)
    rings('WindSealGlow',[(.255,.20),(.26,.20)],21,glow=True,caps=True)
    # Ромб висит над открытым основанием; ориентация видна в исходной Battle camera.
    beam('BronzeSuspension',(0,0,1.62),(0,0,1.38),.04,9)
    points=[(0,0,1.43),(.19,-.19,1.07),(0,0,.69),(-.19,.19,1.07)]
    for j in range(4): beam('TalismanBronzeEdge',points[j],points[(j+1)%4],.06,9)
    # Настоящая тонкая ромбическая пластина, не куб/фонарь/колокол другого семейства.
    verts=[(0,-.025,1.36),(.145,-.17,1.07),(0,-.025,.78),(-.145,.12,1.07),
           (0,.025,1.36),(.145,-.12,1.07),(0,.025,.78),(-.145,.17,1.07)]
    mesh=bpy.data.meshes.new('JadeTalismanMesh');mesh.from_pydata(verts,[],[(0,1,2,3),(7,6,5,4),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)]);mesh.update()
    obj=bpy.data.objects.new('JadeTalisman',mesh);bpy.context.collection.objects.link(obj);finish(obj,'JadeTalisman',6)
    # Светлая диагональная прорезь подчёркивает направленный ветер общей palette.
    beam('WindRune',( -.09,.06,.99),(.09,-.12,1.17),.038,21)
    arc('OpenWindFrame',.49,1.12,-.5*math.pi,1.30*math.pi,9,.055)
    beam('JadeFrameFoot',(-.27,.27,.38),(-.32,.32,.85),.075,24)
    beam('WindTassel',(0,0,.69),(.08,-.08,.43),.045,17)
    if level>=2:
        # Две крупные боковые дуги, а не мелкие декоративные добавления L1.
        for side in (-1,1):
            c=(side*.18,-side*.18)
            arc('TwinWindSail',.34,1.30,(-.65 if side==1 else .35)*math.pi,(.65 if side==1 else 1.65)*math.pi,6,.07,center=c)
            beam('SailBronzeRoot',(side*.17,-side*.17,.32),(side*.32,-side*.32,.99),.065,9)
        arc('WindSailInlay',.40,1.37,.10*math.pi,.92*math.pi,21,.03)
    if level==3:
        # Высокая разомкнутая спираль: большая новая конструкция над L2, без крыши.
        arc('UpperSpiral',.44,1.93,-.30*math.pi,1.35*math.pi,9,.065)
        arc('UpperJadeSpiral',.34,1.93,-.15*math.pi,1.22*math.pi,24,.045)
        beam('SpiralSupport',(-.32,.32,1.04),(-.25,.25,2.07),.065,6)
        arc('CrossWindArc',.40,1.46,-.10*math.pi,.85*math.pi,21,.045,center=(.05,.05))
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    obj = parts[0]
    name = f'Tower_GaleTalismanL{level}'
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
    obj['art3c_level'] = level
    obj['art3c_provenance'] = 'Original GALE geometry / issue80 / 2026-11-21; shared accepted ART1 palette only'
    for image in bpy.data.images:
        if image.has_data and not image.packed_file:
            image.pack()
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(FOLDER / (name + '.blend')), compress=True)
