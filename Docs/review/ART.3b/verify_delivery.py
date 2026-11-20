"""Read-only clean-checkout verifier. Проверяет поставленные proofs, не запускает Unity/device."""
import hashlib
import json
from pathlib import Path
import struct
import subprocess
import sys
sys.dont_write_bytecode = True

ROOT = Path(__file__).resolve().parents[3]
FOLDER = Path(__file__).resolve().parent
PACKAGE = 'Docs/review/ART.3b/'
BASE = '57cae61557ab71a79aa1d67a1d10bdad7cc972cc'
SOURCE = '7aef7206b788ca57e754a43c52fe337ab088e3c3'


def require(value, why):
    if not value:
        raise SystemExit('FAIL: ' + why)


def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT)


def read(path):
    return json.loads(path.read_text())


def sha(data):
    return hashlib.sha256(data).hexdigest()


def files_at(ref):
    return set(git('ls-tree', '-r', '--name-only', ref).decode().splitlines())


def verify_blobs(ref, paths):
    process = subprocess.Popen(['git', 'cat-file', '--batch'], cwd=ROOT, stdin=subprocess.PIPE, stdout=subprocess.PIPE)
    data, _ = process.communicate(('\n'.join(ref + ':' + p for p in paths) + '\n').encode())
    require(process.returncode == 0, 'git blob read')
    offset = 0
    for path in paths:
        end = data.index(b'\n', offset)
        fields = data[offset:end].split()
        require(len(fields) == 3 and fields[1] == b'blob', 'missing blob ' + path)
        size = int(fields[2]); offset = end + 1
        local = ROOT / path
        require(local.is_file() and not local.is_symlink(), 'nonregular ' + path)
        require(local.read_bytes() == data[offset:offset+size], 'source/history drift ' + path)
        offset += size + 1
    require(offset == len(data), 'unexpected blob data')


def suite(path, total):
    report = read(path)
    require(report.get('success') is True if 'success' in report else report.get('status') == 'completed',
            'suite success envelope ' + path.name)
    candidates = [report, report.get('result'), report.get('data', {}).get('result')]
    result = next((r for r in candidates if isinstance(r, dict) and ('Summary' in r or 'summary' in r)), None)
    require(result is not None, 'suite result ' + path.name)
    summary = {k.lower(): v for k, v in result.get('Summary', result.get('summary', {})).items()}
    require(type(total) is int and total > 0 and summary.get('total') == summary.get('passed') == total,
            'positive full total ' + path.name)
    require(all(type(summary.get(k)) is int and summary[k] == 0 for k in ('failed', 'skipped', 'inconclusive')),
            'suite nonpass ' + path.name)
    cases = result.get('Results', result.get('results', []))
    require(len(cases) == total and all(c['Status'] == 'Passed' for c in cases), 'per-case full PASS ' + path.name)
    require(not result.get('FilterApplied'), 'filtered run is not full suite ' + path.name)


