"""ART11-02/03/04: проверка реальной saved геометрии; Blender, без regeneration."""
import bpy
from pathlib import Path
from mathutils import Vector
ROOT = Path(__file__).resolve().parents[4]

def load(name):
    bpy.ops.wm.open_mainfile(filepath=str(ROOT/'ArtSource/Blender/ART1'/(name+'.blend')))
    obj = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
    assert obj.name == name and obj.data.name == name+'Mesh'
    assert obj.location.length < 1e-6 and (obj.scale-Vector((1,1,1))).length < 1e-6
    return obj.data

def components(me):
    adjacent = {v.index: set() for v in me.vertices}
    for e in me.edges:
        a,b=e.vertices; adjacent[a].add(b);adjacent[b].add(a)
    remaining=set(adjacent)
    while remaining:
        todo=[remaining.pop()]; found=[]
        while todo:
            i=todo.pop(); found.append(i)
            for j in adjacent[i]:
                if j in remaining: remaining.remove(j);todo.append(j)
        yield [me.vertices[i].co for i in found]

me=load('Environment_FloatingIsland')
points=[v.co for v in me.vertices]
assert min(p.z for p in points)<-4.2, 'Недостаточная глубина скального основания'
masses=[c for c in components(me) if min(p.z for p in c)<-2 and max(p.z for p in c)-min(p.z for p in c)>1.5]
assert len(masses)>=3, ('Остров должен иметь >=3 отдельные нижние массы',len(masses))
print('PASS island: deep underside,',len(masses),'distinct rock masses')
me=load('Tower_FlameLantern')
roof=[v.co for v in me.vertices if 1.49-1e-5<=v.co.z<1.96]
assert roof and max(abs(v.x) for v in roof)<.405 and max(abs(v.y) for v in roof)<.365, 'Свес крыши остался слишком широким'
assert abs(min(v.co.z for v in me.vertices))<.002
assert 2.2<max(v.co.z for v in me.vertices)<2.4
print('PASS FLAME: reduced roof footprint, original pivot/height')
me=load('Enemy_Shade');uv=me.uv_layers.active
lower=[p for p in me.polygons if max(me.vertices[i].co.z for i in p.vertices)<.26 and len(p.vertices)==4]
assert len(lower)==10, ('Проверяем весь нижний ряд плаща',len(lower))
for p in lower:
    color=int(uv.data[p.loop_start].uv.x*8)+8*int(uv.data[p.loop_start].uv.y*4)
    assert color in (18,19,28), ('Нижний плащ не получил холодную светлую плоскость',color)
hem=[v.co for v in me.vertices if v.co.z<.13]
assert max(abs(v.x) for v in hem)>.245, 'Нижние лоскуты слишком узкие'
# Весь замкнутый mantle: индексы, signed volume и направление нормали от оси.
triangles=[]
for p in me.polygons:
    color=int(uv.data[p.loop_start].uv.x*8)+8*int(uv.data[p.loop_start].uv.y*4)
    if color in (16,18,19,28) and all(i<50 or (me.vertices[i].co-Vector((0,0,.04))).length<1e-6 for i in p.vertices): triangles.append(p.index)
assert len(triangles)==51, ('Неполный mantle',len(triangles))
me.calc_loop_triangles();volume=0
for tri in me.loop_triangles:
    if tri.polygon_index not in triangles:continue
    a,b,c=[me.vertices[i].co for i in tri.vertices];n=(b-a).cross(c-a);center=(a+b+c)/3
    assert n.dot(center-Vector((0,0,.53)))>1e-8, ('Inward mantle',tri.polygon_index)
    volume+=a.dot(b.cross(c))/6
assert volume>.05
print('PASS Shade: complete cold hem, broad torn silhouette, outward mantle; volume',volume)
