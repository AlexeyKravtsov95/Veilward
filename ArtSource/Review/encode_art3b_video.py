"""Encode actual composited PlayMode PNG frames with their captured real-time timestamps."""
import hashlib
import json
from pathlib import Path
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'Docs/review/ART.3b'
FRAMES = ROOT / 'TestResults/ART3B/video-frames'


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
    concat = ROOT / 'TestResults/ART3B/video.ffconcat'
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
                 source='actual ScreenCapture composited GameView frames, variable real-time capture intervals',
                 limitation='Capture/encoding overhead is not a performance benchmark. Sparse captured frames are not native display presentation timing.',
                 command=command, outputSha256=hashlib.sha256((OUTPUT / 'editor-flow.mp4').read_bytes()).hexdigest())
    (OUTPUT / 'reports/video-proof.json').write_text(json.dumps(proof, indent=2) + '\n')
    (OUTPUT / 'reports/video-source-frames.json').write_text(json.dumps(manifest, indent=2) + '\n')
    # Не перерисовываем кадры. Эти PNG — точные source pixels, выбранные по stage.
    picks = {'TIDE L1 /': 'video-L1', 'TIDE L2 /': 'video-L2', 'TIDE L3 /': 'video-L3',
             'actual Wet enemy': 'video-Wet-inspect', 'restored Preparation': 'video-rollback', 'sold /': 'video-sold'}
    for needle, name in picks.items():
        matches = [i for i, r in enumerate(rows) if needle in r[3]]
        assert matches, needle
        shutil.copyfile(files[matches[len(matches)//2]], OUTPUT / ('playmode/' + name + '.png'))
    print(f'PASS: {len(rows)} real-time Editor frames, {pts[-1]:.3f}s, no splices/speed change; exact PNG picks copied')


if __name__ == '__main__':
    main()
