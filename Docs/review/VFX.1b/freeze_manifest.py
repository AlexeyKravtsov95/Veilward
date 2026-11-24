"""Freeze committed source/evidence inventory; no Unity or project copies."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import runpy
sys.dont_write_bytecode=True
ROOT=Path(__file__).resolve().parents[3];P=Path(__file__).resolve().parent
tracked=[p for p in subprocess.check_output(['git','ls-files','-z'],cwd=ROOT).decode().split('\0') if p]
head=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()
sha=lambda p:hashlib.sha256((ROOT/p).read_bytes()).hexdigest()
namespace=runpy.run_path(str(P/'verify_delivery.py'))
source={p:sha(p) for p in sorted(namespace['source_keys'](tracked))}
artifacts={p:sha(p) for p in sorted(namespace['artifact_keys'](tracked))}
existing=sorted(namespace['CHANGES'])
m=dict(baseline='8a9e3cb172ab6abe55efafbbf1232a2a5b53912d',testedSource=json.loads((P/'reports/gates-byte-audit.json').read_text())['testedSource'],mediaSource=json.loads((P/'reports/media-source.json').read_text())['mediaSource'],evidenceSource=head,changedExisting=existing,trackedInventory=tracked,source=source,artifacts=artifacts,totals={'edit-mode':1283,'play-mode':98})
(P/'manifest.json').write_text(json.dumps(m,indent=2,ensure_ascii=False)+'\n')
print(head,len(source),len(artifacts))
