using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using Veilward.Domain;
using Veilward.Presentation;

namespace Veilward.Tests.EditMode
{
    public sealed class BattleRegressionTests
    {
        private const string ConfigPath = "Assets/_Game/Configs/TestWave.asset";
        private static readonly GridCoordinate FreeCell = new GridCoordinate(-3, 0);
        private static readonly GridCoordinate RoadCell = new GridCoordinate(-3, -2);

        [Test]
        public void InitialState_SavedScenario_MatchesRegressionContract()
        {
            var simulation = CreateSimulation();

            Assert.That(simulation.State.Phase, Is.EqualTo(BattlePhase.Preparation));
            Assert.That(simulation.State.Tick, Is.Zero);
            Assert.That(simulation.State.Gold, Is.EqualTo(150));
            Assert.That(simulation.State.BaseHealth, Is.EqualTo(20));
            Assert.That(simulation.State.Kills, Is.Zero);
            Assert.That(simulation.State.Towers, Is.Empty);
            Assert.That(simulation.State.Enemies, Is.Empty);
            Assert.That(simulation.Path.TotalLength, Is.EqualTo(14f));
            Assert.That(simulation.TowerDefinition.Price, Is.EqualTo(60));
            Assert.That(simulation.TowerDefinition.Range, Is.EqualTo(3f));
            Assert.That(simulation.TowerDefinition.FireRate, Is.EqualTo(2f));
            Assert.That(simulation.TowerDefinition.Damage, Is.EqualTo(25));
        }

        [Test]
        public void PlacementAndRejectedCommands_PreserveEconomyAndGrid()
        {
            var simulation = CreateSimulation();

            var blocked = Place(simulation, RoadCell);
            Assert.That(blocked.Success, Is.False);
            Assert.That(blocked.FailureReason, Is.EqualTo(PlaceTowerFailureReason.Blocked));
            Assert.That(simulation.State.Gold, Is.EqualTo(150));
            Assert.That(simulation.State.Towers, Is.Empty);

            var placed = Place(simulation, FreeCell);
            Assert.That(placed.Success, Is.True, placed.FailureReason.ToString());
            Assert.That(placed.Tower.Id, Is.EqualTo(new TowerId(1)));
            Assert.That(simulation.State.Gold, Is.EqualTo(90));
            Assert.That(simulation.Grid.GetState(FreeCell), Is.EqualTo(GridCellState.Occupied));

            var repeated = Place(simulation, FreeCell);
            Assert.That(repeated.Success, Is.False);
            Assert.That(repeated.FailureReason, Is.EqualTo(PlaceTowerFailureReason.Occupied));
            Assert.That(simulation.State.Gold, Is.EqualTo(90));
            Assert.That(simulation.State.Towers.Count, Is.EqualTo(1));
            Assert.That(simulation.State.Towers[0].Id, Is.EqualTo(placed.Tower.Id));
        }

        [Test]
        public void NoTowerWave_SpawnsFiveUniqueShadeOnSchedule()
        {
            var simulation = CreateSimulation();
            var spawned = new List<EnemyState>();

            simulation.StartWave();
            CaptureNewEnemies(simulation, spawned);
            AssertSpawn(simulation, expectedTick: 0, expectedCount: 1);

            for (var tick = 1; tick <= 120; tick++)
            {
                simulation.Step();
                CaptureNewEnemies(simulation, spawned);
                var expectedCount = 1 + tick / 30;
                Assert.That(simulation.State.Enemies.Count, Is.EqualTo(expectedCount), $"Tick {tick}");
            }

            Assert.That(spawned.Select(enemy => enemy.Id.Value), Is.EqualTo(new[] { 1, 2, 3, 4, 5 }));
            Assert.That(spawned.Select(enemy => enemy.Definition.Id.Value).Distinct(), Is.EqualTo(new[] { "shade" }));
            Assert.That(spawned.Select(enemy => enemy.Health), Is.All.EqualTo(100));
        }

