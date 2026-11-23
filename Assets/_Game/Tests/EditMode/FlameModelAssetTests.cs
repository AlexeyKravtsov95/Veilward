using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Veilward.Editor;
using Veilward.Domain;
using Veilward.Presentation;

namespace Veilward.Tests.EditMode
{
    public sealed class FlameModelAssetTests
    {
        [TestCase(2)] [TestCase(3)]
        public void Art3A_R2_OpenBrazierFrameExpandsAcrossGameplayCamera(int level)
        {
            float MiddleWidth(int index)
            {
                var mesh = AssetDatabase.LoadAssetAtPath<GameObject>(FlameModelBuilder.ModelFolder + "Tower_FlameLanternL" + index + ".fbx")
                    .GetComponentInChildren<MeshFilter>().sharedMesh;
                var projected = mesh.vertices.Where(p => p.y >= .8f && p.y <= 1.65f)
                    .Select(p => (p.x - p.z) / Mathf.Sqrt(2)).ToArray();
                return projected.Max() - projected.Min();
            }
            // Не заменяет visual acceptance: ловит раму, случайно развёрнутую вдоль глубины камеры.
            Assert.That(MiddleWidth(level), Is.GreaterThanOrEqualTo(MiddleWidth(1) * 1.3f));
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void Art3A01_ModelImportPreservesBudgetsGroundAndPalette(int level)
        {
            var path = FlameModelBuilder.ModelFolder + "Tower_FlameLanternL" + level + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.globalScale, Is.EqualTo(1));
            Assert.That(importer.bakeAxisConversion, Is.True);
            Assert.That(importer.isReadable || importer.importAnimation || importer.importLights || importer.addCollider, Is.False);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var mesh = model.GetComponentInChildren<MeshFilter>().sharedMesh;
            Assert.That(mesh.triangles.Length / 3, Is.InRange(1, 5000));
            Assert.That(mesh.subMeshCount, Is.EqualTo(2));
            Assert.That(mesh.bounds.min.y, Is.EqualTo(0).Within(.00001));
            Assert.That(mesh.bounds.size.x, Is.LessThanOrEqualTo(.941));
            Assert.That(mesh.bounds.size.z, Is.LessThanOrEqualTo(.941));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FlameModelBuilder.PrefabPath(level));
            Assert.That(prefab.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(prefab.GetComponentsInChildren<Collider>(), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Light>(), Is.Empty);
            Assert.That(prefab.transform.Find("ShotSource").localPosition, Is.EqualTo(new Vector3(0, 1.15f, 0)));
            var materials = prefab.GetComponentInChildren<MeshRenderer>().sharedMaterials;
            Assert.That(materials.Select(AssetDatabase.GetAssetPath), Is.EquivalentTo(new[] {
                ArtReferenceBuilder.MaterialFolder + "MAT_Art1_Surface.mat",
                ArtReferenceBuilder.MaterialFolder + "MAT_Art1_Glow.mat" }));
        }

        [TestCase(.5f)] [TestCase(1f)]
        public void Art3A02_BindingReplacesOnlyModelAndDoesNotTintPalette(float cellSize)
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/TestTower.prefab"));
            try
            {
                root.transform.position = new Vector3(2, 0, -1); root.transform.localScale = Vector3.one * cellSize;
                var config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/TestWave.asset");
                var flame = config.CreateSimulation().TowerCatalog.Get(new TowerDefinitionId("flame"));
                var binding = root.GetComponent<FlameModelBinding>();
                var colliders = root.GetComponentsInChildren<Collider>();
                for (var level = 1; level <= 3; level++)
                {
                    TowerDisplay.ApplyVisual(root.transform, flame.Levels[level - 1], level);
                    var model = binding.Model;
                    TowerDisplay.ApplyVisual(root.transform, flame.Levels[level - 1], level);
                    Assert.That(binding.Model, Is.SameAs(model), "same-level refresh is allocation free");
                    Assert.That(binding.Level, Is.EqualTo(level));
                    Assert.That(model.localPosition, Is.EqualTo(Vector3.zero));
                    Assert.That(model.localScale, Is.EqualTo(Vector3.one));
                    Assert.That(root.transform.position, Is.EqualTo(new Vector3(2, 0, -1)));
                    Assert.That(root.transform.localScale, Is.EqualTo(Vector3.one * cellSize));
                    CollectionAssert.AreEqual(colliders, root.GetComponentsInChildren<Collider>());
                    foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                    {
                        Assert.That(renderer.enabled, Is.True);
                        Assert.That(renderer.HasPropertyBlock(), Is.False, "production palette must remain authored");
                    }
                    Assert.That(root.transform.Find("Foundation").GetComponent<Renderer>().enabled, Is.False);
                    Assert.That(root.GetComponentsInChildren<MeshFilter>().Length, Is.EqualTo(4), "two fallback meshes, model and stencil pass");
                }
                Assert.That(binding.Apply(new TowerDefinitionId("flame"), 4), Is.False);
                Assert.That(binding.Model, Is.Null);
                Assert.That(root.transform.Find("Foundation").GetComponent<Renderer>().enabled, Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase("test-tower")]
        public void Art3A04_OtherTypesKeepGrayboxAndTint(string id)
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/TestTower.prefab"));
            try
            {
                var simulation = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/TestWave.asset").CreateSimulation();
                var catalog = simulation.TowerCatalog;
                TowerDisplay.ApplyVisual(root.transform, catalog.Get(new TowerDefinitionId("flame")).Levels[0], 1);
                TowerDisplay.ApplyVisual(root.transform, catalog.Get(new TowerDefinitionId(id)).Levels[0], 1);
                Assert.That(root.GetComponent<FlameModelBinding>().Model, Is.Null);
                var renderers = root.GetComponentsInChildren<Renderer>();
                Assert.That(renderers.Length, Is.EqualTo(2));
                Assert.That(renderers.All(r => r.enabled && r.HasPropertyBlock()), Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Art3A05_OcclusionPassSharesMeshesAndTracksActualHealthFill()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Shade.prefab"));
            try
            {
                FlameModelBuilder.BindEnemyOcclusion(root);
                FlameModelBuilder.BindEnemyOcclusion(root);
                Assert.That(root.GetComponentsInChildren<MeshFilter>().Length, Is.EqualTo(6), "three source meshes plus idempotent local cues");
                var health = root.GetComponent<EnemyHealthBarView>();
                var cameraRoot = new GameObject("test camera");
                try
                {
                    health.SetNormalizedHealth(.6f); health.Face(cameraRoot.AddComponent<Camera>());
                    var fill = root.transform.Find("Health Bar/Health Fill");
                    var cue = fill.Find("HP fill occlusion cue");
                    Assert.That(fill.localScale.x, Is.EqualTo(.54f).Within(.00001f));
                    Assert.That(cue.localPosition, Is.EqualTo(Vector3.zero));
                    Assert.That(cue.localScale, Is.EqualTo(Vector3.one));
                    Assert.That(cue.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(fill.GetComponent<MeshFilter>().sharedMesh));
                    Assert.That(Vector4.Distance(cue.GetComponent<Renderer>().sharedMaterial.GetColor("_Tint"),
                        fill.GetComponent<Renderer>().sharedMaterial.GetColor("_BaseColor")), Is.LessThan(.000001f));
                }
                finally { Object.DestroyImmediate(cameraRoot); }
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
