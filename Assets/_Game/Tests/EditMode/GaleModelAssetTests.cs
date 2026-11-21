using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Object = UnityEngine.Object;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Veilward.Domain;
using Veilward.Editor;
using Veilward.Presentation;

namespace Veilward.Tests.EditMode
{
    public sealed class GaleModelAssetTests
    {
        private static GameObject Tower() => Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/TestTower.prefab"));

        [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void Art3C01_ImportedOriginalGeometryHasGroundFootprintPaletteUvAndNormals(int level)
        {
            var path = GaleModelBuilder.ModelFolder + "Tower_GaleTalismanL" + level + ".fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.globalScale, Is.EqualTo(1)); Assert.That(importer.useFileScale, Is.True);
            Assert.That(importer.bakeAxisConversion, Is.True);
            Assert.That(importer.isReadable || importer.importAnimation || importer.importCameras || importer.importLights || importer.addCollider, Is.False);
            Assert.That(importer.importNormals, Is.EqualTo(ModelImporterNormals.Import));
            Assert.That(importer.importTangents, Is.EqualTo(ModelImporterTangents.None));
            var imported = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(imported.GetComponentsInChildren<MeshFilter>().Length, Is.EqualTo(1));
            var mesh = imported.GetComponentInChildren<MeshFilter>().sharedMesh;
            Assert.That(mesh.triangles.Length / 3, Is.InRange(1, 5000));
            Assert.That(mesh.subMeshCount, Is.EqualTo(2));
            Assert.That(mesh.bounds.min.y, Is.EqualTo(0).Within(.00001));
            Assert.That(mesh.bounds.size.x, Is.LessThanOrEqualTo(.94f));
            Assert.That(mesh.bounds.size.z, Is.LessThanOrEqualTo(.94f));
            Assert.That(mesh.bounds.max.y, Is.GreaterThan(level == 3 ? 2.35f : level == 2 ? 1.63f : 1.60f));
            Assert.That(mesh.normals.Length, Is.EqualTo(mesh.vertexCount));
            Assert.That(mesh.normals.All(n => Mathf.Abs(n.magnitude - 1) < .0001), Is.True);
            Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount));
            Assert.That(mesh.uv.All(uv => uv.x > 0 && uv.x < 1 && uv.y > 0 && uv.y < 1), Is.True);
            Assert.That(mesh.uv.Distinct().Count(), Is.GreaterThanOrEqualTo(6));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GaleModelBuilder.PrefabPath(level));
            Assert.That(prefab.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(prefab.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(prefab.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(prefab.GetComponentsInChildren<Collider>(), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Light>(), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Animator>(), Is.Empty);
            Assert.That(prefab.transform.Find("ShotSource").localPosition, Is.EqualTo(new Vector3(0, 1.07f, 0)));
            var filters = prefab.GetComponentsInChildren<MeshFilter>();
            Assert.That(filters.Length, Is.EqualTo(2));
            Assert.That(filters[0].sharedMesh, Is.SameAs(mesh)); Assert.That(filters[1].sharedMesh, Is.SameAs(mesh));
            var renderers = prefab.GetComponentsInChildren<MeshRenderer>();
            Assert.That(renderers.Length, Is.EqualTo(2));
            CollectionAssert.AreEqual(new[] { ArtReferenceBuilder.MaterialFolder + "MAT_Art1_Surface.mat",
                ArtReferenceBuilder.MaterialFolder + "MAT_Art1_Glow.mat" }, renderers[0].sharedMaterials.Select(AssetDatabase.GetAssetPath));
            Assert.That(renderers[1].sharedMaterials.All(m => AssetDatabase.GetAssetPath(m) == ArtReferenceBuilder.MaterialFolder + "MAT_Art21_TowerMask.mat"), Is.True);
        }

        [Test]
        public void Art3C03_BuildersPreserveThreeFamiliesFallbackAndStableAssetReferences()
        {
            string Hash(string path) => Convert.ToBase64String(SHA256.Create().ComputeHash(File.ReadAllBytes(path)));
            var protectedPaths = Enumerable.Range(1, 3).SelectMany(level => new[] {
                FlameModelBuilder.ModelFolder + "Tower_FlameLanternL" + level + ".fbx",
                FlameModelBuilder.ModelFolder + "Tower_FlameLanternL" + level + ".fbx.meta",
                FlameModelBuilder.PrefabPath(level), FlameModelBuilder.PrefabPath(level) + ".meta",
                TideModelBuilder.ModelFolder + "Tower_TideBellL" + level + ".fbx",
                TideModelBuilder.ModelFolder + "Tower_TideBellL" + level + ".fbx.meta",
                TideModelBuilder.PrefabPath(level), TideModelBuilder.PrefabPath(level) + ".meta" }).ToArray();
            var hashes = protectedPaths.Select(Hash).ToArray();
            string[] References() => Enumerable.Range(1, 3).SelectMany(level => new[] {
                GaleModelBuilder.ModelFolder + "Tower_GaleTalismanL" + level + ".fbx",
                GaleModelBuilder.PrefabPath(level) }).SelectMany(path => AssetDatabase.LoadAllAssetsAtPath(path))
                .Select(asset => { Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long id), Is.True);
                    return guid + ":" + id + ":" + asset.name; }).OrderBy(id => id).ToArray();
            var refs = References();
            var outputPaths = Enumerable.Range(1, 3).SelectMany(level => new[] {
                GaleModelBuilder.ModelFolder + "Tower_GaleTalismanL" + level + ".fbx",
                GaleModelBuilder.ModelFolder + "Tower_GaleTalismanL" + level + ".fbx.meta",
                GaleModelBuilder.PrefabPath(level), GaleModelBuilder.PrefabPath(level) + ".meta" })
                .Concat(new[] { "Assets/_Game/Prefabs/TestTower.prefab", "Assets/_Game/Prefabs/TestTower.prefab.meta" });
            var before = outputPaths.ToDictionary(path => path, File.ReadAllBytes);
            void AssertBindings(FlameModelBinding binding)
            {
                Assert.That(binding, Is.Not.Null);
                var serialized = new SerializedObject(binding);
                foreach (var field in new[] { "levels", "tideLevels", "galeLevels" })
                {
                    var array = serialized.FindProperty(field); Assert.That(array.arraySize, Is.EqualTo(3));
                    for (var i = 0; i < 3; i++)
                    {
                        var expectedPath = field == "levels" ? FlameModelBuilder.PrefabPath(i + 1) : field == "tideLevels" ? TideModelBuilder.PrefabPath(i + 1) : GaleModelBuilder.PrefabPath(i + 1);
                        var expectedRoot = AssetDatabase.LoadAssetAtPath<GameObject>(expectedPath).transform;
                        Assert.That(array.GetArrayElementAtIndex(i).objectReferenceValue, Is.SameAs(expectedRoot));
                    }
                }
                var fallback = serialized.FindProperty("fallbackRenderers");
                Assert.That(fallback.arraySize, Is.EqualTo(2));
                CollectionAssert.AreEquivalent(new[] {
                    binding.transform.Find("Foundation").GetComponent<Renderer>(),
                    binding.transform.Find("Spirit").GetComponent<Renderer>() },
                    Enumerable.Range(0, fallback.arraySize).Select(i => fallback.GetArrayElementAtIndex(i).objectReferenceValue));
            }
            try
            {
                // Проверяем authored refs до Build: тест не должен сначала чинить повреждённый prefab.
                AssertBindings(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/TestTower.prefab")
                    .GetComponent<FlameModelBinding>());
                GaleModelBuilder.Build(); GaleModelBuilder.Build();
                CollectionAssert.AreEqual(refs, References());
                CollectionAssert.AreEqual(hashes, protectedPaths.Select(Hash));
                var root = Tower();
                try
                {
                    var binding = root.GetComponent<FlameModelBinding>();
                    AssertBindings(binding);
                    Assert.That(binding.Apply(new TowerDefinitionId("gale"), 1), Is.True);
                    GaleModelBuilder.Bind(root); FlameModelBuilder.Bind(root); TideModelBuilder.Bind(root); GaleModelBuilder.Bind(root);
                    AssertBindings(binding);
                    Assert.That(binding.Apply(new TowerDefinitionId("flame"), 1), Is.True);
                    Assert.That(binding.Apply(new TowerDefinitionId("gale"), 1), Is.True);
                    Assert.That(root.GetComponents<FlameModelBinding>().Length, Is.EqualTo(1));
                    Assert.That(root.transform.Find("Foundation").GetComponent<Renderer>().enabled, Is.False);
                }
                finally { Object.DestroyImmediate(root); }
            }
            finally
            {
                // Включает первый Build и failed assertions; remap .meta и authored prefab bytes возвращаются точно.
                foreach (var pair in before) File.WriteAllBytes(pair.Key, pair.Value);
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                foreach (var pair in before) CollectionAssert.AreEqual(pair.Value, File.ReadAllBytes(pair.Key), pair.Key);
            }
        }

