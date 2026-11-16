"""Blender-side regression редактируемого source и фактического exported FBX."""
import bpy
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[4]

def check(label):
    groups={(color,side):[] for color in (17,18) for side in (-1,1)}
    for obj in bpy.context.scene.objects:
        if obj.type!='MESH':continue
        data=obj.data;data.calc_loop_triangles();uv=data.uv_layers.active
        for tri in data.loop_triangles:
            points=[obj.matrix_world @ data.vertices[i].co for i in tri.vertices]
            coords=[uv.data[i].uv for i in tri.loops]
            for (color,side),triangles in groups.items():
                if not all(abs(p.x-(color%8+.5)/8)<.001 and abs(p.y-(color//8+.5)/4)<.001 for p in coords):continue
                if not all(p.x*side>0 and (color==17 or p.z>.98) for p in points):continue
                triangles.append(points)
    for (color,side),triangles in groups.items():
        unique={tuple(round(x,6) for x in p) for t in triangles for p in t}
        assert len(unique)==5 and len(triangles)==6,(label,color,side,'incomplete hull',len(unique),len(triangles))
        center=sum((Vector(p) for p in unique),Vector())/len(unique)
        volume=0
        for i,(a,b,c) in enumerate(triangles):
            direction=(b-a).cross(c-a).dot((a+b+c)/3-center)
            assert direction>1e-7,(label,color,side,i,'inward winding',direction)
            volume+=(a-center).dot((b-center).cross(c-center))/6
        assert volume>1e-6,(label,color,side,'negative volume',volume)
        print('PASS',label,'swatch',color,'side',side,'6 outward triangles; volume',volume)

bpy.ops.wm.open_mainfile(filepath=str(ROOT/'ArtSource/Blender/ART1/Enemy_Shade.blend'))
check('saved blend')
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/_Game/Art/Models/ART1/Enemy_Shade.fbx'))
check('exported FBX')
