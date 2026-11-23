"""Read-only actual editable-source live-pose ground clearance; no regeneration/export."""
import bpy,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3];rows=[]
for name in ('Enemy_Shade','Enemy_RiftWarden'):
 bpy.ops.wm.open_mainfile(filepath=str(ROOT/'ArtSource/Blender/ART5'/(name+'.blend')))
 phases=[]
 for clip,first,last in [('Idle',1,31),('Move',32,62)]:
  values=[]
  for frame in range(first,last+1):
   bpy.context.scene.frame_set(frame)
   points=[o.matrix_world@v.co for o in bpy.context.scene.objects if o.type=='MESH' for v in o.data.vertices]
   values.append(dict(frame=frame,minY=min(p.z for p in points),maxY=max(p.z for p in points)))
  phases.append(dict(clip=clip,minY=min(r['minY'] for r in values),maxY=max(r['maxY'] for r in values),frames=values))
 rows.append(dict(model=name,sourceWorldMeterBounds=True,liveClipBounds=phases))
(ROOT/'Docs/review/ART.5/reports/motion-bounds.json').write_text(json.dumps(rows,indent=2)+'\n')
print([(r['model'],[(p['clip'],p['minY'],p['maxY']) for p in r['liveClipBounds']]) for r in rows])
