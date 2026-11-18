using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Veilward.Editor;
using Veilward.Presentation;

namespace Veilward.Tests.EditMode
{
    public sealed class BattleSceneBuilderTests
    {
        [Test]
        public void RegBattle185_BuilderRecreatesMissingEightWaveWithNativeSettings()
        {
            const string path = "Assets/_Game/Configs/EightWaveAbilityBoss.asset";
            var paths = new List<string> { path, path + ".meta", "Assets/_Game/Configs/TestWave.asset",
                "Assets/_Game/Scenes/Battle.unity", "Assets/_Game/Scenes/MainMenu.unity",
                "Assets/_Game/Prefabs/Shade.prefab", "Assets/_Game/Prefabs/TestTower.prefab" };
            paths.AddRange(Directory.GetFiles("Assets/_Game/Art/Materials", "*.mat"));
            var before = new Dictionary<string, byte[]>();
            foreach (var file in paths) before[file] = File.ReadAllBytes(file);
            try
            {
                Assert.That(AssetDatabase.DeleteAsset(path), Is.True);
                Assert.DoesNotThrow(() => BattleSceneBuilder.Build());
                var config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>(path);
                Assert.That(config.ScenarioContentId, Is.EqualTo("eight-wave-ability-boss-v1"));
                var simulation = config.CreateSimulation();
                Assert.That(simulation.State.WaveCount, Is.EqualTo(8));
                Assert.That(simulation.State.ElementProgression.PendingChoiceId, Is.EqualTo("start-resonance"));
                Assert.That(BattleAssetValidator.ValidateSavedBattle(), Is.Empty);
                Assert.That(BattleAssetValidator.ValidateSavedMainMenu(), Is.Empty);
            }
            finally
            {
                foreach (var pair in before) File.WriteAllBytes(pair.Key, pair.Value);
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                EditorSceneManager.OpenScene("Assets/_Game/Scenes/Battle.unity");
                foreach (var pair in before) CollectionAssert.AreEqual(pair.Value, File.ReadAllBytes(pair.Key), pair.Key);
            }
        }

