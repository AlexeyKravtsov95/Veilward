using System;
using NUnit.Framework;
using Veilward.Domain;

namespace Veilward.Tests.EditMode
{
    public sealed class TowerPlacementTests
    {
        private static readonly GridCoordinate FreeCell = new GridCoordinate(-3, 0);
        private static readonly GridCoordinate SecondFreeCell = new GridCoordinate(-2, 0);
        private static readonly GridCoordinate RoadCell = new GridCoordinate(-3, -2);

        [Test]
        public void PlaceTower_FreeCell_Succeeds()
        {
            var simulation = CreateSimulation();

            var result = Place(simulation, FreeCell);

            Assert.That(result.Success, Is.True);
            Assert.That(result.FailureReason, Is.EqualTo(PlaceTowerFailureReason.None));
            Assert.That(result.Tower, Is.Not.Null);
            Assert.That(result.Tower.Coordinate, Is.EqualTo(FreeCell));
            Assert.That(simulation.State.Towers.Count, Is.EqualTo(1));
            Assert.That(simulation.Grid.GetState(FreeCell), Is.EqualTo(GridCellState.Occupied));
        }

        [Test]
        public void PlaceTower_RoadCell_IsRejectedWithoutChanges()
        {
            var simulation = CreateSimulation();

            var result = Place(simulation, RoadCell);

            AssertRejectedWithoutChanges(simulation, result, PlaceTowerFailureReason.Blocked, 150);
        }

        [Test]
        public void PlaceTower_OutsideMap_IsRejectedWithoutChanges()
        {
            var simulation = CreateSimulation();

            var result = Place(simulation, new GridCoordinate(20, 0));

            AssertRejectedWithoutChanges(simulation, result, PlaceTowerFailureReason.OutsideMap, 150);
        }

        [Test]
        public void PlaceTower_OccupiedCell_IsRejectedWithoutChanges()
        {
            var simulation = CreateSimulation();
            Place(simulation, FreeCell);
            var firstTower = simulation.State.Towers[0];

            var result = Place(simulation, FreeCell);

            AssertRejectedWithoutChanges(
                simulation,
                result,
                PlaceTowerFailureReason.Occupied,
                expectedGold: 90,
                expectedTowerCount: 1);
            Assert.That(simulation.State.Towers[0].Id, Is.EqualTo(firstTower.Id));
        }

        [Test]
        public void PlaceTower_InsufficientGold_IsRejectedWithoutChanges()
        {
            var simulation = CreateSimulation(startingGold: 50);

            var result = Place(simulation, FreeCell);

            AssertRejectedWithoutChanges(simulation, result, PlaceTowerFailureReason.InsufficientGold, 50);
        }

        [Test]
        public void PlaceTower_Success_DebitsGoldExactlyOnce()
        {
            var simulation = CreateSimulation(startingGold: 150, price: 60);

            Place(simulation, FreeCell);

            Assert.That(simulation.State.Gold, Is.EqualTo(90));
        }

        [Test]
        public void PlaceTower_RepeatedCommand_DoesNotDebitTwice()
        {
            var simulation = CreateSimulation();
            Place(simulation, FreeCell);

            Place(simulation, FreeCell);
            Place(simulation, FreeCell);

            Assert.That(simulation.State.Gold, Is.EqualTo(90));
            Assert.That(simulation.State.Towers.Count, Is.EqualTo(1));
        }

