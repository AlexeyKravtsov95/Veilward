"""Freeze committed source/evidence inventory; no Unity or project copies."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
sys.dont_write_bytecode=True
ROOT=Path(__file__).resolve().parents[3];P=Path(__file__).resolve().parent
tracked=[p for p in subprocess.check_output(['git','ls-files','-z'],cwd=ROOT).decode().split('\0') if p]
head=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()
sha=lambda p:hashlib.sha256((ROOT/p).read_bytes()).hexdigest()
source={p:sha(p) for p in tracked if p.startswith(('Assets/','Packages/','ProjectSettings/','Scripts/','ArtSource/'))}
artifacts={p:sha(p) for p in tracked if (p.startswith('Docs/review/VFX.1b/') or p in ('README.md','Docs/TECH_DESIGN.md','Docs/ROADMAP.md','Docs/VFX1B_BASIC_SHOTS_STATUS.md')) and p!='Docs/review/VFX.1b/manifest.json'}
namespace={}
exec(compile((P/'verify_delivery.py').read_text(),str(P/'verify_delivery.py'),'exec'),dict(__name__='freeze_input',__file__=str(P/'verify_delivery.py')),namespace)
existing=sorted(namespace['CHANGES'])
m=dict(baseline='8a9e3cb172ab6abe55efafbbf1232a2a5b53912d',testedSource=json.loads((P/'reports/gates-byte-audit.json').read_text())['testedSource'],mediaSource=json.loads((P/'reports/media-source.json').read_text())['mediaSource'],evidenceSource=head,changedExisting=existing,trackedInventory=tracked,source=source,artifacts=artifacts,totals={'edit-mode':1283,'play-mode':98})
(P/'manifest.json').write_text(json.dumps(m,indent=2,ensure_ascii=False)+'\n')
print(head,len(source),len(artifacts))
