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
            serialized.FindProperty("towerDamage").floatValue = 12f;
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
            Assert.That(simulation.TowerDefinition.Damage, Is.EqualTo(12f));
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
    }
}
