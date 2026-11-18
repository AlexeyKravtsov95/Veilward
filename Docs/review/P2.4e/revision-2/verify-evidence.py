#!/usr/bin/env python3
"""Проверка происхождения/согласованности R2. Gameplay FAIL остаётся FAIL."""
from pathlib import Path
import gzip,hashlib,json,struct,subprocess,tarfile,xml.etree.ElementTree as ET
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[3]
sha=lambda b:hashlib.sha256(b).hexdigest()
gates=json.loads((HERE/'gates.json').read_text())
assert gates['acceptance']=='BLOCKED' and gates['all_suites_pass'] is False
for path,digest in gates['artifacts'].items():assert sha((HERE/path).read_bytes())==digest,path
for name,report in gates['reports'].items():
 packed=(HERE/report['path']).read_bytes();raw=gzip.decompress(packed)
 assert sha(packed)==report['sha256'] and sha(raw)==report['raw_sha256'],name
 r=ET.fromstring(raw)
 for field in ('total','passed','failed','inconclusive','skipped'):assert int(r.get(field))==report[field],(name,field)
 assert report['total']>0 and report['total']==sum(report[f] for f in ('passed','failed','inconclusive','skipped'))
 assert report['inconclusive']==report['skipped']==0
 failures=[t.get('name') for t in r.iter('test-case') if t.get('result')=='Failed'];assert failures==report['failed_tests']
 if name=='Research-initial-timeout':
  assert len(failures)==2 and all('PublicCheckpointPreparationEightRepair' in x for x in failures)
  assert all('180000 ms' in t.findtext('failure/message','') for t in r.iter('test-case') if t.get('result')=='Failed')
 elif name=='EditMode':
  assert len(failures)==2 and all('NaturalProductionVictoryAndDeliberateBossLeak' in x and 'False)' in x for x in failures)
 elif name in ('PlayMode','Visual'):assert set(failures)=={'RegBattle213_VisualNarrowNaturalGale','RegBattle213_VisualWideNaturalFlame'}
 else:assert report['failed']==0 and report['passed']==report['total']
manifest=json.loads((HERE/'source-sha256.json').read_text())
for revision,files in manifest['research_files'].items():
 for path,digest in files.items():assert sha(subprocess.check_output(['git','show',revision+':'+path],cwd=ROOT))==digest,path
for category in ('files','inputs','controls','historical'):
 for path,digest in manifest[category].items():assert sha((ROOT/path).read_bytes())==digest,path
for path,digest in {**manifest['files'],**manifest['inputs']}.items():
 assert sha(subprocess.check_output(['git','show',gates['source']+':'+path],cwd=ROOT))==digest,path
# Старый verifier связывал source с рабочим деревом b970003. Для нового source
# проверяем те же неизменённые bytes по Git, не переписывая прежний FAIL пакет.
old=HERE.parent;old_gates=json.loads((old/'gates.json').read_text());old_manifest=json.loads((old/'source-sha256.json').read_text())
for path,digest in old_gates['artifacts'].items():assert sha((old/path).read_bytes())==digest,path
for path,digest in old_manifest['files'].items():
 assert sha(subprocess.check_output(['git','show',old_gates['source']+':'+path],cwd=ROOT))==digest,path
for packed,manifest_name in [('research.tar.gz','research-sha256.json'),('initial-a46-research.tar.gz','initial-a46-sha256.json')]:
 entries=json.loads((HERE/manifest_name).read_text())
 with tarfile.open(HERE/packed) as archive:
  members={m.name:m for m in archive.getmembers() if m.isfile()};assert set(members)==set(entries)
  for path,digest in entries.items():assert sha(archive.extractfile(members[path]).read())==digest,path
for path,meta in gates['images'].items():
 b=(HERE/path).read_bytes();assert b[:8]==b'\x89PNG\r\n\x1a\n';assert struct.unpack('>II',b[16:24])==(meta['width'],meta['height']);assert sha(b)==meta['sha256']
assert gates['environment']['screenshots']==len(gates['images']) and len(gates['images'])>0
for name in ('flame-victory','gale-victory','flame-leak'):
 r=json.loads((HERE/'natural'/(name+'.json')).read_text())
 assert r['phase']=='Defeat' and r['bossSpawns']==1 and r['bossKills']==0 and r['bossLeaks']==1
 assert r['abilityHpLoss']>0
 assert r['bossAbilityHpLoss']==0 if name=='flame-leak' else r['bossAbilityHpLoss']>0
 assert len((HERE/'natural'/(name+'.waves.jsonl')).read_text().splitlines())==8
console=json.loads((HERE/'console-audit.json').read_text());assert console['unexpected_errors']==0
for proof in console['frame_proof'].values():assert proof['last_frame']>proof['first_frame'] and proof['distinct_frames']>1
print('R2 evidence consistent; natural Victory FLAME/GALE and full green suites BLOCKED. Historical FAIL bytes preserved.')
