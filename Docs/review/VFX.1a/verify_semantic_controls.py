"""Reproduce eight independent review mutations in memory, without project writes."""
import contextlib
import copy
import importlib.util
import io
from pathlib import Path
import sys
from unittest.mock import patch
sys.dont_write_bytecode=True
spec=importlib.util.spec_from_file_location('delivery',Path(__file__).with_name('verify_delivery.py'));v=importlib.util.module_from_spec(spec);spec.loader.exec_module(v)
# Baseline must pass first; an unrelated delivery failure cannot make all negative cases appear green.
with contextlib.redirect_stdout(io.StringIO()):v.main()
original=v.read
cases={
 'wrong source digest':('manifest.json',lambda x:x['source'].__setitem__('Assets/_Game/Scripts/Presentation/FlameProjectilePool.cs','0'*64),'source: Assets/_Game/Scripts/Presentation/FlameProjectilePool.cs'),
 'wrong artifact digest':('manifest.json',lambda x:x['artifacts'].__setitem__('Docs/review/VFX.1a/editor-flow.mp4','0'*64),'artifact: Docs/review/VFX.1a/editor-flow.mp4'),
 'non-FlameImpulse shader':('reports/actual-render-inputs.json',lambda x:x['materials'][0].__setitem__('shader','Unlit/Color'),'actual shared projectile material inputs'),
 'wrong actual pool capacity':('reports/actual-render-inputs.json',lambda x:x.__setitem__('capacity',9),'actual bounded pool'),
 'replay difference':('reports/after-flame.json',lambda x:x.__setitem__('reviewTamper',True),'whole replay report changed flame'),
 'video speed alteration':('reports/video-proof.json',lambda x:x.__setitem__('speed',2),'complete continuous video'),
 'video PTS alteration':('reports/video-decoded.json',lambda x:x['frames'][1].__setitem__('best_effort_timestamp_time','9.0'),'strict advancing decoded PTS'),
 'false compile result':('reports/compile.json',lambda x:x.__setitem__('failed',True),'final compile'),
}
for label,(path,change,expected) in cases.items():
 def read(p):
  x=copy.deepcopy(original(p))
  if str(p)==path:change(x)
  return x
 with patch.object(v,'read',read):
  try:v.main()
  except ValueError as error:
   assert str(error)==expected,(label,str(error),expected)
   print('REJECT',label,':',error)
  else:raise AssertionError('ACCEPTED '+label)
print('Independent in-memory semantic controls 8/8 rejected; no project writes')
