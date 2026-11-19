#!/usr/bin/env python3
"""Проверка сохранённых P2.4f hashes, XML, traces и честного BLOCKED REG214."""
import gzip
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
BASE = 'a9a9ae4fd02e86a1d1f59ee689d4a941976e146e'


def sha(data):
    return hashlib.sha256(data).hexdigest()


def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT)


def raw(path):
    data = path.read_bytes()
    return gzip.decompress(data) if path.suffix == '.gz' else data


def xml_report(path):
    data = raw(path)
    root = ET.fromstring(data)
    counts = {k: int(root.get(k)) for k in ['total', 'passed', 'failed', 'inconclusive', 'skipped']}
    assert counts['total'] > 0
    assert sum(counts[k] for k in ['passed', 'failed', 'inconclusive', 'skipped']) == counts['total']
    counts.update(raw_sha256=sha(data), raw_bytes=len(data), sha256=sha(path.read_bytes()),
                  failures=[t.get('name') for t in root.findall('.//test-case[@result="Failed"]')])
    return counts


def record():
    source = sys.argv[2]
    history = {}
    for name in git('ls-tree', '-r', '--name-only', BASE, '--', 'Docs/review').decode().splitlines():
        before = git('show', BASE + ':' + name)
        assert (ROOT / name).read_bytes() == before, 'historical drift: ' + name
        history[name] = sha(before)
    (HERE / 'historical-audit.json').write_text(json.dumps({'base': BASE, 'count': len(history), 'files': history}, indent=2) + '\n')
    source_files = git('ls-files', '--', 'Assets', 'Scripts', 'Packages', 'ProjectSettings').decode().splitlines()
    source_hashes = {}
    for name in source_files:
        data = (ROOT / name).read_bytes()
        assert data == git('show', source + ':' + name), 'source not committed/matches: ' + name
        source_hashes[name] = sha(data)
    (HERE / 'source-sha256.json').write_text(json.dumps({'source': source, 'files': source_hashes}, indent=2) + '\n')
    reports = {p.name: xml_report(p) for p in sorted((HERE / 'reports').glob('*.xml.gz'))}
    (HERE / 'report-audit.json').write_text(json.dumps(reports, indent=2) + '\n')
    manifest = {str(p.relative_to(HERE)): {'sha256': sha(p.read_bytes()), 'bytes': p.stat().st_size}
                for p in sorted(HERE.rglob('*')) if p.is_file() and p.name != 'manifest.json'}
    (HERE / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')


def verify():
    manifest = json.loads((HERE / 'manifest.json').read_text())
    for name, expected in manifest.items():
        path = HERE / name
        assert path.stat().st_size == expected['bytes'], name
        assert sha(path.read_bytes()) == expected['sha256'], name
    source = json.loads((HERE / 'source-sha256.json').read_text())
    for name, expected in source['files'].items():
        assert sha((ROOT / name).read_bytes()) == expected, 'current source differs: ' + name
        assert sha(git('show', source['source'] + ':' + name)) == expected, 'source commit differs: ' + name
    history = json.loads((HERE / 'historical-audit.json').read_text())
    for name, expected in history['files'].items():
        assert sha((ROOT / name).read_bytes()) == expected, 'historical drift: ' + name
        assert sha(git('show', BASE + ':' + name)) == expected, name
    reports = json.loads((HERE / 'report-audit.json').read_text())
    for name, expected in reports.items():
        assert xml_report(HERE / 'reports' / name) == expected, name
    for name, total in [('baseline-edit.xml.gz', 1048), ('baseline-play.xml.gz', 58),
                        ('PlayMode.xml.gz', 60), ('Visual.xml.gz', 5)]:
        r = reports[name]
        assert r['total'] == r['passed'] == total and r['failed'] == r['inconclusive'] == r['skipped'] == 0, name
    edit = reports['EditMode.xml.gz']
    assert edit['total'] == 1071 and edit['passed'] == 1069 and edit['failed'] == 2
    assert edit['inconclusive'] == edit['skipped'] == 0
    assert set(edit['failures']) == {
        'RegBattle214_SameProductionRunMustWinWithAbilityBossAndFourPositiveCells("flame")',
        'RegBattle214_SameProductionRunMustWinWithAbilityBossAndFourPositiveCells("gale")'}
    for start in ['flame', 'gale']:
        first = raw(HERE / 'double' / (start + '.first.trace.gz'))
        assert first == raw(HERE / 'double' / (start + '.second.trace.gz'))
        for suffix in ['continued', 'rollback7', 'rollback8']:
            assert first == raw(HERE / 'files' / (start + '.' + suffix + '.trace.gz')), start + suffix
        for directory, suffix in [('double', '.first'), ('double', '.second'), ('files', '.continued'),
                                  ('files', '.rollback7'), ('files', '.rollback8'), ('natural', '')]:
            report = json.loads((HERE / directory / (start + suffix + '.json')).read_text())
            trace = raw(HERE / directory / (start + suffix + '.trace.gz'))
            assert sha(trace) == report['traceSha256'] and len(trace) == report['rawBytes']
            assert report['outcome'] == 'Defeat' and report['bossKills'] == 0 and report['bossLeaks'] == 1
            assert report['abilityKills'] > 0 and report['abilityApplied'] > 0
            assert all(c['applied'] > 0 and c['procs'] > 0 for c in report['cells'])
            assert all(e['applied'] + (e['leakedHp'] if e['leaked'] else e['remaining']) == e['maxHp'] for e in report['enemies'])
        winner_control = json.loads((HERE / 'controls' / (start + '.stone-victory.json')).read_text())
        assert winner_control['outcome'] == 'Victory' and winner_control['bossKills'] == 1
        assert winner_control['abilityKills'] > 0
    leak = json.loads((HERE / 'controls/flame.leak.json').read_text())
    assert leak['outcome'] == 'Defeat' and leak['bossLeaks'] == 1 and leak['bossKills'] == 0
    assert leak['waves'][6]['hp'] > 0 and leak['bossHpBeforeLeak'] > 0
    console = json.loads((HERE / 'console-audit.json').read_text())
    assert console['unexpected_errors'] == 0
    assert all(case['max_frame'] > case['min_frame'] for case in console['cases'] if case['screenshots'])
    assert 'REG214' in (HERE / 'validation.md').read_text() and 'BLOCKED' in (HERE / 'validation.md').read_text()
    print(f'P2.4f evidence verified: {len(manifest)} artifacts, {len(history["files"])} historical files unchanged; '
          'EditMode1069/1071 with two mandatory REG214 FAIL; Play60/60; Visual5/5. Acceptance BLOCKED.')


if __name__ == '__main__':
    if len(sys.argv) > 1 and sys.argv[1] == '--record':
        record()
    verify()
