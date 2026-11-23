"""Freeze ART4 delivery metadata only. Does not edit source/assets or run Unity."""
import importlib.util,json,hashlib,subprocess
from pathlib import Path
folder=Path(__file__).resolve().parent;root=folder.parents[2]
spec=importlib.util.spec_from_file_location('delivery',folder/'verify_delivery.py');v=importlib.util.module_from_spec(spec);spec.loader.exec_module(v)
tracked=[p for p in subprocess.check_output(['git','ls-files','-z'],cwd=root).decode().split('\0') if p]
source={p:v.sha((root/p).read_bytes()) for p in tracked if p.startswith(('Assets/','Packages/','ProjectSettings/','ArtSource/','Scripts/'))}
documentation={'README.md','Docs/TECH_DESIGN.md','Docs/ROADMAP.md','Docs/ART4_PRODUCTION_MAP.md','Docs/ART4_IMPLEMENTATION_PLAN.md'}
artifacts={p:v.sha((root/p).read_bytes()) for p in tracked if (p.startswith('Docs/review/ART.4/') or p in documentation) and p!='Docs/review/ART.4/manifest.json'}
audit=json.loads((folder/'reports/gates-byte-audit.json').read_text())
totals={n:v.full_pass(json.loads((folder/f'reports/{n}.json').read_text()))[0] for n in ('edit-mode','play-mode')}
manifest=dict(baseline=v.BASE,testedSource=audit['testedSource'],mediaSource='03af55397b08e6d18a9f5ed8a3879497906cdf42',
 mediaProvenance='Actual media used the exact rendering inputs later committed in03af553; capture began from uncommitted implementation. Final testedSource additionally removes exactly six unused Editor material writers, adds test-only byte/aspect guards and expands the gate backup list; saved rendering assets/runtime inputs are exact. Actual four carry-in preserved, not canonical Unity run.',
 trackedInventory=tracked,source=source,artifacts=artifacts,protectedExclusions=v.EXCLUSIONS,
 protectedCount=len([p for p in v.tree(v.BASE) if p not in v.EXCLUSIONS and p not in v.CARRY]),carryIn=v.CARRY,suiteTotals=totals,
 device='Deferred to prerelease QA #53/#54/#55',verifierIsNewUnityRun=False)
(folder/'manifest.json').write_text(json.dumps(manifest,indent=2,ensure_ascii=False)+'\n')
print('Frozen',len(source),'source /',manifest['protectedCount'],'protected /',len(artifacts),'artifacts; suites',totals)
