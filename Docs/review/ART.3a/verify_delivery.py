"""Read-only clean-checkout verification; does not run Unity or claim device PASS."""
import hashlib
import json
import subprocess
import struct
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
require(manifest['deviceAcceptance'] == 'NOT VERIFIED', 'partial package cannot claim device acceptance')
print(f'PASS: {len(inputs)} tested source files, {len(protected)} protected baseline/history files, '
      f'{len(actual)} tracked evidence artifacts; full nonempty suites and 3-level roundtrip. Device NOT VERIFIED.')
