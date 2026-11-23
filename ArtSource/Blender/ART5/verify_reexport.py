"""Real saved2sources→FBX→Unity10clips reexport; no full project copy."""
import json, subprocess, os
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3];HERE=Path(__file__).resolve().parent;OUT=ROOT/'Docs/review/ART.5/reports'
UNITY=str(Path.home()/'.unity/bin/unity')
def snap(stage):
 args=[UNITY,'command','script run','--caller','plugin','--skill','unity-cli','--project-path',str(ROOT),'--format','json','--file','ArtSource/Review/ReadbackArt5.cs','--entry','ReadbackArt5.'+stage]
 r=subprocess.run(args,cwd=ROOT,capture_output=True,text=True,check=True)
 (OUT/('readback-'+stage.lower()+'-command.json')).write_text(r.stdout)
 assert json.loads(r.stdout)['success'] is True

def main():
 snap('Before')
 sources=sorted(HERE.glob('Enemy_*.blend'))
 assert {p.name for p in sources}=={'Enemy_Shade.blend','Enemy_RiftWarden.blend'}
 receipts=[]
 for source in sources:
  with (OUT/('reexport-'+source.stem+'.txt')).open('w') as out:
   subprocess.run(['/Applications/Blender.app/Contents/MacOS/Blender','--background',str(source),'--python-exit-code','1','--python',str(HERE/'export_enemies.py')],cwd=ROOT,stdout=out,stderr=subprocess.STDOUT,check=True)
  receipts.append(dict(source=source.name,exit=0,log='reexport-'+source.stem+'.txt'))
 (OUT/'source-reexport-commands.json').write_text(json.dumps(receipts,indent=2)+'\n')
 snap('After')
 a=json.loads((OUT/'reexport-before.json').read_text());b=json.loads((OUT/'reexport-after.json').read_text())
 assert a==b,'Actual geometry/clip keys/sampled poses/IDs/remaps/refs changed'
 assert len(a['models'])==2 and sum(len(m['clips']) for m in a['models'])==10
 print('PASS original2sources/2FBX/8meshes/10clips: real reexport and Unity geometry+keyframes+sampled poses+GUID/remaps/refs exact')
if __name__=='__main__':main()
