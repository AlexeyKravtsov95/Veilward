#!/usr/bin/env python3
"""R3 provenance/acceptance audit. Editor evidence не заменяет устройство/G2/#40."""
from pathlib import Path
import csv,gzip,hashlib,json,struct,subprocess,tarfile,xml.etree.ElementTree as ET
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
sha=lambda b:hashlib.sha256(b).hexdigest()
g=json.loads((HERE/'gates.json').read_text());m=json.loads((HERE/'source-sha256.json').read_text())
assert g['all_suites_pass'] is True and g['editor_acceptance']=='VERIFIED'
assert g['selected']=={'hp':1000,'armor':5,'changedField':'boss.armor','control':{'hp':1000,'armor':10}}
for path,digest in g['artifacts'].items():assert sha((HERE/path).read_bytes())==digest,path
for label,report in g['reports'].items():
 packed=(HERE/report['path']).read_bytes();raw=gzip.decompress(packed);assert sha(raw)==report['raw_sha256'] and sha(packed)==report['sha256']
 r=ET.fromstring(raw)
 for key in ['total','passed','failed','inconclusive','skipped']:assert int(r.get(key))==report[key],(label,key)
 assert report['total']>0 and report['total']==report['passed'] and report['failed']==report['skipped']==report['inconclusive']==0,label
 assert not [t for t in r.iter('test-case') if t.get('result')=='Failed']
for category in ['files','inputs','controls','historical']:
 for path,digest in m[category].items():assert sha((ROOT/path).read_bytes())==digest,path
for path,digest in {**m['files'],**m['inputs']}.items():assert sha(subprocess.check_output(['git','show',g['source']+':'+path],cwd=ROOT))==digest,path
for revision,files in m['research_files'].items():
 for path,digest in files.items():assert sha(subprocess.check_output(['git','show',revision+':'+path],cwd=ROOT))==digest,path
# Historical source is checked in Git, not rebound to the changed R3 worktree.
for old in [HERE.parent,HERE.parent/'revision-2']:
 old_g=json.loads((old/'gates.json').read_text());old_m=json.loads((old/'source-sha256.json').read_text())
 for path,digest in old_g['artifacts'].items():assert sha((old/path).read_bytes())==digest,path
 for path,digest in old_m['files'].items():assert sha(subprocess.check_output(['git','show',old_g['source']+':'+path],cwd=ROOT))==digest,path
entries=json.loads((HERE/'coverage-search-sha256.json').read_text())
with tarfile.open(HERE/'coverage-search.tar.gz') as archive:
 members={p.name:p for p in archive.getmembers() if p.isfile()};assert set(members)==set(entries)
 for path,digest in entries.items():assert sha(archive.extractfile(members[path]).read())==digest,path
 searches=json.loads((HERE/'search-summary.json').read_text())['passes'];assert sum(x['attempts'] for x in searches.values())==60000
 for path,info in searches.items():
  lines=archive.extractfile(path+'/candidates.txt').read().decode().splitlines();assert len(lines)==info['attempts']
  phases={}
  for line in lines:phase=line.split('|')[1];phases[phase]=phases.get(phase,0)+1
  assert phases==info['phases']
matrix=json.loads((HERE/'candidate-matrix.json').read_text());assert matrix['selected']=='1000-5'
assert {k for k,v in matrix['eligibility'].items() if v['eligible']}=={'800-10','1000-5'}
assert set(matrix['eligibility'])=={'1000-10','900-10','800-10','1000-8','1000-5'}
assert len(matrix['rows'])==20
for row in matrix['rows']:
 assert row['initialGold']==150 and row['initialHP']==20
 assert row['bossDirect']+row['bossBurn']+row['bossReaction']+row['bossAbility']+row['bossRemaining']==row['hp']
for route in ['flame-stone','gale-stone','flame-coverage','gale-coverage']:
 control=(HERE/'matrix/1000-10'/(route+'.waves.jsonl')).read_text().splitlines()[:7]
 for variant in matrix['eligibility']:assert (HERE/'matrix'/variant/(route+'.waves.jsonl')).read_text().splitlines()[:7]==control
for start in ['flame','gale']:
 r=json.loads((HERE/'natural'/(start+'-victory.json')).read_text());assert r['phase']=='Victory' and r['bossSpawns']==1 and r['bossKills']==1 and r['bossLeaks']==0 and r['bossAbilityHpLoss']>0
 assert r['bossMaxHealth']==1000 and r['bossArmor']==5
 assert len(r['waves'])==8 and r['receipts'][-1]['hp']==(10 if start=='flame' else 14)
 counts=json.loads((HERE/'production-coverage'/(start+'.json')).read_text())['reactions']
 assert all(any(c['kind']+'/'+c['trigger']==key and c['appliedHpLoss']>0 for c in counts) for key in ['Steam/StatusPair','Steam/Intrinsic','Storm/StatusPair','Storm/Intrinsic'])
 for fault in range(3):
  p=HERE/'persistence'/(start+'-fault'+str(fault)+'.json');assert json.loads(p.read_text())['state']['phase']==2 # Victory enum
leak=json.loads((HERE/'natural/flame-leak.json').read_text());assert leak['phase']=='Defeat' and leak['bossKills']==0 and leak['bossLeaks']==1 and leak['bossHealthBeforeLeak']>0 and leak['bossAbilityHpLoss']==0
assert len(g['images'])==34
for path,meta in g['images'].items():
 b=(HERE/path).read_bytes();assert b[:8]==b'\x89PNG\r\n\x1a\n';assert struct.unpack('>II',b[16:24])==(meta['width'],meta['height']);assert sha(b)==meta['sha256']
console=json.loads((HERE/'console-audit.json').read_text());assert sum(c['unexpected_errors'] for c in console['scenarios'].values())==0 and sum(c['expected_io_errors'] for c in console['scenarios'].values())==2
for proof in console['frame_proof'].values():assert proof['last_frame']>proof['first_frame'] and proof['distinct_frames']>1
print('R3 Editor acceptance/provenance consistent; full suites green, natural Victory both starts; R1/R2 bytes preserved. Device/G2/ART/#40 separately not verified.')