def main():
    require(not git('status', '--porcelain'), 'clean checkout including untracked files required')
    m = read(FOLDER / 'manifest.json')
    require(m['baseline'] == BASE and m['device'] == 'Deferred to prerelease QA #53/#54/#55', 'baseline/device contract')
    source = m['testedSource']
    require(source == SOURCE, 'tested SourceSHA pin')
    require(subprocess.run(['git', 'merge-base', '--is-ancestor', source, 'HEAD'], cwd=ROOT).returncode == 0,
            'tested source ancestor')
    paths = sorted(p for p in files_at(source) if not p.startswith(PACKAGE))
    tracked = set(git('ls-files').decode().splitlines())
    require(paths == sorted(p for p in tracked if not p.startswith(PACKAGE)), 'tested source inventory')
    require(sha('\n'.join(paths).encode()) == m['sourceInventorySha256'], 'source inventory digest')
    verify_blobs(source, paths)
    protected_prefixes = ('Assets/_Game/Scripts/Domain/', 'Assets/_Game/Scripts/Infrastructure/',
        'Assets/_Game/Configs/', 'Assets/_Game/Scenes/', 'Packages/', 'ProjectSettings/',
        'ArtSource/Blender/ART1/', 'Assets/_Game/Art/Models/ART1/', 'Assets/_Game/Art/Textures/',
        'Assets/_Game/Art/Materials/', 'Assets/_Game/Prefabs/ART1/', 'Docs/review/', 'Assets/Settings/')
    protected = sorted(p for p in files_at(BASE) if p.startswith(protected_prefixes) or
        (p.startswith(('ArtSource/Blender/ART3/', 'Assets/_Game/Art/Models/ART3/', 'Assets/_Game/Prefabs/ART3/')) and '/TIDE/' not in p))
    verify_blobs(BASE, protected)
    actual = {str(p.relative_to(ROOT)) for p in FOLDER.rglob('*') if p.is_file() and p != FOLDER / 'manifest.json'}
    require(actual and actual == set(m['artifacts']), 'evidence inventory')
    for path, digest in m['artifacts'].items():
        require(path in tracked and not (ROOT/path).is_symlink(), 'tracked regular evidence ' + path)
        require(sha((ROOT/path).read_bytes()) == digest, 'artifact digest ' + path)
    for mode in ('edit-mode', 'play-mode'):
        suite(FOLDER / ('reports/' + mode + '.json'), m['suites'][mode])
    static = (FOLDER / 'reports/static-final.txt').read_text()
    require('Ran 38 tests' in static and 'Static Unity checks: PASS' in static, 'static full PASS')
    compile_report = read(FOLDER / 'reports/compile.json')
    require(compile_report['success'] is True and compile_report['data']['summary']['errors'] == 0 and
            not compile_report['data']['failed'], 'compile audit')
    preview = read(FOLDER / 'reports/build-preview.json')
    require(preview['success'] is True and preview['result']['type'] == 'tide' and
            preview['result']['gold'] == 106 and preview['result']['towers'] == 1, 'public preview without purchase')
    for record in read(FOLDER / 'reports/unity-generated-final.json')['files']:
        require(record['sourceSha256'] == record['restoredSha256'] == sha((ROOT / record['path']).read_bytes()),
                'generated serialization restored ' + record['path'])
    before = read(FOLDER / 'reports/reexport-before.json')
    after = read(FOLDER / 'reports/reexport-after.json')
    require(before == after and len(after['models']) == 3, 'actual all-level roundtrip equality')
    require(before == read(FOLDER / 'reports/reexport-canonical-readback.json'), 'canonical committed FBX readback')
    geometry = read(ROOT / 'ArtSource/Blender/ART3/TIDE/geometry-manifest.json')
    require(len(geometry) == 3, 'source geometry inventory')
    for model, original in zip(after['models'], geometry):
        require(model['level'] == original['level'] and model['triangles'] == original['triangles'] and
                0 < model['triangles'] <= 5000 and model['materialSlots'] == original['materialSlots'] == 2,
                'geometry budget and source/import counts')
        require(model['vertices'] == model['normals'] == model['uv'] > 0, 'geometry channels')
        require(len(model['importerRemaps']) == 2 and model['ids'] and model['rendererMaterials'], 'material/id evidence')
    for record in after['files']:
        require(sha((ROOT / record['path']).read_bytes()) == record['sha256'], 'roundtrip reference bytes')
    for png in (FOLDER / 'playmode').glob('*.png'):
        data = png.read_bytes()
        require(data[:8] == b'\x89PNG\r\n\x1a\n' and struct.unpack('>II', data[16:24]) == (1600, 900), 'PlayMode image dimensions')
    completion = read(FOLDER / 'reports/video-completion.json')
    require(completion['success'] and completion['frames'] > 50 and completion['elapsed'] > 10 and
            completion['editor'] and completion['realTime'] and not completion['liveResourceMutation'], 'Editor video flow')
    origin = read(FOLDER / 'reports/video-checkpoint-origin.json')
    require(origin['sha256'] == origin['readbackSha256'] == sha((FOLDER / 'reports/natural-L1-checkpoint.json').read_bytes()) and
            not origin['freshRun'] and not origin['liveResourceMutation'], 'earned checkpoint replay provenance')
    rows = [line.split('|') for line in (FOLDER / 'reports/video-frames.txt').read_text().splitlines()]
    require(len(rows) == completion['frames'] and all(float(b[1]) > float(a[1]) and int(b[2]) > int(a[2]) for a, b in zip(rows, rows[1:])), 'advancing capture time/frame')
    wet = [r for r in rows if int(r[9]) > 0]
    require(wet and max(int(r[4]) for r in wet) > min(int(r[4]) for r in wet), 'actual advancing Wet combat')
    require(any('rollback' in r[3] for r in rows) and any('sold /' in r[3] for r in rows), 'rollback/Sell video stages')
    video = read(FOLDER / 'reports/video-proof.json')
    require(video['sourceFrames'] == len(rows) and video['splices'] == 0 and video['speed'] == 1, 'continuous Editor recording')
    probe = json.loads(subprocess.check_output(['ffprobe', '-v', 'quiet', '-select_streams', 'v:0', '-show_entries',
        'stream=width,height:frame=best_effort_timestamp_time', '-show_frames', '-of', 'json', str(FOLDER / 'editor-flow.mp4')]))
    require((probe['streams'][0]['width'], probe['streams'][0]['height']) == (1600, 900), 'Editor video dimensions')
    pts = [float(f['best_effort_timestamp_time']) for f in probe['frames']]
    require(len(pts) == video['outputFrames'] and all(b > a for a, b in zip(pts, pts[1:])), 'video frame PTS')
    require(abs(pts[-1] - float(rows[-1][1]) + float(rows[0][1])) < .15, 'real elapsed video duration')
    for console in ('console.txt', 'video-console.txt'):
        require(not any(line.startswith(('Error|', 'Exception|', 'Assert|')) for line in
            (FOLDER / ('reports/' + console)).read_text().splitlines()), 'Console errors ' + console)
    print(f"PASS ART3B: {len(paths)} tested source files, {len(protected)} protected baseline files, "
          f"{len(actual)} tracked artifacts, full suites {m['suites']}, roundtrip3, Editor video/Wet/rollback/Sell. Device Deferred.")


if __name__ == '__main__':
    main()