        [TestCase(4, false)]
        [TestCase(0, false)] [TestCase(3, false)] [TestCase(5, false)]
        [TestCase(4, true)] [TestCase(6, true)]
        public void RegBattle153_BuilderExtendsOnlyCompleteLegacyCatalog_RejectsMalformedWithoutWriting(int count, bool malformed)
        {
            const string path = "Assets/_Game/Configs/TestWave.asset";
            var before = File.ReadAllBytes(path);
            var scene = File.ReadAllBytes("Assets/_Game/Scenes/Battle.unity");
            var config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>(path);
            var originalName = config.name;
            try
            {
                var serialized = new SerializedObject(config);
                var catalog = serialized.FindProperty("towerArchetypes");
                catalog.arraySize = count;
                if (malformed) catalog.GetArrayElementAtIndex(0).FindPropertyRelative("id").stringValue = "unknown";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(config); AssetDatabase.SaveAssets();
                var damagedBytes = File.ReadAllBytes(path);
                if (count == 4 && !malformed)
                {
                    var basicBefore = catalog.GetArrayElementAtIndex(1).FindPropertyRelative("levels")
                        .GetArrayElementAtIndex(0).FindPropertyRelative("damage").intValue;
                    BattleSceneBuilder.Build();
                    Assert.That(config.name, Is.EqualTo(originalName));
                    Assert.That(config.CreateSimulation().TowerCatalog.Archetypes.Count, Is.EqualTo(7));
                    Assert.That(config.CreateSimulation().TowerCatalog.Get(new Domain.TowerDefinitionId("tide"))
                        .Levels[0].Damage, Is.EqualTo(basicBefore));
                    var extended = File.ReadAllBytes(path);
                    BattleSceneBuilder.Build();
                    CollectionAssert.AreEqual(extended, File.ReadAllBytes(path));
                }
                else
                {
                    Assert.Catch<System.ArgumentException>(() => BattleSceneBuilder.Build());
                    CollectionAssert.AreEqual(damagedBytes, File.ReadAllBytes(path));
                    CollectionAssert.AreEqual(scene, File.ReadAllBytes("Assets/_Game/Scenes/Battle.unity"));
                }
            }
            finally
            {
                File.WriteAllBytes(path, before); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                File.WriteAllBytes("Assets/_Game/Scenes/Battle.unity", scene);
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void RegBattle153_BuilderMigratesOnlyCompleteDefaultWrapperCopies()
        {
            const string path = "Assets/_Game/Configs/EightWave.asset";
            var before = File.ReadAllBytes(path);
            var defaults = BattleScenarioConfig.CreateDefault();
            try
            {
                var config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>(path);
                var serialized = new SerializedObject(config);
                var source = new SerializedObject(defaults);
                serialized.CopyFromSerializedProperty(source.FindProperty("towerArchetypes"));
                serialized.CopyFromSerializedProperty(source.FindProperty("reactions"));
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(config); AssetDatabase.SaveAssets();
                BattleSceneBuilder.Build();
                serialized.Update();
                Assert.That(serialized.FindProperty("towerArchetypes").arraySize, Is.Zero);
                Assert.That(serialized.FindProperty("reactions").arraySize, Is.Zero);
                Assert.That(config.CreateSimulation().TowerCatalog.Archetypes.Count, Is.EqualTo(7));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(defaults);
                File.WriteAllBytes(path, before); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }

        [TestCase("towerArchetypes")] [TestCase("reactions")]
        public void RegBattle153_BuilderRejectsPartialSharedWrapperWithoutWriting(string field)
        {
            const string path = "Assets/_Game/Configs/EightWave.asset";
            var before = File.ReadAllBytes(path);
            var scene = File.ReadAllBytes("Assets/_Game/Scenes/Battle.unity");
            try
            {
                var config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>(path);
                var serialized = new SerializedObject(config);
                serialized.FindProperty(field).arraySize = 1;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(config); AssetDatabase.SaveAssets();
                var damaged = File.ReadAllBytes(path);
                Assert.Catch<System.ArgumentException>(() => BattleSceneBuilder.Build());
                CollectionAssert.AreEqual(damaged, File.ReadAllBytes(path));
                CollectionAssert.AreEqual(scene, File.ReadAllBytes("Assets/_Game/Scenes/Battle.unity"));
            }
            finally
            {
                File.WriteAllBytes(path, before); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                File.WriteAllBytes("Assets/_Game/Scenes/Battle.unity", scene); AssetDatabase.Refresh();
            }
        }

        [Test]
        public void RegBattle122_BuildTwice_PreservesSavedConfigBytesAndCatalogReferences()
        {
            var paths = new[] { "Assets/_Game/Configs/TestWave.asset", "Assets/_Game/Configs/EightWave.asset" };
            var before = new Dictionary<string, byte[]>();
            foreach (var path in paths) before[path] = File.ReadAllBytes(path);
            try
            {
                for (var pass = 0; pass < 2; pass++)
                {
                    BattleSceneBuilder.Build();
                    foreach (var path in paths) CollectionAssert.AreEqual(before[path], File.ReadAllBytes(path), path);
                    Assert.That(BattleAssetValidator.ValidateSavedBattle(), Is.Empty);
                }
            }
            finally
            {
                foreach (var pair in before)
                {
                    File.WriteAllBytes(pair.Key, pair.Value);
                    AssetDatabase.ImportAsset(pair.Key, ImportAssetOptions.ForceUpdate);
                }
            }
        }

        [Test]
        public void Build_ExistingConfigs_PreserveCombatAndWaveParametersAcrossRepeatedGeneration()
        {
            const string configPath = "Assets/_Game/Configs/TestWave.asset";
            const string scenePath = "Assets/_Game/Scenes/Battle.unity";
            var outputPaths = new List<string>
            {
                configPath,
                scenePath,
                "Assets/_Game/Scenes/MainMenu.unity",
                "Assets/_Game/Configs/EightWave.asset",
                "Assets/_Game/Configs/EightWave.asset.meta",
                "Assets/_Game/Configs/EightWaveAbilityBoss.asset",
                "Assets/_Game/Configs/EightWaveAbilityBoss.asset.meta",
                "Assets/_Game/Prefabs/Shade.prefab",
                "Assets/_Game/Prefabs/TestTower.prefab"
            };
            outputPaths.AddRange(Directory.GetFiles("Assets/_Game/Art/Materials", "*.mat"));
            var snapshots = new Dictionary<string, byte[]>();
            foreach (var path in outputPaths)
            {
                snapshots[path] = File.ReadAllBytes(path);
            }

            try
            {
                var config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>(configPath);
                var serialized = new SerializedObject(config);
                serialized.FindProperty("towerRange").floatValue = 4.25f;
                serialized.FindProperty("towerFireRate").floatValue = 3.5f;
                serialized.FindProperty("towerDamage").intValue = 37;
                serialized.FindProperty("enemyArmor").intValue = 3;
                serialized.FindProperty("towerArchetypes").GetArrayElementAtIndex(1)
                    .FindPropertyRelative("levels").GetArrayElementAtIndex(0)
                    .FindPropertyRelative("statusEffect.power").intValue = 23;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var waveConfig = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>(
                    "Assets/_Game/Configs/EightWave.asset");
                Assert.That(waveConfig, Is.Not.Null, "EightWave.asset must exist before reproducibility checks.");
                var serializedWaves = new SerializedObject(waveConfig);
                serializedWaves.FindProperty("enemyReward").intValue = 9;
                serializedWaves.FindProperty("waveCompletionBonus").intValue = 13;
                serializedWaves.FindProperty("waves").GetArrayElementAtIndex(0)
                    .FindPropertyRelative("enemyCount").intValue = 3;
                serializedWaves.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();

                BattleSceneBuilder.Build();
                BattleSceneBuilder.Build();

                config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>(configPath);
                var simulation = config.CreateSimulation();
                var tower = simulation.TowerDefinition;
                Assert.That(tower.Range, Is.EqualTo(4.25f).Within(0.0001f));
                Assert.That(tower.FireRate, Is.EqualTo(3.5f).Within(0.0001f));
                Assert.That(tower.Damage, Is.EqualTo(37));
                Assert.That(simulation.TowerCatalog.Get(new Domain.TowerDefinitionId("tide"))
                    .Levels[0].StatusEffect.Power, Is.EqualTo(23));
                simulation.StartWave();
                Assert.That(simulation.State.Enemies[0].Definition.Armor, Is.EqualTo(3));
                waveConfig = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>(
                    "Assets/_Game/Configs/EightWave.asset");
                serializedWaves = new SerializedObject(waveConfig);
                Assert.That(serializedWaves.FindProperty("enemyReward").intValue, Is.EqualTo(9));
                Assert.That(serializedWaves.FindProperty("waveCompletionBonus").intValue, Is.EqualTo(13));
                Assert.That(
                    serializedWaves.FindProperty("waves").GetArrayElementAtIndex(0)
                        .FindPropertyRelative("enemyCount").intValue,
                    Is.EqualTo(3));
            }
            finally
            {
                foreach (var snapshot in snapshots)
                {
                    File.WriteAllBytes(snapshot.Key, snapshot.Value);
                }

                AssetDatabase.ImportAsset(configPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(
                    "Assets/_Game/Configs/EightWave.asset",
                    ImportAssetOptions.ForceUpdate);
                AssetDatabase.Refresh();
                EditorSceneManager.OpenScene(scenePath);
            }
        }

        [Test]
        public void CreatePathGeometry_ModifiedConfig_MatchesDomainPath()
        {
            var config = BattleScenarioConfig.CreateDefault();
            var serialized = new SerializedObject(config);
            var waypoints = serialized.FindProperty("waypoints");
            waypoints.arraySize = 3;
            waypoints.GetArrayElementAtIndex(0).vector2IntValue = new Vector2Int(-3, -1);
            waypoints.GetArrayElementAtIndex(1).vector2IntValue = new Vector2Int(2, -1);
            waypoints.GetArrayElementAtIndex(2).vector2IntValue = new Vector2Int(2, 4);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            GameObject pathObject = null;

            try
            {
                pathObject = BattleSceneBuilder.CreatePathGeometry(config, material, material, material);
                var path = config.CreateSimulation().Path;

                Assert.That(path.TotalLength, Is.EqualTo(10f).Within(0.0001f));
                Assert.That(pathObject.transform.childCount, Is.EqualTo(path.Points.Count + 1));
                for (var index = 0; index < path.Points.Count - 1; index++)
                {
                    var start = ToWorld(path.Points[index]);
                    var end = ToWorld(path.Points[index + 1]);
                    var delta = end - start;
                    var segment = pathObject.transform.Find($"Path Segment {index}");

                    Assert.That(segment, Is.Not.Null);
                    AssertVector(segment.position, (start + end) * 0.5f, segment.name);
                    AssertVector(segment.localScale, new Vector3(0.92f, 0.1f, delta.magnitude), segment.name);
                    Assert.That(
                        Quaternion.Angle(segment.rotation, Quaternion.LookRotation(delta.normalized)),
                        Is.LessThan(0.001f));
                }

                AssertVector(
                    pathObject.transform.Find("Spawn Marker").position,
                    MarkerPosition(path.Points[0]),
                    "Spawn Marker");
                AssertVector(
                    pathObject.transform.Find("Seal Heart").position,
                    MarkerPosition(path.Points[path.Points.Count - 1]),
                    "Seal Heart");
            }
            finally
            {
                if (pathObject != null)
                {
                    Object.DestroyImmediate(pathObject);
                }

                Object.DestroyImmediate(material);
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void CreateGridGeometry_DefaultConfig_MatchesDomainCells()
        {
            var config = BattleScenarioConfig.CreateDefault();
            var available = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var blocked = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            GameObject gridObject = null;

            try
            {
                gridObject = BattleSceneBuilder.CreateGridGeometry(config, available, blocked);

                Assert.That(gridObject.transform.childCount, Is.EqualTo(96));
                var free = gridObject.transform.Find("Cell_-3_0");
                var road = gridObject.transform.Find("Cell_-3_-2");
                Assert.That(free, Is.Not.Null);
                Assert.That(road, Is.Not.Null);
                Assert.That(free.GetComponent<Renderer>().sharedMaterial, Is.SameAs(available));
                Assert.That(road.GetComponent<Renderer>().sharedMaterial, Is.SameAs(blocked));
                AssertVector(free.position, new Vector3(-3f, 0f, 0f), free.name);
            }
            finally
            {
                if (gridObject != null)
                {
                    Object.DestroyImmediate(gridObject);
                }

                Object.DestroyImmediate(available);
                Object.DestroyImmediate(blocked);
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void CreateGridGeometry_HalfSizeCells_MatchesVisiblePath()
        {
            var config = BattleScenarioConfig.CreateDefault();
            var serialized = new SerializedObject(config);
            serialized.FindProperty("cellSize").floatValue = 0.5f;
            var waypoints = serialized.FindProperty("waypoints");
            waypoints.arraySize = 2;
            waypoints.GetArrayElementAtIndex(0).vector2IntValue = new Vector2Int(-2, 0);
            waypoints.GetArrayElementAtIndex(1).vector2IntValue = new Vector2Int(2, 0);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var available = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var blocked = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            GameObject gridObject = null;
            GameObject pathObject = null;

            try
            {
                gridObject = BattleSceneBuilder.CreateGridGeometry(config, available, blocked);
                pathObject = BattleSceneBuilder.CreatePathGeometry(config, blocked, blocked, blocked);

                var roadCell = gridObject.transform.Find("Cell_-4_0");
                var adjacentBuildableCell = gridObject.transform.Find("Cell_-4_1");
                var pathSegment = pathObject.transform.Find("Path Segment 0");
                Assert.That(roadCell, Is.Not.Null);
                Assert.That(adjacentBuildableCell, Is.Not.Null);
                Assert.That(roadCell.GetComponent<Renderer>().sharedMaterial, Is.SameAs(blocked));
                Assert.That(adjacentBuildableCell.GetComponent<Renderer>().sharedMaterial, Is.SameAs(available));
                AssertVector(roadCell.position, new Vector3(-2f, 0f, 0f), roadCell.name);
                AssertVector(roadCell.localScale, new Vector3(0.46f, 0.04f, 0.46f), roadCell.name);
                AssertVector(pathSegment.position, new Vector3(0f, 0.05f, 0f), pathSegment.name);
                AssertVector(pathSegment.localScale, new Vector3(0.46f, 0.1f, 4f), pathSegment.name);
                Assert.That(
                    pathSegment.GetComponent<Renderer>().bounds.Intersects(
                        adjacentBuildableCell.GetComponent<Renderer>().bounds),
                    Is.False,
                    "The visible road must not overlap an adjacent buildable cell.");
            }
            finally
            {
                if (gridObject != null)
                {
                    Object.DestroyImmediate(gridObject);
                }

                if (pathObject != null)
                {
                    Object.DestroyImmediate(pathObject);
                }

                Object.DestroyImmediate(available);
                Object.DestroyImmediate(blocked);
                Object.DestroyImmediate(config);
            }
        }

        private static Vector3 ToWorld(Veilward.Domain.GridCoordinate point)
        {
            return new Vector3(point.X, 0.05f, point.Y);
        }

        private static Vector3 MarkerPosition(Veilward.Domain.GridCoordinate point)
        {
            return new Vector3(point.X, 0.25f, point.Y);
        }

        private static void AssertVector(Vector3 actual, Vector3 expected, string label)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.0001f), label);
        }
    }
}
