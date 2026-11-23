"""Freeze exact tracked source/media inputs after their commit. Does not run Unity or copy projects."""
import hashlib
import json
from pathlib import Path
import subprocess
ROOT=Path(__file__).resolve().parents[3];P=Path(__file__).resolve().parent
tracked=[p for p in subprocess.check_output(['git','ls-files','-z'],cwd=ROOT).decode().split('\0') if p]
head=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()
sha=lambda p:hashlib.sha256((ROOT/p).read_bytes()).hexdigest()
source={p:sha(p) for p in tracked if p.startswith(('Assets/','Packages/','ProjectSettings/','Scripts/','ArtSource/'))}
artifacts={p:sha(p) for p in tracked if (p.startswith('Docs/review/VFX.1a/') or p in ('README.md','Docs/TECH_DESIGN.md','Docs/ROADMAP.md','Docs/VFX1A_FLAME_PROJECTILE.md')) and p not in ('Docs/review/VFX.1a/manifest.json',)}
existing=['Assets/_Game/Scenes/Battle.unity','Assets/_Game/Scripts/Presentation/BattleSceneController.cs','Assets/_Game/Scripts/Presentation/FlameStrikeView.cs','Assets/_Game/Scripts/Editor/BattleSceneBuilder.cs','Assets/_Game/Scripts/Editor/BattleAssetValidator.cs','Assets/_Game/Tests/PlayMode/AbilityBossIntegrationPlayModeTests.cs','README.md','Docs/TECH_DESIGN.md','Docs/ROADMAP.md']
m=dict(baseline='45efa5676ac5c27ee70d4a13b1fbe2622d3f82f6',testedSource=json.loads((P/'reports/gates-byte-audit.json').read_text())['testedSource'],mediaSource=head,evidenceSource=head,changedExisting=existing,trackedInventory=tracked,source=source,artifacts=artifacts,totals={'edit-mode':1252,'play-mode':96})
(P/'manifest.json').write_text(json.dumps(m,indent=2,ensure_ascii=False)+'\n')
print(head,len(source),len(artifacts))
