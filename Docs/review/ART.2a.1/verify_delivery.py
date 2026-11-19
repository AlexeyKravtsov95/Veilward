#!/usr/bin/env python3
"""Read-only verification of tracked media/source and protected base blobs."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[3]
PACKAGE = Path(__file__).resolve().parent
MANIFEST = PACKAGE / 'provenance.json'


def git(*args):
    return subprocess.check_output(['git', '-C', str(ROOT), *args])


def check():
    manifest = json.loads(MANIFEST.read_text())
    tracked = set(git('ls-files', '-z').decode().split('\0'))
    errors = []
    base_blobs = {}
    for row in git('ls-tree', '-rz', manifest['base']).split(b'\0'):
        if row:
            info, path = row.split(b'\t')
            base_blobs[path.decode()] = info.split()[2].decode()
    for section in ('protected', 'delivery'):
        entries = manifest[section]
        if not entries:
            errors.append(f'{section}: empty manifest')
        for entry in entries:
            path = entry['path']
            target = ROOT / path
            if path not in tracked or not target.is_file():
                errors.append(f'{section}: missing or untracked {path}')
                continue
            data = target.read_bytes()
            actual = hashlib.sha256(data).hexdigest()
            if section == 'protected':
                blob = hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest()
                if blob != entry.get('baseGitBlob') or blob != base_blobs.get(path):
                    errors.append(f'protected: base Git blob mismatch {path}')
            if actual != entry['sha256']:
                errors.append(f'{section}: hash mismatch {path}')
        print(f'{section}: {len(entries)} entries checked')
    # Every package file must be in Git, including review/docs/verifier excluded from self-hashing.
    package_files = [p for p in PACKAGE.rglob('*') if p.is_file() and '__pycache__' not in p.parts]
    for path in package_files:
        relative = path.relative_to(ROOT).as_posix()
        if relative not in tracked:
            errors.append(f'package: untracked {relative}')
    print(f'package: {len(package_files)} tracked files checked')
    for path in ('before-combat.mp4', 'after-combat.mp4'):
        result = subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-i', str(PACKAGE / path), '-f', 'null', '-'], capture_output=True)
        if result.returncode:
            errors.append(f'video decode failed: {path}')
        probe = json.loads(subprocess.check_output(['ffprobe', '-v', 'error', '-select_streams', 'v:0', '-count_frames',
            '-show_entries', 'stream=width,height,avg_frame_rate,nb_read_frames', '-of', 'json', str(PACKAGE / path)]))['streams'][0]
        if (probe['width'], probe['height'], probe['avg_frame_rate'], int(probe['nb_read_frames'])) != (1280, 720, '30/1', 240):
            errors.append(f'wrong video dimensions/rate/frame count: {path}')
        telemetry = json.loads((PACKAGE / path.replace('-combat.mp4', '-video-capture.json')).read_text())
        if telemetry['frames'] != 240 or telemetry['firstFrame'] >= telemetry['lastFrame'] or telemetry['firstTick'] >= telemetry['lastTick']:
            errors.append(f'non-advancing telemetry: {path}')
    for name in ('EditMode.json', 'PlayMode.json'):
        payload = json.loads((PACKAGE / name).read_text())
        summary = {k.lower(): v for k, v in (payload.get('Summary') or payload.get('summary')).items()}
        if not (summary['total'] > 0 and summary['passed'] == summary['total'] and all(summary[k] == 0 for k in ('failed', 'skipped', 'inconclusive'))):
            errors.append(f'non-green {name}: {summary}')
        if name == 'PlayMode.json':
            cases = [r for r in (payload.get('Results') or payload.get('results')) if 'ArtReadabilityPlayModeTests.' in r['FullName']]
            if len(cases) != 3 or any(r['Status'] != 'Passed' for r in cases):
                errors.append('missing/non-green new ArtReadability tests')
    if errors:
        print('\n'.join(errors), file=sys.stderr)
        return 1
    print('PASS: protected base, tracked delivery, media decode and positive complete test totals')
    return 0


if __name__ == '__main__':
    raise SystemExit(check())
