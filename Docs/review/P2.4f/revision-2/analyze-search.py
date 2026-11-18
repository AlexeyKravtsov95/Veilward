#!/usr/bin/env python3
"""Read-only разбор outcomes/parents; stdout и output JSON не изменяют Unity/inputs."""
import argparse
import collections
import json
from pathlib import Path

PRICES={'flame':[70,105,140],'tide':[60,90,120],'gale':[80,120,160],
        'steam':[110,165,220],'storm':[110,165,220]}
REQUIRES={'flame':{'flame'},'tide':{'tide'},'gale':{'gale'},
          'steam':{'flame','tide'},'storm':{'tide','gale'}}


def analyze(root):
    result={'starts':{},'histories':{},'wave_outcomes':0,'full_new_game_outcomes':0}
    for start in ['flame','gale']:
        folder=root/start
        summary=json.loads((folder/'summary.json').read_text())
        parents={r['id']:r for line in (folder/'parents.jsonl').open() if (r:=json.loads(line))}
        assert len(parents)==summary['retained']
        stats={};unique=set();victories=0;rows=0
        for line in (folder/'outcomes.jsonl').open():
            r=json.loads(line);rows+=1;history=r['history'];wave=r['wave']
            assert r['phase']!='INVALID_SUFFIX'
            if r['parent']<0:
                # Root из public NewGame/Choose; только независимая audit модель, не simulation input.
                cp={'gold':150,'kills':0,'baseHealth':20,'nextTowerId':1,'towers':[],
                    'elementProgression':{'unlockedElementIds':[start]}}
                remaining=0;sequence=1
            else:
                parent=parents[r['parent']];assert parent['history']==history
                cp=parent['state'];remaining=parent['abilityRemaining'];sequence=parent['abilitySequence']
                assert cp['waveIndex']+1==wave and cp['phase']==0
            key=(history,json.dumps(cp,sort_keys=True),remaining,sequence,json.dumps(r['commands'],sort_keys=True),r['policy'])
            assert key not in unique;unique.add(key)
            army={t['towerId']:dict(t) for t in cp['towers']};gold=cp['gold'];next_id=cp['nextTowerId']
            assert len(r['commands'])==len(r['receipts'])
            for c,receipt in zip(r['commands'],r['receipts']):
                assert receipt['before']==gold and receipt['op']==c['op'] and receipt['type']==c['type']
                assert c['wave']==wave and receipt['result']=='None'
                price=refund=0
                if c['op']=='sell':
                    t=army.pop(c['id']);refund=t['investedGold']*70//100
                elif c['op']=='upgrade':
                    t=army[c['id']];assert t['level']<3
                    price=PRICES[t['towerTypeId']][t['level']];t['level']+=1;t['investedGold']+=price
                else:
                    assert REQUIRES[c['type']]<=set(cp['elementProgression']['unlockedElementIds'])
                    assert not any(t['x']==c['x'] and t['y']==c['y'] for t in army.values())
                    assert -6<=c['x']<6 and -4<=c['y']<4
                    price=PRICES[c['type']][0];assert receipt['id']==next_id
                    army[next_id]={'towerId':next_id,'towerTypeId':c['type'],'level':1,'investedGold':price,'x':c['x'],'y':c['y']};next_id+=1
                gold+=refund-price
                assert gold>=0 and receipt['after']==gold and receipt['price']==price and receipt['refund']==refund
            kills=r['kills']-cp['kills'];bonus=20 if r['phase'] in ['WaveComplete','Victory'] else 0
            assert r['gold']==gold+kills*5+r['bossKills']*45+bonus
            assert r['hp']<=cp['baseHealth'] and 0<=r['remaining']<=600
            elapsed=r['tick']-cp.get('tick',0);last=0
            for cast in r['casts']:
                tick=cast['relativeTick'];assert cast['wave']==wave and 0<=tick<=elapsed
                remaining=max(0,remaining-(tick-last));assert remaining==0
                assert -6<=cast['x']<6 and -4<=cast['y']<4
                remaining=600;sequence+=1;last=tick
            remaining=max(0,remaining-(elapsed-last))
            assert r['remaining']==remaining and r['sequence']==sequence
            if wave==8:assert sum(r['bossApplied'])==1000-r['bossHp']
            victories+=r['phase']=='Victory'
            a=stats.setdefault(history,{'wave_counts':collections.Counter(),'accepted_commands':collections.Counter(),
                'upgrades_by_type':collections.Counter(),'level_outcomes':collections.Counter(),'earliest_level':{},
                'max_hp_by_wave':{},'max_wealth_by_wave':{},'hold_outcomes':0,'fully_covered_finals':0,
                'final_profiles':{},'final_level_profiles':{}})
            a['wave_counts'][wave]+=1;a['hold_outcomes']+=not r['commands']
            for c in r['commands']:
                a['accepted_commands'][c['op']]+=1
                if c['op']=='upgrade':a['upgrades_by_type'][c['type']]+=1
            levels={t['level'] for t in army.values()}
            for level in levels:a['level_outcomes'][level]+=1
            for t in army.values():
                k=t['towerTypeId']+'L'+str(t['level']);a['earliest_level'][k]=min(a['earliest_level'].get(k,9),wave)
            a['max_hp_by_wave'][wave]=max(a['max_hp_by_wave'].get(wave,0),r['hp'])
            wealth=r['gold']+sum(t['investedGold'] for t in army.values())
            a['max_wealth_by_wave'][wave]=max(a['max_wealth_by_wave'].get(wave,0),wealth)
            if wave==8 and all(r['cells']):
                a['fully_covered_finals']+=1
                profile=','.join(sorted(t['towerTypeId']+'L'+str(t['level']) for t in army.values()))
                for bucket,key in [('final_profiles',profile),('final_level_profiles','maxL'+str(max(levels,default=0)))]:
                    if key not in a[bucket] or r['bossHp']<a[bucket][key]['bossHp']:
                        a[bucket][key]={k:r[k] for k in ['id','bossHp','bossApplied','army','policy','cells','gold','kills','abilityKills']}
        assert rows==summary['waveOutcomes'] and victories==summary['victories']==0
        assert summary['waveOutcomes']+summary['fullNewGameOutcomes']<=50000 and summary['seconds']<=600
        assert summary['invalidSuffixes']==0 and len(stats)==2
        for history,a in stats.items():
            a['final_profiles']=sorted(a['final_profiles'].values(),key=lambda r:r['bossHp'])[:12]
            result['histories'][history]=a
        result['starts'][start]=summary;result['wave_outcomes']+=summary['waveOutcomes'];result['full_new_game_outcomes']+=summary['fullNewGameOutcomes']
    result['total_outcomes']=result['wave_outcomes']+result['full_new_game_outcomes']
    assert result['total_outcomes']<=100000
    return result


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--input',type=Path,required=True);parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args();args.output.write_text(json.dumps(analyze(args.input),indent=2)+'\n')
    print('All outcome receipts/Gold/cooldown/sequences/parent histories validated; '+str(args.output))
