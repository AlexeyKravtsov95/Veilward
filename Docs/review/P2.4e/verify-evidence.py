#!/usr/bin/env python3
"""Проверяет согласованность evidence; не заменяет Unity и не превращает FAIL в PASS."""
from pathlib import Path
import gzip,hashlib,json,struct,subprocess,xml.etree.ElementTree as ET
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
hash_bytes=lambda b:hashlib.sha256(b).hexdigest()
gates=json.loads((HERE/'gates.json').read_text())
assert gates['acceptance']=='BLOCKED' and gates['all_suites_pass'] is False
for path,digest in gates['artifacts'].items():assert hash_bytes((HERE/path).read_bytes())==digest,path
for name,report in gates['reports'].items():
 packed=(HERE/report['path']).read_bytes();raw=gzip.decompress(packed)
 assert hash_bytes(packed)==report['sha256'] and hash_bytes(raw)==report['raw_sha256'],name
 r=ET.fromstring(raw)
 for field in ('total','passed','failed','inconclusive','skipped'):assert int(r.get(field))==report[field],(name,field)
 assert report['total']>0 and report['total']==sum(report[f] for f in ('passed','failed','inconclusive','skipped'))
 assert report['inconclusive']==report['skipped']==0
 failures=[t.get('name') for t in r.iter('test-case') if t.get('result')=='Failed']
 assert failures==report['failed_tests']
 if name in ('EditMode','PlayMode','Visual'):
  assert len(failures)==2
  if name=='EditMode':assert all('NaturalProductionVictoryAndDeliberateBossLeak' in x and 'False)' in x for x in failures)
  else:assert set(failures)=={'RegBattle213_VisualNarrowNaturalGale','RegBattle213_VisualWideNaturalFlame'}
 elif name.startswith('red-'):assert report['failed']>0
 else:assert report['failed']==0 and report['passed']==report['total']
for path,meta in gates['images'].items():
 b=(HERE/path).read_bytes();assert b[:8]==b'\x89PNG\r\n\x1a\n';assert struct.unpack('>II',b[16:24])==(meta['width'],meta['height']);assert hash_bytes(b)==meta['sha256']
assert len(gates['images'])==26
manifest=json.loads((HERE/'source-sha256.json').read_text())
for category in ('files','controls','historical'):
 for path,digest in manifest[category].items():assert hash_bytes((ROOT/path).read_bytes())==digest,path
for start in ('flame','gale'):
 path='Docs/review/P2.4e/'+start+'.commands.json'
 assert (ROOT/path).read_bytes()==subprocess.check_output(['git','show',gates['source']+':'+path],cwd=ROOT)
for name in ('flame-victory','gale-victory','flame-leak'):
 r=json.loads((HERE/(name+'.json')).read_text())
 assert r['phase']=='Defeat' and r['bossSpawns']==1 and r['bossKills']==0 and r['bossLeaks']==1
 assert r['abilityHpLoss']>0 and r['bossAbilityHpLoss']==(0 if name=='flame-leak' else 110)
 assert len((HERE/(name+'.waves.jsonl')).read_text().splitlines())==8
console=json.loads((HERE/'console-audit.json').read_text());assert console['unexpected_errors']==0
for proof in console['frame_proof'].values():assert proof['last_frame']>proof['first_frame'] and proof['distinct_frames']>1
print('Evidence consistent. Gameplay acceptance BLOCKED: natural Victory FLAME/GALE failed; full gates FAIL as recorded.')
