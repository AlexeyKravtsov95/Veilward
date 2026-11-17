#!/usr/bin/env python3
"""Read-only проверка committed traces, положительного reaction damage и всех gates."""
import gzip
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parent
results = {}
for route in ('A-survival', 'B-survival', 'A-coverage', 'B-coverage'):
    summary = json.loads((root / f'{route}.summary.json').read_text())
    first = gzip.decompress((root / f'{route}.first.trace.gz').read_bytes())
    assert first == gzip.decompress((root / f'{route}.second.trace.gz').read_bytes())
    assert hashlib.sha256(first).hexdigest() == summary['traceSha256']
    assert len(first.decode().splitlines()) == summary['entries']
    assert summary['outcome'] == 'Victory' and len(summary['waves']) == 8
    assert summary['waves'][5]['state']['elementProgression']['pendingChoiceId'] == 'after-wave-6'
    assert summary['waves'][7]['state']['elementProgression']['choicesSpent'] == 2
    counters = {}
    for line in first.decode().splitlines():
        fields = line.split('|')
        if fields[0] != 'S':
            continue
        wave = int(fields[2]) + 1
        events = [s[2:].split(',') for s in fields if s.startswith('R:')]
        hits = [s[2:].split(',') for s in fields if s.startswith('D:')]
        for event in events:
            assert wave >= 4
            if (route.startswith('A') and event[2] == 'Storm') or (route.startswith('B') and event[2] == 'Steam'):
                assert wave >= 7
            matching = [d for d in hits if d[2] == 'Reaction' and
                        (d[0], d[16], d[15], d[3]) == (event[0], event[1], event[2], event[4])]
            assert matching, (route, 'proc without damage targets', event)
            loss = sum(int(d[12]) for d in matching)
            if loss <= 0:
                continue
            key = event[2] + '/' + event[3]
            item = counters.setdefault(key, {'positiveProcs': 0, 'targets': 0, 'appliedHpLoss': 0,
                                             'firstWave': wave, 'firstTick': int(event[0]), 'sourceTower': int(event[4])})
            item['positiveProcs'] += 1
            item['targets'] += len(matching)
            item['appliedHpLoss'] += loss
    if route.endswith('coverage'):
        assert set(counters) == {'Steam/StatusPair', 'Steam/Intrinsic', 'Storm/StatusPair', 'Storm/Intrinsic'}
    results[route] = counters
for mode in ('EditMode', 'PlayMode', 'Visual'):
    attrs = ET.parse(root / 'reports' / f'{mode}.xml').getroot().attrib
    assert int(attrs['total']) > 0 and attrs['passed'] == attrs['total']
    assert all(int(attrs[k]) == 0 for k in ('failed', 'inconclusive', 'skipped'))
assert results == json.loads((root / 'reaction-damage-audit.json').read_text())
print(json.dumps(results, indent=2))
print('PASS: four complete double New Game traces; positive pair/intrinsic Steam/Storm; nonempty complete gates.')
