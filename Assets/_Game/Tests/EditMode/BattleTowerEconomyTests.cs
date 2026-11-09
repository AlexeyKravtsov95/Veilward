using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Veilward.Domain;

namespace Veilward.Tests.EditMode
{
    public sealed class BattleTowerEconomyTests
    {
        private static readonly int[] EightWaveCounts = { 5, 6, 7, 8, 9, 10, 11, 12 };
        private static readonly GridCoordinate Cell = new GridCoordinate(0, 2);

        [Test]
        public void Upgrade_LevelOneToTwo_UpdatesStatsAndInvestment()
        {
            var simulation = CreateSimulation(new[] { 1 }, startingGold: 150);
            var placed = Place(simulation, Cell);
            var originalId = placed.Tower.Id;
            var originalCoordinate = placed.Tower.Coordinate;
            var originalCooldown = placed.Tower.NextAttackTick;

            var result = simulation.UpgradeTower(originalId);

            Assert.That(result.Success, Is.True, result.FailureReason.ToString());
            Assert.That(result.Tower.Id, Is.EqualTo(originalId));
            Assert.That(result.Tower.Coordinate, Is.EqualTo(originalCoordinate));
            Assert.That(result.Tower.NextAttackTick, Is.EqualTo(originalCooldown));
            Assert.That(result.Tower.Level, Is.EqualTo(2));
            Assert.That(result.Tower.InvestedGold, Is.EqualTo(110));
            Assert.That(result.Tower.Definition.Damage, Is.EqualTo(35));
            Assert.That(result.Tower.Definition.Range, Is.EqualTo(3f));
            Assert.That(result.Tower.Definition.FireRate, Is.EqualTo(2f));
            Assert.That(simulation.State.Gold, Is.EqualTo(40));
        }

        [Test]
        public void Upgrade_LevelTwoToThree_UpdatesStatsAndInvestment()
        {
            var simulation = CreateSimulation(new[] { 1 }, startingGold: 200);
            var id = Place(simulation, Cell).Tower.Id;
            Assert.That(simulation.UpgradeTower(id).Success, Is.True);

            var result = simulation.UpgradeTower(id);

            Assert.That(result.Success, Is.True, result.FailureReason.ToString());
            Assert.That(result.Tower.Level, Is.EqualTo(3));
            Assert.That(result.Tower.InvestedGold, Is.EqualTo(190));
            Assert.That(result.Tower.Definition.Damage, Is.EqualTo(50));
            Assert.That(result.Tower.Definition.Range, Is.EqualTo(3f));
            Assert.That(result.Tower.Definition.FireRate, Is.EqualTo(2f));
            Assert.That(simulation.State.Gold, Is.EqualTo(10));
        }

        [Test]
        public void Upgrade_MaxLevel_IsRejectedWithoutMutation()
        {
            var simulation = CreateSimulation(new[] { 1 }, startingGold: 1000);
            var id = Place(simulation, Cell).Tower.Id;
            simulation.UpgradeTower(id);
            simulation.UpgradeTower(id);
            var snapshot = Snapshot(simulation);

            var result = simulation.UpgradeTower(id);

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(TowerOperationFailureReason.MaxLevel));
            Assert.That(Snapshot(simulation), Is.EqualTo(snapshot));
        }

