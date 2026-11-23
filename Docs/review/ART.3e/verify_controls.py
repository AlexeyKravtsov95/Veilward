"""Малые Git fixtures текущих source/artifact validators; Unity project не копирует и не меняет."""
import contextlib
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import subprocess
import sys
import tempfile
sys.dont_write_bytecode = True

FOLDER = Path(__file__).resolve().parent
VERIFIER = FOLDER/'verify_delivery.py'
spec=importlib.util.spec_from_file_location('art3e_delivery',VERIFIER)
v=importlib.util.module_from_spec(spec);spec.loader.exec_module(v)


def main():
    commands=[]
    with tempfile.TemporaryDirectory(prefix='art3e-small-verifier-fixture-') as directory:
        fixture=Path(directory);v.ROOT=fixture
        def git(*args):
            command=['git',*args]
            r=subprocess.run(command,cwd=fixture,capture_output=True,text=True)
            commands.append(dict(command=command,exit=r.returncode,stdout=r.stdout,stderr=r.stderr))
            if r.returncode:raise RuntimeError(r.stderr)
            return r.stdout.strip()
        def commit(message):
            git('add','--all');git('-c','user.name=ART3E fixture','-c','user.email=control@localhost','commit','-m',message)
            assert not git('status','--porcelain')
        source='Assets/Fixture.txt';artifact=v.PACKAGE+'fixture.json'
        for path,data in [(source,b'original fixture source\n'),(artifact,b'{"original":true}\n')]:
            p=fixture/path;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(data)
        git('init');commit('fixture baseline');baseline=git('rev-parse','HEAD')
        tracked=lambda:set(git('ls-files').splitlines())
        digest=hashlib.sha256((fixture/artifact).read_bytes()).hexdigest()
        v.verify_inventory(baseline,tracked());v.verify_artifacts({artifact:digest},tracked())
        positive=dict(passed=True,clean=True,sourceFiles=1,artifactFiles=1,baseline=baseline)
        negative=[]
        def reject(name,reason,operation):
            assert not git('status','--porcelain')
            try:operation()
            except SystemExit as e:
                message=str(e);assert reason in message,message
                negative.append(dict(name=name,passed=True,expectedExit=1,reason=message,clean=True,head=git('rev-parse','HEAD')))
            else:raise AssertionError(name+' incorrectly accepted')
        foreign=fixture/'Assets/ForeignFixture.txt';foreign.write_text('foreign source\n');commit('foreign source inventory')
        reject('foreign source inventory','tested source inventory',lambda:v.verify_inventory(baseline,tracked()))
        foreign.unlink();(fixture/source).write_text('foreign changed source\n');commit('foreign source bytes')
        reject('foreign source bytes','source/history drift',lambda:v.verify_inventory(baseline,tracked()))
        (fixture/source).write_bytes(b'original fixture source\n');(fixture/artifact).write_text('{"tampered":true}\n');commit('foreign artifact bytes')
        v.verify_inventory(baseline,tracked())
        reject('artifact bytes','artifact digest',lambda:v.verify_artifacts({artifact:digest},tracked()))
    report=dict(verifierSha256=hashlib.sha256(VERIFIER.read_bytes()).hexdigest(),positive=positive,negative=negative,
        commands=commands,scope='actual common inventory/blob/artifact validators on two tiny files; clean Git commits, no dirty shortcut',
        unityProjectCopied=False,worktreeCreated=False,cloneCreated=False,productionCheckoutMutated=False,
        fullDeliveryEntryPoint='run read-only verify_delivery.py separately in current checkout',
        historicalArt3dControlsRerun=False)
    (FOLDER/'reports/delivery-controls.json').write_text(json.dumps(report,indent=2)+'\n')
    print('PASS: current small-fixture source inventory/source blob/artifact negative controls; no Unity checkout copied')


if __name__=='__main__':main()
