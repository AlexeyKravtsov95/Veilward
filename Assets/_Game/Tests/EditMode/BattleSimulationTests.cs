using System;
using NUnit.Framework;
using Veilward.Domain;

namespace Veilward.Tests.EditMode
{
    public sealed class BattleSimulationTests
    {
        [Test]
        public void Constructor_CreatesPreparationState()
        {
            var simulation = CreateSimulation();

            Assert.That(simulation.State.Phase, Is.EqualTo(BattlePhase.Preparation));
            Assert.That(simulation.State.Tick, Is.Zero);
            Assert.That(simulation.State.BaseHealth, Is.EqualTo(20));
            Assert.That(simulation.State.Enemies, Is.Empty);
        }

        [Test]
        public void StartWave_SpawnsFirstEnemyAndIsIdempotent()
        {
            var simulation = CreateSimulation();

            simulation.StartWave();
            simulation.StartWave();

            Assert.That(simulation.State.Phase, Is.EqualTo(BattlePhase.Combat));
            Assert.That(simulation.State.Enemies.Count, Is.EqualTo(1));
            Assert.That(simulation.State.Enemies[0].Id, Is.EqualTo(new EnemyId(1)));
            Assert.That(simulation.State.Enemies[0].Health, Is.EqualTo(100));
        }

        [TestCase(0, 1)]
        [TestCase(29, 1)]
        [TestCase(30, 2)]
        [TestCase(60, 3)]
        [TestCase(90, 4)]
        [TestCase(120, 5)]
        public void Step_SpawnsAtConfiguredTicks(int steps, int expectedCount)
        {
            var simulation = CreateSimulation();
            simulation.StartWave();

            for (var index = 0; index < steps; index++)
            {
                simulation.Step();
            }

            Assert.That(simulation.State.Tick, Is.EqualTo(steps));
            Assert.That(simulation.State.Enemies.Count, Is.EqualTo(expectedCount));
        }

        [Test]
        public void Step_AssignsStableSequentialEnemyIds()
        {
            var simulation = CreateSimulation();
            simulation.StartWave();
            Step(simulation, 120);

            for (var index = 0; index < simulation.State.Enemies.Count; index++)
            {
                Assert.That(simulation.State.Enemies[index].Id, Is.EqualTo(new EnemyId(index + 1)));
            }
        }

        [Test]
        public void Step_MovesEnemyBySpeedPerFixedTick()
        {
            var simulation = CreateSimulation();
            simulation.StartWave();
            Step(simulation, 30);

            Assert.That(simulation.State.Enemies[0].DistanceAlongPath, Is.EqualTo(1.5f).Within(0.0001f));
        }