        [Test]
        public void NoTowerWave_MovesLeaksAndCompletes()
        {
            var simulation = CreateSimulation();
            var leakTicks = new List<long>();
            var previousHealth = simulation.State.BaseHealth;

            simulation.StartWave();
            simulation.Step();
            Assert.That(simulation.State.Enemies[0].DistanceAlongPath, Is.EqualTo(0.05f).Within(0.00001f));

            while (simulation.State.Phase == BattlePhase.Combat && simulation.State.Tick < 1000)
            {
                simulation.Step();
                if (simulation.State.BaseHealth < previousHealth)
                {
                    Assert.That(simulation.State.BaseHealth, Is.EqualTo(previousHealth - 1));
                    leakTicks.Add(simulation.State.Tick);
                    previousHealth = simulation.State.BaseHealth;
                }
            }

            Assert.That(leakTicks.Count, Is.EqualTo(5));
            Assert.That(leakTicks, Is.Ordered);
            Assert.That(simulation.State.Phase, Is.EqualTo(BattlePhase.WaveComplete));
            Assert.That(simulation.State.Tick, Is.EqualTo(leakTicks[^1]));
            Assert.That(simulation.State.BaseHealth, Is.EqualTo(15));
            Assert.That(simulation.State.Gold, Is.EqualTo(150));
            Assert.That(simulation.State.Kills, Is.Zero);
            Assert.That(simulation.State.Enemies, Is.Empty);

            var completedTick = simulation.State.Tick;
            simulation.Step();
            Assert.That(simulation.State.Tick, Is.EqualTo(completedTick));
            Assert.That(simulation.State.BaseHealth, Is.EqualTo(15));
            Assert.That(simulation.State.Phase, Is.EqualTo(BattlePhase.WaveComplete));
        }

        [Test]
        public void IdenticalSavedScenarioRuns_ProduceIdenticalCompleteTimelines()
        {
            var first = CreateTimeline();
            var second = CreateTimeline();

            Assert.That(second, Is.EqualTo(first));
            Assert.That(first.Count, Is.GreaterThan(1));
        }

        private static BattleSimulation CreateSimulation()
        {
            var config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>(ConfigPath);
            Assert.That(config, Is.Not.Null, ConfigPath);
            return config.CreateSimulation();
        }

        private static PlaceTowerResult Place(BattleSimulation simulation, GridCoordinate coordinate)
        {
            return simulation.PlaceTower(new PlaceTowerCommand(simulation.TowerDefinition.Id, coordinate));
        }

        private static void CaptureNewEnemies(BattleSimulation simulation, List<EnemyState> captured)
        {
            foreach (var enemy in simulation.State.Enemies)
            {
                if (captured.All(existing => existing.Id != enemy.Id))
                {
                    captured.Add(enemy);
                }
            }
        }

        private static void AssertSpawn(BattleSimulation simulation, int expectedTick, int expectedCount)
        {
            Assert.That(simulation.State.Tick, Is.EqualTo(expectedTick));
            Assert.That(simulation.State.Enemies.Count, Is.EqualTo(expectedCount));
        }

        private static IReadOnlyList<string> CreateTimeline()
        {
            var simulation = CreateSimulation();
            Assert.That(Place(simulation, FreeCell).Success, Is.True);
            var timeline = new List<string> { Snapshot(simulation) };
            simulation.StartWave();
            timeline.Add(Snapshot(simulation));

            while (simulation.State.Phase == BattlePhase.Combat && simulation.State.Tick < 1000)
            {
                simulation.Step();
                timeline.Add(Snapshot(simulation));
                simulation.ClearAttackEvents();
            }

            Assert.That(simulation.State.Phase, Is.EqualTo(BattlePhase.WaveComplete));
            Assert.That(simulation.State.Kills + 20 - simulation.State.BaseHealth, Is.EqualTo(5));
            Assert.That(simulation.State.BaseHealth, Is.InRange(0, 20));
            Assert.That(simulation.State.Gold, Is.EqualTo(90));
            return timeline;
        }

        private static string Snapshot(BattleSimulation simulation)
        {
            var state = simulation.State;
            var text = new StringBuilder();
            text.Append(state.Tick).Append('|')
                .Append(state.Phase).Append('|')
                .Append(state.BaseHealth).Append('|')
                .Append(state.Gold).Append('|')
                .Append(state.Kills).Append('|');

            foreach (var enemy in state.Enemies)
            {
                text.Append("E:").Append(enemy.Id.Value).Append(',')
                    .Append(enemy.Definition.Id.Value).Append(',')
                    .Append(enemy.Health).Append(',')
                    .Append(enemy.DistanceAlongPath.ToString("R", CultureInfo.InvariantCulture)).Append(';');
            }

            foreach (var tower in state.Towers)
            {
                text.Append("T:").Append(tower.Id.Value).Append(',')
                    .Append(tower.Coordinate.X).Append(',')
                    .Append(tower.Coordinate.Y).Append(',')
                    .Append(tower.NextAttackTick).Append(';');
            }

            foreach (var attack in simulation.AttackEvents)
            {
                text.Append("A:").Append(attack.AttackerTowerId.Value).Append(',')
                    .Append(attack.TargetEnemyId.Value).Append(',')
                    .Append(attack.TargetPosition.X.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(attack.TargetPosition.Y.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(attack.Tick).Append(',')
                    .Append(attack.Damage).Append(',')
                    .Append(attack.WasLethal).Append(';');
            }

            return text.ToString();
        }
    }
}
