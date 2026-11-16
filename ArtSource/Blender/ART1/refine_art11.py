"""ART.1.1: однократная правка трёх существующих editable sources, без генератора карт.
Run from clean ART.1 sources; повторный запуск блокируется. Export — отдельным export_art1.py.
"""
import bpy, math, json
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[3]
SOURCE=ROOT/'ArtSource/Blender/ART1'

def load(name):
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE/(name+'.blend')))
    o=bpy.data.objects[name]
    assert not o.get('art11_refined'), 'Source уже изменён; повторный deformation запрещён'
    return o

def save(o):
    o['art11_refined']=True
    o.data.update()
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(o.name+'.blend')),compress=True)

def swatch(me,p,c):
    for i in p.loop_indices:me.uv_layers.active.data[i].uv=((c%8+.5)/8,(c//8+.5)/4)

# Сохраняем основу/высоту/огонь: деформируется только roof envelope и концы brackets/pendants.
o=load('Tower_FlameLantern')
for v in o.data.vertices:
    x,y,z=v.co
    if z>=1.49-1e-5:
        v.co.x*=.66;v.co.y*=.58
        if z<1.96:v.co.z+=.20*(1-(z-1.49)/.47)
    elif z>1.46 and abs(x)>.37: # конец диагонального кронштейна
        v.co.x*=.78;v.co.y*=.78;v.co.z+=.20
    elif z>=1.34 and .19<abs(x)<.37 and .19<abs(y)<.37:
        v.co.z+=.20*(z-1.34)/.12
# Подвесы вписаны под новую кромку; четыре опоры сохранены, их верх поднят вместе с просветом.
for v in o.data.vertices:
    if 1.12<v.co.z<1.49 and abs(v.co.x)>.37:
        v.co.x*=.66;v.co.y*=.58
save(o)

# Широкие холодные плоскости, а не тонкая обводка shader-ом.
o=load('Enemy_Shade');me=o.data
for v in list(me.vertices)[:10]:
    v.co.x*=1.47;v.co.y*=1.47
for p in list(me.polygons)[:40]:
    row=p.index//10;sector=p.index%10
    color=16 if row>=3 else (18 if sector%3 else 19)
    swatch(me,p,color)
swatch(me,me.polygons[40],18) # закрытое основание mantle
# Неплоский ngon с рваным краем FBX триангулировал другой диагональю.
# Явный fan к внутренней точке оставляет все нижние faces outward после round trip.
coords=[tuple(v.co) for v in me.vertices]+[(0,0,.04)]
center=len(coords)-1;faces=[];assignments=[];tones=[]
for p in me.polygons:
    tone=int(me.uv_layers.active.data[p.loop_start].uv.x*8)+8*int(me.uv_layers.active.data[p.loop_start].uv.y*4)
    if p.index==40:
        boundary=list(p.vertices)
        for j in range(len(boundary)):
            faces.append((boundary[j],boundary[(j+1)%len(boundary)],center));assignments.append(p.material_index);tones.append(tone)
    else:faces.append(tuple(p.vertices));assignments.append(p.material_index);tones.append(tone)
updated=bpy.data.meshes.new('ART11OutwardShade');updated.from_pydata(coords,[],faces);updated.update()
for m in me.materials:updated.materials.append(m)
updated.uv_layers.new(name='UVMap')
for p,slot,tone in zip(updated.polygons,assignments,tones):p.material_index=slot;swatch(updated,p,tone)
o.data=updated;bpy.data.meshes.remove(me);updated.name='Enemy_ShadeMesh'
save(o)

# Авторский perimeter покрывает прежнюю дорогу/площадки, но разрывает овал.
o=load('Environment_FloatingIsland');old=o.data;mat=old.materials[0]
vs=[];fs=[];colors=[]
def solid(outline,layers,tones):
    """Локальная сборка одного mesh, только фиксированные художественные массы ART.1.1."""
    offset=len(vs);n=len(outline)
    for z,sx,sy,dx,dy in layers:
        vs.extend((x*sx+dx,y*sy+dy,z) for x,y in outline)
    fs.append(tuple(offset+i for i in range(n)));colors.append(tones[0])
    for k in range(len(layers)-1):
        for j in range(n):
            fs.append((offset+k*n+j,offset+(k+1)*n+j,offset+(k+1)*n+(j+1)%n,offset+k*n+(j+1)%n))
            colors.append(tones[1+(j+k)% (len(tones)-1)])
    fs.append(tuple(reversed([offset+(len(layers)-1)*n+i for i in range(n)])));colors.append(4)
outline=[(-6.25,-2.45),(-5.45,-3.65),(-3.85,-4.15),(-2.75,-3.75),(-1.9,-4.05),
         (.4,-3.9),(1.1,-3.35),(2.1,-3.85),(4.15,-3.75),(5.45,-2.95),
         (5.05,-2.15),(6.15,-1.1),(6.45,.15),(5.65,.7),(6.05,1.75),
         (5.8,3.05),(4.4,3.85),(3.35,3.55),(2.7,4.35),(.75,4.25),
         (-.15,3.75),(-1.2,4.15),(-3.35,3.9),(-4.35,3.15),(-5.25,3.35),
         (-6.05,1.85),(-5.75,.75),(-6.55,-.15),(-6.4,-1.45),(-5.95,-1.8)]
solid(outline,[(-.09,1,1,0,0),(-.45,1,1,0,0),(-1.15,.92,.91,.08,.03)], [26,2,1,28])
# Три массивные, смещённые друг относительно друга скалы; расщелины и выступающие плечи.
rock=[(-1,-.65),(-.38,-1),(.42,-.85),(1,-.35),(.9,.42),(.25,1),(-.6,.8),(-1.08,.15)]
for x,y,sx,sy,z,dx,dy in [(-3.65,-.7,2.65,2.8,-6.4,1.4,1.8),
                           (.1,.75,2.5,3.1,-7.2,.4,1.4),
                           (3.7,.25,2.35,2.65,-6.1,.3,1.2)]:
    shape=[(a*sx,b*sy) for a,b in rock]
    solid(shape,[(-.5,1,1,x,y),(-1.45,1.12,1.04,x-.12,y),
                 (-2.6,.78,.77,x+.1,y), (z,.19,.24,x+dx,y+dy)], [28,6,27,4,28,29])
# Несколько широких broken shelves вместо непрерывного бортика.
for x,y,sx,sy in [(-4.6,-2.8,1.6,.75),(-1.3,-3.5,1.5,.65),(3.7,-2.9,1.6,.8),
                   (5.6,.2,.8,1.25),(-5.6,1.5,.75,1.1),(1.7,3.8,1.2,.6)]:
    solid(rock,[(-.07,sx,sy,x,y),(-.30,sx,sy,x,y),(-.64,sx*.85,sy*.82,x,y)], [1,2,28])
# Отдельные короткие каменные секции на избранных краях, оставлены крупные разрывы.
for j in (0,1,4,7,8,11,14,15,18,21,22,24,27):
    a=Vector((*outline[j],0));b=Vector((*outline[(j+1)%len(outline)],0));mid=(a+b)*.5
    direction=(b-a).normalized();normal=Vector((-direction.y,direction.x,0))*.10
    corners=[a+normal,b+normal,b-normal,a-normal]
    # CCW для outward winding верхней плоскости.
    coords=[(p.x,p.y) for p in reversed(corners)]
    solid(coords,[(.055,1,1,0,0),(-.20,1,1,0,0)], [1,2,0])
me=bpy.data.meshes.new('ART11EditableIsland');me.from_pydata(vs,[],fs);me.update();me.materials.append(mat)
uv=me.uv_layers.new(name='UVMap')
for p,c in zip(me.polygons,colors):swatch(me,p,c)
o.data=me;bpy.data.meshes.remove(old);me.name='Environment_FloatingIslandMesh'
save(o)
# Source counts измеряются из сохранённых редактируемых meshes, без записи старого evidence.
records=[]
for name in ['Tower_FlameLantern', 'Enemy_Shade', 'Environment_FloatingIsland', 'Environment_PathTile', 'Environment_BuildPad', 'Prop_Waystone', 'Prop_MossRock', 'Prop_RitualShrub']:
    path=SOURCE/(name+'.blend')
    bpy.ops.wm.open_mainfile(filepath=str(path));o=bpy.data.objects[path.stem];o.data.calc_loop_triangles()
    records.append(dict(asset=path.stem,triangles=len(o.data.loop_triangles),materialSlots=len(o.data.materials),vertices=len(o.data.vertices),source=str(path.relative_to(ROOT)),fbx='Assets/_Game/Art/Models/ART1/'+path.stem+'.fbx'))
(SOURCE/'geometry-manifest.json').write_text(json.dumps(records,indent=2)+'\n')
print('Saved 3 refined editable sources; export with export_art1.py only after baseline capture.')
