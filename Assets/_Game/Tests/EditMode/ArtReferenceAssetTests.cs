using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Veilward.Editor;
using Veilward.Presentation;

namespace Veilward.Tests.EditMode
{
    public sealed class ArtReferenceAssetTests
    {
        [TestCaseSource(typeof(ArtReferenceBuilder), nameof(ArtReferenceBuilder.ModelNames))]
        public void ImportedModel_RespectsMobileBudgetAndStableReferences(string name)
        {
            var path = ArtReferenceBuilder.ModelFolder + name + ".fbx";
            var prefabPath = ArtReferenceBuilder.PrefabFolder + "P_" + name + ".prefab";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(model, Is.Not.Null); Assert.That(prefab, Is.Not.Null);
            var meshes = model.GetComponentsInChildren<MeshFilter>();
            Assert.That(meshes.Length, Is.EqualTo(1));
            var mesh = meshes[0].sharedMesh;
            var triangles = 0L;
            for (var i=0; i<mesh.subMeshCount; i++) triangles += (long)mesh.GetIndexCount(i)/3;
            Assert.That(triangles, Is.InRange(1,5000), name);
            var expectedSlots = name == "Tower_FlameLantern" || name == "Enemy_Shade" ? 2 : 1;
            Assert.That(mesh.subMeshCount, Is.EqualTo(expectedSlots), "Surface/Glow assignments must survive export.");
            Assert.That(model.GetComponentInChildren<MeshRenderer>().sharedMaterials.Any(m=>m.name == "MAT_Art1_Glow"),
                Is.EqualTo(expectedSlots == 2));
            Assert.That(model.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(model.transform.rotation, Is.EqualTo(Quaternion.identity));
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.globalScale, Is.EqualTo(1));
            Assert.That(importer.importAnimation, Is.False);
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>())
                foreach (var mat in renderer.sharedMaterials)
                {
                    Assert.That(mat, Is.Not.Null);
                    Assert.That(mat.shader.name, Does.StartWith("Universal Render Pipeline/"));
                    Assert.That(mat.shader.isSupported, Is.True);
                    if (mat.mainTexture != null)
                    {
                        Assert.That(mat.mainTexture.width, Is.LessThanOrEqualTo(1024));
                        Assert.That(mat.mainTexture.height, Is.LessThanOrEqualTo(1024));
                    }
                }
            var before = AssetDatabase.LoadAllAssetsAtPath(path).Select(Id).OrderBy(x=>x).ToArray();
            var deps = AssetDatabase.GetDependencies(prefabPath).OrderBy(x=>x).ToArray();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(path).Select(Id).OrderBy(x=>x).ToArray(), Is.EqualTo(before));
            Assert.That(AssetDatabase.GetDependencies(prefabPath).OrderBy(x=>x).ToArray(), Is.EqualTo(deps));
            Assert.That(prefab.GetComponentsInChildren<MeshFilter>().All(f=>f.sharedMesh != null), Is.True);
        }

        [Serializable] private sealed class ImportManifest { public ImportEntry[] models; }
        [Serializable] private sealed class ImportEntry { public string name; public int triangles; public int materialSlots; }

        [Test]
        public void Manifest_ReportsActualImportedCounts()
        {
            var manifest = JsonUtility.FromJson<ImportManifest>(System.IO.File.ReadAllText("Docs/review/ART.1.1/imported-models.json"));
            Assert.That(manifest.models.Select(m=>m.name), Is.EquivalentTo(ArtReferenceBuilder.ModelNames));
            foreach (var entry in manifest.models)
            {
                var mesh = AssetDatabase.LoadAssetAtPath<GameObject>(ArtReferenceBuilder.ModelFolder+entry.name+".fbx")
                    .GetComponentInChildren<MeshFilter>().sharedMesh;
                var count=0L;
                for (var i=0;i<mesh.subMeshCount;i++) count+=(long)mesh.GetIndexCount(i)/3;
                Assert.That(count, Is.EqualTo(entry.triangles), entry.name);
                Assert.That(mesh.subMeshCount, Is.EqualTo(entry.materialSlots), entry.name);
            }
        }

        [Test]
        public void UnitsPivotsAndShadeFace_AreCorrectInUnitySpace()
        {
            var tower = AssetDatabase.LoadAssetAtPath<GameObject>(ArtReferenceBuilder.ModelFolder+"Tower_FlameLantern.fbx")
                .GetComponentInChildren<MeshFilter>().sharedMesh;
            Assert.That(tower.bounds.min.y, Is.EqualTo(0).Within(.002));
            Assert.That(tower.bounds.size.y, Is.InRange(2.2f,2.4f));
            var shade = AssetDatabase.LoadAssetAtPath<GameObject>(ArtReferenceBuilder.ModelFolder+"Enemy_Shade.fbx")
                .GetComponentInChildren<MeshFilter>().sharedMesh;
            Assert.That(shade.bounds.min.y, Is.EqualTo(0).Within(.002));
            Assert.That(shade.bounds.size.y, Is.InRange(1.3f,1.4f));
            // Маска занимает palette swatch 22; проверяем реальный импорт, а не экспортный флаг.
            var uv = shade.uv; var vertices = shade.vertices; var face = new System.Collections.Generic.List<float>();
            for (var i=0;i<uv.Length;i++)
                if (Mathf.Abs(uv[i].x-6.5f/8)<.001f && Mathf.Abs(uv[i].y-2.5f/4)<.001f) face.Add(vertices[i].z);
            Assert.That(face, Is.Not.Empty);
            Assert.That(face.Average(), Is.GreaterThan(.17f), "Shade face must point +Z in Unity.");
        }

        [TestCase(17, -1)]
        [TestCase(17, 1)]
        [TestCase(18, -1)]
        [TestCase(18, 1)]
        public void ShadeShouldersAndHorns_HaveOutwardTriangleWinding(int swatch, int side)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<GameObject>(ArtReferenceBuilder.ModelFolder+"Enemy_Shade.fbx")
                .GetComponentInChildren<MeshFilter>().sharedMesh;
            var vertices=mesh.vertices; var uv=mesh.uv; var indices=mesh.triangles;
            var triangles=new System.Collections.Generic.List<int>();
            var points=new System.Collections.Generic.HashSet<Vector3>();
            for(var i=0;i<indices.Length;i+=3)
            {
                var a=indices[i]; var b=indices[i+1]; var c=indices[i+2];
                bool Matches(int index) => Mathf.Abs(uv[index].x-(swatch%8+.5f)/8)<.001f &&
                    Mathf.Abs(uv[index].y-(swatch/8+.5f)/4)<.001f && vertices[index].x*side>0 &&
                    (swatch==17 || vertices[index].y>.98f);
                if(!Matches(a)||!Matches(b)||!Matches(c)) continue;
                triangles.Add(a);triangles.Add(b);triangles.Add(c);
                points.Add(vertices[a]);points.Add(vertices[b]);points.Add(vertices[c]);
            }
            Assert.That(points.Count, Is.EqualTo(5), "Проверяем полный hull, а не пустой набор.");
            Assert.That(triangles.Count/3, Is.EqualTo(6));
            var center=Vector3.zero;
            foreach(var p in points) center+=p;
            center/=points.Count;
            var volume=0f;
            for(var i=0;i<triangles.Count;i+=3)
            {
                var a=vertices[triangles[i]];var b=vertices[triangles[i+1]];var c=vertices[triangles[i+2]];
                var normal=Vector3.Cross(b-a,c-a);
                Assert.That(Vector3.Dot(normal,(a+b+c)/3-center), Is.GreaterThan(1e-7f),
                    $"Shade swatch {swatch}, side {side}, triangle {i/3}: inward winding");
                volume+=Vector3.Dot(a-center,Vector3.Cross(b-center,c-center))/6;
            }
            Assert.That(volume, Is.GreaterThan(1e-6f));
        }

        [Test]
        public void ArtScene_UsesSavedBattleCameraAndScenarioWithoutReleaseRegistration()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var art = EditorSceneManager.OpenScene(ArtReferenceBuilder.ScenePath, OpenSceneMode.Single);
            var battle = EditorSceneManager.OpenScene("Assets/_Game/Scenes/Battle.unity", OpenSceneMode.Additive);
            try
            {
                var a = art.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>()).Single();
                var b = battle.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>()).Single();
                Assert.That(a.transform.position, Is.EqualTo(b.transform.position));
                Assert.That(Quaternion.Angle(a.transform.rotation,b.transform.rotation), Is.LessThan(.001));
                Assert.That(a.orthographic, Is.EqualTo(b.orthographic));
                Assert.That(a.orthographicSize, Is.EqualTo(b.orthographicSize));
                var showcase = art.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ArtReferenceShowcase>()).Single();
                var so = new SerializedObject(showcase);
                foreach (var field in new[] { "scenario","towerPrefab","enemyPrefab","strikeMaterial","strikeCoreMaterial","worldCamera","environmentRoot" })
                    Assert.That(so.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
                Assert.That(AssetDatabase.GetAssetPath(so.FindProperty("scenario").objectReferenceValue),
                    Is.EqualTo("Assets/_Game/Configs/TestWave.asset"));
                foreach (var go in art.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)))
                    Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go.gameObject), Is.Zero, go.name);
                Assert.That(EditorBuildSettings.scenes.Any(s=>s.path == ArtReferenceBuilder.ScenePath), Is.False);
            }
            finally
            {
                if (setup.Any(s=>s.isLoaded && s.isActive && !string.IsNullOrEmpty(s.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static Mesh ModelMesh(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(ArtReferenceBuilder.ModelFolder+name+".fbx")
            .GetComponentInChildren<MeshFilter>().sharedMesh;

        [Test]
        public void Art11Flame_ReducedRoofRetainsFullSizeBaseAndFire()
        {
            var mesh=ModelMesh("Tower_FlameLantern");var v=mesh.vertices;var uv=mesh.uv;
            var roof=v.Where(p=>p.y>=1.49f-.00001f && p.y<1.96f).ToArray();
            Assert.That(roof.Length, Is.GreaterThan(100));
            Assert.That(roof.Max(p=>Mathf.Abs(p.x)), Is.LessThan(.405f));
            Assert.That(roof.Max(p=>Mathf.Abs(p.z)), Is.LessThan(.365f));
            Assert.That(mesh.bounds.size.x, Is.EqualTo(.94f).Within(.002));
            Assert.That(mesh.bounds.size.z, Is.EqualTo(.94f).Within(.002));
            var fire=Enumerable.Range(0,v.Length).Where(i=>uv[i].y>.25f && uv[i].y<.5f && v[i].y<1.46f && v[i].y>.44f).Select(i=>v[i]).ToArray();
            Assert.That(fire.Length,Is.GreaterThan(20));
            Assert.That(fire.Max(p=>p.y),Is.EqualTo(1.45f).Within(.002));
            Assert.That(fire.Max(p=>p.x)-fire.Min(p=>p.x),Is.GreaterThan(.4f));
        }

        [Test]
        public void Art11Shade_ColdBroadHemHasOutwardWindingAndCulling()
        {
            var mesh=ModelMesh("Enemy_Shade");var v=mesh.vertices;var uv=mesh.uv;var indices=mesh.triangles;
            var hem=Enumerable.Range(0,v.Length).Where(i=>v[i].y<.13f).ToArray();
            Assert.That(hem.Max(i=>Mathf.Abs(v[i].x)),Is.GreaterThan(.245f));
            var cold=0;var volume=0f;var mantleTriangles=0;
            for(var i=0;i<indices.Length;i+=3)
            {
                var a=indices[i];var b=indices[i+1];var c=indices[i+2];
                bool Body(int j)
                {
                    var color=Mathf.FloorToInt(uv[j].x*8)+8*Mathf.FloorToInt(uv[j].y*4);
                    if(color!=16 && color!=18 && color!=19)return false;
                    var p=v[j];var radial=p.x*p.x+p.z*p.z/.64f;
                    bool Ring(float height,float radius)=>Mathf.Abs(p.y-height)<.0001f && Mathf.Abs(radial-radius*radius)<.0001f;
                    return Ring(.04f,0)||Ring(0,.2646f)||Ring(.12f,.2646f)||Ring(.23f,.34f)||Ring(.65f,.29f)||Ring(.91f,.23f)||Ring(1.04f,.16f);
                }
                if(!Body(a)||!Body(b)||!Body(c))continue;
                var x=v[a];var y=v[b];var z=v[c];
                Assert.That(Vector3.Dot(Vector3.Cross(y-x,z-x),(x+y+z)/3-new Vector3(0,.53f,0)),Is.GreaterThan(1e-8f), $"Inward mantle {i/3}: {x}, {y}, {z}; UV {uv[a]}");
                volume+=Vector3.Dot(x,Vector3.Cross(y,z))/6;mantleTriangles++;
                if(Mathf.Max(x.y,Mathf.Max(y.y,z.y))<.26f) {Assert.That(uv[a].y,Is.GreaterThan(.5f));cold++;}
            }
            Assert.That(mantleTriangles,Is.EqualTo(98),"Проверяется весь экспортированный mantle, включая крышки.");
            Assert.That(cold,Is.GreaterThanOrEqualTo(20));Assert.That(volume,Is.GreaterThan(.05f));
            foreach(var r in ArtReferenceBuilder.Prefab("Enemy_Shade").GetComponentsInChildren<MeshRenderer>())
                foreach(var m in r.sharedMaterials)if(m.HasProperty("_Cull"))Assert.That(m.GetFloat("_Cull"),Is.EqualTo(2));
        }

        [Test]
        public void Art11Island_DeepSeparateMassesAndSupportUnderUnchangedRoute()
        {
            var mesh=ModelMesh("Environment_FloatingIsland");var v=mesh.vertices;var triangles=mesh.triangles;
            Assert.That(mesh.bounds.min.y,Is.LessThan(-4.2f));
            // Weld по positions: Unity разделяет vertices по UV/flat normals, поэтому vertex count не является числом масс.
            var edges=new System.Collections.Generic.Dictionary<Vector3,System.Collections.Generic.HashSet<Vector3>>();
            void Link(Vector3 a,Vector3 b){if(!edges.ContainsKey(a))edges[a]=new System.Collections.Generic.HashSet<Vector3>();edges[a].Add(b);}
            for(int i=0;i<triangles.Length;i+=3)for(int k=0;k<3;k++)
            {var a=v[triangles[i+k]];var b=v[triangles[i+(k+1)%3]];Link(a,b);Link(b,a);}
            var unseen=new System.Collections.Generic.HashSet<Vector3>(edges.Keys);var masses=0;
            while(unseen.Count>0)
            {
                var stack=new System.Collections.Generic.Stack<Vector3>();var first=unseen.First();unseen.Remove(first);stack.Push(first);
                var min=first.y;var max=first.y;
                while(stack.Count>0){var a=stack.Pop();min=Mathf.Min(min,a.y);max=Mathf.Max(max,a.y);foreach(var b in edges[a])if(unseen.Remove(b))stack.Push(b);}
                if(min<-2 && max-min>1.5f)masses++;
            }
            Assert.That(masses,Is.GreaterThanOrEqualTo(3));
            var path=AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/TestWave.asset").CreateSimulation().Path;
            for(float d=0;d<=path.TotalLength;d+=.25f)
            {
                var p=path.GetPosition(d);var point=new Vector2(p.X,p.Y);bool supported=false;
                for(int i=0;i<triangles.Length;i+=3)
                {
                    var a=v[triangles[i]];var b=v[triangles[i+1]];var c=v[triangles[i+2]];
                    if(a.y<-.12f||b.y<-.12f||c.y<-.12f)continue;
                    float Cross(Vector2 x,Vector2 y)=>x.x*y.y-x.y*y.x;
                    var x=new Vector2(a.x,a.z);var y=new Vector2(b.x,b.z);var z=new Vector2(c.x,c.z);
                    if(Mathf.Abs(Cross(y-x,z-x))<1e-6f)continue;
                    var u=Cross(y-x,point-x);var w=Cross(z-y,point-y);var t=Cross(x-z,point-z);
                    if((u>=-1e-5f&&w>=-1e-5f&&t>=-1e-5f)||(u<=1e-5f&&w<=1e-5f&&t<=1e-5f)){supported=true;break;}
                }
                Assert.That(supported,Is.True,$"Route unsupported at {d}: {point}");
            }
        }

        private static string Id(UnityEngine.Object obj)
        {
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj,out string guid,out long local), Is.True);
            return obj.GetType().Name+":"+obj.name+":"+guid+":"+local;
        }
    }
}
