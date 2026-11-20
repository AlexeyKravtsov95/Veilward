"""Read-only проверка R2 из чистого checkout; Unity/device заново не запускает."""
import csv
import hashlib
import importlib.util
import json
import math
from pathlib import Path
import statistics
import struct
import subprocess
import sys
sys.dont_write_bytecode = True

ROOT = Path(__file__).resolve().parents[4]
FOLDER = Path(__file__).resolve().parent
PACKAGE = 'Docs/review/ART.3a/'
SOURCE = 'f890a51c9df2f506a46bbe3f10f5f2607368e319'
BUILD = 'ff6a333940e707366bdf6b8df42a12f311d3e143'
R1 = '31369a208109d1c0eddf16ffb3c62d21e0d6a0b1'
BASE = '7dfdf0ec1bc1ef63e6df76810cfd08ad6de5181f'


def require(value, message):
    if not value:
        raise SystemExit('FAIL: ' + message)


def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def read(path):
    return json.loads(path.read_text())


def tree(ref):
    return dict(line.split('\t', 1)[::-1] for line in
                git('ls-tree', '-r', ref).decode().splitlines())


def verify_blobs(ref, paths):
    # Один batch вместо тысяч процессов git show; проверяем именно bytes checkout.
    names = [ref + ':' + p for p in paths]
    proc = subprocess.Popen(['git', 'cat-file', '--batch'], cwd=ROOT,
                            stdin=subprocess.PIPE, stdout=subprocess.PIPE)
    # communicate предотвращает deadlock при большом списке и больших .blend.
    output, _ = proc.communicate(('\n'.join(names) + '\n').encode())
    require(proc.returncode == 0, 'git cat-file')
    offset = 0
    for path in paths:
        end = output.index(b'\n', offset)
        header = output[offset:end].split()
        require(len(header) == 3 and header[1] == b'blob', 'missing git blob: ' + path)
        size = int(header[2]); offset = end + 1
        require((ROOT/path).is_file() and not (ROOT/path).is_symlink(), 'nonregular: ' + path)
        require((ROOT/path).read_bytes() == output[offset:offset+size], 'source/history drift: ' + path)
        offset += size + 1
    require(offset == len(output), 'unexpected blob batch output')


