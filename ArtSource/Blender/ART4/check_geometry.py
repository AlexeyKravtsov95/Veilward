"""Read-only source checks: no regeneration/export and no dependence on manifest claims."""
import bpy,json,math
from pathlib import Path
folder=Path(__file__).resolve().parent
manifest=json.loads((folder/'geometry-manifest.json').read_text())
for record in manifest:
 bpy.ops.wm.open_mainfile(filepath=str(folder/(record['asset']+'.blend')))
 sc=bpy.context.scene
 assert sc.unit_settings.system=='METRIC' and sc.unit_settings.scale_length==1
 objects=[o for o in sc.objects if o.type=='MESH'];assert len(objects)==1
 o=objects[0];m=o.data;m.calc_loop_triangles()
 assert all(abs(v)<1e-6 for v in o.location) and all(abs(v)<1e-6 for v in o.rotation_euler) and all(abs(v-1)<1e-6 for v in o.scale)
 assert len(m.loop_triangles)==record['triangles'] and len(m.vertices)==record['vertices']
 assert len(m.materials)==record['slots']<=2 and set(x.name for x in m.materials)<={'Art1Surface','Art1Glow'}
 assert len(o.vertex_groups)>=3 and m.uv_layers.active is not None
 assert all(0<uv.uv.x<1 and 0<uv.uv.y<1 for uv in m.uv_layers.active.data)
 assert all(abs(p.normal.length-1)<1e-5 for p in m.polygons)
 budget=5000 if record['asset'].endswith('Island') else 400 if record['asset'].endswith(('Path','Socket')) else 1000 if record['asset'].endswith('Pine') else 2000
 assert 0<len(m.loop_triangles)<=budget
 print('PASS',record['asset'],len(m.loop_triangles),'tri',len(m.materials),'slots',len(o.vertex_groups),'semantic groups')
