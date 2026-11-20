"""Однократное получение ART3 из принятого ART1; не изменяет baseline.
Blender --background --python-exit-code 1 --python ArtSource/Blender/ART3/derive_flame_levels.py
Редактировать .blend, затем export_flame.py. Этот script пересоздаёт только ART3.
"""
import bpy
import bmesh
import json
import math
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[3]
SOURCE = Path(__file__).resolve().parent
BASELINE = ROOT / 'ArtSource/Blender/ART1/Tower_FlameLantern.blend'


def finish(obj, tone, glow=False):
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    obj.data.materials.clear()
    obj.data.materials.append(bpy.data.materials['Art1Glow' if glow else 'Art1Surface'])
    uv = obj.data.uv_layers.get('UVMap') or obj.data.uv_layers.new(name='UVMap')
    for face in obj.data.polygons:
        for index in face.loop_indices:
            uv.data[index].uv = ((tone % 8 + .5) / 8, (tone // 8 + .5) / 4)
    parts.append(obj)
    return obj


def beam(name, a, b, width, tone):
    a, b = Vector(a), Vector(b)
    bpy.ops.mesh.primitive_cube_add(size=1, location=(a+b)*.5)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = (width, width, (b-a).length)
    obj.rotation_euler = (b-a).to_track_quat('Z', 'Y').to_euler()
    return finish(obj, tone)


def cone(name, position, r1, r2, depth, tone, glow=False):
    bpy.ops.mesh.primitive_cone_add(vertices=8, radius1=r1, radius2=r2,
                                  depth=depth, location=position)
    obj = bpy.context.object
    obj.name = name
    return finish(obj, tone, glow)


for level in range(1, 4):
    bpy.ops.wm.open_mainfile(filepath=str(BASELINE))
    base = bpy.data.objects['Tower_FlameLantern']
    parts = [base]
    # L1: отделяем корпус от ступени небольшой бронзовой окантовкой.
    for x in (-.29, .29):
        beam('HearthBinding', (x, -.29, .36), (x, .29, .36), .028, 9)
    if level >= 2:
        # Боковые открытые рёбра/чаши, просвет перед ядром сохраняется.
        for side in (-1, 1):
            x = side * .33
            beam('BronzeFlyingRib', (side*.28, 0, .43), (x, 0, 1.24), .065, 8)
            beam('RibReturn', (x, 0, 1.24), (side*.27, 0, 1.57), .055, 9)
            cone('SideBrazier', (x, 0, .91), .10, .13, .12, 8)
            cone('SideEmber', (x, 0, 1.07), .09, .015, .25, 15, True)
        # Сильная форма нижней чаши вместо изменения общей яркости.
        cone('RaisedHearth', (0, 0, .49), .31, .26, .14, 9)
    if level == 3:
        # Разомкнутый венец: два отдельных зубца, не сплошная новая крыша.
        for side in (-1, 1):
            beam('CrownFork', (side*.15, 0, 1.91), (side*.30, 0, 2.19), .07, 8)
            beam('CrownTip', (side*.30, 0, 2.19), (side*.24, 0, 2.40), .055, 9)
            cone('CrownEmber', (side*.24, 0, 2.43), .057, 0, .15, 13, True)
        # Два явных боковых языка в открытом корпусе — другая масса ядра.
        for side in (-1, 1):
            cone('ForkedCore', (side*.14, 0, 1.19), .125, .018, .64, 13, True)
        for y in (-.27, .27):
            beam('IvoryHearthRail', (-.27, y, .60), (.27, y, .60), .05, 0)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = base
    bpy.ops.object.join()
    name = f'Tower_FlameLanternL{level}'
    base.name = name
    base.data.name = name + 'Mesh'
    # Join оставляет повторные ссылки на те же два palette slots.
    old = list(base.data.materials)
    assignments = [0 if old[p.material_index].name == 'Art1Surface' else 1
                   for p in base.data.polygons]
    base.data.materials.clear()
    for material in ('Art1Surface', 'Art1Glow'):
        base.data.materials.append(bpy.data.materials[material])
    for face, slot in zip(base.data.polygons, assignments):
        face.material_index = slot
    # У исходных колец огня есть грани нулевой площади у вершины.
    # Удаляем только эти faces production-копии, сохраняя все исходные vertices/UV.
    bm = bmesh.new()
    bm.from_mesh(base.data)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.calc_area() < 1e-12], context='FACES_ONLY')
    bm.to_mesh(base.data)
    bm.free()
    base.data.update()
    base['art3a_level'] = level
    base['art3a_baseline'] = 'ART1 accepted / PR75 main 7dfdf0e'
    bpy.context.scene.unit_settings.system = 'METRIC'
    bpy.context.scene.unit_settings.scale_length = 1
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / (name + '.blend')), compress=True)