        [Test]
        public void Art3C03_OriginalSerializedFlameOnlyShapeDeserializesWithoutGaleField()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var flame = AssetDatabase.LoadAssetAtPath<GameObject>(FlameModelBuilder.PrefabPath(1)).transform;
                var fallback = root.GetComponent<Renderer>();
                var binding = root.AddComponent<FlameModelBinding>();
                var legacy = "{\"levels\":[{\"instanceID\":" + flame.GetInstanceID() + "}],\"fallbackRenderers\":[{\"instanceID\":" + fallback.GetInstanceID() + "}]}";
                JsonUtility.FromJsonOverwrite(legacy, binding);
                Assert.That(binding.Apply(new TowerDefinitionId("flame"), 1), Is.True);
                Assert.That(fallback.enabled, Is.False);
                Assert.That(binding.Apply(new TowerDefinitionId("gale"), 1), Is.False);
                Assert.That(fallback.enabled, Is.True);
                Assert.That(binding.Apply(default, 1), Is.False);
                Assert.That(binding.Apply(new TowerDefinitionId("flame"), 1), Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(.5f)] [TestCase(1f)]
        public void Art3C02_AlternatingFamiliesCacheByTypeAndLevelAndPreserveFootprint(float cellSize)
        {
            var root = Tower();
            try
            {
                root.transform.position = new Vector3(2, 0, -1);
                root.transform.localScale = Vector3.one * cellSize;
                var authoredCollider = root.AddComponent<BoxCollider>();
                authoredCollider.center = new Vector3(.05f, .4f, -.05f);
                authoredCollider.size = new Vector3(.8f, .8f, .8f);
                var colliders = root.GetComponentsInChildren<Collider>();
                var catalog = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/TestWave.asset").CreateSimulation().TowerCatalog;
                var binding = root.GetComponent<FlameModelBinding>();
                var previous = (Transform)null;
                var types = new[] { "flame", "tide", "gale", "gale", "tide", "flame", "flame", "tide", "gale" };
                var levels = new[] { 1, 1, 1, 2, 2, 2, 3, 3, 3 };
                for (var i = 0; i < types.Length; i++)
                {
                    var definition = catalog.Get(new TowerDefinitionId(types[i])).Levels[levels[i] - 1];
                    TowerDisplay.ApplyVisual(root.transform, definition, levels[i]);
                    Assert.That(binding.Model, Is.Not.Null);
                    Assert.That(binding.Model, Is.Not.SameAs(previous), "same level of another family must replace the model");
                    Assert.That(binding.Model.name, Is.EqualTo(types[i].ToUpperInvariant() + " L" + levels[i]));
                    previous = binding.Model;
                    var renderers = previous.GetComponentsInChildren<Renderer>();
                    var materials = renderers.SelectMany(r => r.sharedMaterials).ToArray();
                    TowerDisplay.ApplyVisual(root.transform, definition, levels[i]);
                    Assert.That(binding.Model, Is.SameAs(previous));
                    CollectionAssert.AreEqual(materials, binding.Model.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials));
                    Assert.That(root.transform.Cast<Transform>().Count(t => t.gameObject.activeSelf && (t.name.StartsWith("FLAME L") || t.name.StartsWith("TIDE L") || t.name.StartsWith("GALE L"))), Is.EqualTo(1));
                    Assert.That(renderers.All(r => r.enabled && !r.HasPropertyBlock()), Is.True);
                    Assert.That(root.transform.Find("Foundation").GetComponent<Renderer>().enabled, Is.False);
                    Assert.That(root.transform.Find("Spirit").GetComponent<Renderer>().enabled, Is.False);
                    Assert.That(binding.ShotSource, Is.Not.Null);
                    Assert.That(previous.localPosition, Is.EqualTo(Vector3.zero));
                    Assert.That(previous.localScale, Is.EqualTo(Vector3.one));
                    Assert.That(root.transform.position, Is.EqualTo(new Vector3(2, 0, -1)));
                    Assert.That(root.transform.localScale, Is.EqualTo(Vector3.one * cellSize));
                    CollectionAssert.AreEqual(colliders, root.GetComponentsInChildren<Collider>());
                    Assert.That(authoredCollider.center, Is.EqualTo(new Vector3(.05f, .4f, -.05f)));
                    Assert.That(authoredCollider.size, Is.EqualTo(new Vector3(.8f, .8f, .8f)));
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase("unknown", 1)] [TestCase("test-tower", 1)]
        [TestCase("steam", 1)] [TestCase("storm", 1)]
        [TestCase("gale", 0)] [TestCase("tide", 0)] [TestCase("tide", 4)] [TestCase("gale", 4)] [TestCase("flame", 0)] [TestCase("flame", 4)]
        public void Art3C03_UnsupportedPairClearsModelAndReenablesOnlyFallback(string type, int level)
        {
            var root = Tower();
            try
            {
                var binding = root.GetComponent<FlameModelBinding>();
                Assert.That(binding.Apply(new TowerDefinitionId("gale"), 3), Is.True);
                Assert.That(binding.Apply(new TowerDefinitionId(type), level), Is.False);
                Assert.That(binding.Model, Is.Null); Assert.That(binding.Level, Is.Zero); Assert.That(binding.ShotSource, Is.Null);
                Assert.That(root.GetComponentsInChildren<Renderer>().Length, Is.EqualTo(2));
                Assert.That(root.GetComponentsInChildren<Renderer>().All(r => r.enabled), Is.True);
                Assert.That(binding.Apply(new TowerDefinitionId("flame"), 1), Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Art3C03_LegacyAuthoredFlameOnlyAndMissingGaleEntryRemainSafe()
        {
            var root = Tower();
            try
            {
                var binding = root.GetComponent<FlameModelBinding>();
                var serialized = new SerializedObject(binding);
                var gale = serialized.FindProperty("galeLevels");
                Assert.That(gale, Is.Not.Null, "new optional serialized family must preserve the old fields");
                gale.GetArrayElementAtIndex(1).objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(binding.Apply(new TowerDefinitionId("gale"), 1), Is.True);
                Assert.That(binding.Apply(new TowerDefinitionId("gale"), 2), Is.False);
                Assert.That(root.GetComponentsInChildren<Renderer>().All(r => r.enabled), Is.True);
                // В старом authored prefab поле отсутствует: Unity десериализует его как пустой массив/null.
                gale.arraySize = 0; serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(binding.Apply(new TowerDefinitionId("flame"), 3), Is.True);
                Assert.That(binding.Apply(new TowerDefinitionId("gale"), 1), Is.False);
                Assert.That(binding.Apply(new TowerDefinitionId("flame"), 1), Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
