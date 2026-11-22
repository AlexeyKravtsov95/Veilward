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
PACKAGE = 'Docs/review/ART.3e/'
BASE = '62d4dbd17ef9a8179f2a9ee6163dd7da75a72e12'
SOURCE = '9654133029902fb2541ee833d6ef80710755a5bb'


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
    if path.suffix == '.json':
        p=read(path)
        if 'Summary' in p:
            require(p.get('success') is True and p.get('Mode') == 'EditMode', 'synchronous EditMode success')
            summary={k.lower():v for k,v in p['Summary'].items()}; cases=p.get('Results',[])
        else:
            require(p.get('status') == 'completed', 'asynchronous suite completed')
            summary=p.get('summary',{}); cases=p.get('results',[])
        require(summary.get('total') == summary.get('passed') == total > 0, 'full positive JSON suite')
        require(all(type(summary.get(k)) is int and summary[k] == 0 for k in ('failed','skipped','inconclusive')), 'JSON suite nonpass')
        require(len(cases)==total and all(c['Status']=='Passed' for c in cases), 'per-case JSON PASS')
        return
    import xml.etree.ElementTree as ET
    root = ET.parse(path).getroot()
    require(type(total) is int and total > 0, 'positive suite total')
    require(root.tag == 'test-run' and root.get('result') == 'Passed', 'NUnit full PASS ' + path.name)
    require(int(root.get('total', 0)) == int(root.get('passed', 0)) == total, 'full positive total ' + path.name)
    require(all(int(root.get(k, -1)) == 0 for k in ('failed', 'skipped', 'inconclusive')), 'suite nonpass ' + path.name)
    cases = root.findall('.//test-case')
    require(len(cases) == total and all(c.get('result') == 'Passed' for c in cases), 'per-case PASS ' + path.name)


def is_source(path):
    return not path.startswith(PACKAGE) or path.startswith(PACKAGE + 'inputs/')


def verify_inventory(source, tracked):
    paths = sorted(p for p in files_at(source) if is_source(p))
    require(paths == sorted(p for p in tracked if is_source(p)), 'tested source inventory')
    verify_blobs(source, paths)
    return paths


def verify_artifacts(artifacts, tracked):
    for path, digest in artifacts.items():
        require(path in tracked and not (ROOT/path).is_symlink(), 'tracked regular evidence ' + path)
        require(sha((ROOT/path).read_bytes()) == digest, 'artifact digest ' + path)


