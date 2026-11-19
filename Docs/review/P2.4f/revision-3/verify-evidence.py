#!/usr/bin/env python3
"""Immutable history + exact source, full gates, raw full traces, matrix, balance and real media."""
import gzip
import hashlib
import io
import json
from pathlib import Path
import re
import struct
import subprocess
import sys
import tarfile
import xml.etree.ElementTree as ET

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[3]
BASE='a5faca3f8133a46335e6c224e23b9cb2158bc65c'
def sha(b):return hashlib.sha256(b).hexdigest()
def git(*args):return subprocess.check_output(['git',*args],cwd=ROOT)
def archive(revision,*paths):
    with tarfile.open(fileobj=io.BytesIO(git('archive',revision,*paths))) as source:
        return {member.name:source.extractfile(member).read() for member in source if member.isfile()}
def raw_report(path):
    data=gzip.decompress(path.read_bytes());r=ET.fromstring(data)
    result={key:int(r.get(key)) for key in ['total','passed','failed','inconclusive','skipped']}
    result.update(sha256=sha(data),raw_bytes=len(data),failures=[t.get('fullname') for t in r.iter('test-case') if t.get('result')=='Failed'])
    return result

def record(source):
    committed=archive(source,'Assets','Scripts','Packages','ProjectSettings')
    for name,data in committed.items():assert (ROOT/name).read_bytes()==data,name
    (HERE/'source-sha256.json').write_text(json.dumps(dict(source=source,files={name:sha(data) for name,data in sorted(committed.items())}),indent=2)+'\n')
    history=archive(BASE,'Docs/review')
    for name,data in history.items():assert (ROOT/name).read_bytes()==data,name
    (HERE/'historical-audit.json').write_text(json.dumps(dict(base=BASE,count=len(history),files={name:sha(data) for name,data in sorted(history.items())}),indent=2)+'\n')
    reports={p.name:raw_report(p) for p in sorted((HERE/'reports').glob('*.xml.gz'))}
    (HERE/'report-audit.json').write_text(json.dumps(reports,indent=2)+'\n')
    manifest={str(p.relative_to(HERE)):dict(sha256=sha(p.read_bytes()),bytes=p.stat().st_size)
              for p in sorted(HERE.rglob('*')) if p.is_file() and p.name!='manifest.json' and '__pycache__' not in p.parts}
    (HERE/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')

def verify():
    manifest=json.loads((HERE/'manifest.json').read_text())
    actual={str(p.relative_to(HERE)) for p in HERE.rglob('*') if p.is_file() and p.name!='manifest.json' and '__pycache__' not in p.parts}
    assert actual==set(manifest),'artifact set changed'
    for name,expected in manifest.items():
        data=(HERE/name).read_bytes();assert sha(data)==expected['sha256'] and len(data)==expected['bytes'],name
    source=json.loads((HERE/'source-sha256.json').read_text());committed=archive(source['source'],'Assets','Scripts','Packages','ProjectSettings')
    assert set(committed)==set(source['files'])
    for name,data in committed.items():assert sha(data)==source['files'][name] and (ROOT/name).read_bytes()==data,name
    history=json.loads((HERE/'historical-audit.json').read_text());historical=archive(BASE,'Docs/review')
    assert len(historical)==history['count']==1301 and set(historical)==set(history['files'])
    for name,data in historical.items():assert (ROOT/name).read_bytes()==data and sha(data)==history['files'][name],name
    frozen=['Assets/_Game/Configs/EightWaveAbilityBoss.asset','Assets/_Game/Configs/EightWave.asset','Assets/_Game/Configs/TestWave.asset','Packages/manifest.json','Packages/packages-lock.json','ProjectSettings/ProjectVersion.txt']
    for name,data in archive(BASE,*frozen).items():assert (ROOT/name).read_bytes()==data,'historical rules changed: '+name
    audits=json.loads((HERE/'report-audit.json').read_text())
    for name,expected in audits.items():assert raw_report(HERE/'reports'/name)==expected,name
    for name,count in [('EditMode.xml.gz',1100),('PlayMode.xml.gz',60),('Visual.xml.gz',5)]:
        r=audits[name];assert r['total']==r['passed']==count and r['failed']==r['skipped']==r['inconclusive']==0,name
    red=audits['validator-red.xml.gz'];assert red['total']==red['failed']==1 and red['passed']==red['skipped']==red['inconclusive']==0
    assert 'Ran 38 tests' in (HERE/'reports/static.log').read_text() and 'Static Unity checks: PASS' in (HERE/'reports/static.log').read_text()
    traces=0
    for path in HERE.rglob('*.trace.gz'):
        report=json.loads(path.with_name(path.name[:-9]+'.json').read_text());data=gzip.decompress(path.read_bytes())
        assert sha(data)==report['traceSha256'] and len(data)==report['rawBytes'] and len(data.decode().splitlines())==report['entries'],str(path)
        assert all(e['applied']+(e['leakedHp'] if e['leaked'] else e['remaining'])==e['maxHp'] for e in report['enemies'])
        final=report['waves'][-1];assert final['gold']==150+report['goldCommands']+report['killGold']+report['bonusGold']
        assert final['kills']==sum(e['lethal'] for e in report['enemies']);traces+=1
    for start in ['flame','gale']:
        natural=json.loads((HERE/'natural'/f'{start}.json').read_text())
        assert natural['outcome']=='Victory' and natural['bossKills']==1 and natural['bossLeaks']==0
        assert natural['abilityApplied']>0 and natural['abilityKills']>0 and all(c['applied']>0 for c in natural['cells'])
        for name in [f'double/{start}.first',f'double/{start}.second',f'files/{start}.continued',f'files/{start}.rollback7',f'files/{start}.rollback8']:
            report=json.loads((HERE/(name+'.json')).read_text());assert report==natural,name
        for wave in [7,8]:
            lines=(HERE/'files'/f'{start}.combat{wave}.prefixes.txt').read_text().splitlines();assert len(lines)%2==0
            a=lines[:len(lines)//2];b=lines[len(lines)//2:];assert a==b
            assert 'S|Combat|' in a[-1] and all(marker in a[-1] for marker in ['|E:','|A:','|D:','|Cast:','|R:'])
        assert len((HERE/'files'/f'{start}.faults.txt').read_text().splitlines())==13
    namespace={'__name__':'balance_audit'}
    exec(compile((HERE/'analyze-balance.py').read_text(),str(HERE/'analyze-balance.py'),'exec'),namespace)
    actual=namespace['analyze'](HERE);assert actual==json.loads((HERE/'balance-analysis.json').read_text())
    by_name={row['name']:row for row in actual}
    histories=['flame-tide-gale','flame-gale-tide','gale-tide-flame','gale-flame-tide']
    for history,boss_hp in zip(histories,[376,434,376,369]):
        control=json.loads((HERE/'experiments/matrix/1000'/f'{history}.json').read_text())
        original=json.loads((ROOT/'Docs/review/P2.4f/revision-2/full-replays'/f'best-{history}.full.json').read_text())
        assert control==original and control['bossHpBeforeLeak']==boss_hp
        for hp in [650,700]:assert by_name[f'experiments/matrix/{hp}/{history}']['outcome']=='Defeat'
    for start,margin in [('flame',38),('gale',26)]:
        assert by_name['natural/'+start]['margin_ticks']==margin
        for shift in [-30,-15,15,30]:
            history='flame-tide-gale' if start=='flame' else 'gale-tide-flame'
            assert by_name[f'experiments/timing/{shift}/{history}']['qualifies']
        # Matched no-boss-cast counterfactual: exact full prefix before the final cast, not speed extrapolation.
        baseline=gzip.decompress((HERE/'natural'/f'{start}.trace.gz').read_bytes()).decode().splitlines()
        other=gzip.decompress((HERE/'experiments/winning-timing'/f'{start}-boss--15.trace.gz').read_bytes()).decode().splitlines()
        last=max(i for i,line in enumerate(baseline) if line.startswith('C|Cast after tick='))
        assert baseline[:last]==other[:last]
    leak=json.loads((HERE/'controls/flame.leak.json').read_text());assert leak['outcome']=='Defeat' and leak['bossLeaks']==1 and leak['bossHpBeforeLeak']==600 and leak['waves'][6]['hp']==16
    assert not json.loads(leak['waves'][-1]['checkpoint'].split('|ability:')[0])['towers']
    console=json.loads((HERE/'console-audit.json').read_text());assert console['unexpected_errors']==0 and console['screenshots']>0 and len(console['cases'])==5
    image_count=0
    for case in console['cases']:
        directory=HERE/case['directory'];images=list(directory.glob('*.png'));assert len(images)==case['screenshots']
        assert case['max_frame']>case['min_frame']
        lines=(directory/'console.txt').read_text().splitlines();assert not any(line.startswith(('Error|','Exception|','Assert|')) for line in lines)
        assert sum(line.startswith('ExpectedError|') for line in lines)==case['expected_errors']
        for p in images:
            data=p.read_bytes();assert data[:8]==b'\x89PNG\r\n\x1a\n';assert list(struct.unpack('>II',data[16:24]))==case['resolution'];image_count+=1
        if case['directory'].startswith('play/'):
            required=['naturalCoverage-Victory','pending6','confirmed6','preparation7','preparation8','interrupted-combat7','interrupted-combat8','clean-continue7','clean-continue8','failed-confirm6','retry-confirm6','failed-completion7','retry-completion7','failed-completion8','retry-completion8','positive-Steam-StatusPair','positive-Steam-Intrinsic','positive-Storm-StatusPair','positive-Storm-Intrinsic','terminal-continue-off']
            for name in required:assert (directory/(name+'.png')).is_file(),name
    assert image_count==console['screenshots']
    assert b'\r' not in (HERE/'per-wave.csv').read_bytes()
    subprocess.run(['git','diff','--check',BASE],cwd=ROOT,check=True)
    print(f'Revision3 verified: {len(manifest)} artifacts, {traces} full traces; 1301 historical files unchanged; Static38, Edit1100, Play60, Visual5 PASS; {image_count} real PNG. REG214 same-run Victory both starts.')

if __name__=='__main__':
    if len(sys.argv)>1 and sys.argv[1]=='--record':record(sys.argv[2])
    verify()
