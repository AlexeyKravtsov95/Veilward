"""Run full gates sequentially; preserve exact user carry-in and only restore generated serialized bytes."""
import base64
import hashlib
import json
from pathlib import Path
import subprocess
import sys
sys.dont_write_bytecode=True
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Docs/review/VFX.1a/reports'
def sha(b):return hashlib.sha256(b).hexdigest()
def inputs():
    return {str(p.relative_to(ROOT)):sha(p.read_bytes()) for prefix in ('Assets','Packages','ProjectSettings') for p in (ROOT/prefix).rglob('*') if p.is_file() and not p.is_symlink()}
def run_editor(stage,code):
    command=[str(Path.home()/'.unity/bin/unity'),'command','script eval','--caller','plugin','--skill','unity-cli','--project-path',str(ROOT),'--format','json','--timeout','45','--code',code]
    try:
        result=subprocess.run(command,cwd=ROOT,capture_output=True,text=True,timeout=55)
    except subprocess.TimeoutExpired as error:
        output=error.stdout or '';output=output.decode(errors='replace') if isinstance(output,bytes) else output
        (OUT/(stage+'.json')).write_text(output)
        raise
    (OUT/(stage+'.json')).write_text(result.stdout)
    (OUT/(stage+'-stderr.txt')).write_text(result.stderr)
    payload=json.loads(result.stdout) if result.stdout else {}
    if result.returncode!=0 or payload.get('success') is not True:
        raise RuntimeError(stage+' failed exit='+str(result.returncode)+' '+result.stdout+' '+result.stderr)
    return payload

def restore_known_inputs(saved,before,after,execute=run_editor):
    # Audit и exact known-byte restoration обязательны даже при timeout/ошибке Editor.
    changed=[{'path':p,'before':digest,'generated':after.get(p)} for p,digest in before.items() if after.get(p)!=digest]
    unexpected=[r['path'] for r in changed if r['path'] not in saved]
    diagnostics=[];detached=False;refreshed=False
    try:
        execute('pre-restore-empty-scene','if (UnityEditor.EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop task test Play Mode first"); UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single); return "Closed task suite scene before exact disk restoration";')
        detached=True
    except Exception as error:
        diagnostics.append(dict(stage='detach',error=repr(error)))
    finally:
        for row in changed:
            if row['path'] in saved:
                try:(ROOT/row['path']).write_bytes(saved[row['path']])
                except Exception as error:diagnostics.append(dict(stage='restore',path=row['path'],error=repr(error)))
        # При failed detach не enqueue Refresh/Open за неизвестным modal; disk preservation имеет приоритет.
        if detached:
            try:
                execute('restored-refresh','UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceSynchronousImport); UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Game/Scenes/Battle.unity"); return "Refreshed exact restored inputs and reopened saved Battle";')
                refreshed=True
            except Exception as error:diagnostics.append(dict(stage='refresh',error=repr(error)))
        final=inputs()
    return dict(trackedInputs=len(before),changed=changed,unexpected=unexpected,newInputs=sorted(set(after)-set(before)),allOriginalInputsRestored=before==final,
        editorSceneDetached=detached,editorInputsRefreshed=refreshed,recoveryNeeded=bool(diagnostics),commandDiagnostics=diagnostics,copiedWholeProject=False)

def main():
    historical=json.loads((ROOT/'Docs/review/ART.3e/reports/final-gates-restored.json').read_text())
    paths={r['path'] for r in historical['changed']}|set(historical['carryInSha256'])|{'Assets/_Game/Prefabs/TestTower.prefab'}
    paths|={'Assets/_Game/Art/Materials/GrayboxCellAvailable.mat','Assets/_Game/Art/Materials/GrayboxCellBlocked.mat'}
    paths|={str(p.relative_to(ROOT)) for folder in ('Assets/_Game/Prefabs/ART3/STORM','Assets/_Game/Art/Models/ART3/STORM','Assets/_Game/Prefabs/ART4','Assets/_Game/Art/Models/ART4','Assets/_Game/Prefabs/ART5','Assets/_Game/Art/Models/ART5') for p in (ROOT/folder).glob('*') if p.suffix in ('.meta','.prefab')}
    saved={p:(ROOT/p).read_bytes() for p in sorted(paths)}
    before=inputs();head=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()
    # Только малые serialized outputs известных builders; не копия проекта/Library.
    backup=ROOT/'TestResults/vfx1a/gate-serialized-before.json'
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
        report=restore_known_inputs(saved,before,inputs())
        final=inputs()
        report.update(testedSource=head,results=results,carryInSha256={p:final.get(p) for p in historical['carryInSha256']})
        (OUT/'gates-byte-audit.json').write_text(json.dumps(report,indent=2)+'\n')
        if report['unexpected'] or not report['allOriginalInputsRestored'] or report['recoveryNeeded']:
            raise RuntimeError('source drift or Editor recovery needed; exact byte/command audit preserved')
    print(json.dumps(results))
if __name__=='__main__':main()
