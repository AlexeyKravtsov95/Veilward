using System;
using System.Linq;
using NUnit.Framework;
using Veilward.Domain;

namespace Veilward.Tests.EditMode
{
    public sealed class BattleCombatTests
    {
        [Test]
        public void SelectFirst_ReturnsEnemyWithMaximumProgress()
        {
            var path = StraightPath();
            var enemies = new[] { Enemy(1, 1f), Enemy(2, 4f), Enemy(3, 2f) };

            var selected = EnemyTargeting.SelectFirst(new PathPosition(0f, 0f), 10f, enemies, path);

            Assert.That(selected.Id, Is.EqualTo(new EnemyId(2)));
        }

        [Test]
        public void SelectFirst_EqualProgress_ReturnsLowestEnemyId()
        {
            var path = StraightPath();
            var enemies = new[] { Enemy(9, 2f), Enemy(3, 2f), Enemy(5, 2f) };

            var selected = EnemyTargeting.SelectFirst(new PathPosition(0f, 0f), 10f, enemies, path);

            Assert.That(selected.Id, Is.EqualTo(new EnemyId(3)));
        }

        [Test]
        public void SelectFirst_EnemyOutsideRange_IsNotSelected()
        {
            var path = StraightPath();
            var enemies = new[] { Enemy(1, 0f) };

            var selected = EnemyTargeting.SelectFirst(new PathPosition(0f, 3f), 2.99f, enemies, path);

            Assert.That(selected, Is.Null);
        }

        [Test]
        public void SelectFirst_EnemyAtRangeBoundary_IsSelected()
        {
            var path = StraightPath();
            var enemies = new[] { Enemy(1, 4f) };

            var selected = EnemyTargeting.SelectFirst(new PathPosition(0f, 3f), 5f, enemies, path);

            Assert.That(selected, Is.SameAs(enemies[0]));
        }

        [Test]
        public void SelectFirst_DeadEnemy_IsNotSelected()
        {
            var path = StraightPath();
            var enemies = new[] { Enemy(1, 0f, health: 0) };

            var selected = EnemyTargeting.SelectFirst(new PathPosition(0f, 0f), 10f, enemies, path);

            Assert.That(selected, Is.Null);
        }

        [TestCase(0.5f, 60)]
        [TestCase(1f, 30)]
        [TestCase(1.6f, 19)]
        [TestCase(2f, 15)]
        [TestCase(30f, 1)]
        [TestCase(60f, 1)]
        public void TowerDefinition_ConvertsFireRateToTickInterval(float fireRate, int expected)
        {
            var definition = Definition(range: 3f, fireRate: fireRate, damage: 25);

            Assert.That(definition.AttackIntervalTicks, Is.EqualTo(expected));
        }

        [Test]
        public void Step_WithoutTarget_DoesNotDamageOrStartCooldown()
        {
            var simulation = CombatSimulation(range: 1f);
            Place(simulation, new GridCoordinate(0, 3));
            simulation.StartWave();

            simulation.Step();

            Assert.That(simulation.State.Enemies[0].Health, Is.EqualTo(100));
            Assert.That(simulation.AttackEvents, Is.Empty);
            Assert.That(simulation.State.Towers[0].NextAttackTick, Is.Zero);
        }

        [Test]
        public void Step_Cooldown_AttacksOnExactTicks()
        {
            var simulation = CombatSimulation(fireRate: 1f, damage: 25);
            Place(simulation, new GridCoordinate(0, 2));
            simulation.StartWave();

            Step(simulation, 1);
            Assert.That(simulation.State.Enemies[0].Health, Is.EqualTo(75));
            Step(simulation, 29);
            Assert.That(simulation.State.Enemies[0].Health, Is.EqualTo(75));
            Step(simulation, 1);

            Assert.That(simulation.State.Enemies[0].Health, Is.EqualTo(50));
            Assert.That(simulation.AttackEvents.Select(attack => attack.Tick), Is.EqualTo(new long[] { 1, 31 }));
            Assert.That(simulation.State.Towers[0].NextAttackTick, Is.EqualTo(61));
        }

        [Test]
        public void Step_FireRateAboveThirty_AttacksOnlyOncePerTick()
        {
            var simulation = CombatSimulation(fireRate: 60f, damage: 10);
            Place(simulation, new GridCoordinate(0, 2));
            simulation.StartWave();

            simulation.Step();

            Assert.That(simulation.State.Enemies[0].Health, Is.EqualTo(90));
            Assert.That(simulation.AttackEvents.Count, Is.EqualTo(1));
        }