        [Test]
        public void Step_AcrossCorner_UsesPathDistanceInterpolation()
        {
            var path = new PathDefinition(new[]
            {
                new GridCoordinate(0, 0),
                new GridCoordinate(1, 0),
                new GridCoordinate(1, 1)
            });
            var simulation = CreateSimulation(speed: 15f, count: 1, path: path);
            simulation.StartWave();
            Step(simulation, 3);

            var enemy = simulation.State.Enemies[0];
            var position = path.GetPosition(enemy.DistanceAlongPath);
            Assert.That(position.X, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(position.Y, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void Step_WithIdenticalInputs_ProducesIdenticalState()
        {
            var first = CreateSimulation();
            var second = CreateSimulation();
            first.StartWave();
            second.StartWave();
            Step(first, 150);
            Step(second, 150);

            Assert.That(second.State.Tick, Is.EqualTo(first.State.Tick));
            Assert.That(second.State.Phase, Is.EqualTo(first.State.Phase));
            Assert.That(second.State.BaseHealth, Is.EqualTo(first.State.BaseHealth));
            Assert.That(second.State.Enemies.Count, Is.EqualTo(first.State.Enemies.Count));
            for (var index = 0; index < first.State.Enemies.Count; index++)
            {
                Assert.That(second.State.Enemies[index].Id, Is.EqualTo(first.State.Enemies[index].Id));
                Assert.That(
                    second.State.Enemies[index].DistanceAlongPath,
                    Is.EqualTo(first.State.Enemies[index].DistanceAlongPath));
            }
        }

        [Test]
        public void Step_AfterLeak_DoesNotApplyDamageTwice()
        {
            var simulation = CreateSimulation(speed: 300f, count: 1);
            simulation.StartWave();
            simulation.Step();
            var health = simulation.State.BaseHealth;
            var tick = simulation.State.Tick;

            simulation.Step();

            Assert.That(health, Is.EqualTo(19));
            Assert.That(simulation.State.BaseHealth, Is.EqualTo(health));
            Assert.That(simulation.State.Tick, Is.EqualTo(tick));
            Assert.That(simulation.State.Enemies, Is.Empty);
        }

        [Test]
        public void Step_LeadingLeakCompletesTestWave()
        {
            var simulation = CreateSimulation(speed: 300f, count: 1);
            simulation.StartWave();

            simulation.Step();

            Assert.That(simulation.State.Phase, Is.EqualTo(BattlePhase.WaveComplete));
            Assert.That(simulation.State.BaseHealth, Is.EqualTo(19));
        }

        [Test]
        public void Step_LastLeakCompletesWaveWithFifteenHealthAndFreezesState()
        {
            var simulation = CreateSimulation();
            simulation.StartWave();
            Step(simulation, 400);

            Assert.That(simulation.State.Phase, Is.EqualTo(BattlePhase.WaveComplete));
            Assert.That(simulation.State.BaseHealth, Is.EqualTo(15));
            Assert.That(simulation.State.Enemies, Is.Empty);
            var completedTick = simulation.State.Tick;

            Step(simulation, 60);

            Assert.That(simulation.State.Phase, Is.EqualTo(BattlePhase.WaveComplete));
            Assert.That(simulation.State.BaseHealth, Is.EqualTo(15));
            Assert.That(simulation.State.Tick, Is.EqualTo(completedTick));
            Assert.That(simulation.State.Enemies, Is.Empty);
        }

        [Test]
        public void Step_LethalLeak_ClampsHealthAndEntersDefeat()
        {
            var simulation = CreateSimulation(speed: 300f, count: 1, leakDamage: 2, baseHealth: 1);
            simulation.StartWave();

            simulation.Step();

            Assert.That(simulation.State.BaseHealth, Is.Zero);
            Assert.That(simulation.State.Phase, Is.EqualTo(BattlePhase.Defeat));
        }

        [Test]
        public void Step_AfterDefeat_DoesNotSpawnOrAdvanceTick()
        {
            var simulation = CreateSimulation(speed: 300f, count: 2, baseHealth: 1);
            simulation.StartWave();
            simulation.Step();
            var tick = simulation.State.Tick;

            Step(simulation, 60);

            Assert.That(simulation.State.Phase, Is.EqualTo(BattlePhase.Defeat));
            Assert.That(simulation.State.Tick, Is.EqualTo(tick));
            Assert.That(simulation.State.BaseHealth, Is.Zero);
            Assert.That(simulation.State.Enemies, Is.Empty);
        }

        [Test]
        public void Step_BeforeStartWave_DoesNothing()
        {
            var simulation = CreateSimulation();

            simulation.Step();

            Assert.That(simulation.State.Tick, Is.Zero);
            Assert.That(simulation.State.Phase, Is.EqualTo(BattlePhase.Preparation));
            Assert.That(simulation.State.Enemies, Is.Empty);
        }

        [Test]
        public void WaveDefinition_NullEnemy_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new WaveDefinition(null, 1, 0, 30));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void WaveDefinition_NonPositiveCount_Throws(int count)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new WaveDefinition(CreateEnemy(), count, 0, 30));
        }

        [Test]
        public void WaveDefinition_NegativeFirstSpawnTick_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new WaveDefinition(CreateEnemy(), 1, -1, 30));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void WaveDefinition_NonPositiveInterval_Throws(int interval)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new WaveDefinition(CreateEnemy(), 1, 0, interval));
        }

        private static BattleSimulation CreateSimulation(
            float speed = 1.5f,
            int count = 5,
            int leakDamage = 1,
            int baseHealth = 20,
            PathDefinition path = null)
        {
            var enemy = CreateEnemy(speed, leakDamage);
            var wave = new WaveDefinition(enemy, count, firstSpawnTick: 0, spawnIntervalTicks: 30);
            var actualPath = path ?? new PathDefinition(new[]
            {
                new GridCoordinate(0, 0),
                new GridCoordinate(10, 0)
            });
            return new BattleSimulation(actualPath, wave, baseHealth);
        }

        private static EnemyDefinition CreateEnemy(float speed = 1.5f, int leakDamage = 1)
        {
            return new EnemyDefinition(
                new EnemyDefinitionId("shade"),
                maxHealth: 100,
                speed: speed,
                leakDamage: leakDamage,
                reward: 0);
        }

        private static void Step(BattleSimulation simulation, int count)
        {
            for (var index = 0; index < count; index++)
            {
                simulation.Step();
            }
        }
    }
}
