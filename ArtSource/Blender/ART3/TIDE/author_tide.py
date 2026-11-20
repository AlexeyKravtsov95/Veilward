"""Оригинальное детерминированное mesh-authoring только семейства TIDE.
Перезаписывает три TIDE sources; после ручных правок применять export_tide.py.
"""
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


for level in (1, 2, 3):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    with bpy.data.libraries.load(str(BASE), link=False) as (available, imported):
        imported.materials = ['Art1Surface', 'Art1Glow']
    parts = []
    box('FoundationStone', (0, 0, .075), (.90, .90, .15), 1, .035)
    box('JadeStep', (0, 0, .17), (.76, .76, .11), 6, .025)
    # Низкая открытая чаша: настоящая внутренняя стенка и видимая вода, не закрытый конус.
    rings('MainBasin', [(.215, .22), (.26, .33), (.40, .35), (.415, .33),
                        (.37, .29), (.275, .24)], 24, caps=True)
    rings('BasinBronzeRim', [(.38, .35), (.425, .35), (.425, .32), (.38, .32), (.38, .35)], 9)
    rings('StillWater', [(.355, .287), (.365, .287)], 20, glow=True, caps=True)
    rings('WaterIndigoSeal', [(.367, .17), (.37, .17), (.37, .145), (.367, .145), (.367, .17)], 17)
    for side in (-1, 1):
        a = side * .275
        box('FramePedestal', (a, -a, .31), (.17, .17, .22), 0, .018)
        beam('JadeSuspensionPillar', (a, -a, .38), (a, -a, 1.40), .11, 24)
        beam('BronzePillarInlay', (a+.041, -a+.041, .48), (a+.041, -a+.041, 1.32), .025, 9)
        beam('OpenSuspensionArch', (a, -a, 1.40), (side*.13, -side*.13, 1.64), .11, 6)
        beam('ArchBronzeCap', (side*.13, -side*.13, 1.64), (0, 0, 1.67), .085, 9)
    beam('BellHanger', (0, 0, 1.66), (0, 0, 1.43), .055, 9)
    # Полый профиль колокола с расширенным раструбом, внутренней стенкой и языком.
    rings('BronzeBell', [(1.44, .08), (1.36, .15), (1.22, .185), (.92, .215),
                         (.79, .28), (.735, .30), (.71, .30), (.71, .255),
                         (.78, .245), (.93, .18), (1.23, .15), (1.34, .105)], 8, caps=True, n=16)
    rings('BellCrownBand', [(1.29, .17), (1.34, .17), (1.34, .15), (1.29, .15), (1.29, .17)], 9, n=16)
    rings('BellIndigoBand', [(.85, .256), (.89, .239), (.89, .23), (.85, .247), (.85, .256)], 17, n=16)
    rings('Clapper', [(.62, .05), (.68, .075), (.74, .055), (1.18, .018)], 9, caps=True, n=8)
    if level >= 2:
        # Силуэт растёт поперёк диагонали Battle camera; крупные чаши остаются внутри XY .94m.
        for side in (-1, 1):
            outer = side * .405
            beam('ReinforcedWaterButtress', (side*.28, -side*.28, .30), (outer, -outer, 1.32), .09, 6)
            beam('CollectorArch', (outer, -outer, 1.32), (side*.20, -side*.20, 1.79), .08, 24)
            center = (side*.285, -side*.285)
            rings('SideWaterCollector', [(.77, .08), (.85, .13), (1.03, .17), (1.06, .16),
                                        (1.01, .13), (.88, .07)], 24, center, caps=True)
            rings('CollectorBronzeRim', [(1.025, .172), (1.075, .172), (1.075, .145),
                                         (1.025, .145), (1.025, .172)], 9, center)
            rings('CollectorWater', [(1.025, .139), (1.035, .139)], 20, center, glow=True, caps=True)
            beam('WaterChannel', (side*.27, -side*.27, .86), (side*.19, -side*.19, .49), .045, 20)
        rings('RaisedWaterSeal', [(.23, .31), (.27, .31), (.27, .28), (.23, .28), (.23, .31)], 9)
    if level == 3:
        # Два высоких открытых водяных рога; между ними нет большой сплошной крыши.
        for side in (-1, 1):
            beam('CrownRoot', (side*.20, -side*.20, 1.65), (side*.34, -side*.34, 1.96), .10, 24)
            beam('CrownHorn', (side*.34, -side*.34, 1.96), (side*.28, -side*.28, 2.25), .085, 6)
            beam('CrownBronzeTip', (side*.28, -side*.28, 2.25), (side*.19, -side*.19, 2.36), .06, 9)
            beam('CrownWaterInlay', (side*.31+.033, -side*.31+.033, 2.02),
                 (side*.255+.033, -side*.255+.033, 2.25), .03, 20)
        rings('GrandBellLowerBand', [(.93, .218), (1.00, .208), (1.00, .198), (.93, .208), (.93, .218)], 9, n=16)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    obj = parts[0]
    name = f'Tower_TideBellL{level}'
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
    obj['art3b_level'] = level
    obj['art3b_provenance'] = 'Original TIDE geometry / issue78 / 2026-11-20; shared accepted ART1 palette only'
    for image in bpy.data.images:
        if image.has_data and not image.packed_file:
            image.pack()
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(FOLDER / (name + '.blend')), compress=True)
