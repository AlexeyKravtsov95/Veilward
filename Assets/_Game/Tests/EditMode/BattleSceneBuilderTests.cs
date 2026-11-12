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
                var tower = config.CreateSimulation().TowerDefinition;
                Assert.That(tower.Range, Is.EqualTo(4.25f).Within(0.0001f));
                Assert.That(tower.FireRate, Is.EqualTo(3.5f).Within(0.0001f));
                Assert.That(tower.Damage, Is.EqualTo(37));
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
