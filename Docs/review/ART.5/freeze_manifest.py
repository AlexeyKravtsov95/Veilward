"""Freeze metadata only after source/media/gates. Does not run Unity or alter assets."""
import importlib.util,json,subprocess
from pathlib import Path
folder=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('delivery',folder/'verify_delivery.py');v=importlib.util.module_from_spec(spec);spec.loader.exec_module(v)
tracked=[p for p in v.git('ls-files','-z').decode().split('\0') if p]
source={p:v.sha((v.ROOT/p).read_bytes()) for p in tracked if p.startswith(('Assets/','Packages/','ProjectSettings/','ArtSource/','Scripts/'))}
documentation=['README.md','Docs/TECH_DESIGN.md','Docs/ROADMAP.md','Docs/ART5_ENEMIES.md']
artifacts={p:v.sha((v.ROOT/p).read_bytes()) for p in tracked if (p.startswith('Docs/review/ART.5/') or p in documentation) and p!='Docs/review/ART.5/manifest.json'}
gates=v.read('reports/gates-byte-audit.json');totals={name:v.full_pass(v.read('reports/'+name+'.json'))[0] for name in ('edit-mode','play-mode')}
m=dict(baseline=v.BASE,testedSource=gates['testedSource'],mediaSource=v.read('reports/media-source.json')['sourceHead'],trackedInventory=tracked,source=source,artifacts=artifacts,documentation=documentation,protectedExclusions=v.EXCLUSIONS,protectedCount=len([p for p in v.tree(v.BASE) if p not in v.EXCLUSIONS and p not in v.CARRY]),carryIn=v.CARRY,suiteTotals=totals,device='Deferred QA53/54/55',verifierIsNewUnityRun=False)
(folder/'manifest.json').write_text(json.dumps(m,indent=2,ensure_ascii=False)+'\n');print('Frozen',len(source),'source/',len(artifacts),'artifacts/',totals)
