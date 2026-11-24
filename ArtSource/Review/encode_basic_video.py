"""Encode actual composited PlayMode PNG frames with their captured real-time timestamps."""
import hashlib
import json
import gzip
import importlib.util
import sys
sys.dont_write_bytecode=True
from pathlib import Path
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'Docs/review/VFX.1b'
FRAMES = ROOT / 'TestResults/vfx1b/video-frames'


# Конкретные серии выбраны независимым visual review; это editorial selection,
# а не первый enabled impact внутри силуэта. Все фазы привязаны к одному событию.
REVIEW_SERIES={'Tide':(119,121,125),'Gale':(129,130,133),'Stone':(147,149,153)}
spec=importlib.util.spec_from_file_location('vfx1b_delivery',OUTPUT/'verify_delivery.py')
audit=importlib.util.module_from_spec(spec);spec.loader.exec_module(audit)
PICK_STAGES=audit.SYSTEM_PICKS

def read_json(path):
    return json.loads(path.read_text() if path.is_file() else gzip.decompress(path.with_suffix(path.suffix+'.gz').read_bytes()))

def select_stage_frames(manifest):
    import math,re
    shots=read_json(OUTPUT/'reports/video-shots.json')
    picks={}
    def pick(name,index,event=None):
        frame=manifest[index]
        picks[name]=dict(manifestIndex=index,sourceFrameIndex=int(Path(frame['file']).stem),sourceFile=frame['file'],gameFrame=frame['frame'],elapsed=frame['elapsed'],stage=frame['stage'],sha256=frame['sha256'])
        if event is not None:picks[name]['event']={k:event[k] for k in ('kind','tower','enemy','attackTick','level')}
    for name,stage in PICK_STAGES.items():
        matches=[i for i,f in enumerate(manifest) if f['stage']==stage];assert matches,stage
        pick(name,matches[len(matches)//2])
    camera=read_json(OUTPUT/'reports/actual-render-inputs.json')['productionCamera']
    for kind,indices in REVIEW_SERIES.items():
        events=[]
        for phase,index in zip(('launch','flight','impact'),indices):
            candidates=[v for v in shots[index]['shots'] if v['kind']==kind and v['remaining']>0 and (v['impact'] if phase=='impact' else v['flying'])]
            assert len(candidates)==1,(kind,phase,index)
            shot=candidates[0];events.append({k:shot[k] for k in ('kind','tower','enemy','attackTick','level')})
            if phase=='impact':assert audit.visible_impact_pixels(FRAMES/manifest[index]['file'],kind,shot['target'],camera)>=3,(kind,'impact occluded')
            pick(kind.lower()+'-'+phase+'.png',index,shot)
        assert events[0]==events[1]==events[2],kind
    for kind in ('Wet','Gust','Crack'):
        matches=[i for i,s in enumerate(shots) if not s['paused'] and any(c['first']==kind or c['second']==kind for c in s['cues'] or [])]
        assert matches,kind
        pick('video-status-'+kind.lower()+'.png',matches[len(matches)//2])
    return picks


def refresh_picks():
    manifest=read_json(OUTPUT/'reports/video-source-frames.json');proof=read_json(OUTPUT/'reports/video-proof.json')
    original_hash=hashlib.sha256((OUTPUT/'editor-flow.mp4').read_bytes()).hexdigest()
    assert original_hash==proof['outputSha256']
    picks=select_stage_frames(manifest)
    for name,pick in picks.items():
        source=FRAMES/pick['sourceFile'];assert hashlib.sha256(source.read_bytes()).hexdigest()==pick['sha256']
        shutil.copyfile(source,OUTPUT/'playmode'/name)
    proof['picks']=picks
    (OUTPUT/'reports/video-proof.json').write_text(json.dumps(proof,indent=2)+'\n')
    (OUTPUT/'reports/video-picked-frames.json').write_text(json.dumps(picks,indent=2)+'\n')
    assert hashlib.sha256((OUTPUT/'editor-flow.mp4').read_bytes()).hexdigest()==original_hash
    print('PASS:16 required picks refreshed;3same-event visible impact series; MP4 bytes unchanged')


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
    if '--refresh-picks' in sys.argv:refresh_picks()
    else:main()
