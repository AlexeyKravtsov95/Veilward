"""Read-only STORM delivery verifier: exact source + pinned rendering carry-in, artifacts, protected baseline.
Не запускает Unity, не подменяет пользовательские файлы. Это не новый clean Unity run.
"""
import hashlib
import importlib.util
import json
from pathlib import Path
import struct
import subprocess
import sys
sys.dont_write_bytecode=True
ROOT=Path(__file__).resolve().parents[3]
HERE=Path(__file__).resolve().parent
PACKAGE='Docs/review/ART.3f/'
BASE='d7895fe0927f6d5890a2439fcdae4168dd224e1a'
ALLOWED_BASELINE_CHANGES={
    'README.md','Docs/TECH_DESIGN.md','Assets/_Game/Prefabs/TestTower.prefab',
    'Assets/_Game/Tests/PlayMode/BattleReactionPlayModeTests.cs',
    'Assets/_Game/Scripts/Editor/BattleSceneBuilder.cs','Assets/_Game/Scripts/Presentation/FlameModelBinding.cs',
    *('Assets/_Game/Tests/EditMode/'+n+'ModelAssetTests.cs' for n in ('Flame','Tide','Gale','Stone','Steam')),
    *('Assets/_Game/Tests/PlayMode/'+n+'ModelPlayModeTests.cs' for n in ('Tide','Gale','Stone','Steam'))}
def require(value,why):
    if not value:raise AssertionError(why)
def sha(data):return hashlib.sha256(data).hexdigest()
def git(*args):return subprocess.check_output(['git',*args],cwd=ROOT)
def files(ref):return set(git('ls-tree','-r','--name-only',ref).decode().splitlines())
def source(path):return not path.startswith(PACKAGE) or path.startswith(PACKAGE+'inputs/')
def carry_in():
    cert=json.loads((ROOT/'Docs/review/ART.3e/canonical-inputs/equivalence.json').read_text())
    return {r['path']:r for r in cert['files']}
def verify_blobs(ref,paths,rendering):
    proc=subprocess.Popen(['git','cat-file','--batch'],cwd=ROOT,stdin=subprocess.PIPE,stdout=subprocess.PIPE)
    data,_=proc.communicate(('\n'.join(ref+':'+p for p in paths)+'\n').encode());require(proc.returncode==0,'git blob read')
    offset=0
    for path in paths:
        end=data.index(b'\n',offset);fields=data[offset:end].split();require(len(fields)==3 and fields[1]==b'blob','missing '+path)
        size=int(fields[2]);offset=end+1;expected=data[offset:offset+size];offset+=size+1
        p=ROOT/path;require(p.is_file() and not p.is_symlink(),'regular source '+path)
        actual=p.read_bytes()
        if path in rendering:
            require(sha(expected)==rendering[path]['canonicalSha256'],'canonical source '+path)
            require(sha(actual)==rendering[path]['carryInSha256'],'exact rendering carry-in '+path)
        else:require(actual==expected,'source/protected drift '+path)
    require(offset==len(data),'blob inventory')
def verify_inventory(pin,tracked,rendering):
    paths=sorted(p for p in files(pin) if source(p))
    require(paths==sorted(p for p in tracked if source(p)),'exact source inventory')
    verify_blobs(pin,paths,rendering)
    return paths
def verify_artifacts(artifacts,tracked):
    for path,digest in artifacts.items():
        p=ROOT/path;require(path in tracked and p.is_file() and not p.is_symlink(),'tracked regular artifact '+path)
        require(sha(p.read_bytes())==digest,'artifact hash '+path)
def suite(path,total):
    p=json.loads(path.read_text())
    if 'Summary' in p:
        require(p.get('success') is True and p.get('Mode')=='EditMode','sync Edit result')
        summary={k.lower():v for k,v in p['Summary'].items()};cases=p.get('Results',[])
    else:
        require(p.get('status')=='completed','async complete');summary=p.get('summary',{});cases=p.get('results',[])
    require(type(total) is int and total>0,'positive requested total')
    require(type(summary.get('total')) is int and type(summary.get('passed')) is int and summary['total']==summary['passed']==total,'full positive suite '+str(path))
    require(all(type(summary.get(k)) is int and summary[k]==0 for k in ('failed','skipped','inconclusive')),'nonpass suite')
    require(len(cases)==total and all(c.get('Status')=='Passed' for c in cases),'per-case full PASS')
