#!/usr/bin/env python3
"""Проверка целостности evidence; не запускает Unity и не заменяет gates."""
import gzip
import hashlib
import json
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
gates = json.loads((HERE / 'gates.json').read_text())
for name, expected in gates['suites'].items():
    raw = gzip.decompress((HERE / (name + '.xml.gz')).read_bytes())
    assert hashlib.sha256(raw).hexdigest() == expected['sha256'], name
    report = ET.fromstring(raw)
    for key in ['total', 'passed', 'failed', 'inconclusive', 'skipped']:
        assert report.get(key) == expected[key], (name, key)
    if not name.startswith('Red'):
        assert int(report.get('total')) > 0
        assert report.get('total') == report.get('passed')
        assert all(int(report.get(key)) == 0 for key in ['failed', 'inconclusive', 'skipped'])
for path, expected in json.loads((HERE / 'source-sha256.json').read_text()).items():
    assert hashlib.sha256((ROOT / path).read_bytes()).hexdigest() == expected, path
    committed = subprocess.check_output(['git', 'show', gates['sourceCommit'] + ':' + path], cwd=ROOT)
    assert hashlib.sha256(committed).hexdigest() == expected, ('commit', path)
for path, expected in json.loads((HERE / 'media-sha256.json').read_text()).items():
    assert hashlib.sha256((HERE / path).read_bytes()).hexdigest() == expected, path
assert hashlib.sha256((HERE / 'static.log').read_bytes()).hexdigest() == gates['static']['sha256']
historical = subprocess.check_output(['git', 'ls-tree', '-r', '--name-only', gates['baseCommit'], 'Docs/review', 'Assets/_Game/Tests'], cwd=ROOT).decode().splitlines()
for path in historical:
    assert (ROOT / path).read_bytes() == subprocess.check_output(['git', 'show', gates['baseCommit'] + ':' + path], cwd=ROOT), path
assert len(historical) == gates['historicalAudit']['files']
print(f"PASS: reports/source/media hashes; {len(historical)} historical files unchanged. Unity not rerun.")
