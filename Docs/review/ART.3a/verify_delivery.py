"""Read-only clean-checkout verification; does not run Unity or claim device PASS."""
import hashlib
import json
import subprocess
import struct
import csv
import statistics
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
FOLDER = Path(__file__).resolve().parent


def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def require(condition, message):
    if not condition:
        raise SystemExit('FAIL: ' + message)


manifest = json.loads((FOLDER/'manifest.json').read_text())
source = manifest['testedSource']
require(manifest['baseline'] == '7dfdf0ec1bc1ef63e6df76810cfd08ad6de5181f', 'baseline changed')
require(source == 'd2bf5bf06e3a04ea05c30f24419b46b690cd42e7', 'tested source changed')
require(len(source) == 40, 'exact tested source SHA required')
require(not git('status', '--porcelain'), 'clean checkout required (including untracked files)')
subprocess.run(['git', 'merge-base', '--is-ancestor', source, 'HEAD'], cwd=ROOT, check=True)
package = 'Docs/review/ART.3a/'
paths = git('ls-tree', '-r', '--name-only', source).decode().splitlines()
inputs = [p for p in paths if not p.startswith(package)]
current = [p for p in git('ls-files').decode().splitlines() if not p.startswith(package)]
require(current == inputs, 'current source inventory differs from tested source')
require(sha('\n'.join(inputs).encode()) == manifest['sourceInventorySha256'], 'source inventory changed')
for path in inputs:
    require((ROOT/path).is_file(), 'missing source: '+path)
    require((ROOT/path).read_bytes() == git('show', source+':'+path), 'tested source drift: '+path)

base = manifest['baseline']
protected_prefixes = ('Assets/_Game/Scripts/Domain/', 'Assets/_Game/Scripts/Infrastructure/',
    'Assets/_Game/Configs/', 'Assets/_Game/Scenes/', 'Packages/', 'ProjectSettings/',
    'ArtSource/Blender/ART1/', 'Assets/_Game/Art/Models/ART1/', 'Assets/_Game/Art/Textures/',
    'Assets/_Game/Art/Materials/ART1/', 'Assets/_Game/Prefabs/ART1/')
protected = [p for p in git('ls-tree', '-r', '--name-only', base).decode().splitlines()
    if p.startswith(protected_prefixes) or p.startswith('Docs/review/')]
for path in protected:
    require((ROOT/path).read_bytes() == git('show', base+':'+path), 'baseline/history changed: '+path)

require(manifest['artifacts'], 'empty artifact manifest')
for path, expected in manifest['artifacts'].items():
    require(not Path(path).is_absolute() and '..' not in Path(path).parts, 'unsafe artifact path')
    require((ROOT/path).is_file() and not (ROOT/path).is_symlink(), 'missing/nonregular artifact: '+path)
    require(sha((ROOT/path).read_bytes()) == expected, 'artifact hash: '+path)
    git('ls-files', '--error-unmatch', path)
actual = {str(p.relative_to(ROOT)) for p in FOLDER.rglob('*') if p.is_file() and p.name != 'manifest.json'}
require(actual == set(manifest['artifacts']), 'evidence inventory differs')
for mode in ('edit-mode', 'play-mode'):
    data = json.loads((FOLDER/('reports/'+mode+'.json')).read_text())
    summary = data.get('Summary', data.get('summary', {}))
    summary = {k.lower(): v for k, v in summary.items()}
    require(summary.get('total', 0) > 0 and summary.get('passed') == summary['total'], mode+' nonempty full PASS')
    require(summary['total'] == {'edit-mode': 1123, 'play-mode': 75}[mode], mode+' expected full suite count')
    require(all(summary.get(k) == 0 for k in ('failed', 'skipped', 'inconclusive')), mode+' incomplete suite')
    cases = data.get('Results', data.get('results', []))
    require(len(cases) == summary['total'] and all(r['Status'] == 'Passed' for r in cases), mode+' case inventory')
before = json.loads((FOLDER/'reports/reexport-before.json').read_text())
after = json.loads((FOLDER/'reports/reexport-after.json').read_text())
require(before == after and len(before['models']) == 3, 'roundtrip differs')
for record in after['files']:
    require(sha((ROOT/record['path']).read_bytes()) == record['sha256'].replace('-', '').lower(),
            'roundtrip file drift: '+record['path'])
geometry = json.loads((ROOT/'ArtSource/Blender/ART3/geometry-manifest.json').read_text())
for model, saved in zip(after['models'], geometry):
    require(model['level'] == saved['level'] and model['triangles'] == saved['nondegenerateTriangles'], 'source/import triangle mismatch')
    require(0 < saved['triangles'] <= 5000 and model['materialSlots'] == saved['materialSlots'] == 2, 'model budget')
rows = [dict(field.split('=', 1) for field in row.split('|'))
        for row in (FOLDER/'playmode/motion-telemetry.txt').read_text().splitlines()]
