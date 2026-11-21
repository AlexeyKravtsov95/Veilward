"""Реальный .blend→FBX→Unity roundtrip только GALE, при открытом Edit Mode этого checkout."""
import json
from pathlib import Path
import subprocess
import sys
sys.dont_write_bytecode = True

ROOT = Path(__file__).resolve().parents[4]
FOLDER = Path(__file__).resolve().parent
OUTPUT = ROOT / 'Docs/review/ART.3c/reports'
CODE = r'''
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using Veilward.Editor;
if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
string Hash(byte[] data){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(data)).Replace("-","").ToLowerInvariant();}
string Id(UnityEngine.Object o){if(o==null)return "null";AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o,out string g,out long id);return o.GetType().Name+":"+o.name+":"+g+":"+id;}
AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
var models=Enumerable.Range(1,3).Select(level=>{
    var path=GaleModelBuilder.ModelFolder+"Tower_GaleTalismanL"+level+".fbx";
    AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
    var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
    var mesh=model.GetComponentInChildren<MeshFilter>().sharedMesh;
    var data=string.Join(";",mesh.vertices.Select(v=>v.ToString("R")))+"|normals|"+string.Join(";",mesh.normals.Select(v=>v.ToString("R")))+
        "|uv|"+string.Join(";",mesh.uv.Select(v=>v.ToString("R")))+"|submeshes|"+
        string.Join(";",Enumerable.Range(0,mesh.subMeshCount).Select(i=>string.Join(",",mesh.GetTriangles(i))));
    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(GaleModelBuilder.PrefabPath(level));
    return new{level,ids=AssetDatabase.LoadAllAssetsAtPath(path).Select(Id).OrderBy(x=>x).ToArray(),geometrySha256=Hash(Encoding.UTF8.GetBytes(data)),
        vertices=mesh.vertexCount,normals=mesh.normals.Length,uv=mesh.uv.Length,triangles=mesh.triangles.Length/3,materialSlots=mesh.subMeshCount,
        importerRemaps=((ModelImporter)AssetImporter.GetAtPath(path)).GetExternalObjectMap().Select(k=>k.Key.name+":"+Id(k.Value)).OrderBy(x=>x).ToArray(),
        rendererMaterials=prefab.GetComponentsInChildren<Renderer>(true).Select(r=>r.name+":"+string.Join(";",r.sharedMaterials.Select(Id))).OrderBy(x=>x).ToArray(),
        dependencies=AssetDatabase.GetDependencies(GaleModelBuilder.PrefabPath(level)).OrderBy(x=>x).ToArray()};
}).ToArray();
var files=Directory.GetFiles(GaleModelBuilder.PrefabFolder,"*",SearchOption.AllDirectories)
    .Concat(Directory.GetFiles(GaleModelBuilder.ModelFolder,"*.meta"))
    .Concat(Directory.GetFiles(FlameModelBuilder.PrefabFolder,"*",SearchOption.TopDirectoryOnly))
    .Concat(Directory.GetFiles(TideModelBuilder.PrefabFolder,"*",SearchOption.AllDirectories))
    .Concat(new[]{"Assets/_Game/Prefabs/TestTower.prefab","Assets/_Game/Prefabs/TestTower.prefab.meta","Assets/_Game/Prefabs/Shade.prefab","Assets/_Game/Prefabs/Shade.prefab.meta","Assets/_Game/Scenes/Battle.unity","Assets/_Game/Scenes/Battle.unity.meta"})
    .Distinct().OrderBy(p=>p).Select(p=>new{path=p.Replace('\\','/'),sha256=Hash(File.ReadAllBytes(p))}).ToArray();
return new{models,files};
'''


def snapshot():
    run = subprocess.run(['unity', 'command', 'script eval', '--caller', 'plugin', '--skill', 'unity-cli',
                          '--project-path', str(ROOT), '--format', 'json', '--', '--code', CODE],
                         cwd=ROOT, capture_output=True, text=True, check=True)
    payload = json.loads(run.stdout)
    if payload.get('success') is not True or not payload.get('result'):
        raise RuntimeError(run.stdout)
    return payload['result']


def main():
    before = snapshot()
    sources = [FOLDER / f'Tower_GaleTalismanL{i}.blend' for i in (1, 2, 3)]
    for source in sources:
        log = ROOT / 'TestResults' / ('art3c-reexport-' + source.stem + '.txt')
        with log.open('w') as out:
            subprocess.run(['/Applications/Blender.app/Contents/MacOS/Blender', '--background', str(source),
                            '--python-exit-code', '1', '--python', str(FOLDER / 'export_gale.py')],
                           cwd=ROOT, stdout=out, stderr=subprocess.STDOUT, check=True)
    after = snapshot()
    if before != after:
        raise RuntimeError('Roundtrip changed geometry/UV/normals/materials/GUID/localIDs/prefab-scene references')
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for name, data in [('reexport-before.json', before), ('reexport-after.json', after)]:
        (OUTPUT / name).write_text(json.dumps(data, indent=2) + '\n')
    print('PASS: three GALE sources reexported; imported geometry/UV/normals/submeshes/materials/GUID/localIDs/references identical')


if __name__ == '__main__':
    main()
