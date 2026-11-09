using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using Veilward.Domain;
using Veilward.Presentation;

namespace Veilward.Tests.EditMode
{
    public enum SavedStrategy
    {
        TowerExpansion,
        TowerUpgrades,
        FlexibleEconomy
    }

    public sealed class SavedStrategyReport
    {
        public SavedStrategyReport(
            SavedStrategy strategy,
            IReadOnlyList<string> commands,
            IReadOnlyList<string> waveSummaries,
            IReadOnlyList<string> timeline,
            string finalSummary)
        {
            Strategy = strategy;
            Commands = commands;
            WaveSummaries = waveSummaries;
            Timeline = timeline;
            FinalSummary = finalSummary;
        }

        public SavedStrategy Strategy { get; }
        public IReadOnlyList<string> Commands { get; }
        public IReadOnlyList<string> WaveSummaries { get; }
        public IReadOnlyList<string> Timeline { get; }
        public string FinalSummary { get; }
    }

    public sealed class BattleGameplayReplayTests
    {
        private const string EightWavePath = "Assets/_Game/Configs/EightWave.asset";
        private const int MaxTicksPerWave = 20000;
        private static readonly int[] ExpectedKills = { 5, 11, 18, 26, 35, 45, 56, 68 };

        private static readonly GridCoordinate[] ExpansionCells =
        {
            new GridCoordinate(-1, 0), new GridCoordinate(1, 0),
            new GridCoordinate(-1, 1), new GridCoordinate(1, 1),
            new GridCoordinate(-1, -1), new GridCoordinate(1, -1),
            new GridCoordinate(-2, 0), new GridCoordinate(2, 0)
        };

        [Test]
        public void SavedFlexibleReplay_ProducesIdenticalCompleteTimelines()
        {
            var first = RunSavedStrategy(SavedStrategy.FlexibleEconomy);
            var second = RunSavedStrategy(SavedStrategy.FlexibleEconomy);

            Assert.That(second.Timeline, Is.EqualTo(first.Timeline));
            Assert.That(second.Commands, Is.EqualTo(first.Commands));
            Assert.That(second.WaveSummaries, Is.EqualTo(first.WaveSummaries));
            Assert.That(second.FinalSummary, Is.EqualTo(first.FinalSummary));
            Assert.That(first.Timeline[0], Does.Contain("Phase=Preparation"));
            Assert.That(first.Timeline[0], Does.Contain("Gold=150"));
            Assert.That(first.Timeline[0], Does.Contain("HP=20"));
            Assert.That(first.Timeline.Count, Is.GreaterThan(2000));
            Assert.That(first.Commands, Has.Some.Contains("Sell Tower=1"));
            Assert.That(first.Commands, Has.Some.Contains("Place Tower=2"));
            Assert.That(first.FinalSummary, Does.Contain("Phase=Victory"));
        }

        [Test]
        public void SavedStrategies_ProduceReproducibleMechanicalResults()
        {
            AssertStrategyReplays(SavedStrategy.TowerExpansion);
            AssertStrategyReplays(SavedStrategy.TowerUpgrades);
            AssertStrategyReplays(SavedStrategy.FlexibleEconomy);
        }

