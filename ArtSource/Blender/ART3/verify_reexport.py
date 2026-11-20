"""Actual .blend→FBX→Unity roundtrip; open this checkout in Edit Mode first."""
import json
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
CODE = r'''
using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;
using Veilward.Editor;
if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode.");
AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
var models = Enumerable.Range(1,3).Select(level => {
    var path=FlameModelBuilder.ModelFolder+"Tower_FlameLanternL"+level+".fbx";
    AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
    var assets=AssetDatabase.LoadAllAssetsAtPath(path);
    var ids=assets.Select(o=> { AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o,out string g,out long id); return o.GetType().Name+":"+o.name+":"+g+":"+id; }).OrderBy(x=>x).ToArray();
    var mesh=AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentInChildren<MeshFilter>().sharedMesh;
    var data=string.Join(";",mesh.vertices.Select(v=>v.ToString("R")))+string.Join(";",mesh.normals.Select(v=>v.ToString("R")))+string.Join(";",mesh.uv.Select(v=>v.ToString("R")))+string.Join(",",mesh.triangles);
    var hash=BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(data)));
    return new { level, ids, geometrySha256=hash, triangles=mesh.triangles.Length/3, materialSlots=mesh.subMeshCount,
        dependencies=AssetDatabase.GetDependencies(FlameModelBuilder.PrefabPath(level)).OrderBy(x=>x).ToArray() };
}).ToArray();
var files=Directory.GetFiles(FlameModelBuilder.PrefabFolder,"*",SearchOption.AllDirectories)
    .Concat(Directory.GetFiles(FlameModelBuilder.ModelFolder,"*.meta"))
    .Concat(new[]{"Assets/_Game/Prefabs/TestTower.prefab","Assets/_Game/Prefabs/TestTower.prefab.meta","Assets/_Game/Scenes/Battle.unity","Assets/_Game/Scenes/Battle.unity.meta"}).OrderBy(x=>x)
    .Select(p=>new {path=p.Replace('\\','/'),sha256=BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(p)))}).ToArray();
return new { models, files };
'''


def snapshot():
    run = subprocess.run(['unity', 'command', 'script eval', '--caller', 'plugin',
        '--skill', 'unity-cli', '--project-path', str(ROOT), '--format', 'json', '--',
        '--code', CODE], cwd=ROOT, capture_output=True, text=True, check=True)
    payload = json.loads(run.stdout)
    if payload.get('success') is not True or not payload.get('result'):
        raise RuntimeError(run.stdout)
    return payload['result']


before = snapshot()
for source in sorted(Path(__file__).parent.glob('*.blend')):
    log = ROOT/'TestResults'/('art3a-reexport-'+source.stem+'.txt')
    log.parent.mkdir(exist_ok=True)
    with log.open('w') as output:
        subprocess.run(['/Applications/Blender.app/Contents/MacOS/Blender', '--background',
            str(source), '--python-exit-code', '1', '--python', str(Path(__file__).with_name('export_flame.py'))],
            cwd=ROOT, stdout=output, stderr=subprocess.STDOUT, check=True)
after = snapshot()
if before != after:
    raise RuntimeError('Roundtrip changed imported geometry/GUID/local IDs/references/serialized bytes')
output = ROOT/'Docs/review/ART.3a/reports'
output.mkdir(parents=True, exist_ok=True)
for name, data in (('reexport-before.json', before), ('reexport-after.json', after)):
    (output/name).write_text(json.dumps(data, indent=2)+'\n')
print('PASS: 3 edited sources roundtripped, geometry/GUID/local IDs/materials/prefab and scene bytes preserved')
