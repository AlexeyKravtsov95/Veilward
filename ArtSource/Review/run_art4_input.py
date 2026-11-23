"""Последовательный actual PlayMode input capture; dependent steps stop on any failed command."""
from pathlib import Path
import json,subprocess
ROOT=Path(__file__).resolve().parents[2]
REPORT=ROOT/'Docs/review/ART.4/reports/input-commands.json'
rows=[]
def call(args):
 result=subprocess.run(['unity',*args,'--format','json'],cwd=ROOT,capture_output=True,text=True)
 try:data=json.loads(result.stdout)
 except ValueError:data=result.stdout
 rows.append(dict(args=args,exit=result.returncode,result=data,stderr=result.stderr))
 REPORT.write_text(json.dumps(rows,indent=2)+'\n')
 if result.returncode or isinstance(data,dict) and data.get('success') is False:raise RuntimeError(result.stdout+result.stderr)
 return data
common=['--caller','plugin','--skill','unity-cli','--project-path',str(ROOT)]
def command(group,*args):return call(['command',*group.split(),*common,*args])
def frame():
 result=command('job wait','--','--condition','{"member":"UnityEngine.Time.frameCount","op":"changed"}','--timeout','10')
 if isinstance(result,dict) and result.get('met') is False:raise RuntimeError('Player loop not advancing')
def shot(name):
 frame();command('capture game_view','--width','1600','--height','900','--source','screen','--output-path','Docs/review/ART.4/playmode/'+name+'.png')
 assert (ROOT/'Docs/review/ART.4/playmode'/f'{name}.png').stat().st_size>1000
def run(entry):return command('script run','--file','ArtSource/Review/CaptureArt4Input.cs','--entry','CaptureArt4Input.'+entry)
if __name__=='__main__':
 command('editor.playmode start')
 call(['status','--until-ready','--project-path',str(ROOT)])
 frame();run('Ready');shot('after-overview')
 run('Preview');shot('input-build-preview')
 run('Occupied');shot('input-occupied-range')
 run('Blocked');shot('input-blocked-rejected')
 command('editor.playmode stop')
 print('PASS actual PlayMode overview, free preview, occupied/range, rejected blocked input; commands and liveness receipts saved')