def main():
    m = read(FOLDER/'manifest.json')
    require((m['testedSource'], m['buildSource'], m['reviewedInput'], m['baseline']) ==
            (SOURCE, BUILD, R1, BASE), 'source pins')
    require(not git('status', '--porcelain'), 'clean checkout required (including untracked files)')
    for ref in (SOURCE, BUILD, R1):
        require(subprocess.run(['git', 'merge-base', '--is-ancestor', ref, 'HEAD'], cwd=ROOT).returncode == 0,
                'source ancestry')
    paths = sorted(p for p in tree(SOURCE) if not p.startswith(PACKAGE))
    current = sorted(p for p in git('ls-files').decode().splitlines() if not p.startswith(PACKAGE))
    require(current == paths, 'current source inventory differs from tested source')
    require(sha('\n'.join(paths).encode()) == m['sourceInventorySha256'], 'source inventory digest')
    verify_blobs(SOURCE, paths)
    protected_prefixes = ('Assets/_Game/Scripts/Domain/', 'Assets/_Game/Scripts/Infrastructure/',
        'Assets/_Game/Configs/', 'Assets/_Game/Scenes/', 'Packages/', 'ProjectSettings/',
        'ArtSource/Blender/ART1/', 'Assets/_Game/Art/Models/ART1/', 'Assets/_Game/Art/Textures/',
        'Assets/_Game/Art/Materials/ART1/', 'Assets/_Game/Prefabs/ART1/', 'Docs/review/')
    protected = sorted(p for p in tree(BASE) if p.startswith(protected_prefixes))
    verify_blobs(BASE, protected)
    r1 = sorted(p for p in tree(R1) if p.startswith(PACKAGE) and p not in
                (PACKAGE+'README.md', PACKAGE+'verify_delivery.py'))
    verify_blobs(R1, r1)
    require(git('diff', R1, SOURCE, '--', 'ArtSource/Blender/ART3/Tower_FlameLanternL1.blend',
                'Assets/_Game/Art/Models/ART3/Tower_FlameLanternL1.fbx') == b'', 'R2 changed L1 bytes')
    artifacts = m['artifacts']
    actual = {str(p.relative_to(ROOT)) for p in (ROOT/PACKAGE).rglob('*') if p.is_file()
              and p != FOLDER/'manifest.json'}
    require(actual == set(artifacts) and actual, 'evidence inventory')
    tracked = set(git('ls-files').decode().splitlines())
    for path, digest in artifacts.items():
        require(not Path(path).is_absolute() and '..' not in Path(path).parts, 'unsafe evidence path')
        require(path in tracked and not (ROOT/path).is_symlink(), 'untracked/nonregular evidence')
        require(sha((ROOT/path).read_bytes()) == digest, 'artifact hash: ' + path)
    for mode, total in [('edit-mode',1126), ('play-mode',77)]:
        report = read(FOLDER/('reports/'+mode+'.json'))
        summary = {k.lower(): v for k,v in report.get('Summary',report.get('summary',{})).items()}
        require(summary.get('total') == summary.get('passed') == total and
                all(summary.get(k) == 0 for k in ('failed','skipped','inconclusive')), mode+' nonempty full PASS')
        cases = report.get('Results',report.get('results',[]))
        require(len(cases) == total and all(c['Status'] == 'Passed' for c in cases), mode+' case inventory')
    before = read(FOLDER/'reports/reexport-before.json'); after = read(FOLDER/'reports/reexport-after.json')
    require(before == after and len(after['models']) == 3, 'roundtrip mismatch')
    for record in after['files']:
        require(sha((ROOT/record['path']).read_bytes()) == record['sha256'].replace('-','').lower(), 'roundtrip drift')
    geometry = read(ROOT/'ArtSource/Blender/ART3/geometry-manifest.json')
    require([r['triangles'] for r in after['models']] == [1256,1580,1736], 'import triangle counts')
    for a,b in zip(after['models'],geometry):
        require(a['triangles'] == b['nondegenerateTriangles'] and b['triangles'] <= 5000 and
                a['materialSlots'] == b['materialSlots'] == 2, 'geometry budget')
    for png in (FOLDER/'playmode').glob('*.png'):
        require(struct.unpack('>II',png.read_bytes()[16:24]) == (1600,900), 'PlayMode dimensions')
    device = FOLDER/'device'; receipt = read(device/'device-receipt.json')
    require(receipt['testedRuntimeSource'] == SOURCE and receipt['buildSource'] == BUILD and
            not receipt['unityDevelopmentBuild'], 'device receipt')
    runtime_paths = sorted(p for p in paths if p.startswith(('Assets/','Packages/','ProjectSettings/')))
    source_tree = tree(SOURCE); build_tree = tree(BUILD)
    for number,profile in [('77','production'),('78','art-reference')]:
        b = device/('build'+number); build = read(b/'build-receipt.json')
        require(build['sourceSHA'] == BUILD and build['buildNumber'] == number and build['profile'] == profile,
                'build receipt')
        hashes = read(b/'runtime-input-hashes.json'); audit = read(b/'source-audit.json')
        require(sorted(hashes) == runtime_paths and len(hashes) == 580, 'runtime input inventory')
        for path,digest in hashes.items():
            require(source_tree[path] == build_tree[path], 'build/runtime source differs: '+path)
            require(sha((ROOT/path).read_bytes()) == digest == audit[path], 'build runtime input digest')
    flow = read(device/'phone-flow.json'); require(len(flow) == 6, 'native observation count')
    states = {row['label']:row for row in flow}
    for row in flow:
        path = device/(row['label']+'.json'); cp = read(path)
        require(cp['schemaVersion'] == row['schema'] == 4 and cp['scenarioContentId'] == row['content'] == 'eight-wave-ability-boss-v2', 'native schema/content')
        require(sha(path.read_bytes()) == row['checkpointSha256'], 'native checkpoint hash')
        require((row['tick'],row['hp'],row['kills'],row['waveIndex'],row['phase']) == (2815,7,32,6,0), 'safe checkpoint context')
        for a,b in [('phase','phase'),('waveIndex','waveIndex'),('tick','tick'),('hp','baseHealth'),
                    ('gold','gold'),('kills','kills'),('towers','towers')]:
            require(row[a] == cp['state'][b], 'native checkpoint crosscheck')
    for label,level,gold,invested in [('upgrade-L1-solo',1,282,70),('upgrade-L2-inspect',2,177,175),('upgrade-L3-inspect',3,37,315)]:
        row=states[label]; require(row['gold']==gold and len(row['towers'])==1, 'earned economy')
        t=row['towers'][0]
        require((t['towerId'],t['towerTypeId'],t['x'],t['y'],t['level'],t['investedGold']) ==
                (1,'flame',2,1,level,invested), 'native tower identity')
    require(states['upgrade-L1-solo']['checkpointSha256'] == sha((ROOT/PACKAGE/'device/natural-L1-solo.json').read_bytes()), 'earned L1 restore provenance')
    require(states['rollback-L3']['checkpointSha256'] == states['upgrade-L3-inspect']['checkpointSha256'], 'rollback drift')
    require(states['sold-L3']['checkpointSha256'] == states['sold-continue']['checkpointSha256'] and
            states['sold-L3']['gold']==257 and not states['sold-L3']['towers'], 'Sell/Continue cleanup')
    for png in device.rglob('*.png'):
        data=png.read_bytes(); require(data[:8]==b'\x89PNG\r\n\x1a\n' and struct.unpack('>II',data[16:24])==(2622,1206), 'native PNG dimensions')
    observations=read(device/'occlusion/capture-settings.json')
    require(len(observations)==9 and {r['file'] for r in observations} ==
            {f'{v}-{p}.png' for v in ('L1','L2','L3') for p in ('front','side','behind')}, 'native occlusion inventory')
    for row in observations:
        s=row['settings']; require(row['file']==s['modelVariant']+'-'+s['scenario']+'.png' and
                json.loads(s['sourceReceipt'])['sourceSHA']==BUILD and json.loads(s['sourceReceipt'])['buildNumber']=='78', 'native occlusion setup/source')
    build_obs=read(device/'build-observations.json')['builds']
    require(len(build_obs)==2 and all(r['xcodeBuildExit']==r['codesignStrictDeepExit']==r['installExit']==0 for r in build_obs), 'native build status')
    spec=importlib.util.spec_from_file_location('analyze_art2b',ROOT/'Scripts/analyze-art2b.py')
    analyzer=importlib.util.module_from_spec(spec);spec.loader.exec_module(analyzer)
    comparison=read(device/'matched/comparison.json'); settings=[]
    for variant in ('ART1','L1','L2','L3'):
        folder=device/'matched'/variant
        computed=analyzer.analyze(folder)
        require(all(read(folder/f'window-{i}.json')['warmupSeconds'] == 5 for i in (1,2,3)), 'matched warmup protocol')
        require(computed==read(folder/'summary.json'), 'raw timing analysis: '+variant)
        s=read(folder/'run.json'); require(s['modelVariant']==variant and s['scenario']=='dense' and
                json.loads(s['sourceReceipt'])['sourceSHA']==BUILD and json.loads(s['sourceReceipt'])['buildNumber']=='78', 'matched variant/source')
        require(comparison['variants'][variant]['timing']==computed, 'comparison timing')
        settings.append({k:v for k,v in s.items() if k not in ('modelVariant','modelRendererCount','modelMaterialSubmissions')})
        with (folder/'native-memory.csv').open() as f: samples=list(csv.DictReader(f))
        times=[int(row['start']) for row in samples]
        require(len(samples)>=2 and all(b>a for a,b in zip(times,times[1:])), 'native sample span')
        summary=comparison['variants'][variant]['nativeMemory']
        require(summary['sampleCount']==len(samples) and summary['sampleSpanSeconds']==(times[-1]-times[0])/1e9, 'native summary count/span')
        for key,stat in summary['metrics'].items():
            values=[float(row[key]) for row in samples if row[key]]
            require(stat==dict(min=min(values),median=statistics.median(values),max=max(values),samples=len(values)), 'native memory recompute')
    require(all(s==settings[0] for s in settings), 'unmatched device/camera/quality/sampling configuration')
    for clip in read(device/'video-proof.json')['clips']:
        path=ROOT/clip['file']; require(sha(path.read_bytes())==clip['outputSha256'] and clip['continuous'] and clip['speed']==1 and clip['splices']==0, 'video provenance')
        probe=json.loads(subprocess.check_output(['ffprobe','-v','quiet','-select_streams','v:0','-show_entries','stream=width,height:frame=best_effort_timestamp_time','-show_frames','-of','json',str(path)]))
        require((probe['streams'][0]['width'],probe['streams'][0]['height'])==(2622,1206), 'native video dimensions')
        pts=[float(f['best_effort_timestamp_time']) for f in probe['frames']]
        require(len(pts)==clip['outputFrames'] and all(b>a for a,b in zip(pts,pts[1:])) and pts[-1]==clip['outputLastPts'], 'video PTS')
    print(f'PASS R2: {len(paths)} exact tested source files; {len(protected)} baseline + {len(r1)} R1 protected; '
          f'{len(actual)} evidence artifacts; suites1126/77; roundtrip3; native77 flow + continuous videos; matched78 four variants. Owner ART approval separate.')


if __name__ == '__main__':
    main()
