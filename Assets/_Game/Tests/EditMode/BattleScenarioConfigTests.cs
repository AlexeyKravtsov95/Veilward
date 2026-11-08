using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Veilward.Domain;
using Veilward.Presentation;

namespace Veilward.Tests.EditMode
{
    public sealed class BattleScenarioConfigTests
    {
        [Test]
        public void CreateSimulation_UsesSerializedScenarioValues()
        {
            var config = BattleScenarioConfig.CreateDefault();
            var serialized = new SerializedObject(config);
            serialized.FindProperty("baseHealth").intValue = 9;
            serialized.FindProperty("maxHealth").intValue = 75;
            serialized.FindProperty("speed").floatValue = 2f;
            serialized.FindProperty("leakDamage").intValue = 3;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var simulation = config.CreateSimulation();
            simulation.StartWave();

            Assert.That(simulation.State.BaseHealth, Is.EqualTo(9));
            Assert.That(simulation.State.Enemies[0].Definition.MaxHealth, Is.EqualTo(75));
            Assert.That(simulation.State.Enemies[0].Definition.Speed, Is.EqualTo(2f));
            Assert.That(simulation.State.Enemies[0].Definition.LeakDamage, Is.EqualTo(3));
            Object.DestroyImmediate(config);
        }

        [Test]
        public void CreateSimulation_UsesDefaultSpawnSchedule()
        {
            var config = BattleScenarioConfig.CreateDefault();
            var simulation = config.CreateSimulation();
            simulation.StartWave();

            for (var index = 0; index < 120; index++)
            {
                simulation.Step();
            }

            Assert.That(simulation.State.Enemies.Count, Is.EqualTo(5));
            Assert.That(simulation.State.Enemies[0].Definition.Id.Value, Is.EqualTo("shade"));
            Assert.That(simulation.State.Enemies[0].Health, Is.EqualTo(100));
            Object.DestroyImmediate(config);
        }

        [Test]
        public void CreateSimulation_ExposesConfiguredPathForPresentation()
        {
            var config = BattleScenarioConfig.CreateDefault();
            var simulation = config.CreateSimulation();

            var position = simulation.Path.GetPosition(7f);

            Assert.That(simulation.Path.TotalLength, Is.EqualTo(14f).Within(0.0001f));
            Assert.That(position.X, Is.Zero.Within(0.0001f));
            Assert.That(position.Y, Is.Zero.Within(0.0001f));
            Object.DestroyImmediate(config);
        }

