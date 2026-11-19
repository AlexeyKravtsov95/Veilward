#!/usr/bin/env python3
"""Read-only revision-2 source/media/timeline audit; no historical manifest updates."""
import hashlib
import json
from pathlib import Path
import struct
import subprocess
import sys

PACKAGE = Path(__file__).resolve().parent
ROOT = PACKAGE.parents[3]


def git(*args):
    return subprocess.check_output(['git', '-C', str(ROOT), *args])


def main():
    manifest = json.loads((PACKAGE / 'provenance.json').read_text())
    tracked = set(git('ls-files', '-z').decode().split('\0'))
    errors = []
    trees = {}
    for entry in manifest['protected'] + manifest['delivery']:
        path = entry['path']; file = ROOT / path
        if path not in tracked or not file.is_file():
            errors.append('missing/untracked: ' + path); continue
        data = file.read_bytes()
        if hashlib.sha256(data).hexdigest() != entry['sha256']:
            errors.append('SHA256 mismatch: ' + path)
        if 'ref' in entry:
            ref = entry['ref']
            if ref not in trees:
                trees[ref] = {}
                for row in git('ls-tree', '-rz', ref).split(b'\0'):
                    if row:
                        info, name = row.split(b'\t'); trees[ref][name.decode()] = info.split()[2].decode()
            blob = hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest()
            if blob != entry['baseGitBlob'] or blob != trees[ref].get(path): errors.append('base blob mismatch: ' + path)
    assert manifest['protected'] and manifest['delivery']
    for file in PACKAGE.rglob('*'):
        if file.is_file() and '__pycache__' not in file.parts and file.relative_to(ROOT).as_posix() not in tracked:
            errors.append('untracked package file: ' + str(file))
    for file in PACKAGE.glob('*.png'):
        if struct.unpack('>II', file.read_bytes()[16:24]) != (1600, 900): errors.append('wrong PNG size: ' + file.name)
    for stage in ('before', 'after'):
        file = PACKAGE / f'{stage}-combat.mp4'
        result = subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-i', str(file), '-f', 'null', '-'], capture_output=True)
        if result.returncode: errors.append('decode failed: ' + str(file))
        probe = json.loads(subprocess.check_output(['ffprobe', '-v', 'error', '-select_streams', 'v:0', '-count_frames',
            '-show_entries', 'stream=width,height,avg_frame_rate,nb_read_frames', '-of', 'json', str(file)]))['streams'][0]
        if (probe['width'], probe['height'], probe['avg_frame_rate'], int(probe['nb_read_frames'])) != (1280, 720, '30/1', 240):
            errors.append('wrong video parameters: ' + stage)
        info = json.loads((PACKAGE / f'{stage}-video-capture.json').read_text())
        if not (info['frames'] == 240 and info['firstFrame'] < info['lastFrame'] and info['firstTick'] == 0 and info['lastTick'] == 239 and info['attacks'] == 8):
            errors.append('non-advancing/incorrect telemetry: ' + stage)
    for mode in ('EditMode', 'PlayMode'):
        report = json.loads((PACKAGE / f'{mode}.json').read_text()); results = report.get('Results') or report.get('results')
        total = {k.lower():v for k,v in (report.get('Summary') or report.get('summary')).items()}
        if not (total['total'] > 0 and total['total'] == total['passed'] == len(results) and
                all(total[k] == 0 for k in ('failed','skipped','inconclusive')) and all(r['Status'] == 'Passed' for r in results)):
            errors.append('non-positive/full-pass suite: ' + mode)
        if mode == 'PlayMode':
            names = {r['FullName'] for r in results}
            required = {
                'Veilward.Tests.PlayMode.FlameProjectilePlayModeTests.FlightCannotOverpaintHealthBarPixels',
                'Veilward.Tests.PlayMode.FlameProjectilePlayModeTests.AuthoritativeTimeline_NonlethalLethalMovingTargetsAndPoolReuseDoNotChangeDomain',
                'Veilward.Tests.PlayMode.FlameProjectilePlayModeTests.PauseAndRestart_FreezeFlightAndPendingDeathAndClearBothFlightAndImpact',
                'Veilward.Tests.PlayMode.FlameProjectilePlayModeTests.TerminalDrainAndSceneReload_DoNotLeaveDelayedEnemiesOrEffects',
                'Veilward.Tests.PlayMode.ArtReadabilityPlayModeTests.FlameProjectile_MovingHeadArrivesBeforeImpactAndClearsWithoutNewObjects'}
            if not required.issubset(names): errors.append('new projectile regressions missing')
    frames = [json.loads(line) for line in (PACKAGE / 'video-frames.jsonl').read_text().splitlines()]
    assert len(frames) == 240 and len({f['frame'] for f in frames}) == 240
    groups = {}
    for frame in frames:
        assert frame['playMode'] and frame['ortho'] == 8.5 and frame['cameraPosition'] == {'x':8.0,'y':11.0,'z':8.0}
        assert frame['presentedTick'] == max(0, frame['tick'] - 6)
        if frame['attacks'] > 0: groups.setdefault(frame['attacks'], []).append(frame)
    assert len(groups) == 8
    lethal = nonlethal = 0
    for group in groups.values():
        flying = [f for f in group if f['phase'] == 'flight']; impact = [f for f in group if f['phase'] == 'impact']
        assert len(flying) == 6 and len(impact) == 5
        assert len({tuple(f['head'].values()) for f in flying}) == 6
        assert all(f['targetVisible'] and f['shownHp'] > 0 and f['projectileTargetId'] == f['targetId'] for f in flying)
        assert all(f['targetId'] == flying[0]['targetId'] and f['target'] == flying[0]['target'] for f in flying + impact)
        assert all(sum((f['head'][k]-f['tailEnd'][k])**2 for k in ('x','y','z')) <= .481**2 for f in flying)
        hit = impact[0]; assert hit['tick'] - flying[0]['tick'] == 6 and hit['presentedTick'] == flying[0]['tick'] and hit['head'] == hit['target']
        if hit['lethal']:
            lethal += 1; assert not hit['targetVisible'] and hit['shownHp'] == -1 and all(f['liveHp'] == -1 for f in flying)
        else:
            nonlethal += 1; assert hit['targetVisible'] and hit['shownHp'] > 0
    assert lethal > 0 and nonlethal > 0
    print(f"protected {len(manifest['protected'])}, delivery {len(manifest['delivery'])}; timeline 8 arrivals: {lethal} lethal/{nonlethal} nonlethal")
    if errors:
        print('\n'.join(errors), file=sys.stderr); return 1
    print('PASS: tracked source/media/base blobs, full suites, moving head and delayed authoritative hit/death timeline')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