        [Test]
        public void TowerDefinition_InvalidValues_AreRejected()
        {
            Assert.Throws<ArgumentException>(() => new TowerDefinition(
                default,
                price: 60,
                range: 2.5f,
                fireRate: 1f,
                damage: 10f));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateDefinition(price: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateDefinition(range: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateDefinition(fireRate: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateDefinition(damage: float.PositiveInfinity));
        }

        [Test]
        public void PlaceTower_UnknownDefinition_IsRejectedWithoutChanges()
        {
            var simulation = CreateSimulation();
            var command = new PlaceTowerCommand(new TowerDefinitionId("unknown"), FreeCell);

            var result = simulation.PlaceTower(command);

            AssertRejectedWithoutChanges(simulation, result, PlaceTowerFailureReason.UnknownTowerDefinition, 150);
        }

        [Test]
        public void PlaceTower_TwoTowers_AssignsUniqueIds()
        {
            var simulation = CreateSimulation();

            var first = Place(simulation, FreeCell);
            var second = Place(simulation, SecondFreeCell);

            Assert.That(first.Success, Is.True);
            Assert.That(second.Success, Is.True);
            Assert.That(second.Tower.Id, Is.Not.EqualTo(first.Tower.Id));
            Assert.That(simulation.State.Gold, Is.EqualTo(30));
        }

        [Test]
        public void PlaceTower_ExactGoldBalance_NeverBecomesNegative()
        {
            var simulation = CreateSimulation(startingGold: 60);

            var result = Place(simulation, FreeCell);

            Assert.That(result.Success, Is.True);
            Assert.That(simulation.State.Gold, Is.Zero);
            Assert.That(simulation.State.Gold, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void PlaceTower_DuringCombat_IsRejectedWithoutChanges()
        {
            var simulation = CreateSimulation();
            simulation.StartWave();
            var enemyCount = simulation.State.Enemies.Count;

            var result = Place(simulation, FreeCell);

            AssertRejectedWithoutChanges(simulation, result, PlaceTowerFailureReason.InvalidPhase, 150);
            Assert.That(simulation.State.Enemies.Count, Is.EqualTo(enemyCount));
        }

        [Test]
        public void PlaceTower_Success_DoesNotChangeRoute()
        {
            var simulation = CreateSimulation();
            var path = simulation.Path;
            var totalLength = path.TotalLength;
            var positionBefore = path.GetPosition(7.25f);

            var result = Place(simulation, FreeCell);
            var positionAfter = path.GetPosition(7.25f);

            Assert.That(result.Success, Is.True);
            Assert.That(simulation.Path, Is.SameAs(path));
            Assert.That(simulation.Path.TotalLength, Is.EqualTo(totalLength));
            Assert.That(positionAfter.X, Is.EqualTo(positionBefore.X));
            Assert.That(positionAfter.Y, Is.EqualTo(positionBefore.Y));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void TowerId_NonPositiveValue_Throws(int value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TowerId(value));
        }

        private static PlaceTowerResult Place(BattleSimulation simulation, GridCoordinate cell)
        {
            return simulation.PlaceTower(new PlaceTowerCommand(simulation.TowerDefinition.Id, cell));
        }

        private static void AssertRejectedWithoutChanges(
            BattleSimulation simulation,
            PlaceTowerResult result,
            PlaceTowerFailureReason reason,
            int expectedGold,
            int expectedTowerCount = 0)
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(reason));
            Assert.That(result.Tower, Is.Null);
            Assert.That(simulation.State.Gold, Is.EqualTo(expectedGold));
            Assert.That(simulation.State.Towers.Count, Is.EqualTo(expectedTowerCount));
        }

        private static BattleSimulation CreateSimulation(int startingGold = 150, int price = 60)
        {
            var map = new GridMapDefinition(12, 8, new GridCoordinate(-6, -4), 1f);
            var path = new PathDefinition(new[]
            {
                new GridCoordinate(-5, -2),
                new GridCoordinate(0, -2),
                new GridCoordinate(0, 2),
                new GridCoordinate(5, 2)
            });
            var enemy = new EnemyDefinition(
                new EnemyDefinitionId("shade"),
                maxHealth: 100,
                speed: 1.5f,
                leakDamage: 1,
                reward: 0);
            var wave = new WaveDefinition(enemy, count: 5, firstSpawnTick: 0, spawnIntervalTicks: 30);
            return new BattleSimulation(
                map,
                path,
                wave,
                CreateDefinition(price),
                baseHealth: 20,
                startingGold: startingGold);
        }

        private static TowerDefinition CreateDefinition(
            int price = 60,
            float range = 2.5f,
            float fireRate = 1f,
            float damage = 10f)
        {
            return new TowerDefinition(
                new TowerDefinitionId("test-tower"),
                price,
                range,
                fireRate,
                damage);
        }
    }
}
