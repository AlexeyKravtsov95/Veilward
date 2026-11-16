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
            var manifest = JsonUtility.FromJson<ImportManifest>(System.IO.File.ReadAllText("Docs/review/ART.1/imported-models.json"));
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

        private static string Id(UnityEngine.Object obj)
        {
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj,out string guid,out long local), Is.True);
            return obj.GetType().Name+":"+obj.name+":"+guid+":"+local;
        }
    }
}
