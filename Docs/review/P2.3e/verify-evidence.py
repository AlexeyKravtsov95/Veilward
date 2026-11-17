#!/usr/bin/env python3
"""Проверяет сохранённые evidence bytes, не запускает и не изменяет игру."""
import gzip
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parent
report = json.loads((root / 'replay-summary.json').read_text())
results = {}
for route in ('A', 'B', 'Stone'):
    summary = json.loads((root / f'{route}.summary.json').read_text())
    first = gzip.decompress((root / f'{route}.first.trace.gz').read_bytes())
    assert first == gzip.decompress((root / f'{route}.second.trace.gz').read_bytes())
    assert hashlib.sha256(first).hexdigest() == summary['traceSha256']
    counters = {}
    for line in first.decode().splitlines():
        fields = line.split('|')
        events = [s[2:].split(',') for s in fields if s.startswith('R:')]
        hits = [s[2:].split(',') for s in fields if s.startswith('D:')]
        for e in events:
            matching = [d for d in hits if d[2] == 'Reaction' and (d[0], d[16], d[15], d[3]) == (e[0], e[1], e[2], e[4])]
            assert matching, (route, 'proc without damage targets', e)
            loss = sum(int(d[12]) for d in matching)
            assert loss > 0, (route, 'zero applied HP loss', e)
            key = e[2] + '/' + e[3]
            item = counters.setdefault(key, {'procs': 0, 'targets': 0, 'appliedHpLoss': 0, 'firstTick': int(e[0]), 'sourceTower': int(e[4])})
            item['procs'] += 1
            item['targets'] += len(matching)
            item['appliedHpLoss'] += loss
    results[route] = counters
for mode in ('EditMode', 'PlayMode', 'Visual'):
    attrs = ET.parse(root / 'reports' / f'{mode}.xml').getroot().attrib
    assert int(attrs['total']) > 0 and attrs['passed'] == attrs['total']
    assert all(int(attrs[k]) == 0 for k in ('failed', 'inconclusive', 'skipped'))
assert {'Steam/StatusPair', 'Steam/Intrinsic'} <= results['A'].keys()
assert {'Storm/StatusPair', 'Storm/Intrinsic'} <= results['B'].keys()
assert not results['Stone']
expected = root / 'reaction-damage-audit.json'
if expected.exists():
    assert results == json.loads(expected.read_text())
else:
    expected.write_text(json.dumps(results, indent=2) + '\n')
print(json.dumps(results, indent=2))
print('PASS: stored full trace identity, positive reaction HP loss and non-empty complete reports; late production coverage remains BLOCKED.')
