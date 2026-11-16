"""Временные mesh fixtures для occlusion comparison; источники только из merge PR #24.
Перед запуском git show e33142a:ArtSource/Blender/ART1/{name}.blend > Temp/Art11Baseline/{name}.blend
Не импортирует новые Unity assets и не заменяет source of truth.
"""
import bpy,json
from pathlib import Path
root=Path(__file__).resolve().parents[2]
for name in ('Tower_FlameLantern',):
    bpy.ops.wm.open_mainfile(filepath=str(root/'Temp/Art11Baseline'/(name+'.blend')))
    o=bpy.data.objects[name];me=o.data;me.calc_loop_triangles()
    data={'vertices':[dict(x=v.co.x,y=v.co.z,z=v.co.y) for v in me.vertices],
          'indices':[i for t in me.loop_triangles for i in reversed(t.vertices)]}
    (root/'Temp/Art11Baseline'/(name+'.json')).write_text(json.dumps(data))