require(len(rows) == 12 and float(rows[-1]['elapsed']) - float(rows[0]['elapsed']) >= 7.7, 'motion window')
require(all(int(b['frame']) > int(a['frame']) and int(b['tick']) > int(a['tick']) for a, b in zip(rows, rows[1:])), 'motion is frozen')
require(int(rows[-1]['kills']) > int(rows[0]['kills']) and any(int(r['beams']) > 0 for r in rows), 'motion activity missing')
for png in (FOLDER/'playmode').glob('*.png'):
    data = png.read_bytes()
    require(data[:8] == b'\x89PNG\r\n\x1a\n' and struct.unpack('>II', data[16:24]) == (1600, 900), 'capture dimensions: '+png.name)
require(manifest['deviceAcceptance'] == 'PARTIAL', 'partial package cannot claim full device acceptance')
device = FOLDER/'device'
receipt = json.loads((device/'device-receipt.json').read_text())
build = json.loads((device/'build-receipt.json').read_text())
require(receipt['testedRuntimeSource'] == source and build['sourceSHA'] == receipt['buildSource'], 'device source receipt')
subprocess.run(['git', 'merge-base', '--is-ancestor', receipt['buildSource'], 'HEAD'], cwd=ROOT, check=True)
require(build['buildNumber'] == receipt['buildNumber'] == '76' and build['profile'] == 'production', 'device build profile')
require(not receipt['unityDevelopmentBuild'] and receipt['deviceAcceptance'] == 'PARTIAL', 'device scope')
runtime = json.loads((device/'runtime-input-hashes.json').read_text())
expected_runtime = [p for p in inputs if p.startswith(('Assets/', 'Packages/', 'ProjectSettings/'))]
require(sorted(runtime) == sorted(expected_runtime) and len(runtime) == receipt['runtimeInputCount'], 'device runtime inventory')
for path, digest in runtime.items():
    require(sha(git('show', source+':'+path)) == digest, 'device runtime input: '+path)
    require(sha(git('show', receipt['buildSource']+':'+path)) == digest, 'device built source input: '+path)
with (device/'production-memory.csv').open() as handle:
    samples = list(csv.DictReader(handle))
memory = json.loads((device/'memory-summary.json').read_text())
require(len(samples) == memory['sampleCount'] == 21, 'native memory sample count')
times = [int(row['start']) for row in samples]
require(all(b > a for a, b in zip(times, times[1:])), 'native memory sample order')
require((times[-1]-times[0])/1e9 == memory['sampleSpanSeconds'], 'native memory span')
for key, saved in memory['metrics'].items():
    values = [float(row[key]) for row in samples if row[key]]
    require(saved == dict(min=min(values), mean=statistics.mean(values), max=max(values), samples=len(values)), 'native memory summary: '+key)
flow = json.loads((device/'phone-flow.json').read_text())
states = {r['label']: r for r in flow}
require(len(states) == len(flow) == 9, 'device observation inventory')
for row in flow:
    require(sha((device/(row['label']+'.json')).read_bytes()) == row['checkpointSha256'], 'device checkpoint hash')
    checkpoint = json.loads((device/(row['label']+'.json')).read_text())
    require(checkpoint['schemaVersion'] == row['schema'] == 4 and checkpoint['scenarioContentId'] == row['content'], 'device schema/content')
    for observed, saved in [('phase', 'phase'), ('waveIndex', 'waveIndex'), ('tick', 'tick'), ('hp', 'baseHealth'), ('gold', 'gold'), ('kills', 'kills'), ('towers', 'towers')]:
        require(row[observed] == checkpoint['state'][saved], 'device state cross-check: '+observed)
    require((row['tick'], row['hp'], row['kills'], row['waveIndex'], row['phase']) == (2815, 7, 32, 6, 0), 'device checkpoint context')
    if row['publishedScreenshot']:
        data = (device/row['publishedScreenshot']).read_bytes()
        require(data[:8] == b'\x89PNG\r\n\x1a\n' and struct.unpack('>II', data[16:24]) == (2622, 1206), 'native dimensions')
for label, level, gold, invested in [('natural-L1-solo', 1, 282, 70), ('natural-L2-solo', 2, 177, 175), ('natural-L3-solo', 3, 37, 315)]:
    row = states[label]
    require(row['gold'] == gold and len(row['towers']) == 1, 'device economy: '+label)
    tower = row['towers'][0]
    require((tower['towerId'], tower['towerTypeId'], tower['x'], tower['y'], tower['level'], tower['investedGold']) == (1, 'flame', 2, 1, level, invested), 'device identity/level')
for a, b in [('natural-L2-inspect', 'natural-L2-solo'), ('natural-L3-inspect', 'natural-L3-solo'), ('natural-L3-solo', 'natural-L3-rollback'), ('natural-L3-sold', 'natural-sold-continue'), ('natural-L3-sold', 'natural-sold-final')]:
    require(states[a]['checkpointSha256'] == states[b]['checkpointSha256'], 'device Continue/rollback drift')
require(states['natural-L3-sold']['gold'] == 257 and not states['natural-L3-sold']['towers'], 'device Sell cleanup')
print(f'PASS: {len(inputs)} tested source files, {len(protected)} protected baseline/history files, '
      f'{len(actual)} tracked evidence artifacts; full nonempty suites and 3-level roundtrip. Device PARTIAL (build76, 9 observations).')