        [Test]
        public void CreateSimulation_UsesSerializedGridAndTowerValues()
        {
            var config = BattleScenarioConfig.CreateDefault();
            var serialized = new SerializedObject(config);
            serialized.FindProperty("gridWidth").intValue = 10;
            serialized.FindProperty("gridHeight").intValue = 6;
            serialized.FindProperty("gridOrigin").vector2IntValue = new Vector2Int(-5, -3);
            serialized.FindProperty("cellSize").floatValue = 1f;
            serialized.FindProperty("startingGold").intValue = 175;
            serialized.FindProperty("towerId").stringValue = "dummy";
            serialized.FindProperty("towerPrice").intValue = 65;
            serialized.FindProperty("towerRange").floatValue = 3.5f;
            serialized.FindProperty("towerFireRate").floatValue = 1.25f;
            serialized.FindProperty("towerDamage").intValue = 12;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var simulation = config.CreateSimulation();
            var result = simulation.PlaceTower(new PlaceTowerCommand(
                new TowerDefinitionId("dummy"),
                new GridCoordinate(-3, 0)));

            Assert.That(simulation.State.Gold, Is.EqualTo(110));
            Assert.That(simulation.Grid.Map.Width, Is.EqualTo(10));
            Assert.That(simulation.Grid.Map.Height, Is.EqualTo(6));
            Assert.That(simulation.Grid.Map.Origin, Is.EqualTo(new GridCoordinate(-5, -3)));
            Assert.That(simulation.Grid.Map.CellSize, Is.EqualTo(1f));
            Assert.That(simulation.TowerDefinition.Id.Value, Is.EqualTo("dummy"));
            Assert.That(simulation.TowerDefinition.Range, Is.EqualTo(3.5f));
            Assert.That(simulation.TowerDefinition.FireRate, Is.EqualTo(1.25f));
            Assert.That(simulation.TowerDefinition.Damage, Is.EqualTo(12));
            Assert.That(result.Success, Is.True);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void CreateSimulation_UsesDefaultGridAndEconomyValues()
        {
            var config = BattleScenarioConfig.CreateDefault();
            var simulation = config.CreateSimulation();

            Assert.That(simulation.Grid.Map.Width, Is.EqualTo(12));
            Assert.That(simulation.Grid.Map.Height, Is.EqualTo(8));
            Assert.That(simulation.Grid.Map.Origin, Is.EqualTo(new GridCoordinate(-6, -4)));
            Assert.That(simulation.Grid.Map.CellSize, Is.EqualTo(1f));
            Assert.That(simulation.State.Gold, Is.EqualTo(150));
            Assert.That(simulation.TowerDefinition.Price, Is.EqualTo(60));
            Object.DestroyImmediate(config);
        }

        [Test]
        public void CreateSimulation_UsesSerializedWaveSequenceAndRewards()
        {
            var config = BattleScenarioConfig.CreateDefault();
            var serialized = new SerializedObject(config);
            var waves = serialized.FindProperty("waves");
            Assert.That(waves, Is.Not.Null, "Serialized wave sequence is required.");
            serialized.FindProperty("speed").floatValue = 0.03f;
            serialized.FindProperty("towerDamage").intValue = 100;
            serialized.FindProperty("towerFireRate").floatValue = 30f;
            serialized.FindProperty("enemyReward").intValue = 7;
            serialized.FindProperty("waveCompletionBonus").intValue = 11;
            waves.arraySize = 2;
            for (var index = 0; index < waves.arraySize; index++)
            {
                var wave = waves.GetArrayElementAtIndex(index);
                wave.FindPropertyRelative("enemyCount").intValue = 1;
                wave.FindPropertyRelative("firstSpawnTick").intValue = 0;
                wave.FindPropertyRelative("spawnIntervalTicks").intValue = 30;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            var simulation = config.CreateSimulation();
            Assert.That(simulation.State.WaveCount, Is.EqualTo(2));
            var placement = simulation.PlaceTower(new PlaceTowerCommand(
                simulation.TowerDefinition.Id,
                new GridCoordinate(-4, 0)));
            Assert.That(placement.Success, Is.True, placement.FailureReason.ToString());

            simulation.StartWave();
            simulation.Step();
            Assert.That(simulation.State.Phase, Is.EqualTo(BattlePhase.WaveComplete));
            Assert.That(simulation.State.Gold, Is.EqualTo(108));
            Assert.That(simulation.State.Kills, Is.EqualTo(1));

            Assert.That(
                simulation.ContinueToNextWave(),
                Is.EqualTo(WaveTransitionResult.Continued));
            simulation.StartWave();
            simulation.Step();
            Assert.That(simulation.State.Phase, Is.EqualTo(BattlePhase.Victory));
            Assert.That(simulation.State.Gold, Is.EqualTo(126));
            Assert.That(simulation.State.Kills, Is.EqualTo(2));
            Object.DestroyImmediate(config);
        }

        [Test]
        public void EightWaveAsset_UsesSharedMapAndConfiguredWaveSequence()
        {
            const string configPath = "Assets/_Game/Configs/EightWave.asset";
            var config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>(configPath);
            Assert.That(config, Is.Not.Null, configPath);
            var legacy = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>(
                "Assets/_Game/Configs/TestWave.asset");
            var serialized = new SerializedObject(config);
            Assert.That(serialized.FindProperty("sharedScenario").objectReferenceValue, Is.SameAs(legacy));
            Assert.That(serialized.FindProperty("enemyReward").intValue, Is.EqualTo(5));
            Assert.That(serialized.FindProperty("waveCompletionBonus").intValue, Is.EqualTo(20));
            var waves = serialized.FindProperty("waves");
            Assert.That(waves.arraySize, Is.EqualTo(8));
            var expectedCounts = new[] { 5, 6, 7, 8, 9, 10, 11, 12 };
            for (var index = 0; index < expectedCounts.Length; index++)
            {
                Assert.That(
                    waves.GetArrayElementAtIndex(index).FindPropertyRelative("enemyCount").intValue,
                    Is.EqualTo(expectedCounts[index]));
            }

            var simulation = config.CreateSimulation();
            var legacySimulation = legacy.CreateSimulation();
            Assert.That(simulation.State.WaveCount, Is.EqualTo(8));
            Assert.That(simulation.Path.TotalLength, Is.EqualTo(legacySimulation.Path.TotalLength));
            Assert.That(simulation.TowerDefinition.Price, Is.EqualTo(legacySimulation.TowerDefinition.Price));
            Assert.That(simulation.TowerDefinition.Range, Is.EqualTo(legacySimulation.TowerDefinition.Range));
            Assert.That(simulation.TowerDefinition.Damage, Is.EqualTo(legacySimulation.TowerDefinition.Damage));
        }
    }
}
