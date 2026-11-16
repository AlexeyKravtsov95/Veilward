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
            Assert.That(mesh.subMeshCount, Is.InRange(1,2));
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
