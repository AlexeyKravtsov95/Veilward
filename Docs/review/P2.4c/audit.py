#!/usr/bin/env python3
"""Побайтовый audit historical evidence/assets/packages и прежних tests вне schema projection."""
import hashlib
import json
from pathlib import Path
import subprocess

BASE = '9adc17f251ad98b72304753cc0b69173e502c9c8'
ROOT = Path(__file__).resolve().parents[3]
SCOPES = ['Docs/review', 'Assets/_Game/Tests', 'Assets/Settings', 'Assets/_Game/Art',
          'Assets/_Game/Configs', 'Assets/_Game/Prefabs', 'Assets/_Game/Scenes', 'Packages', 'ProjectSettings']
# Только техническая schema/wire-проекция и замена прежнего временного unsupported-контракта.
PROJECTIONS = {
    'Assets/_Game/Tests/EditMode/BattleAbilityTests.cs',
    'Assets/_Game/Tests/EditMode/BattleElementalReplayTests.cs',
    'Assets/_Game/Tests/EditMode/BattleHybridTowerTests.cs',
    'Assets/_Game/Tests/EditMode/BattleTowerCatalogTests.cs',
    'Assets/_Game/Tests/EditMode/ElementProgressionCheckpointTests.cs',
    'Assets/_Game/Tests/EditMode/ElementProgressionStorageTests.cs',
    'Assets/_Game/Tests/EditMode/NativeEightWaveFileTests.cs',
    'Assets/_Game/Tests/EditMode/WaveCheckpointStoreTests.cs',
    'Assets/_Game/Tests/PlayMode/BattleElementUnlockPlayModeTests.cs',
}
paths = subprocess.check_output(['git', 'ls-tree', '-r', '--name-only', BASE, '--', *SCOPES], cwd=ROOT).decode().splitlines()
records = []
for name in paths:
    if name in PROJECTIONS:
        continue
    expected = subprocess.check_output(['git', 'show', BASE + ':' + name], cwd=ROOT)
    actual = (ROOT / name).read_bytes()
    if actual != expected:
        raise SystemExit('FAIL: baseline changed: ' + name)
    records.append(name + '\t' + hashlib.sha256(actual).hexdigest())
print(json.dumps({'base': BASE, 'files_compared_byte_for_byte': len(records),
    'paths_sha256_manifest': hashlib.sha256(('\n'.join(records) + '\n').encode()).hexdigest(),
    'scope': SCOPES, 'technical_projection_exceptions': sorted(PROJECTIONS), 'result': 'PASS'}, ensure_ascii=False, indent=2))
