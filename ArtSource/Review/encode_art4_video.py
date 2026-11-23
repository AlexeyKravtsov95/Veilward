"""Encode actual composited PlayMode PNG frames with their captured real-time timestamps."""
import hashlib
import json
from pathlib import Path
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'Docs/review/ART.4'
FRAMES = ROOT / 'TestResults/art4/video-frames'


# Полные stage IDs исключают совпадение STORM L3 с более поздним rollback stage.
PICK_STAGES = {
    'video-menu.png': 'MainMenu / fresh NewGame',
    'video-preview.png': 'Build preview / selected socket + range / no purchase',
    'video-combat.png': 'Combat1 / production Update / moving Shade + HP + attacks',
    'video-ability.png': 'Ability preview / live combat / public Confirm',
    'video-upgrade.png': 'Preparation2 / earned gold / Upgrade FLAME L2',
    'video-continue.png': 'Continue / restored Preparation2 + FLAME L2 + Grid',
    'video-sold.png': 'Sold / public flow complete / no resource substitution',
}


def select_stage_frames(manifest):
    picks = {}
    for filename, expected_stage in PICK_STAGES.items():
        matches = [(i, frame) for i, frame in enumerate(manifest) if frame['stage'] == expected_stage]
        assert matches, expected_stage
        index, frame = matches[len(matches) // 2]
        picks[filename] = dict(manifestIndex=index, sourceFrameIndex=int(Path(frame['file']).stem),
                               sourceFile=frame['file'], gameFrame=frame['frame'], elapsed=frame['elapsed'],
                               stage=frame['stage'], sha256=frame['sha256'])
    assert len({pick['sourceFrameIndex'] for pick in picks.values()}) == len(PICK_STAGES)
    assert len({pick['sha256'] for pick in picks.values()}) == len(PICK_STAGES), 'Distinct stages must have distinct source pixels'
    return picks


def main():
    completion = json.loads((OUTPUT / 'reports/video-completion.json').read_text())
    if not completion['success']:
        raise RuntimeError('The actual recorded flow failed; do not encode a success clip.')
    rows = [line.split('|') for line in (OUTPUT / 'reports/video-frames.txt').read_text().splitlines()]
    assert len(rows) == completion['frames'] > 50
    files = [FRAMES / (r[0].zfill(5) + '.png') for r in rows]
    assert all(f.is_file() for f in files)
    manifest = [{'file': f.name, 'elapsed': float(row[1]), 'frame': int(row[2]),
                 'stage': row[3], 'sha256': hashlib.sha256(f.read_bytes()).hexdigest()} for f, row in zip(files, rows)]
    picks = select_stage_frames(manifest)
    concat = ROOT / 'TestResults/art4/video.ffconcat'
    lines = ['ffconcat version 1.0']
    for index, file in enumerate(files):
        lines.append("file '" + str(file).replace("'", "'\\''") + "'")
        lines.append('option framerate 1000')
        if index + 1 < len(files):
            lines.append('duration ' + format(float(rows[index+1][1]) - float(rows[index][1]), '.9f'))
    concat.write_text('\n'.join(lines) + '\n')
    command = ['ffmpeg', '-v', 'error', '-y', '-f', 'concat', '-safe', '0', '-i', str(concat),
               '-fps_mode', 'vfr', '-c:v', 'libx264', '-preset', 'medium', '-crf', '18',
               '-pix_fmt', 'yuv420p', '-video_track_timescale', '1000000', '-movflags', '+faststart',
               str(OUTPUT / 'editor-flow.mp4')]
    subprocess.run(command, cwd=ROOT, check=True)
    probe = json.loads(subprocess.check_output(['ffprobe', '-v', 'quiet', '-select_streams', 'v:0',
        '-show_entries', 'stream=width,height:frame=best_effort_timestamp_time', '-show_frames', '-of', 'json',
        str(OUTPUT / 'editor-flow.mp4')]))
    pts = [float(f['best_effort_timestamp_time']) for f in probe['frames']]
    assert len(pts) == len(rows) and all(b > a for a, b in zip(pts, pts[1:]))
    assert abs(pts[-1] - float(rows[-1][1]) + float(rows[0][1])) < .15
    proof = dict(sourceFrames=len(rows), outputFrames=len(pts), firstPts=pts[0], lastPts=pts[-1],
                 sourceFirstElapsed=float(rows[0][1]), sourceLastElapsed=float(rows[-1][1]),
                 speed=1, splices=0, resolution=[1600, 900], editor=True, device=False,
                 source='actual Pipeline capture game_view composited render-target frames, variable real-time capture intervals',
                 limitation='Capture/encoding overhead is not a performance benchmark. Sparse captured frames are not native display presentation timing.',
                 command=command, outputSha256=hashlib.sha256((OUTPUT / 'editor-flow.mp4').read_bytes()).hexdigest())
    # PNG не перерисовываются; проверяем exact stage и SHA исходного и скопированного кадра.
    for filename, pick in picks.items():
        assert pick['stage'] == PICK_STAGES[filename]
        source = FRAMES / pick['sourceFile']
        assert hashlib.sha256(source.read_bytes()).hexdigest() == pick['sha256']
        destination = OUTPUT / 'playmode' / filename
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, destination)
        assert hashlib.sha256(destination.read_bytes()).hexdigest() == pick['sha256']
    proof['picks'] = picks
    (OUTPUT / 'reports/video-proof.json').write_text(json.dumps(proof, indent=2) + '\n')
    (OUTPUT / 'reports/video-picked-frames.json').write_text(json.dumps(picks, indent=2) + '\n')
    (OUTPUT / 'reports/video-source-frames.json').write_text(json.dumps(manifest, indent=2) + '\n')
    print(f'PASS: {len(rows)} real-time Editor frames, {pts[-1]:.3f}s, no splices/speed change; exact PNG picks copied')


if __name__ == '__main__':
    main()
