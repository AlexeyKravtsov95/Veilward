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
    }
}
