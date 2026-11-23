"""Run full gates sequentially; preserve exact user carry-in and only restore generated serialized bytes."""
import base64
import hashlib
import json
from pathlib import Path
import subprocess
import sys
sys.dont_write_bytecode=True
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Docs/review/ART.4/reports'
def sha(b):return hashlib.sha256(b).hexdigest()
def inputs():
    return {str(p.relative_to(ROOT)):sha(p.read_bytes()) for prefix in ('Assets','Packages','ProjectSettings') for p in (ROOT/prefix).rglob('*') if p.is_file() and not p.is_symlink()}
def main():
    historical=json.loads((ROOT/'Docs/review/ART.3e/reports/final-gates-restored.json').read_text())
    paths={r['path'] for r in historical['changed']}|set(historical['carryInSha256'])|{'Assets/_Game/Prefabs/TestTower.prefab'}
    paths|={'Assets/_Game/Art/Materials/GrayboxCellAvailable.mat','Assets/_Game/Art/Materials/GrayboxCellBlocked.mat'}
    paths|={str(p.relative_to(ROOT)) for folder in ('Assets/_Game/Prefabs/ART3/STORM','Assets/_Game/Art/Models/ART3/STORM','Assets/_Game/Prefabs/ART4','Assets/_Game/Art/Models/ART4') for p in (ROOT/folder).glob('*') if p.suffix in ('.meta','.prefab')}
    saved={p:(ROOT/p).read_bytes() for p in sorted(paths)}
    before=inputs();head=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()
    # Только малые serialized outputs известных builders; не копия проекта/Library.
    backup=ROOT/'TestResults/art4/gate-serialized-before.json'
    backup.write_text(json.dumps({p:base64.b64encode(b).decode() for p,b in saved.items()}))
    results={};changed=[]
    try:
        for name,command in [('static',['./Scripts/run-static-checks.sh']),('edit-mode',['./Scripts/run-edit-mode-tests.sh']),('play-mode',['./Scripts/run-play-mode-tests.sh'])]:
            with (OUT/(name+'-command.txt')).open('w') as log:
                results[name]=subprocess.run(command,cwd=ROOT,stdout=log,stderr=subprocess.STDOUT).returncode
            if name in ('edit-mode','play-mode'):
                raw=ROOT/'TestResults'/('EditMode.json' if name=='edit-mode' else 'PlayMode.json')
                if raw.is_file():(OUT/(name+'.json')).write_bytes(raw.read_bytes())
            if results[name]!=0:raise RuntimeError(name+' failed; raw result preserved')
    finally:
        after=inputs()
        changed=[{'path':p,'before':digest,'generated':after.get(p)} for p,digest in before.items() if after.get(p)!=digest]
        unexpected=[r['path'] for r in changed if r['path'] not in saved]
        # Не маскируем неизвестную mutation произвольным overwrite.
        for r in changed:
            if r['path'] in saved:(ROOT/r['path']).write_bytes(saved[r['path']])
        final=inputs()
        report=dict(testedSource=head,results=results,trackedInputs=len(before),changed=changed,unexpected=unexpected,
            newInputs=sorted(set(after)-set(before)),allOriginalInputsRestored=before==final,
            carryInSha256={p:sha((ROOT/p).read_bytes()) for p in historical['carryInSha256']},copiedWholeProject=False)
        (OUT/'gates-byte-audit.json').write_text(json.dumps(report,indent=2)+'\n')
        if unexpected or before!=final:raise RuntimeError('unexpected source drift; inspect byte audit')
    print(json.dumps(results))
if __name__=='__main__':main()
