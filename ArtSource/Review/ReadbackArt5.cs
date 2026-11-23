// Read-only serialized mesh/animation/IDs/remaps and real sampled poses.
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using Veilward.Editor;
public static class ReadbackArt5
{
    const string Output="Docs/review/ART.5/reports/";
    public static object Data()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Edit Mode required");
        string Hash(byte[] b){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(b)).Replace("-","").ToLowerInvariant();}
        string Id(UnityEngine.Object o){if(o==null)return "null";AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o,out string g,out long n);return o.GetType().Name+":"+o.name+":"+g+":"+n;}
        string V(Vector3 v)=>v.ToString("R");
        var models=new[]{"Enemy_Shade","Enemy_RiftWarden"}.Select(name=>{
            var path=EnemyModelBuilder.Folder+name+".fbx";
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
            var assets=AssetDatabase.LoadAllAssetsAtPath(path);
            var meshes=assets.OfType<Mesh>().OrderBy(m=>m.name).Select(mesh=>new{mesh.name,vertices=mesh.vertexCount,triangles=mesh.triangles.Length/3,slots=mesh.subMeshCount,
                geometry=Hash(Encoding.UTF8.GetBytes(string.Join(";",mesh.vertices.Select(V))+"|normals|"+string.Join(";",mesh.normals.Select(V))+"|uv|"+string.Join(";",mesh.uv.Select(v=>v.ToString("R")))+"|indices|"+string.Join(";",Enumerable.Range(0,mesh.subMeshCount).Select(i=>string.Join(",",mesh.GetTriangles(i)))))),bounds=new[]{V(mesh.bounds.min),V(mesh.bounds.max)}}).ToArray();
            var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            object[] clips;
            try
            {
                foreach(var a in go.GetComponentsInChildren<Animation>())a.enabled=false;
                var transforms=go.GetComponentsInChildren<Transform>();
                clips=assets.OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).OrderBy(c=>c.name).Select(clip=>{
                    var bindings=AnimationUtility.GetCurveBindings(clip);
                    var curveData=bindings.Select(b=>b.path+"|"+b.propertyName+"|"+string.Join(";",AnimationUtility.GetEditorCurve(clip,b).keys.Select(k=>string.Join(",",k.time.ToString("R"),k.value.ToString("R"),k.inTangent.ToString("R"),k.outTangent.ToString("R"))))).OrderBy(x=>x).ToArray();
                    var poses=new[]{0f,.25f,.5f,1f}.Select(f=>{
                        clip.SampleAnimation(go,clip.length*f);
                        return new{fraction=f,parts=transforms.Select(t=>new{t.name,position=V(t.localPosition),rotation=t.localRotation.ToString("R"),scale=V(t.localScale)}).ToArray()};
                    }).ToArray();
                    return (object)new{clip.name,clip.length,clip.frameRate,clip.legacy,curves=bindings.Length,keyframes=bindings.Sum(b=>AnimationUtility.GetEditorCurve(clip,b).length),curveSha256=Hash(Encoding.UTF8.GetBytes(string.Join("\n",curveData))),poses};
                }).ToArray();
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
            var prefab=name=="Enemy_Shade"?EnemyModelBuilder.ShadePath:EnemyModelBuilder.BossPath;
            return new{name,ids=assets.Select(Id).OrderBy(x=>x).ToArray(),meshes,clips,
                remaps=((ModelImporter)AssetImporter.GetAtPath(path)).GetExternalObjectMap().Select(k=>k.Key.name+":"+Id(k.Value)).OrderBy(x=>x).ToArray(),
                rendererMaterials=AssetDatabase.LoadAssetAtPath<GameObject>(prefab).GetComponentsInChildren<Renderer>(true).Select(r=>r.name+":"+string.Join(";",r.sharedMaterials.Select(Id))).OrderBy(x=>x).ToArray(),dependencies=AssetDatabase.GetDependencies(prefab).OrderBy(x=>x).ToArray()};
        }).ToArray();
        var files=new[]{EnemyModelBuilder.ShadePath,EnemyModelBuilder.BossPath,"Assets/_Game/Scenes/Battle.unity","Assets/_Game/Prefabs/TestTower.prefab"}
            .Concat(Directory.GetFiles(EnemyModelBuilder.Folder,"*.meta")).SelectMany(p=>p.EndsWith(".meta")?new[]{p}:new[]{p,p+".meta"}).Distinct().OrderBy(p=>p)
            .Select(p=>new{path=p,sha256=Hash(File.ReadAllBytes(p))}).ToArray();
        return new{models,files};
    }
    static string Save(string stage)
    {Directory.CreateDirectory(Output);var text=Newtonsoft.Json.JsonConvert.SerializeObject(Data(),Newtonsoft.Json.Formatting.Indented)+"\n";File.WriteAllText(Output+"reexport-"+stage+".json",text);return "ART5 "+stage+" real mesh/animation sampled readback";}
    public static string Before()=>Save("before");
    public static string After()=>Save("after");
    public static string Canonical()=>Save("canonical-readback");
}