def main():
    m=json.loads((HERE/'manifest.json').read_text());require(m['baseline']==BASE,'baseline pin')
    rendering=carry_in()
    changed={line[3:] for line in git('status','--porcelain','--untracked-files=no').decode().splitlines()}
    require(changed==set(rendering),'only exact four declared carry-in files may be dirty')
    untracked=git('ls-files','--others','--exclude-standard').decode().splitlines()
    require(not any(p.startswith(('Assets/','Packages/','ProjectSettings/','ArtSource/','Docs/','Scripts/')) and not p.startswith('Scripts/__pycache__/') for p in untracked),'untracked source/evidence')
    pin=m['testedSource'];require(subprocess.run(['git','merge-base','--is-ancestor',pin,'HEAD'],cwd=ROOT).returncode==0,'tested source ancestor')
    tracked=set(git('ls-files').decode().splitlines())
    paths=verify_inventory(pin,tracked,rendering)
    require(sha('\n'.join(paths).encode())==m['sourceInventorySha256'],'source inventory hash')
    protected=sorted(files(BASE)-ALLOWED_BASELINE_CHANGES);verify_blobs(BASE,protected,rendering)
    actual={str(p.relative_to(ROOT)) for p in HERE.rglob('*') if p.is_file() and p.name!='manifest.json'}
    require(actual==set(m['artifacts']),'exact evidence inventory');verify_artifacts(m['artifacts'],tracked)
    spec=importlib.util.spec_from_file_location('current_inputs',HERE/'verify_render_inputs.py');module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
    receipt=module.verify();require(receipt==json.loads((HERE/'reports/render-input-applicability.json').read_text()),'recomputed certificate applicability')
    loaded=json.loads((HERE/'reports/actual-render-inputs.json').read_text())
    require(loaded['actualPlayMode'] and loaded['pipelinePath']=='Assets/Settings/Mobile_RPAsset.asset' and loaded['quality']=='Mobile','actual loaded Battle pipeline')
    for path,digest in loaded['inputSha256'].items():require(sha((ROOT/path).read_bytes())==digest,'actual loaded input SHA '+path)
    for mode,total in m['suites'].items():suite(HERE/('reports/'+mode+'.json'),total)
    static=(HERE/'reports/static-command.txt').read_text();require('Ran 38 tests' in static and 'Static Unity checks: PASS' in static,'static full PASS')
    compile=json.loads((HERE/'reports/compile.json').read_text());require(compile['status'] in ('completed','up_to_date') and not compile['compilationFailed'] and not compile['failed'] and compile['errors']==[],'compile PASS')
    console=json.loads((HERE/'reports/console-warnings-errors.json').read_text())
    classification=json.loads((HERE/'reports/console-classification.json').read_text())
    require(classification['entries']==len(console['entries'])==236 and not classification['unexpectedEntries'] and classification['consoleCleared'] is False,'complete classified Console history')
    require(classification['counts']['expectedCheckpointErrors']==226 and classification['counts']['expectedFrozenMeasurementGuard']==2 and classification['counts']['resolvedAttemptCompileErrors']==2,'explicit expected/resolved error counts')
    audit=json.loads((HERE/'reports/gates-byte-audit.json').read_text());require(audit['testedSource']==pin and audit['allOriginalInputsRestored'] and not audit['unexpected'] and not audit['newInputs'],'gate byte restoration')
    require(audit['results']=={'static':0,'edit-mode':0,'play-mode':0},'gate commands success')
    before=json.loads((HERE/'reports/reexport-before.json').read_text());after=json.loads((HERE/'reports/reexport-after.json').read_text())
    require(before==after and len(after['models'])==3,'real three source roundtrip')
    require(after==json.loads((HERE/'reports/reexport-canonical-readback.json').read_text()),'canonical post-suite geometry/ref readback')
    for r in after['files']:require(sha((ROOT/r['path']).read_bytes())==r['sha256'],'canonical roundtrip refs '+r['path'])
    intrinsic=json.loads((HERE/'reports/intrinsic-positive.json').read_text());require(intrinsic['tower']==3 and intrinsic['primary']==intrinsic['attackTarget'] and intrinsic['reactionSequence']>intrinsic['directSequence'],'real Attack intrinsic attribution')
    hits=intrinsic['hits'];require(1<=len(hits)<=2 and len({h['enemy'] for h in hits})==len(hits) and all(h['enemy']!=intrinsic['primary'] and h['loss']>0 and h['sequence']>intrinsic['reactionSequence'] for h in hits),'positive additional HP loss no primary')
    for level in (1,2,3):
        p=HERE/('playmode/single-L'+str(level)+'.png');data=p.read_bytes();require(data[:8]==b'\x89PNG\r\n\x1a\n' and struct.unpack('>II',data[16:24])==(1600,900),'real PNG dimensions')
    video=json.loads((HERE/'reports/video-proof.json').read_text());completion=json.loads((HERE/'reports/video-completion.json').read_text())
    require(completion['success'] and video['sourceFrames']==video['outputFrames']>50 and video['speed']==1 and video['splices']==0,'continuous actual video')
    require(video['outputSha256']==sha((HERE/'editor-flow.mp4').read_bytes()),'video hash')
    require((HERE/'reports/video-safe-wire.txt').read_bytes()==(HERE/'reports/video-rollback-wire.txt').read_bytes(),'whole live Grid/snapshot rollback')
    require(not (HERE/'reports/video-console.txt').read_text().strip(),'recording Console errors/warnings')
    require(m['device']=='Deferred to prerelease QA #53/#54/#55','device boundary')
    print(f"PASS: {len(paths)} source / {len(protected)} protected / {len(actual)} artifacts; exact carry-in4 + accepted Battle equivalence; new Unity suites {m['suites']}; read-only verifier is not a Unity run")
if __name__=='__main__':main()