        [Test]
        public void Step_TwoTowers_AttackInTowerIdOrderAndSkipDeadTarget()
        {
            var simulation = CombatSimulation(
                fireRate: 30f,
                damage: 100,
                speed: 1.5f,
                count: 2,
                spawnInterval: 1);
            Place(simulation, new GridCoordinate(-1, 2));
            Place(simulation, new GridCoordinate(1, 2));
            simulation.StartWave();

            simulation.Step();

            Assert.That(
                simulation.AttackEvents.Select(attack => attack.AttackerTowerId.Value),
                Is.EqualTo(new[] { 1, 2 }));
            Assert.That(
                simulation.AttackEvents.Select(attack => attack.TargetEnemyId.Value),
                Is.EqualTo(new[] { 1, 2 }));
            Assert.That(simulation.State.Kills, Is.EqualTo(2));
            Assert.That(simulation.State.Enemies, Is.Empty);
        }

        [Test]
        public void Step_Damage_DecreasesHealthByExpectedValue()
        {
            var simulation = CombatSimulation(damage: 25);
            Place(simulation, new GridCoordinate(0, 2));
            simulation.StartWave();

            simulation.Step();

            Assert.That(simulation.State.Enemies[0].Health, Is.EqualTo(75));
        }

        [Test]
        public void ApplyDamage_Overkill_ClampsHealthToZeroAndReturnsLethalOnce()
        {
            var enemy = Enemy(1, 0f, health: 10);

            var firstLethal = enemy.ApplyDamage(15);
            var secondLethal = enemy.ApplyDamage(15);

            Assert.That(firstLethal, Is.True);
            Assert.That(secondLethal, Is.False);
            Assert.That(enemy.Health, Is.Zero);
            Assert.That(enemy.IsAlive, Is.False);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void ApplyDamage_NonPositiveDamage_Throws(int damage)
        {
            var enemy = Enemy(1, 0f);

            Assert.Throws<ArgumentOutOfRangeException>(() => enemy.ApplyDamage(damage));
        }

        [Test]
        public void Step_KilledEnemy_DiesAndIncreasesKillsExactlyOnce()
        {
            var simulation = CombatSimulation(damage: 150);
            Place(simulation, new GridCoordinate(0, 2));
            simulation.StartWave();

            simulation.Step();
            var completedTick = simulation.State.Tick;
            Step(simulation, 30);

            Assert.That(simulation.State.Enemies, Is.Empty);
            Assert.That(simulation.State.Kills, Is.EqualTo(1));
            Assert.That(simulation.AttackEvents.Count, Is.EqualTo(1));
            Assert.That(simulation.AttackEvents[0].WasLethal, Is.True);
            Assert.That(simulation.State.Tick, Is.EqualTo(completedTick));
        }

        [Test]
        public void Step_KilledEnemy_DoesNotLeakDamage()
        {
            var path = new PathDefinition(new[]
            {
                new GridCoordinate(0, 0),
                new GridCoordinate(2, 0)
            });
            var simulation = CombatSimulation(damage: 100, speed: 30f, path: path);
            Place(simulation, new GridCoordinate(0, 2));
            simulation.StartWave();

            simulation.Step();

            Assert.That(simulation.State.Kills, Is.EqualTo(1));
            Assert.That(simulation.State.BaseHealth, Is.EqualTo(20));
            Assert.That(simulation.State.Phase, Is.EqualTo(BattlePhase.WaveComplete));
        }

        [Test]
        public void Step_DeathRemoval_PreservesRemainingEnemyIdOrder()
        {
            var simulation = CombatSimulation(
                fireRate: 30f,
                damage: 100,
                speed: 1.5f,
                count: 3,
                spawnInterval: 1);
            Place(simulation, new GridCoordinate(0, 2));
            simulation.StartWave();

            simulation.Step();
            Assert.That(simulation.State.Enemies.Select(enemy => enemy.Id.Value), Is.EqualTo(new[] { 2 }));
            simulation.Step();
            Assert.That(simulation.State.Enemies.Select(enemy => enemy.Id.Value), Is.EqualTo(new[] { 3 }));
        }

        [Test]
        public void Step_PreparationAndStartWave_DoNotAttackBeforeFirstAdvanceTick()
        {
            var simulation = CombatSimulation();
            Place(simulation, new GridCoordinate(0, 2));

            simulation.Step();
            simulation.StartWave();

            Assert.That(simulation.State.Phase, Is.EqualTo(BattlePhase.Combat));
            Assert.That(simulation.State.Tick, Is.Zero);
            Assert.That(simulation.AttackEvents, Is.Empty);
            Assert.That(simulation.State.Enemies[0].Health, Is.EqualTo(100));
        }

        [Test]
        public void Step_AfterWaveComplete_DoesNotAttackOrAdvance()
        {
            var simulation = CombatSimulation(damage: 100);
            Place(simulation, new GridCoordinate(0, 2));
            simulation.StartWave();
            simulation.Step();
            simulation.ClearAttackEvents();
            var completedTick = simulation.State.Tick;

            Step(simulation, 30);

            Assert.That(simulation.State.Phase, Is.EqualTo(BattlePhase.WaveComplete));
            Assert.That(simulation.State.Tick, Is.EqualTo(completedTick));
            Assert.That(simulation.AttackEvents, Is.Empty);
        }

        [Test]
        public void Step_AfterDefeat_DoesNotAttackOrAdvance()
        {
            var simulation = CombatSimulation(speed: 300f, baseHealth: 1);
            Place(simulation, new GridCoordinate(0, 2));
            simulation.StartWave();
            simulation.Step();
            simulation.ClearAttackEvents();
            var defeatedTick = simulation.State.Tick;

            Step(simulation, 30);

            Assert.That(simulation.State.Phase, Is.EqualTo(BattlePhase.Defeat));
            Assert.That(simulation.State.Tick, Is.EqualTo(defeatedTick));
            Assert.That(simulation.AttackEvents, Is.Empty);
        }

        [Test]
        public void Step_AttackEvent_ContainsCombatFacts()
        {
            var simulation = CombatSimulation(damage: 25);
            Place(simulation, new GridCoordinate(0, 2));
            simulation.StartWave();

            simulation.Step();

            var attack = simulation.AttackEvents.Single();
            Assert.That(attack.AttackerTowerId, Is.EqualTo(new TowerId(1)));
            Assert.That(attack.TargetEnemyId, Is.EqualTo(new EnemyId(1)));
            Assert.That(attack.Tick, Is.EqualTo(1));
            Assert.That(attack.Damage, Is.EqualTo(25));
            Assert.That(attack.WasLethal, Is.False);
        }

        [Test]
        public void Step_LethalAttackEvent_PreservesConfiguredOverkillDamage()
        {
            var simulation = CombatSimulation(damage: 150);
            Place(simulation, new GridCoordinate(0, 2));
            simulation.StartWave();

            simulation.Step();

            var attack = simulation.AttackEvents.Single();
            Assert.That(attack.Damage, Is.EqualTo(150));
            Assert.That(attack.WasLethal, Is.True);
        }

        [Test]
        public void Step_LethalAttackEvent_PreservesTargetPositionAfterEnemyRemoval()
        {
            var simulation = CombatSimulation(damage: 100, speed: 30f);
            Place(simulation, new GridCoordinate(0, 2));
            simulation.StartWave();

            simulation.Step();

            Assert.That(simulation.State.Enemies, Is.Empty);
            var attack = simulation.AttackEvents.Single();
            Assert.That(attack.TargetPosition.X, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(attack.TargetPosition.Y, Is.Zero.Within(0.0001f));
        }

        [Test]
        public void ClearAttackEvents_ClearsOnlyEventLifetime()
        {
            var simulation = CombatSimulation();
            Place(simulation, new GridCoordinate(0, 2));
            simulation.StartWave();
            simulation.Step();

            simulation.ClearAttackEvents();

            Assert.That(simulation.AttackEvents, Is.Empty);
            Assert.That(simulation.State.Enemies[0].Health, Is.EqualTo(75));
        }

        [Test]
        public void Step_IdenticalCombatRuns_ProduceIdenticalResults()
        {
            var first = CombatSimulation(speed: 0.2f, count: 3);
            var second = CombatSimulation(speed: 0.2f, count: 3);
            Place(first, new GridCoordinate(-1, 2));
            Place(first, new GridCoordinate(1, 2));
            Place(second, new GridCoordinate(-1, 2));
            Place(second, new GridCoordinate(1, 2));
            first.StartWave();
            second.StartWave();

            Step(first, 120);
            Step(second, 120);

            AssertEquivalent(first, second);
        }

        private static BattleSimulation CombatSimulation(
            float range = 3f,
            float fireRate = 2f,
            int damage = 25,
            float speed = 0.03f,
            int count = 1,
            int spawnInterval = 30,
            int baseHealth = 20,
            PathDefinition path = null)
        {
            var map = new GridMapDefinition(9, 7, new GridCoordinate(-4, -3), 1f);
            var actualPath = path ?? new PathDefinition(new[]
            {
                new GridCoordinate(0, 0),
                new GridCoordinate(4, 0)
            });
            var enemy = new EnemyDefinition(
                new EnemyDefinitionId("shade"),
                maxHealth: 100,
                speed,
                leakDamage: 1,
                reward: 0);
            var wave = new WaveDefinition(enemy, count, firstSpawnTick: 0, spawnInterval);
            return new BattleSimulation(
                map,
                actualPath,
                wave,
                Definition(range, fireRate, damage),
                baseHealth,
                startingGold: 150);
        }

        private static TowerDefinition Definition(float range, float fireRate, int damage)
        {
            return new TowerDefinition(
                new TowerDefinitionId("test-tower"),
                price: 60,
                range,
                fireRate,
                damage);
        }

        private static EnemyState Enemy(int id, float distance, int health = 100)
        {
            var definition = new EnemyDefinition(
                new EnemyDefinitionId("shade"),
                maxHealth: 100,
                speed: 1.5f,
                leakDamage: 1,
                reward: 0);
            return new EnemyState(new EnemyId(id), definition, health, distance);
        }

        private static PathDefinition StraightPath()
        {
            return new PathDefinition(new[]
            {
                new GridCoordinate(0, 0),
                new GridCoordinate(20, 0)
            });
        }

        private static void Place(BattleSimulation simulation, GridCoordinate coordinate)
        {
            var result = simulation.PlaceTower(new PlaceTowerCommand(simulation.TowerDefinition.Id, coordinate));
            Assert.That(result.Success, Is.True, result.FailureReason.ToString());
        }

        private static void Step(BattleSimulation simulation, int count)
        {
            for (var index = 0; index < count; index++)
            {
                simulation.Step();
            }
        }

        private static void AssertEquivalent(BattleSimulation expected, BattleSimulation actual)
        {
            Assert.That(actual.State.Tick, Is.EqualTo(expected.State.Tick));
            Assert.That(actual.State.Phase, Is.EqualTo(expected.State.Phase));
            Assert.That(actual.State.BaseHealth, Is.EqualTo(expected.State.BaseHealth));
            Assert.That(actual.State.Gold, Is.EqualTo(expected.State.Gold));
            Assert.That(actual.State.Kills, Is.EqualTo(expected.State.Kills));
            Assert.That(actual.State.Enemies.Count, Is.EqualTo(expected.State.Enemies.Count));
            for (var index = 0; index < expected.State.Enemies.Count; index++)
            {
                var expectedEnemy = expected.State.Enemies[index];
                var actualEnemy = actual.State.Enemies[index];
                Assert.That(actualEnemy.Id, Is.EqualTo(expectedEnemy.Id));
                Assert.That(actualEnemy.Health, Is.EqualTo(expectedEnemy.Health));
                Assert.That(actualEnemy.DistanceAlongPath, Is.EqualTo(expectedEnemy.DistanceAlongPath));
            }

            Assert.That(actual.State.Towers.Count, Is.EqualTo(expected.State.Towers.Count));
            for (var index = 0; index < expected.State.Towers.Count; index++)
            {
                Assert.That(actual.State.Towers[index].Id, Is.EqualTo(expected.State.Towers[index].Id));
                Assert.That(
                    actual.State.Towers[index].NextAttackTick,
                    Is.EqualTo(expected.State.Towers[index].NextAttackTick));
            }

            Assert.That(actual.AttackEvents.Count, Is.EqualTo(expected.AttackEvents.Count));
            for (var index = 0; index < expected.AttackEvents.Count; index++)
            {
                var expectedAttack = expected.AttackEvents[index];
                var actualAttack = actual.AttackEvents[index];
                Assert.That(actualAttack.AttackerTowerId, Is.EqualTo(expectedAttack.AttackerTowerId));
                Assert.That(actualAttack.TargetEnemyId, Is.EqualTo(expectedAttack.TargetEnemyId));
                Assert.That(actualAttack.Tick, Is.EqualTo(expectedAttack.Tick));
                Assert.That(actualAttack.Damage, Is.EqualTo(expectedAttack.Damage));
                Assert.That(actualAttack.WasLethal, Is.EqualTo(expectedAttack.WasLethal));
            }
        }
    }
}
