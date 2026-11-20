#!/usr/bin/env python3
"""Read-only проверка tracked ART.2b evidence и неизменности tested source."""
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import subprocess
import sys

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
TESTED_SOURCE = '7b753e3f8a44abdaca07cb9a3030ac8eac13c0ba'
PREFIX = 'Docs/review/ART.2b/'


def protected(name):
    return name.startswith(('Assets/', 'Packages/', 'ProjectSettings/', 'Scripts/', 'ArtSource/')) or (name.startswith('Docs/review/') and not name.startswith(PREFIX))


def git(*args):
    return subprocess.check_output(['git', '-C', str(ROOT), *args])


def require(condition, message):
    if not condition:
        raise ValueError(message)


def suite(path, expected, required):
    data = json.loads((ROOT / path).read_text())
    summary = data.get('Summary', data.get('summary'))
    require(isinstance(summary, dict), 'missing suite summary')
    summary = {k.lower(): v for k, v in summary.items()}
    require(summary.get('total') == expected and summary.get('passed') == expected and
            all(summary.get(k) == 0 for k in ('failed', 'skipped', 'inconclusive')), 'not a full positive suite PASS')
    rows = data.get('Results', data.get('results'))
    require(isinstance(rows, list) and len(rows) == expected, 'suite case count mismatch')
    require(all(row.get('Status') == 'Passed' for row in rows), 'non-Passed case')
    names = {row.get('FullName') for row in rows}
    require(all(any(name and name.endswith(case) for name in names) for case in required), 'missing required regressions')


def verify():
    require(not git('status', '--porcelain', '--untracked-files=normal').strip(), 'verification requires clean checkout')
    manifest = json.loads((HERE / 'manifest.json').read_text())
    source = manifest['testedSourceSHA']
    require(source == TESTED_SOURCE, 'unexpected tested source SHA')
    inventory = {str(p.relative_to(ROOT)) for p in HERE.rglob('*') if p.is_file() and p.name != 'manifest.json'}
    require(bool(inventory) and set(manifest['artifacts']) == inventory, 'artifact inventory omitted/added files')
    required = {PREFIX + 'reports/export-receipt.json', PREFIX + 'reports/source-audit.json',
                PREFIX + 'reports/edit-mode.json', PREFIX + 'reports/play-mode.json'}
    require(required <= set(manifest['artifacts']), 'required input is not hashed')
    require(manifest['editMode'] == PREFIX + 'reports/edit-mode.json' and
            manifest['playMode'] == PREFIX + 'reports/play-mode.json', 'suite outside tracked delivery')
    expected_runs = {PREFIX + 'measurements/' + kind for kind in ('normal', 'behind', 'dense')}
    require(set(manifest['measurementRuns']) == expected_runs and len(manifest['measurementRuns']) == 3,
            'unexpected measurement directories')
    for run in expected_runs:
        require({run + '/' + name for name in ('run.json', 'complete.json', 'window-1.json', 'window-2.json', 'window-3.json')} <= set(manifest['artifacts']),
                'measurement input is not hashed')
    receipt = json.loads((HERE / 'reports/export-receipt.json').read_text())
    require(receipt['sourceSHA'] == source and receipt['profile'] == 'art-reference' and
            receipt['buildNumber'] == '44' and receipt['contentId'] == 'legacy-single-wave-v1' and
            receipt['scenes'] == ['Assets/_Game/Scenes/ArtReference.unity'], 'wrong tested profile/source/content')
    for name, digest in manifest['artifacts'].items():
        path = ROOT / name
        require(path.is_file() and not path.is_symlink() and ROOT in path.resolve().parents, 'missing/unsafe artifact: ' + name)
        git('ls-files', '--error-unmatch', '--', name)
        require(hashlib.sha256(path.read_bytes()).hexdigest() == digest, 'artifact hash mismatch: ' + name)
        if path.suffix in ('.json', '.txt', '.xml'):
            text = path.read_text()
            require('/Users/' not in text and '/var/containers/' not in text and 'Apple Development:' not in text,
                    'private path/signing output: ' + name)
    # Один git tree lookup вместо сотен отдельных git show.
    tree = {}
    for record in git('ls-tree', '-rz', source).split(b'\0'):
        if not record:
            continue
        header, name = record.split(b'\t', 1)
        tree[name.decode()] = header.split()[2].decode()
    audit = json.loads((HERE / 'reports/source-audit.json').read_text())
    require(set(audit) == set(tree), 'source audit is empty/subset/extra compared with tested Git tree')
    tested_protected = {name for name in tree if protected(name)}
    current_protected = {name.decode() for name in git('ls-files', '-z').split(b'\0') if name and protected(name.decode())}
    require(bool(tested_protected) and current_protected == tested_protected, 'current protected source set differs from tested tree')
    checked = 0
    for name in tested_protected:
        digest = audit[name]
        data = (ROOT / name).read_bytes()
        require(hashlib.sha256(data).hexdigest() == digest, 'tested-source drift: ' + name)
        blob = hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest()
        require(tree.get(name) == blob, 'source audit disagrees with Git commit: ' + name)
        checked += 1
    suite(manifest['editMode'], 1111, ['IosBuildTests.ArtProfilePreservesProductionAndReportsActualSavedScenario'])
    suite(manifest['playMode'], 74, [
        'ArtReferencePlayModeTests.DeviceArtProfileReceiptActivatesWhenIosOmitsManagedArguments',
        'ArtReferencePlayModeTests.DeviceColdStartupWaitsForShowcaseReadiness',
        'ArtReferencePlayModeTests.DeviceProbeDisableRestoresSettingsAndDelayedEnemyViews',
        'ArtReferencePlayModeTests.WarmupActivityCannotApproveFrozenSampleWindow',
        'ArtReferencePlayModeTests.DeviceMeasurementsUseSeparateRunsAndSuspendWithoutCompletion',
        'FlameProjectilePlayModeTests.FlightCannotOverpaintHealthBarPixels'])
    spec = importlib.util.spec_from_file_location('art2b_analysis', ROOT / 'Scripts/analyze-art2b.py')
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    scenarios = set()
    for run in manifest['measurementRuns']:
        report = module.analyze(ROOT / run)
        require(report['sourceReceipt'] == receipt, 'measurement source/profile receipt mismatch')
        scenarios.add(report['scenario'])
    require(scenarios == {'normal', 'behind', 'dense'}, 'missing measured scenarios')
    print(f'PASS: {len(manifest["artifacts"])} tracked artifacts, {checked} protected source/history inputs, EditMode1111/PlayMode74, three complete measured scenarios')


if __name__ == '__main__':
    try:
        verify()
    except (ValueError, KeyError, OSError, subprocess.SubprocessError) as error:
        raise SystemExit('FAIL: ' + str(error))
