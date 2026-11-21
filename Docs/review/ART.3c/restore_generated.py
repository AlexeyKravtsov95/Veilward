"""Возвращает только task-owned Unity-generated outputs к tested source после suites."""
import gzip
import hashlib
import json
from pathlib import Path
import subprocess
import sys
ROOT = Path(__file__).resolve().parents[3]
SOURCE = '9871af6'
label = sys.argv[1]
paths = subprocess.check_output(['git','diff','--name-only','--','Assets'],cwd=ROOT).decode().splitlines()
rows=[]
patch=subprocess.check_output(['git','diff','--binary','--','Assets'],cwd=ROOT)
for name in paths:
    if '/Scripts/' in name or '/Tests/' in name:
        raise SystemExit('Unexpected code mutation: '+name)
    p=ROOT/name
    old=p.read_bytes(); original=subprocess.check_output(['git','show',SOURCE+':'+name],cwd=ROOT)
    p.write_bytes(original)
    h=lambda b:hashlib.sha256(b).hexdigest()
    rows.append(dict(path=name,generatedSha256=h(old),sourceSha256=h(original),restoredSha256=h(p.read_bytes())))
folder=ROOT/'Docs/review/ART.3c/reports'
(folder/(label+'.json')).write_text(json.dumps(dict(source=SOURCE,files=rows),indent=2)+'\n')
(folder/(label+'.patch.gz')).write_bytes(gzip.compress(patch,mtime=0))
print('Restored exact tested-source asset bytes:',len(rows))
