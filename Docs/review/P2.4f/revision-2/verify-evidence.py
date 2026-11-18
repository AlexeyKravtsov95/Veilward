#!/usr/bin/env python3
"""P2.4f revision2: source/history, raw XML, traces, bounded journal и media audit."""
import gzip
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import tarfile
import tempfile
import xml.etree.ElementTree as ET

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[3]
BASE='0e3a755ca72cef9ad56859330c0502704b0d93f9'


def sha(data):return hashlib.sha256(data).hexdigest()
def git(*args):return subprocess.check_output(['git',*args],cwd=ROOT)

def report(path):
    data=gzip.decompress(path.read_bytes());r=ET.fromstring(data)
    result={k:int(r.get(k))for k in ['total','passed','failed','inconclusive','skipped']}
    assert result['total']>0 and result['total']==sum(result[k]for k in ['passed','failed','inconclusive','skipped'])
    result.update(raw_sha256=sha(data),raw_bytes=len(data),sha256=sha(path.read_bytes()),
                  failures=[t.get('name')for t in r.findall('.//test-case[@result="Failed"]')])
    return result


def record():
    source=sys.argv[2]
    files=git('ls-files','--','Assets','Scripts','Packages','ProjectSettings').decode().splitlines()
    current={}
    for name in files:
        data=(ROOT/name).read_bytes();assert data==git('show',source+':'+name),name
        current[name]=sha(data)
    (HERE/'source-sha256.json').write_text(json.dumps({'source':source,'files':current},indent=2)+'\n')
    history={}
    for name in git('ls-tree','-r','--name-only',BASE,'--','Docs/review').decode().splitlines():
        data=git('show',BASE+':'+name);assert (ROOT/name).read_bytes()==data,name;history[name]=sha(data)
    (HERE/'historical-audit.json').write_text(json.dumps({'base':BASE,'count':len(history),'files':history},indent=2)+'\n')
    reports={p.name:report(p)for p in sorted((HERE/'reports').glob('*.xml.gz'))}
    (HERE/'report-audit.json').write_text(json.dumps(reports,indent=2)+'\n')
    manifest={str(p.relative_to(HERE)):{'sha256':sha(p.read_bytes()),'bytes':p.stat().st_size}
              for p in sorted(HERE.rglob('*'))if p.is_file()and p.name!='manifest.json'}
    (HERE/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')


def verify():
    manifest=json.loads((HERE/'manifest.json').read_text())
    for name,expected in manifest.items():
        data=(HERE/name).read_bytes();assert len(data)==expected['bytes']and sha(data)==expected['sha256'],name
    source=json.loads((HERE/'source-sha256.json').read_text())
    for name,expected in source['files'].items():
        assert sha((ROOT/name).read_bytes())==expected,name
        assert sha(git('show',source['source']+':'+name))==expected,name
    history=json.loads((HERE/'historical-audit.json').read_text())
    for name,expected in history['files'].items():
        assert sha((ROOT/name).read_bytes())==expected and sha(git('show',BASE+':'+name))==expected,name
    # Старые matrix implementations, production и packages должны быть буквально прежними.
    for scope in ['Assets/_Game/Scripts','Assets/_Game/Configs','Assets/_Game/Scenes','Assets/_Game/Art','Packages','ProjectSettings','Scripts']:
        assert not git('diff','--name-only',BASE,'--',scope).strip(),scope
    for name in git('ls-tree','-r','--name-only',BASE,'--','Assets/_Game/Tests').decode().splitlines():
        assert (ROOT/name).read_bytes()==git('show',BASE+':'+name),'old test changed: '+name
    audits=json.loads((HERE/'report-audit.json').read_text())
    for name,expected in audits.items():assert report(HERE/'reports'/name)==expected,name
    for name,total in [('targeted-full.xml.gz',2),('targeted-replay.xml.gz',4),('PlayMode.xml.gz',60),('Visual.xml.gz',5)]:
        r=audits[name];assert r['total']==r['passed']==total and r['failed']==r['inconclusive']==r['skipped']==0,name
    r=audits['EditMode.xml.gz'];assert (r['total'],r['passed'],r['failed'],r['inconclusive'],r['skipped'])==(1077,1075,2,0,0)
    assert set(r['failures'])=={
        'RegBattle214_SameProductionRunMustWinWithAbilityBossAndFourPositiveCells("flame")',
        'RegBattle214_SameProductionRunMustWinWithAbilityBossAndFourPositiveCells("gale")'}
    member_hashes=json.loads((HERE/'archive-members.json').read_text())
    with tempfile.TemporaryDirectory(prefix='veilward-targeted-audit-')as temp:
        with tarfile.open(HERE/'search.tar.gz','r:gz')as archive:
            members=archive.getmembers();assert {m.name for m in members}==set(member_hashes)
            for member in members:
                assert member.isfile()and not Path(member.name).is_absolute()and '..'not in Path(member.name).parts
                data=archive.extractfile(member).read();e=member_hashes[member.name]
                assert len(data)==e['bytes']and sha(data)==e['sha256'],member.name
                destination=Path(temp)/member.name;destination.parent.mkdir(parents=True,exist_ok=True);destination.write_bytes(data)
        namespace={'__name__':'search_audit'}
        exec(compile((HERE/'analyze-search.py').read_text(),str(HERE/'analyze-search.py'),'exec'),namespace)
        actual=json.loads(json.dumps(namespace['analyze'](Path(temp))));assert actual==json.loads((HERE/'search-analysis.json').read_text())
        assert actual['wave_outcomes']==99992 and actual['full_new_game_outcomes']==4 and actual['total_outcomes']==99996
        assert sum(s['duplicates']for s in actual['starts'].values())==16301
    for ordered_history,boss_hp in [('flame-tide-gale',376),('flame-gale-tide',434),('gale-tide-flame',376),('gale-flame-tide',369)]:
        a=HERE/'full-replays'/('best-'+ordered_history+'.full');b=HERE/'fixed-replays'/ordered_history
        ra=json.loads(Path(str(a)+'.json').read_text());rb=json.loads(Path(str(b)+'.json').read_text())
        assert ra==rb,ordered_history
        for prefix in [a,b]:
            data=gzip.decompress(Path(str(prefix)+'.trace.gz').read_bytes());report_data=json.loads(Path(str(prefix)+'.json').read_text())
            assert sha(data)==report_data['traceSha256']and len(data)==report_data['rawBytes']
        assert ra['outcome']=='Defeat'and ra['bossKills']==0 and ra['bossLeaks']==1 and ra['bossHpBeforeLeak']==boss_hp
        assert ra['abilityKills']>0 and ra['abilityApplied']>0 and all(c['applied']>0 for c in ra['cells'])
        assert all(e['applied']+(e['leakedHp']if e['leaked']else e['remaining'])==e['maxHp']for e in ra['enemies'])
    console=json.loads((HERE/'console-audit.json').read_text());assert console['screenshots']==108 and console['unexpected_errors']==0
    assert all(c['max_frame']>c['min_frame']for c in console['cases'])
    static=(HERE/'reports/static-runner.txt').read_text();assert 'Ran 38 tests'in static and 'Static Unity checks: PASS'in static
    print(f'Revision2 verified: {len(manifest)} artifacts; {len(history["files"])} historical files unchanged; '
          '99992 wave-only +4 full-NewGame outcomes, Victory0; Edit1075/1077(2mandatory FAIL), Play60/60,Visual5/5. REG214 BLOCKED.')


if __name__=='__main__':
    if len(sys.argv)>1 and sys.argv[1]=='--record':record()
    verify()
