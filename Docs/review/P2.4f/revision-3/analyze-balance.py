#!/usr/bin/env python3
"""Read-only фактические public-NewGame outcomes, timing и экономика; без оценки PASS по одному count."""
import argparse
import gzip
import json
from pathlib import Path


def analyze(root):
    rows=[]
    for path in sorted(root.rglob('*.json')):
        trace=path.with_suffix('.trace.gz')
        if not trace.exists():continue
        r=json.loads(path.read_text())
        if 'waves' not in r or not r['waves']:continue
        start=r['waves'][6]['tick'] if len(r['waves'])==8 else None
        boss=next((e for e in r['enemies'] if e['type']=='rift-warden'),None)
        spawn=None;kill=None;first_hit=None;seen=set()
        for line in gzip.decompress(trace.read_bytes()).decode().splitlines():
            if not line.startswith('S|'):continue
            tick=int(line.split('|')[3])
            if boss and '|E:'+str(boss['id'])+',rift-warden,' in line:
                spawn=tick if spawn is None else min(spawn,tick)
            for text in line.split('|D:')[1:]:
                d=text.split('|',1)[0].split(',');key=(d[0],d[1])
                if key in seen:continue
                seen.add(key)
                if boss and int(d[6])==boss['id'] and int(d[14])>0:
                    first_hit=int(d[0]) if first_hit is None else min(first_hit,int(d[0]))
                    if d[17]=='True':kill=int(d[0])
        cp=json.loads(r['waves'][-1]['checkpoint'].split('|ability:')[0])
        terminal=r['waves'][-1]['tick']
        rows.append(dict(name=str(path.relative_to(root).with_suffix('')),outcome=r['outcome'],
            qualifies=r['outcome']=='Victory' and r['bossKills']==1 and r['abilityKills']>0 and r['abilityApplied']>0 and all(c['applied']>0 for c in r['cells']),
            boss_remaining=r['bossHpBeforeLeak'],boss_origins=boss['byOrigin'] if boss else None,
            boss_spawn_relative=spawn-start if spawn is not None else None,boss_kill_relative=kill-start if kill is not None else None,
            boss_first_damage_relative=first_hit-start if first_hit is not None else None,
            boss_lifetime_ticks=terminal-spawn if spawn is not None else None,terminal_relative=terminal-start if start is not None else None,
            ability_kills=r['abilityKills'],ability_applied=r['abilityApplied'],cells=r['cells'],
            casts=[receipt for receipt in r['receipts'] if '|Cast after tick=' in receipt],
            waves=[{k:w[k] for k in ['wave','phase','tick','hp','gold','kills','remaining','sequence','cells']} for w in r['waves']],
            final_army=cp['towers'],gold_commands=r['goldCommands'],kill_gold=r['killGold'],bonus_gold=r['bonusGold']))
    by_name={r['name']:r for r in rows}
    for start in ['flame','gale']:
        actual=by_name.get('natural/'+start);control=by_name.get('experiments/winning-timing/'+start+'-boss--15')
        if actual and control:
            assert actual['qualifies'] and control['outcome']=='Defeat'
            actual['matched_omitted_boss_cast_leak_tick']=control['terminal_relative']
            actual['margin_ticks']=control['terminal_relative']-actual['boss_kill_relative']
            actual['margin_seconds']=actual['margin_ticks']/30
    return rows

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--input',type=Path,required=True);parser.add_argument('--output',type=Path,required=True);args=parser.parse_args()
    args.output.write_text(json.dumps(analyze(args.input),ensure_ascii=False,indent=2)+'\n')