        public static SavedStrategyReport RunSavedStrategy(SavedStrategy strategy)
        {
            var config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>(EightWavePath);
            if (config == null)
            {
                throw new InvalidOperationException("Saved EightWave asset is missing: " + EightWavePath);
            }

            var simulation = config.CreateSimulation();
            var commands = new List<string>();
            var waveSummaries = new List<string>();
            var timeline = new List<string> { "INITIAL|" + Snapshot(simulation) };
            Prepare(simulation, strategy, commands, timeline);

            for (var wave = 0; wave < simulation.State.WaveCount; wave++)
            {
                var oldKills = simulation.State.Kills;
                var oldHealth = simulation.State.BaseHealth;
                var started = simulation.StartWave();
                timeline.Add("START Wave=" + (wave + 1) + " Result=" + started + "|" + Snapshot(simulation));
                if (started != WaveTransitionResult.Started)
                {
                    throw new InvalidOperationException("StartWave rejected: " + started);
                }

                var guard = 0;
                while (simulation.State.Phase == BattlePhase.Combat && guard < MaxTicksPerWave)
                {
                    simulation.Step();
                    timeline.Add("STEP|" + Snapshot(simulation));
                    simulation.ClearAttackEvents();
                    guard++;
                }

                if (simulation.State.Phase == BattlePhase.Combat)
                {
                    throw new InvalidOperationException("Wave exceeded tick guard: " + (wave + 1));
                }

                waveSummaries.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "Wave={0}|Phase={1}|Tick={2}|Gold={3}|HP={4}|Kills={5}|Leaks={6}|{7}",
                    wave + 1,
                    simulation.State.Phase,
                    simulation.State.Tick,
                    simulation.State.Gold,
                    simulation.State.BaseHealth,
                    simulation.State.Kills,
                    oldHealth - simulation.State.BaseHealth,
                    TowerSummary(simulation)));

                if (simulation.State.Phase == BattlePhase.WaveComplete)
                {
                    var continued = simulation.ContinueToNextWave();
                    timeline.Add("CONTINUE Wave=" + (wave + 1) + " Result=" + continued + "|" + Snapshot(simulation));
                    if (continued != WaveTransitionResult.Continued)
                    {
                        throw new InvalidOperationException("ContinueToNextWave rejected: " + continued);
                    }

                    ApplyBetweenWaves(simulation, strategy, wave + 1, commands, timeline);
                }
            }

