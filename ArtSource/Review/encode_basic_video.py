"""Encode actual composited PlayMode PNG frames with their captured real-time timestamps."""
import hashlib
import json
from pathlib import Path
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'Docs/review/VFX.1b'
FRAMES = ROOT / 'TestResults/vfx1b/video-frames'


# Полные stage IDs исключают совпадение STORM L3 с более поздним rollback stage.
PICK_STAGES = {
    'video-menu.png':'MainMenu / earned file Continue8',
    'video-pause.png':'Pause / frozen basic shot and status geometry',
    'video-continue.png':'Continue8 / clean shots and status owners / earned state',
    'video-sold.png':'Sold GALE / public command / no resource substitution',
}

def select_stage_frames(manifest):
    import math,re
    shots=json.loads((OUTPUT/'reports/video-shots.json').read_text())
    picks={}
    def pick(name,index):
        frame=manifest[index]
        picks[name]=dict(manifestIndex=index,sourceFrameIndex=int(Path(frame['file']).stem),sourceFile=frame['file'],gameFrame=frame['frame'],elapsed=frame['elapsed'],stage=frame['stage'],sha256=frame['sha256'])
    for name,stage in PICK_STAGES.items():
        matches=[i for i,f in enumerate(manifest) if f['stage']==stage];assert matches,stage
        pick(name,matches[len(matches)//2])
    def vector(s):return [float(x) for x in re.findall(r'-?\d+(?:\.\d+)?(?:[Ee][+-]?\d+)?',s)]
    def distance(a,b):return math.dist(vector(a),vector(b))
    for kind in ('Tide','Gale','Stone'):
        rows=[(i,v) for i,s in enumerate(shots) if not s['paused'] for v in s['shots'] or [] if v['kind']==kind and v['remaining']>0]
        groups={}
        for i,v in rows:groups.setdefault((v['tower'],v['enemy'],v['attackTick']),[]).append((i,v))
        candidates=[v for v in groups.values() if sum(x['flying'] for _,x in v)>=2 and any(x['impact'] for _,x in v)]
        assert candidates,(kind,'no complete moving shot captured')
        same=max(candidates,key=len);flying=[(i,v) for i,v in same if v['flying']];impact=[(i,v) for i,v in same if v['impact']]
        launch=min(flying,key=lambda x:distance(x[1]['head'],x[1]['source']))
        mid=min(flying,key=lambda x:abs(distance(x[1]['head'],x[1]['source'])/max(.001,distance(x[1]['source'],x[1]['target']))-.5))
        pick(kind.lower()+'-launch.png',launch[0]);pick(kind.lower()+'-flight.png',mid[0]);pick(kind.lower()+'-impact.png',impact[0][0])
    for kind in ('Wet','Gust','Crack'):
        matches=[i for i,s in enumerate(shots) if not s['paused'] and any(c['first']==kind or c['second']==kind for c in s['cues'] or [])]
        assert matches,kind
        pick('video-status-'+kind.lower()+'.png',matches[len(matches)//2])
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
    concat = ROOT / 'TestResults/vfx1b/video.ffconcat'
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
    (OUTPUT/'reports/video-decoded.json').write_text(json.dumps(probe,indent=2)+'\n')
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
        if filename in PICK_STAGES: assert pick['stage'] == PICK_STAGES[filename]
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