        [Test]
        public void Upgrade_InsufficientGold_IsRejectedWithoutMutation()
        {
            var simulation = CreateSimulation(new[] { 1 }, startingGold: 100);
            var id = Place(simulation, Cell).Tower.Id;
            var snapshot = Snapshot(simulation);

            var result = simulation.UpgradeTower(id);

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(TowerOperationFailureReason.InsufficientGold));
            Assert.That(simulation.State.Gold, Is.EqualTo(40));
            Assert.That(Snapshot(simulation), Is.EqualTo(snapshot));
        }

        [Test]
        public void Commands_UnknownTowerId_AreRejectedWithoutMutation()
        {
            var simulation = CreateSimulation(new[] { 1 });
            var snapshot = Snapshot(simulation);

            var upgrade = simulation.UpgradeTower(new TowerId(999));
            var sell = simulation.SellTower(new TowerId(999));

            Assert.That(upgrade.FailureReason, Is.EqualTo(TowerOperationFailureReason.TowerNotFound));
            Assert.That(sell.FailureReason, Is.EqualTo(TowerOperationFailureReason.TowerNotFound));
            Assert.That(Snapshot(simulation), Is.EqualTo(snapshot));
        }

        [Test]
        public void Commands_Combat_AreRejectedWithoutMutation()
        {
            var simulation = CreateSimulation(new[] { 1 }, speed: 0.03f);
            var id = Place(simulation, Cell).Tower.Id;
            simulation.StartWave();
            var snapshot = Snapshot(simulation);

            var upgrade = simulation.UpgradeTower(id);
            var sell = simulation.SellTower(id);

            Assert.That(upgrade.FailureReason, Is.EqualTo(TowerOperationFailureReason.InvalidPhase));
            Assert.That(sell.FailureReason, Is.EqualTo(TowerOperationFailureReason.InvalidPhase));
            Assert.That(Snapshot(simulation), Is.EqualTo(snapshot));
        }

        [Test]
        public void Commands_TerminalPhases_AreRejectedWithoutMutation()
        {
            var victory = CreateSimulation(new[] { 1 }, speed: 300f);
            var victoryId = Place(victory, Cell).Tower.Id;
            victory.StartWave();
            victory.Step();
            Assert.That(victory.State.Phase, Is.EqualTo(BattlePhase.Victory));
            var victorySnapshot = Snapshot(victory);
            Assert.That(victory.UpgradeTower(victoryId).FailureReason,
                Is.EqualTo(TowerOperationFailureReason.InvalidPhase));
            Assert.That(victory.SellTower(victoryId).FailureReason,
                Is.EqualTo(TowerOperationFailureReason.InvalidPhase));
            Assert.That(Snapshot(victory), Is.EqualTo(victorySnapshot));

            var defeat = CreateSimulation(new[] { 1 }, speed: 300f, baseHealth: 1);
            var defeatId = Place(defeat, Cell).Tower.Id;
            defeat.StartWave();
            defeat.Step();
            Assert.That(defeat.State.Phase, Is.EqualTo(BattlePhase.Defeat));
            var defeatSnapshot = Snapshot(defeat);
            Assert.That(defeat.UpgradeTower(defeatId).FailureReason,
                Is.EqualTo(TowerOperationFailureReason.InvalidPhase));
            Assert.That(defeat.SellTower(defeatId).FailureReason,
                Is.EqualTo(TowerOperationFailureReason.InvalidPhase));
            Assert.That(Snapshot(defeat), Is.EqualTo(defeatSnapshot));
        }

        [TestCase(1, 42, 182)]
        [TestCase(2, 77, 167)]
        [TestCase(3, 133, 143)]
        public void Sell_AllLevels_GrantsExactFloorRefund(int targetLevel, int expectedRefund, int expectedGold)
        {
            var simulation = CreateSimulation(new[] { 1 }, startingGold: 200);
            var id = Place(simulation, Cell).Tower.Id;
            for (var level = 1; level < targetLevel; level++)
            {
                Assert.That(simulation.UpgradeTower(id).Success, Is.True);
            }
            var beforeSell = simulation.State.Gold;

            var result = simulation.SellTower(id);

            Assert.That(result.Success, Is.True, result.FailureReason.ToString());
            Assert.That(result.Refund, Is.EqualTo(expectedRefund));
            Assert.That(simulation.State.Gold, Is.EqualTo(beforeSell + expectedRefund));
            Assert.That(simulation.State.Gold, Is.EqualTo(expectedGold));
            Assert.That(simulation.State.Towers, Is.Empty);
        }

        [Test]
        public void Sell_Repeated_DoesNotGrantRefundTwice()
        {
            var simulation = CreateSimulation(new[] { 1 });
            var id = Place(simulation, Cell).Tower.Id;
            Assert.That(simulation.SellTower(id).Refund, Is.EqualTo(42));
            var snapshot = Snapshot(simulation);

            var repeated = simulation.SellTower(id);

            Assert.That(repeated.Success, Is.False);
            Assert.That(repeated.FailureReason, Is.EqualTo(TowerOperationFailureReason.TowerNotFound));
            Assert.That(repeated.Refund, Is.Zero);
            Assert.That(Snapshot(simulation), Is.EqualTo(snapshot));
        }

        [Test]
        public void Sell_ReleasesOnlyOwnedCell()
        {
            var simulation = CreateSimulation(new[] { 1 });
            var id = Place(simulation, Cell).Tower.Id;
            var blocked = new GridCoordinate(0, 0);
            Assert.That(simulation.Grid.GetState(blocked), Is.EqualTo(GridCellState.Blocked));

            Assert.That(simulation.SellTower(id).Success, Is.True);

            Assert.That(simulation.Grid.GetState(Cell), Is.EqualTo(GridCellState.Available));
            Assert.That(simulation.Grid.GetState(blocked), Is.EqualTo(GridCellState.Blocked));
        }

        [Test]
        public void SellThenPlace_RebuildsOnReleasedCell()
        {
            var simulation = CreateSimulation(new[] { 1 }, startingGold: 150);
            var soldId = Place(simulation, Cell).Tower.Id;
            Assert.That(simulation.SellTower(soldId).Success, Is.True);
            var goldAfterSale = simulation.State.Gold;

            var rebuilt = Place(simulation, Cell);

            Assert.That(rebuilt.Success, Is.True, rebuilt.FailureReason.ToString());
            Assert.That(rebuilt.Tower.Id.Value, Is.GreaterThan(soldId.Value));
            Assert.That(rebuilt.Tower.Id, Is.Not.EqualTo(soldId));
            Assert.That(rebuilt.Tower.Level, Is.EqualTo(1));
            Assert.That(rebuilt.Tower.InvestedGold, Is.EqualTo(60));
            Assert.That(simulation.State.Gold, Is.EqualTo(goldAfterSale - 60));
        }

        [Test]
        public void Upgrade_PreservesCooldownBetweenWaves()
        {
            var simulation = CreateSimulation(new[] { 1, 1 }, speed: 15f, damage: 1);
            var id = Place(simulation, Cell).Tower.Id;
            simulation.StartWave();
            simulation.Step();
            Assert.That(simulation.State.Towers[0].NextAttackTick, Is.EqualTo(16));
            CompleteCurrentWave(simulation);
            simulation.ContinueToNextWave();
            var cooldown = simulation.State.Towers[0].NextAttackTick;

            var result = simulation.UpgradeTower(id);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Tower.NextAttackTick, Is.EqualTo(cooldown));
            Assert.That(result.Tower.NextAttackTick, Is.EqualTo(16));
            Assert.That(simulation.State.Tick, Is.EqualTo(2));
        }

        [Test]
        public void Upgrade_SubsequentAttack_UsesNewDamage()
        {
            var simulation = CreateSimulation(
                new[] { 1, 1 },
                speed: 15f,
                maxHealth: 1000,
                pathLength: 10);
            var id = Place(simulation, Cell).Tower.Id;
            simulation.StartWave();
            CompleteCurrentWave(simulation);
            simulation.ContinueToNextWave();
            Assert.That(simulation.UpgradeTower(id).Success, Is.True);
            simulation.StartWave();
            simulation.ClearAttackEvents();

            simulation.Step();

            Assert.That(simulation.AttackEvents.Count, Is.EqualTo(1));
            Assert.That(simulation.AttackEvents[0].AttackerTowerId, Is.EqualTo(id));
            Assert.That(simulation.AttackEvents[0].TargetEnemyId.Value, Is.EqualTo(2));
            Assert.That(simulation.AttackEvents[0].Damage, Is.EqualTo(35));
            Assert.That(simulation.State.Enemies[0].Health, Is.EqualTo(965));
        }

        [Test]
        public void EconomyOperations_CannotOverflow()
        {
            var simulation = CreateSimulation(
                new[] { 1, 1 },
                speed: 300f,
                startingGold: int.MaxValue,
                waveCompletionBonus: int.MaxValue,
                prices: new[] { 0, int.MaxValue, 1 });
            var id = Place(simulation, Cell).Tower.Id;
            Assert.That(simulation.UpgradeTower(id).Success, Is.True);
            Assert.That(simulation.State.Towers[0].InvestedGold, Is.EqualTo(int.MaxValue));
            simulation.StartWave();
            CompleteCurrentWave(simulation);
            simulation.ContinueToNextWave();
            Assert.That(simulation.State.Gold, Is.EqualTo(int.MaxValue));
            var snapshot = Snapshot(simulation);

            var overflow = simulation.UpgradeTower(id);

            Assert.That(overflow.FailureReason, Is.EqualTo(TowerOperationFailureReason.InvestmentOverflow));
            Assert.That(Snapshot(simulation), Is.EqualTo(snapshot));

            var saleSimulation = CreateSimulation(new[] { 1 }, startingGold: int.MaxValue);
            var saleId = Place(saleSimulation, Cell).Tower.Id;
            var sale = saleSimulation.SellTower(saleId);
            Assert.That(sale.Refund, Is.EqualTo(42));
            Assert.That(saleSimulation.State.Gold, Is.EqualTo(int.MaxValue - 18));
            Assert.That(saleSimulation.State.Gold, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void RejectedCommands_PreserveCompleteState()
        {
            var simulation = CreateSimulation(new[] { 1 }, startingGold: 100);
            var id = Place(simulation, Cell).Tower.Id;
            var before = Snapshot(simulation);
            Assert.That(simulation.UpgradeTower(id).FailureReason,
                Is.EqualTo(TowerOperationFailureReason.InsufficientGold));
            Assert.That(Snapshot(simulation), Is.EqualTo(before));

            simulation.StartWave();
            before = Snapshot(simulation);
            Assert.That(simulation.SellTower(id).FailureReason,
                Is.EqualTo(TowerOperationFailureReason.InvalidPhase));
            Assert.That(simulation.UpgradeTower(new TowerId(999)).FailureReason,
                Is.EqualTo(TowerOperationFailureReason.InvalidPhase));
            Assert.That(Snapshot(simulation), Is.EqualTo(before));
        }

        [Test]
        public void IdenticalFullMatchesWithEconomy_ProduceIdenticalTimelines()
        {
            var first = CreateFullMatchTimeline();
            var second = CreateFullMatchTimeline();

            Assert.That(second, Is.EqualTo(first));
            Assert.That(first.Count, Is.GreaterThan(8));
            Assert.That(first.Last(), Does.Contain("Phase=Victory"));
            Assert.That(first.Last(), Does.Contain("Kills=68"));
            Assert.That(first[0], Does.Contain("Level=3"));
            Assert.That(first[0], Does.Contain("Invested=190"));
            Assert.That(first[0], Does.Contain("T:3"));
        }

        [Test]
        public void TowerLevels_InvalidConfiguration_IsRejectedAtConstruction()
        {
            var waves = new[] { CreateWave(1, 300f, 100) };
            var map = new GridMapDefinition(12, 8, new GridCoordinate(-6, -4), 1f);
            var path = new PathDefinition(new[] { new GridCoordinate(0, 0), new GridCoordinate(1, 0) });

            Assert.Throws<ArgumentException>(() => new BattleSimulation(
                map,
                path,
                waves,
                new[] { CreateLevel(60, 25), null, CreateLevel(80, 50) },
                baseHealth: 20,
                startingGold: 150,
                waveCompletionBonus: 20));
        }

        private static BattleSimulation CreateSimulation(
            IReadOnlyList<int> counts,
            float speed = 300f,
            int baseHealth = 20,
            int startingGold = 150,
            int damage = 25,
            int maxHealth = 100,
            int waveCompletionBonus = 20,
            int pathLength = 1,
            int[] prices = null)
        {
            var waves = counts.Select(count => CreateWave(count, speed, maxHealth)).ToArray();
            var map = new GridMapDefinition(12, 8, new GridCoordinate(-6, -4), 1f);
            var path = new PathDefinition(new[]
            {
                new GridCoordinate(0, 0),
                new GridCoordinate(pathLength, 0)
            });
            var costs = prices ?? new[] { 60, 50, 80 };
            var levels = new[]
            {
                CreateLevel(costs[0], damage),
                CreateLevel(costs[1], 35),
                CreateLevel(costs[2], 50)
            };
            return new BattleSimulation(
                map,
                path,
                waves,
                levels,
                baseHealth,
                startingGold,
                waveCompletionBonus);
        }

        private static WaveDefinition CreateWave(int count, float speed, int maxHealth)
        {
            return new WaveDefinition(
                new EnemyDefinition(new EnemyDefinitionId("shade"), maxHealth, speed, 1, reward: 5),
                count,
                firstSpawnTick: 0,
                spawnIntervalTicks: 1);
        }

        private static TowerDefinition CreateLevel(int price, int damage)
        {
            return new TowerDefinition(new TowerDefinitionId("test-tower"), price, 3f, 2f, damage);
        }

        private static PlaceTowerResult Place(BattleSimulation simulation, GridCoordinate coordinate)
        {
            return simulation.PlaceTower(new PlaceTowerCommand(simulation.TowerDefinition.Id, coordinate));
        }

        private static void CompleteCurrentWave(BattleSimulation simulation)
        {
            var guard = 0;
            while (simulation.State.Phase == BattlePhase.Combat && guard++ < 20000)
            {
                simulation.Step();
                simulation.ClearAttackEvents();
            }

            Assert.That(guard, Is.LessThan(20000), "Wave did not complete within the test guard.");
        }

        private static IReadOnlyList<string> CreateFullMatchTimeline()
        {
            var simulation = CreateSimulation(
                EightWaveCounts,
                speed: 1f,
                startingGold: 1000,
                pathLength: 10);
            var upgraded = Place(simulation, new GridCoordinate(0, 2)).Tower.Id;
            var sold = Place(simulation, new GridCoordinate(1, 2)).Tower.Id;
            simulation.UpgradeTower(upgraded);
            simulation.UpgradeTower(upgraded);
            simulation.SellTower(sold);
            var rebuilt = Place(simulation, new GridCoordinate(2, 2));
            Assert.That(rebuilt.Tower.Id.Value, Is.EqualTo(3));
            simulation.UpgradeTower(rebuilt.Tower.Id);
            simulation.UpgradeTower(rebuilt.Tower.Id);
            var support = Place(simulation, new GridCoordinate(1, 2)).Tower.Id;
            simulation.UpgradeTower(support);
            simulation.UpgradeTower(support);
            var finalSupport = Place(simulation, new GridCoordinate(3, 2)).Tower.Id;
            simulation.UpgradeTower(finalSupport);
            simulation.UpgradeTower(finalSupport);
            var timeline = new List<string> { Snapshot(simulation) };

            for (var wave = 0; wave < EightWaveCounts.Length; wave++)
            {
                Assert.That(simulation.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
                timeline.Add(Snapshot(simulation));
                while (simulation.State.Phase == BattlePhase.Combat)
                {
                    simulation.Step();
                    timeline.Add(Snapshot(simulation));
                    simulation.ClearAttackEvents();
                }

                if (wave < EightWaveCounts.Length - 1)
                {
                    Assert.That(simulation.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.Continued));
                    timeline.Add(Snapshot(simulation));
                }
            }

            return timeline;
        }

        private static string Snapshot(BattleSimulation simulation)
        {
            var state = simulation.State;
            var text = new StringBuilder();
            text.Append("Wave=").Append(state.WaveIndex).Append('/').Append(state.WaveCount)
                .Append("|Phase=").Append(state.Phase)
                .Append("|Tick=").Append(state.Tick)
                .Append("|HP=").Append(state.BaseHealth)
                .Append("|Gold=").Append(state.Gold)
                .Append("|Kills=").Append(state.Kills);

            foreach (var enemy in state.Enemies)
            {
                text.Append("|E:").Append(enemy.Id.Value).Append(',')
                    .Append(enemy.Health).Append(',')
                    .Append(enemy.DistanceAlongPath.ToString("R", CultureInfo.InvariantCulture));
            }

            foreach (var tower in state.Towers)
            {
                text.Append("|T:").Append(tower.Id.Value).Append(',')
                    .Append(tower.Coordinate.X).Append(',')
                    .Append(tower.Coordinate.Y).Append(',')
                    .Append(tower.NextAttackTick).Append(',')
                    .Append("Level=").Append(tower.Level).Append(',')
                    .Append("Invested=").Append(tower.InvestedGold).Append(',')
                    .Append("Damage=").Append(tower.Definition.Damage).Append(',')
                    .Append("Range=").Append(tower.Definition.Range.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append("FireRate=").Append(tower.Definition.FireRate.ToString("R", CultureInfo.InvariantCulture));
            }

            for (var y = simulation.Grid.Map.Origin.Y; y < simulation.Grid.Map.Origin.Y + simulation.Grid.Map.Height; y++)
            {
                for (var x = simulation.Grid.Map.Origin.X; x < simulation.Grid.Map.Origin.X + simulation.Grid.Map.Width; x++)
                {
                    text.Append("|G:").Append(x).Append(',').Append(y).Append('=')
                        .Append(simulation.Grid.GetState(new GridCoordinate(x, y)));
                }
            }

            foreach (var attack in simulation.AttackEvents)
            {
                text.Append("|A:").Append(attack.AttackerTowerId.Value).Append(',')
                    .Append(attack.TargetEnemyId.Value).Append(',')
                    .Append(attack.TargetPosition.X.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(attack.TargetPosition.Y.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(attack.Tick).Append(',')
                    .Append(attack.Damage).Append(',')
                    .Append(attack.WasLethal);
            }

            return text.ToString();
        }
    }
}
