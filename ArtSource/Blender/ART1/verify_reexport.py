"""Actual saved .blend -> FBX -> Unity reference/geometry round trip.
Requires this worktree open in Unity, Edit Mode. No regeneration of source.
"""
import subprocess, json, sys, os
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
os.chdir(ROOT)
BLENDER='/Applications/Blender.app/Contents/MacOS/Blender'
EVIDENCE=ROOT/'Docs/review/ART.1'
CODE=r'''
using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;
using Veilward.Editor;
if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
var models = ArtReferenceBuilder.ModelNames.Select(name => {
    var path=ArtReferenceBuilder.ModelFolder+name+".fbx";
    AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
    var assets=AssetDatabase.LoadAllAssetsAtPath(path);
    var ids=assets.Select(o=> { AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o,out string g,out long id); return o.GetType().Name+":"+o.name+":"+g+":"+id; }).OrderBy(x=>x).ToArray();
    var mesh=AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentInChildren<MeshFilter>().sharedMesh;
    var text=string.Join(";",mesh.vertices.Select(v=>v.ToString("R")))+string.Join(";",mesh.normals.Select(v=>v.ToString("R")))+string.Join(";",mesh.uv.Select(v=>v.ToString("R")))+string.Join(",",mesh.triangles);
    var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(text)));
    return new { name, ids, geometrySha256=hash, dependencies=AssetDatabase.GetDependencies(ArtReferenceBuilder.PrefabFolder+"P_"+name+".prefab").OrderBy(x=>x).ToArray() };
}).ToArray();
var files=Directory.GetFiles(ArtReferenceBuilder.PrefabFolder,"*",SearchOption.AllDirectories)
    .Concat(Directory.GetFiles(ArtReferenceBuilder.ModelFolder,"*.meta"))
    .Concat(new[]{ ArtReferenceBuilder.ScenePath,ArtReferenceBuilder.ScenePath+".meta" }).OrderBy(x=>x)
    .Select(p=>new {path=p.Replace('\\','/'),sha256=Convert.ToHexString(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(p)))}).ToArray();
return new { models, files };
'''
# Unity's bundled .NET profile predates Convert.ToHexString.
CODE=CODE.replace('Convert.ToHexString(', 'BitConverter.ToString(')
def snapshot():
    p=subprocess.run(['unity','command','script eval','--caller','plugin','--skill','unity-cli','--format','json','--','--code',CODE],capture_output=True,text=True)
    if p.returncode: raise RuntimeError(p.stdout+'\n'+p.stderr)
    data=json.loads(p.stdout)
    if data.get('success') is not True or not data.get('result'): raise RuntimeError(p.stdout)
    return data['result']
before=snapshot()
for source in sorted((ROOT/'ArtSource/Blender/ART1').glob('*.blend')):
    log=ROOT/'Temp'/('art1-reexport-'+source.stem+'.log'); log.parent.mkdir(exist_ok=True)
    with log.open('w') as f:
        subprocess.run([BLENDER,'--background',str(source),'--python',str(ROOT/'ArtSource/Blender/ART1/export_art1.py')],stdout=f,stderr=subprocess.STDOUT,check=True)
    print('reexported',source.name,flush=True)
after=snapshot()
EVIDENCE.mkdir(parents=True,exist_ok=True)
(EVIDENCE/'reexport-before.json').write_text(json.dumps(before,ensure_ascii=False,indent=2)+'\n')
(EVIDENCE/'reexport-after.json').write_text(json.dumps(after,ensure_ascii=False,indent=2)+'\n')
if before != after: raise RuntimeError('FAIL: changed geometry, GUID, local IDs, prefab dependencies or serialized files')
print('PASS: 8 edited sources reexported; geometry, GUID/local IDs, .meta, prefab and scene references preserved.')