def main():
    # Посторонние неимпортируемые заметки/pycache сохраняются; tracked source и весь пакет должны быть чистыми.
    require(not git('status', '--porcelain', '--untracked-files=no'), 'clean tracked checkout required')
    untracked=git('ls-files','--others','--exclude-standard').decode().splitlines()
    input_roots=('Assets/','Packages/','ProjectSettings/','ArtSource/','Docs/','Scripts/')
    require(not any(p.startswith(input_roots) and not p.startswith('Scripts/__pycache__/') for p in untracked),
            'untracked source/evidence inputs')
    m = read(FOLDER / 'manifest.json')
    require(m['baseline'] == BASE and m['device'] == 'Deferred to prerelease QA #53/#54/#55', 'baseline/device contract')
    source = m['testedSource']
    require(source == SOURCE, 'tested SourceSHA pin')
    require(subprocess.run(['git', 'merge-base', '--is-ancestor', source, 'HEAD'], cwd=ROOT).returncode == 0,
            'tested source ancestor')
    tracked = set(git('ls-files').decode().splitlines())
    paths = verify_inventory(source, tracked)
    require(sha('\n'.join(paths).encode()) == m['sourceInventorySha256'], 'source inventory digest')
    protected_prefixes = ('Assets/_Game/Scripts/Domain/', 'Assets/_Game/Scripts/Infrastructure/',
        'Assets/_Game/Configs/', 'Assets/_Game/Scenes/', 'Packages/', 'ProjectSettings/',
        'ArtSource/Blender/ART1/', 'Assets/_Game/Art/Models/ART1/', 'Assets/_Game/Art/Textures/',
        'Assets/_Game/Art/Materials/', 'Assets/_Game/Prefabs/ART1/', 'Docs/review/', 'Assets/Settings/')
    protected = sorted(p for p in files_at(BASE) if p.startswith(protected_prefixes) or
        (p.startswith(('ArtSource/Blender/ART3/', 'Assets/_Game/Art/Models/ART3/', 'Assets/_Game/Prefabs/ART3/')) and '/STEAM/' not in p))
    verify_blobs(BASE, protected)
    actual = {str(p.relative_to(ROOT)) for p in FOLDER.rglob('*') if p.is_file() and p != FOLDER / 'manifest.json'}
    require(actual and actual == set(m['artifacts']), 'evidence inventory')
    verify_artifacts(m['artifacts'], tracked)
    for mode in ('edit-mode', 'play-mode'):
        suite(FOLDER / ('reports/' + mode + '.json'), m['suites'][mode])
    static = (FOLDER / 'reports/static-final.txt').read_text()
    require('Ran 38 tests' in static and 'Static Unity checks: PASS' in static, 'static full PASS')
    compile_report = read(FOLDER / 'reports/compile.json')
    require(compile_report['success'] is True and compile_report['data']['summary']['errors'] == 0 and
            not compile_report['data']['failed'], 'compile audit')
    require(read(FOLDER / 'reports/console-startup.json')['groundTruth']['consoleErrors'] == 0, 'reopened Editor startup Console')
    console = read(FOLDER / 'reports/console-final.json')
    logs = read(FOLDER / 'reports/console-suite-errors.json')
    classification = read(FOLDER / 'reports/console-suite-classification.json')
    require(console['counts']['error'] == logs['counts']['error'] == len(logs['entries']) == classification['entries'] == 114,
            'complete expected negative-test Console inventory')
    require(classification['counts'] == {'expectedCheckpointErrors': 113, 'expectedFrozenMeasurementGuard': 1} and
            classification['allMatchExistingLogAssertExpectations'] and classification['unexpectedEntries'] == 0 and
            classification['consoleCleared'] is False, 'explicit expected negative-test Console classification')
    checkpoint_logs = [e for e in logs['entries'] if e['message'].startswith('Checkpoint')]
    require(len(checkpoint_logs) == 113 and all('Veilward.Tests.' in e['stackTrace'] for e in checkpoint_logs),
            'checkpoint negative-test origins')
    other_logs = [e for e in logs['entries'] if e not in checkpoint_logs]
    require(len(other_logs) == 1 and other_logs[0]['message'] ==
            'InvalidOperationException: Frozen normal combat is not measurement evidence.' and
            'ArtDeviceProbe.SaveWindow' in other_logs[0]['stackTrace'], 'expected frozen measurement guard')
    preview = read(FOLDER / 'reports/preview-select.json')
    require(preview['success'] is True and preview['result']['type'] == 'steam' and
            preview['result']['gold'] == 3 and preview['result']['towers'] == 1, 'public preview without purchase')
    before = read(FOLDER / 'reports/reexport-before.json')
    after = read(FOLDER / 'reports/reexport-after.json')
    require(before == after and len(after['models']) == 3, 'actual all-level roundtrip equality')
    require(before == read(FOLDER / 'reports/reexport-canonical-readback.json'), 'canonical committed FBX readback')
    geometry = read(ROOT / 'ArtSource/Blender/ART3/STEAM/geometry-manifest.json')
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
        require(data[:8] == b'\x89PNG\r\n\x1a\n' and struct.unpack('>II', data[16:24]) == (1600, 900), 'PlayMode image dimensions '+png.name)
    checkpoints=[read(FOLDER/f'reports/natural-L{level}-checkpoint.json') for level in (1,2,3)]
    expected=[(1,3,1183,20,12,110),(2,6,2668,50,39,275),(3,7,3168,3,50,495)]
    for cp,(level,wave,tick,gold,kills,invested) in zip(checkpoints,expected):
        state=cp['state'];tower=next(t for t in state['towers'] if t['towerTypeId']=='steam')
        require(cp['schemaVersion']==4 and cp['scenarioContentId']=='eight-wave-ability-boss-v2', 'schema/content unchanged')
        require((state['waveIndex'],state['tick'],state['gold'],state['baseHealth'],state['kills'])==(wave,tick,gold,14,kills), 'earned checkpoint arithmetic')
        require((tower['towerId'],tower['x'],tower['y'],tower['level'],tower['investedGold'])==(3,1,0,level,invested), 'earned identity/level')
        require({'flame','tide'}<=set(state['elementProgression']['unlockedElementIds']), 'genuine hybrid unlocks')
        require(state['elementProgression']['spentChoices'][0]=={'choiceId':'after-wave-3','elementId':'tide'}, 'public after3 unlock')
    require(len(checkpoints[0]['state']['towers'])==len(checkpoints[1]['state']['towers'])==3 and len(checkpoints[2]['state']['towers'])==1, 'public support sales')
    require(150+50*5+7*20-2*(70-49)-495==3, 'full earned budget498')
    sold=read(FOLDER/'reports/video-sold.json')['state']
    require(sold['gold']==349 and not sold['towers'] and sold['tick']==3168 and sold['baseHealth']==14 and sold['kills']==50, 'single Sell refund346')
    intrinsic=read(FOLDER/'reports/video-intrinsic.json')
    require(intrinsic['synthetic'] is False and intrinsic['hp']==15 and intrinsic['hitCount']==2 and intrinsic['appliedHpLoss']==102 and
            intrinsic['reaction']['TriggerKind']==1 and intrinsic['reaction']['SourceTowerId']['Value']==3, 'actual read-only intrinsic AoE/HP evidence')
    require('Активных статусов нет' in (FOLDER/'reports/video-inspector.txt').read_text(), 'STEAM empty basic status payload')
    completion = read(FOLDER / 'reports/video-completion.json')
    require(completion['success'] and completion['frames'] > 50 and completion['elapsed'] > 10 and
            completion['editor'] and completion['realTime'] and not completion['liveResourceMutation'], 'Editor video flow')
    origin = read(FOLDER / 'reports/video-checkpoint-origin.json')
    require(origin['sha256'] == origin['readbackSha256'] == sha((FOLDER / 'reports/natural-L1-checkpoint.json').read_bytes()) and
            not origin['freshRun'] and not origin['liveResourceMutation'], 'earned checkpoint replay provenance')
    require((FOLDER/'reports/video-safe-wire.txt').read_bytes() == (FOLDER/'reports/video-rollback-wire.txt').read_bytes(), 'full live rollback wire')
    require(read(FOLDER/'reports/video-L3-before-combat.json') == read(FOLDER/'reports/natural-L3-checkpoint.json'), 'earned L3 upgrade result')
    rows = [line.split('|') for line in (FOLDER / 'reports/video-frames.txt').read_text().splitlines()]
    require(len(rows) == completion['frames'] and all(float(b[1]) > float(a[1]) and int(b[2]) > int(a[2]) for a, b in zip(rows, rows[1:])), 'advancing capture time/frame')
    combat = [r for r in rows if r[3].startswith(('advancing REAL-TIME Combat','advancing combat / actual intrinsic Steam')) and r[4]]
    require(combat and max(int(r[4]) for r in combat)>min(int(r[4]) for r in combat) and any(int(r[9])>0 for r in combat), 'actual advancing intrinsic Steam combat')
    require(any('restored Preparation' in r[3] for r in rows) and any('sold /' in r[3] for r in rows),
            'rollback/Sell video stages')
    video = read(FOLDER / 'reports/video-proof.json')
    require(video['sourceFrames'] == len(rows) and video['splices'] == 0 and video['speed'] == 1, 'continuous Editor recording')
    source_frames = read(FOLDER / 'reports/video-source-frames.json')
    picks = read(FOLDER / 'reports/video-picked-frames.json')
    prefixes = {'video-L1.png': 'STEAM L1 / Inspect + real Upgrade price', 'video-L2.png': 'STEAM L2 / same TowerId + cell / earned budget', 'video-L3.png': 'STEAM L3 / same TowerId + cell / MAX + range',
                'video-Intrinsic-Steam-inspect.png': 'advancing combat / actual intrinsic Steam enemy Inspect + HP',
                'video-rollback.png': 'restored Preparation / STEAM L3 / unchanged Gold-HP-kills', 'video-sold.png': 'sold / no model roots / public-command Editor proof complete'}
    require(picks == video['picks'] and set(picks) == set(prefixes), 'exact video pick inventory')
    require(len({p['sourceFrameIndex'] for p in picks.values()}) == len(prefixes) and
            len({p['sha256'] for p in picks.values()}) == len(prefixes), 'distinct stage frames/pixels')
    for name, prefix in prefixes.items():
        pick = picks[name]; index = pick['manifestIndex']; original = source_frames[index]
        require(pick['stage'] == prefix and pick['stage'] == original['stage'] == rows[index][3],
                'video pick stage ' + name)
        require(pick['sourceFrameIndex'] == int(rows[index][0]) and pick['sha256'] == original['sha256'] ==
                sha((FOLDER / ('playmode/' + name)).read_bytes()), 'video pick source SHA ' + name)
    probe = json.loads(subprocess.check_output(['ffprobe', '-v', 'quiet', '-select_streams', 'v:0', '-show_entries',
        'stream=width,height:frame=best_effort_timestamp_time', '-show_frames', '-of', 'json', str(FOLDER / 'editor-flow.mp4')]))
    require((probe['streams'][0]['width'], probe['streams'][0]['height']) == (1600, 900), 'Editor video dimensions')
    pts = [float(f['best_effort_timestamp_time']) for f in probe['frames']]
    require(len(pts) == video['outputFrames'] and all(b > a for a, b in zip(pts, pts[1:])), 'video frame PTS')
    require(abs(pts[-1] - float(rows[-1][1]) + float(rows[0][1])) < .15, 'real elapsed video duration')
    require(max(abs(t-float(r[1])+float(rows[0][1])) for t,r in zip(pts,rows)) < .003, 'per-frame source PTS')
    for console in ('console.txt', 'video-console.txt'):
        require(not any(line.startswith(('Error|', 'Exception|', 'Assert|')) for line in
            (FOLDER / ('reports/' + console)).read_text().splitlines()), 'Console errors ' + console)
    controls=read(FOLDER/'reports/delivery-controls.json')
    require(controls['verifierSha256']==sha(Path(__file__).read_bytes()), 'current verifier controls pin')
    require(controls['positive']['passed'] and all(c['passed'] for c in controls['negative']), 'current small-fixture controls')
    print(f"PASS ART3E: {len(paths)} tested source files, {len(protected)} protected baseline files, "
          f"{len(actual)} tracked artifacts, full suites {m['suites']}, roundtrip3, Editor video/Steam/rollback/Sell. Device Deferred.")


if __name__ == '__main__':
    main()
