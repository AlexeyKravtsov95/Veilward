#!/usr/bin/env python3
"""Побайтовый audit исторических assertions/evidence и serialized baseline."""
import hashlib
import json
from pathlib import Path
import subprocess

BASE = 'a7d07f7710a7358659848248643fec8af16a3e73'
ROOT = Path(__file__).resolve().parents[3]
SCOPES = ['Docs/review', 'Assets/_Game/Tests', 'Assets/Settings', 'Assets/_Game/Art',
          'Assets/_Game/Configs', 'Assets/_Game/Prefabs', 'Assets/_Game/Scenes', 'Packages', 'ProjectSettings']
paths = subprocess.check_output(['git', 'ls-tree', '-r', '--name-only', BASE, '--', *SCOPES], cwd=ROOT).decode().splitlines()
records = []
for name in paths:
    expected = subprocess.check_output(['git', 'show', BASE + ':' + name], cwd=ROOT)
    actual = (ROOT / name).read_bytes()
    if actual != expected:
        raise SystemExit('FAIL: baseline changed: ' + name)
    records.append(name + '\t' + hashlib.sha256(actual).hexdigest())
print(json.dumps({'base': BASE, 'files_compared_byte_for_byte': len(records),
    'paths_sha256_manifest': hashlib.sha256(('\n'.join(records) + '\n').encode()).hexdigest(),
    'scope': SCOPES, 'result': 'PASS'}, ensure_ascii=False, indent=2))