            var finalSummary = string.Format(
                CultureInfo.InvariantCulture,
                "Strategy={0}|Phase={1}|Wave={2}/{3}|Tick={4}|Gold={5}|HP={6}|Kills={7}|{8}",
                strategy,
                simulation.State.Phase,
                simulation.State.WaveIndex + 1,
                simulation.State.WaveCount,
                simulation.State.Tick,
                simulation.State.Gold,
                simulation.State.BaseHealth,
                simulation.State.Kills,
                TowerSummary(simulation));
            timeline.Add("FINAL|" + finalSummary + "|" + Snapshot(simulation));
            return new SavedStrategyReport(strategy, commands, waveSummaries, timeline, finalSummary);
        }

        private static void AssertStrategyReplays(SavedStrategy strategy)
        {
            var first = RunSavedStrategy(strategy);
            var second = RunSavedStrategy(strategy);

            Assert.That(second.Commands, Is.EqualTo(first.Commands), strategy + " commands");
            Assert.That(second.WaveSummaries, Is.EqualTo(first.WaveSummaries), strategy + " waves");
            Assert.That(second.Timeline, Is.EqualTo(first.Timeline), strategy + " timeline");
            Assert.That(second.FinalSummary, Is.EqualTo(first.FinalSummary), strategy + " final");
            Assert.That(first.WaveSummaries.Count, Is.EqualTo(8), strategy.ToString());
            Assert.That(first.FinalSummary, Does.Contain("Phase=Victory"), strategy.ToString());
            Assert.That(first.FinalSummary, Does.Contain("HP=20"), strategy.ToString());
            Assert.That(first.FinalSummary, Does.Contain("Kills=68"), strategy.ToString());

            var expectedGold = ExpectedGold(strategy);
            var expectedTicks = ExpectedTicks(strategy);
            for (var wave = 0; wave < first.WaveSummaries.Count; wave++)
            {
                var summary = first.WaveSummaries[wave];
                Assert.That(summary, Does.Contain("Wave=" + (wave + 1) + "|"), strategy.ToString());
                Assert.That(summary, Does.Contain("Gold=" + expectedGold[wave] + "|"), strategy.ToString());
                Assert.That(summary, Does.Contain("Tick=" + expectedTicks[wave] + "|"), strategy.ToString());
                Assert.That(summary, Does.Contain("HP=20|"), strategy.ToString());
                Assert.That(summary, Does.Contain("Kills=" + ExpectedKills[wave] + "|"), strategy.ToString());
                Assert.That(summary, Does.Contain("Leaks=0|"), strategy.ToString());
            }
        }

        private static int[] ExpectedGold(SavedStrategy strategy)
        {
            switch (strategy)
            {
                case SavedStrategy.TowerExpansion:
                    return new[] { 75, 65, 60, 60, 65, 75, 90, 170 };
                case SavedStrategy.TowerUpgrades:
                    return new[] { 135, 135, 110, 110, 125, 115, 130, 160 };
                case SavedStrategy.FlexibleEconomy:
                    return new[] { 117, 107, 112, 122, 107, 97, 112, 132 };
                default:
                    throw new ArgumentOutOfRangeException(nameof(strategy));
            }
        }

        private static int[] ExpectedTicks(SavedStrategy strategy)
        {
            switch (strategy)
            {
                case SavedStrategy.TowerExpansion:
                    return new[] { 210, 450, 720, 981, 1272, 1577, 1912, 2277 };
                case SavedStrategy.TowerUpgrades:
                    return new[] { 321, 612, 843, 1104, 1395, 1716, 2067, 2448 };
                case SavedStrategy.FlexibleEconomy:
                    return new[] { 321, 561, 831, 1116, 1407, 1728, 2079, 2460 };
                default:
                    throw new ArgumentOutOfRangeException(nameof(strategy));
            }
        }

        private static void Prepare(
            BattleSimulation simulation,
            SavedStrategy strategy,
            List<string> commands,
            List<string> timeline)
        {
            switch (strategy)
            {
                case SavedStrategy.TowerExpansion:
                    Place(simulation, ExpansionCells[0], commands, timeline);
                    Place(simulation, ExpansionCells[1], commands, timeline);
                    break;
                case SavedStrategy.TowerUpgrades:
                    Place(simulation, ExpansionCells[0], commands, timeline);
                    break;
                case SavedStrategy.FlexibleEconomy:
                    var sold = Place(simulation, new GridCoordinate(-4, -3), commands, timeline);
                    Sell(simulation, sold.Tower.Id, commands, timeline);
                    Place(simulation, ExpansionCells[0], commands, timeline);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(strategy));
            }
        }

        private static void ApplyBetweenWaves(
            BattleSimulation simulation,
            SavedStrategy strategy,
            int completedWave,
            List<string> commands,
            List<string> timeline)
        {
            switch (strategy)
            {
                case SavedStrategy.TowerExpansion:
                    if (completedWave <= 6)
                    {
                        Place(simulation, ExpansionCells[completedWave + 1], commands, timeline);
                    }
                    break;
                case SavedStrategy.TowerUpgrades:
                    ApplyUpgradeStrategy(simulation, completedWave, commands, timeline);
                    break;
                case SavedStrategy.FlexibleEconomy:
                    ApplyFlexibleStrategy(simulation, completedWave, commands, timeline);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(strategy));
            }
        }

        private static void ApplyUpgradeStrategy(
            BattleSimulation simulation,
            int completedWave,
            List<string> commands,
            List<string> timeline)
        {
            switch (completedWave)
            {
                case 1:
                case 2:
                    Upgrade(simulation, simulation.State.Towers[0].Id, commands, timeline);
                    break;
                case 3:
                    Place(simulation, ExpansionCells[1], commands, timeline);
                    break;
                case 4:
                case 5:
                    Upgrade(simulation, simulation.State.Towers[1].Id, commands, timeline);
                    break;
                case 6:
                    Place(simulation, ExpansionCells[2], commands, timeline);
                    break;
                case 7:
                    Upgrade(simulation, simulation.State.Towers[2].Id, commands, timeline);
                    break;
            }
        }

        private static void ApplyFlexibleStrategy(
            BattleSimulation simulation,
            int completedWave,
            List<string> commands,
            List<string> timeline)
        {
            switch (completedWave)
            {
                case 1:
                    Place(simulation, ExpansionCells[1], commands, timeline);
                    break;
                case 2:
                case 4:
                    Upgrade(simulation, simulation.State.Towers[0].Id, commands, timeline);
                    break;
                case 3:
                case 5:
                    Upgrade(simulation, simulation.State.Towers[1].Id, commands, timeline);
                    break;
                case 6:
                    Place(simulation, ExpansionCells[2], commands, timeline);
                    break;
                case 7:
                    Place(simulation, ExpansionCells[3], commands, timeline);
                    break;
            }
        }

        private static PlaceTowerResult Place(
            BattleSimulation simulation,
            GridCoordinate coordinate,
            List<string> commands,
            List<string> timeline)
        {
            var oldGold = simulation.State.Gold;
            var result = simulation.PlaceTower(new PlaceTowerCommand(simulation.TowerDefinition.Id, coordinate));
            if (!result.Success)
            {
                throw new InvalidOperationException("PlaceTower rejected: " + result.FailureReason);
            }

            AddCommand(commands, timeline, simulation, string.Format(
                CultureInfo.InvariantCulture,
                "Place Tower={0} Cell=({1},{2}) Cost={3} Gold={4}",
                result.Tower.Id.Value,
                coordinate.X,
                coordinate.Y,
                oldGold - simulation.State.Gold,
                simulation.State.Gold));
            return result;
        }

        private static void Upgrade(
            BattleSimulation simulation,
            TowerId id,
            List<string> commands,
            List<string> timeline)
        {
            var oldGold = simulation.State.Gold;
            var result = simulation.UpgradeTower(id);
            if (!result.Success)
            {
                throw new InvalidOperationException("UpgradeTower rejected: " + result.FailureReason);
            }

            AddCommand(commands, timeline, simulation, string.Format(
                CultureInfo.InvariantCulture,
                "Upgrade Tower={0} Level={1} Cost={2} Gold={3}",
                id.Value,
                result.Tower.Level,
                oldGold - simulation.State.Gold,
                simulation.State.Gold));
        }

        private static void Sell(
            BattleSimulation simulation,
            TowerId id,
            List<string> commands,
            List<string> timeline)
        {
            var result = simulation.SellTower(id);
            if (!result.Success)
            {
                throw new InvalidOperationException("SellTower rejected: " + result.FailureReason);
            }

            AddCommand(commands, timeline, simulation, string.Format(
                CultureInfo.InvariantCulture,
                "Sell Tower={0} Refund={1} Gold={2}",
                id.Value,
                result.Refund,
                simulation.State.Gold));
        }

        private static void AddCommand(
            List<string> commands,
            List<string> timeline,
            BattleSimulation simulation,
            string command)
        {
            commands.Add(command);
            timeline.Add("COMMAND " + command + "|" + Snapshot(simulation));
        }

        private static string TowerSummary(BattleSimulation simulation)
        {
            var text = new StringBuilder("Towers=");
            foreach (var tower in simulation.State.Towers)
            {
                text.Append(tower.Id.Value).Append('@')
                    .Append(tower.Coordinate.X).Append(',').Append(tower.Coordinate.Y)
                    .Append(":L").Append(tower.Level)
                    .Append(":I").Append(tower.InvestedGold).Append(';');
            }

            return text.ToString();
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
                    .Append(tower.Coordinate.X).Append(',').Append(tower.Coordinate.Y).Append(',')
                    .Append(tower.NextAttackTick).Append(',')
                    .Append(tower.Level).Append(',').Append(tower.InvestedGold).Append(',')
                    .Append(tower.Definition.Damage).Append(',')
                    .Append(tower.Definition.Range.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(tower.Definition.FireRate.ToString("R", CultureInfo.InvariantCulture));
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
