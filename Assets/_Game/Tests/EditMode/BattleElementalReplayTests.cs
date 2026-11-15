using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Veilward.Domain;
using Veilward.Infrastructure;
using Veilward.Presentation;

namespace Veilward.Tests.EditMode
{
    public sealed class BattleElementalReplayTests
    {
        private const string ConfigPath = "Assets/_Game/Configs/EightWave.asset";
        private const string EvidencePath = "Docs/review/P2.1e/replay-summary.json";
        private const int MaxTicksPerWave = 20000;
        private static readonly GridCoordinate FlameCell = new GridCoordinate(-2, -1);
        private static readonly GridCoordinate TideCell = new GridCoordinate(-1, 0);
        private static readonly GridCoordinate GaleCell = new GridCoordinate(1, 0);
        private static readonly GridCoordinate StoneCell = new GridCoordinate(-3, -1);

        [Test]
        public void RegBattle125_MixedReplay_IsDeterministicThroughNaturalTerminal()
        {
            var first = Run(false);
            var second = Run(false);
            AssertReplayEqual(first, second);
            Assert.That(first.Evidence.statusApplications, Has.All.GreaterThan(0));
            Assert.That(first.Evidence.observedStatuses, Is.EquivalentTo(first.Evidence.statusOrder));
            Assert.That(first.Evidence.dotKills, Is.GreaterThan(0));
            Assert.That(first.Evidence.terminal, Is.EqualTo("Victory").Or.EqualTo("Defeat"));
            Assert.That(first.Evidence.waves.SelectMany(w => w.commands), Has.Some.Contains("Upgrade"));
            Assert.That(first.Evidence.waves.SelectMany(w => w.commands), Has.Some.Contains("Sell"));
            Assert.That(first.Evidence.waves.SelectMany(w => w.commands), Has.Some.Contains("Place 5 tide"));
        }

        [Test]
        public void RegBattle125_CheckpointContinueAndInterruptedCombat_MatchControl()
        {
            var control = Run(false);
            var resumed = Run(true);
            AssertReplayEqual(control, resumed);
            Assert.That(resumed.RoundTrips, Is.GreaterThan(2));
            Assert.That(resumed.Interrupted, Is.True);
            Assert.That(resumed.NonzeroCooldownRestored, Is.True);
            Assert.That(resumed.MixedLevelsRestored, Is.True);
        }

        [Test]
        public void RegBattle126_CommittedBalanceEvidence_MatchesReplay()
        {
            var report = Run(false);
            var actual = JsonUtility.ToJson(report.Evidence, true) + "\n";
            Directory.CreateDirectory("TestResults");
            File.WriteAllText("TestResults/P2.1e-replay-summary.actual.json", actual);
            Assert.That(File.Exists(EvidencePath), Is.True, "Сначала собрать измерения; не подгонять исход матча.");
            Assert.That(File.ReadAllText(EvidencePath).Replace("\r\n", "\n"), Is.EqualTo(actual));
        }

        private static void AssertReplayEqual(Replay first, Replay second)
        {
            Assert.That(second.Snapshots, Is.EqualTo(first.Snapshots), "Полное состояние каждого тика/команды");
            Assert.That(second.Events, Is.EqualTo(first.Events), "Direct events и наблюдаемые Burn deaths");
            Assert.That(JsonUtility.ToJson(second.Evidence), Is.EqualTo(JsonUtility.ToJson(first.Evidence)));
        }

        [Serializable]
        public sealed class Evidence
        {
            public string unityVersion = "6000.3.28f1 (8f082d2fe476)";
            public string sourceCommit = "6b26a87cf1bd7e4b52cc8e9b756a1a23be2379f5";
            public string scenario = ConfigPath;
            public string commandSource = "BattleElementalReplayTests.Prepare; fixed coverage commands by wave, after affordability pilots; no RNG";
            public string rng = "Absent: BattleSimulation uses fixed spawn schedule and stable IDs; no seed";
            public int ticksPerSecond = SimulationTiming.TicksPerSecond;
            public int startingHealth;
            public int startingGold;
            public string[] statusOrder = { "Burn", "Wet", "Gust", "Crack" };
            public int[] statusApplications = new int[4];
            public int dotKills;
            public string[] observedStatuses;
            public int enemyReward;
            public int waveCompletionBonus;
            public string terminal;
            public long terminalTick;
            public WaveEvidence[] waves;
        }

        [Serializable]
        public sealed class WaveEvidence
        {
            public int wave;
            public string phase;
            public long tick;
            public int health;
            public int gold;
            public int kills;
            public int waveKills;
            public int nextEnemyId;
            public int nextTowerId;
            public string[] remainingEnemies;
            public int leaks;
            public int dotKills;
            public int[] statusApplications = new int[4];
            public string[] towers;
            public string[] commands;
            public string[] leakDetails;
        }

        private sealed class Replay
        {
            public readonly Evidence Evidence = new Evidence();
            public readonly List<string> Snapshots = new List<string>();
            public readonly List<string> Events = new List<string>();
            public readonly HashSet<string> ObservedStatuses = new HashSet<string>();
            public int RoundTrips;
            public bool Interrupted;
            public bool NonzeroCooldownRestored;
            public bool MixedLevelsRestored;
        }

        private static Replay Run(bool withRestore)
        {
            var config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>(ConfigPath);
            Assert.That(config, Is.Not.Null);
            var simulation = config.CreateSimulation();
            var report = new Replay();
            var evidence = report.Evidence;
            var serialized = new SerializedObject(config);
            evidence.enemyReward = serialized.FindProperty("enemyReward").intValue;
            evidence.waveCompletionBonus = serialized.FindProperty("waveCompletionBonus").intValue;
            evidence.startingHealth = simulation.State.BaseHealth;
            evidence.startingGold = simulation.State.Gold;
            Assert.That(evidence.startingHealth, Is.EqualTo(20));
            Assert.That(evidence.startingGold, Is.EqualTo(150));
            Assert.That(UnityEngine.Application.unityVersion, Is.EqualTo("6000.3.28f1"));
            Assert.That(simulation.State.WaveCount, Is.EqualTo(8));
            var waves = new List<WaveEvidence>();
            var root = Path.GetFullPath(Path.Combine("TestResults", "ElementalReplay", Guid.NewGuid().ToString("N")));
            var store = new WaveCheckpointStore(root);
            var owner = new GameObject("Elemental replay checkpoint");
            var checkpoint = owner.AddComponent<BattleCheckpointController>();
            checkpoint.Configure(store, simulation, config.ScenarioContentId);
            try
            {
                Capture(report, simulation);
                while (simulation.State.Phase == BattlePhase.Preparation)
                {
                    var wave = new WaveEvidence { wave = simulation.State.WaveIndex + 1 };
                    var commands = new List<string>();
                    Prepare(simulation, commands, report);
                    wave.commands = commands.ToArray();
                    var oldHealth = simulation.State.BaseHealth;
                    var oldKills = simulation.State.Kills;
                    var oldGold = simulation.State.Gold;
                    Assert.That(checkpoint.SaveForStartWave(), Is.True);
                    if (withRestore)
                    {
                        simulation = RoundTrip(config, simulation, checkpoint, store, report);
                        if (wave.wave == 6)
                        {
                            InterruptAndRollback(config, ref simulation, checkpoint, store, report);
                        }
                    }

                    Assert.That(simulation.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
                    Assert.That(checkpoint.ObservePhaseTransition(), Is.True);
                    Capture(report, simulation);
                    var leaks = new List<string>();
                    var guard = 0;
                    while (simulation.State.Phase == BattlePhase.Combat && guard++ < MaxTicksPerWave)
                    {
                        ObserveStep(simulation, wave, report, leaks);
                    }
                    Assert.That(guard, Is.LessThan(MaxTicksPerWave), "Матч обязан завершиться естественно");
                    Assert.That(checkpoint.ObservePhaseTransition(), Is.True);
                    wave.phase = simulation.State.Phase.ToString();
                    wave.tick = simulation.State.Tick;
                    wave.health = simulation.State.BaseHealth;
                    wave.gold = simulation.State.Gold;
                    wave.kills = simulation.State.Kills;
                    wave.waveKills = wave.kills - oldKills;
                    Assert.That(wave.gold, Is.EqualTo(oldGold + wave.waveKills * evidence.enemyReward +
                        (simulation.State.Phase == BattlePhase.Defeat ? 0 : evidence.waveCompletionBonus)));
                    var stable = simulation.ExportCheckpoint();
                    wave.nextEnemyId = stable.nextEnemyId;
                    wave.nextTowerId = stable.nextTowerId;
                    wave.remainingEnemies = simulation.State.Enemies.Select(EnemyText).ToArray();
                    wave.leaks = oldHealth - wave.health; // Saved Shade: LeakDamage == 1.
                    Assert.That(leaks.Count, Is.EqualTo(wave.leaks));
                    wave.leakDetails = leaks.ToArray();
                    wave.towers = simulation.State.Towers.Select(TowerText).ToArray();
                    evidence.dotKills += wave.dotKills;
                    for (var i = 0; i < 4; i++) evidence.statusApplications[i] += wave.statusApplications[i];
                    waves.Add(wave);
                    TestContext.WriteLine(JsonUtility.ToJson(wave));
                    if (withRestore && simulation.State.Phase == BattlePhase.WaveComplete)
                    {
                        simulation = RoundTrip(config, simulation, checkpoint, store, report);
                        // Повторное открытие одного WaveComplete не повторяет награду.
                        simulation = RoundTrip(config, simulation, checkpoint, store, report);
                    }
                    if (simulation.State.Phase != BattlePhase.WaveComplete) break;
                    var gold = simulation.State.Gold;
                    var kills = simulation.State.Kills;
                    Assert.That(simulation.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.Continued));
                    Assert.That(simulation.State.Gold, Is.EqualTo(gold));
                    Assert.That(simulation.State.Kills, Is.EqualTo(kills));
                    Capture(report, simulation);
                    var beforeRepeat = Snapshot(simulation);
                    Assert.That(simulation.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.RejectedInvalidPhase));
                    Assert.That(Snapshot(simulation), Is.EqualTo(beforeRepeat));
                }
                evidence.terminal = simulation.State.Phase.ToString();
                evidence.terminalTick = simulation.State.Tick;
                evidence.waves = waves.ToArray();
                evidence.observedStatuses = report.ObservedStatuses.OrderBy(s => s, StringComparer.Ordinal).ToArray();
                var terminal = Snapshot(simulation);
                simulation.Step();
                Assert.That(Snapshot(simulation), Is.EqualTo(terminal));
                Assert.That(evidence.terminal, Is.EqualTo("Victory").Or.EqualTo("Defeat"));
                return report;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static BattleSimulation RoundTrip(BattleScenarioConfig config, BattleSimulation simulation,
            BattleCheckpointController checkpoint, WaveCheckpointStore store, Replay report)
        {
            var expected = Snapshot(simulation);
            var exported = JsonUtility.ToJson(simulation.ExportCheckpoint());
            var bytes = File.ReadAllBytes(store.PrimaryPath);
            var restored = config.CreateSimulation();
            Assert.That(store.TryLoad(config.ScenarioContentId, restored.TryValidateCheckpoint, out var load), Is.True);
            Assert.That(load.Envelope.schemaVersion, Is.EqualTo(2));
            checkpoint.Configure(store, restored, config.ScenarioContentId);
            checkpoint.RestoreCheckpoint(load.Envelope);
            Assert.That(Snapshot(restored), Is.EqualTo(expected));
            Assert.That(JsonUtility.ToJson(restored.ExportCheckpoint()), Is.EqualTo(exported),
                "type/level/investment/cooldown/nextEnemyId/nextTowerId");
            Assert.That(File.ReadAllBytes(store.PrimaryPath), Is.EqualTo(bytes), "Load не переписывает slot");
            report.RoundTrips++;
            report.NonzeroCooldownRestored |= restored.State.Towers.Any(t => t.NextAttackTick > 0);
            report.MixedLevelsRestored |= restored.State.Towers.Select(t => t.Level).Distinct().Count() > 1;
            return restored;
        }

        private static void InterruptAndRollback(BattleScenarioConfig config, ref BattleSimulation simulation,
            BattleCheckpointController checkpoint, WaveCheckpointStore store, Replay report)
        {
            var preparation = Snapshot(simulation);
            var gold = simulation.State.Gold;
            var kills = simulation.State.Kills;
            var bytes = File.ReadAllBytes(store.PrimaryPath);
            Assert.That(simulation.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
            Assert.That(checkpoint.ObservePhaseTransition(), Is.True);
            var attempted = new List<string> { Snapshot(simulation) };
            for (var guard = 0; guard < MaxTicksPerWave; guard++)
            {
                simulation.Step();
                attempted.Add(Snapshot(simulation));
                simulation.ClearAttackEvents();
                if (simulation.State.Kills > kills && simulation.State.Enemies.Any(e => e.Statuses.Count > 0)) break;
                Assert.That(simulation.State.Phase, Is.EqualTo(BattlePhase.Combat));
            }
            Assert.That(simulation.State.Phase, Is.EqualTo(BattlePhase.Combat));
            Assert.That(simulation.State.Kills, Is.GreaterThan(kills));
            Assert.That(simulation.State.Gold, Is.GreaterThan(gold));
            Assert.That(simulation.State.Enemies.Any(e => e.Statuses.Count > 0), Is.True);
            Assert.That(checkpoint.ObservePhaseTransition(), Is.True);
            Assert.That(File.ReadAllBytes(store.PrimaryPath), Is.EqualTo(bytes));
            TestContext.WriteLine("Interrupted wave 6: tick=" + simulation.State.Tick + " gold=" + simulation.State.Gold +
                " kills=" + simulation.State.Kills + "; saved gold=" + gold + " kills=" + kills);
            RestorePreparation(config, ref simulation, checkpoint, store);
            Assert.That(Snapshot(simulation), Is.EqualTo(preparation));
            Assert.That(simulation.State.Enemies, Is.Empty, "Статусы и враги попытки удалены");
            // Воспроизвести весь прерванный префикс, затем снова откатить перед контрольным проходом.
            Assert.That(simulation.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
            Assert.That(checkpoint.ObservePhaseTransition(), Is.True);
            Assert.That(Snapshot(simulation), Is.EqualTo(attempted[0]));
            for (var i = 1; i < attempted.Count; i++)
            {
                simulation.Step();
                Assert.That(Snapshot(simulation), Is.EqualTo(attempted[i]));
                simulation.ClearAttackEvents();
            }
            RestorePreparation(config, ref simulation, checkpoint, store);
            Assert.That(Snapshot(simulation), Is.EqualTo(preparation));
            report.Interrupted = true;
        }

        private static void RestorePreparation(BattleScenarioConfig config, ref BattleSimulation simulation,
            BattleCheckpointController checkpoint, WaveCheckpointStore store)
        {
            simulation = config.CreateSimulation();
            Assert.That(store.TryLoad(config.ScenarioContentId, simulation.TryValidateCheckpoint, out var loaded), Is.True);
            checkpoint.Configure(store, simulation, config.ScenarioContentId);
            checkpoint.RestoreCheckpoint(loaded.Envelope);
        }

        private static void Prepare(BattleSimulation simulation, List<string> commands, Replay report)
        {
            switch (simulation.State.WaveIndex + 1)
            {
                case 1:
                    Place(simulation, "flame", FlameCell, commands, report);
                    Place(simulation, "gale", GaleCell, commands, report);
                    break;
                case 3: Place(simulation, "tide", TideCell, commands, report); break;
                case 5: Place(simulation, "stone", StoneCell, commands, report); break;
                case 7:
                    var tower = simulation.State.Towers.Single(t => t.Id.Value == 3);
                    var before = simulation.State.Gold;
                    var refund = (int)((long)tower.InvestedGold * 70 / 100);
                    Assert.That(simulation.SellTower(tower.Id).Refund, Is.EqualTo(refund));
                    Assert.That(simulation.State.Gold, Is.EqualTo(before + refund));
                    Assert.That(simulation.Grid.GetState(TideCell), Is.EqualTo(GridCellState.Available));
                    commands.Add("Sell 3 tide refund=" + refund + " gold=" + simulation.State.Gold);
                    Capture(report, simulation);
                    var snapshot = Snapshot(simulation);
                    Assert.That(simulation.SellTower(tower.Id).Success, Is.False);
                    Assert.That(Snapshot(simulation), Is.EqualTo(snapshot));
                    Place(simulation, "tide", TideCell, commands, report);
                    Assert.That(simulation.State.Towers.Last().Id.Value, Is.EqualTo(5));
                    Upgrade(simulation, 5, commands, report);
                    break;
            }
        }

        private static void Place(BattleSimulation simulation, string type, GridCoordinate cell,
            List<string> commands, Replay report)
        {
            var before = simulation.State.Gold;
            var expectedId = simulation.ExportCheckpoint().nextTowerId;
            var result = simulation.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId(type), cell));
            Assert.That(result.Success, Is.True, "Place " + type + ": " + result.FailureReason);
            Assert.That(result.Tower.Id.Value, Is.EqualTo(expectedId));
            Assert.That(simulation.State.Gold, Is.EqualTo(before - simulation.TowerCatalog.Get(new TowerDefinitionId(type)).Levels[0].Price));
            commands.Add("Place " + result.Tower.Id.Value + " " + type + " (" + cell.X + "," + cell.Y +
                ") cost=" + (before - simulation.State.Gold) + " gold=" + simulation.State.Gold);
            Capture(report, simulation);
        }

        private static void Upgrade(BattleSimulation simulation, int id, List<string> commands, Replay report)
        {
            var tower = simulation.State.Towers.Single(t => t.Id.Value == id);
            var before = simulation.State.Gold;
            var cooldown = tower.NextAttackTick;
            var invested = tower.InvestedGold;
            var price = simulation.TowerCatalog.Get(tower.Definition.Id).Levels[tower.Level].Price;
            Assert.That(simulation.UpgradeTower(tower.Id).Success, Is.True);
            Assert.That(tower.NextAttackTick, Is.EqualTo(cooldown));
            Assert.That(tower.InvestedGold, Is.EqualTo(invested + price));
            Assert.That(simulation.State.Gold, Is.EqualTo(before - price));
            commands.Add("Upgrade " + id + " " + tower.Definition.Id.Value + " L" + tower.Level +
                " cost=" + price + " gold=" + simulation.State.Gold);
            Capture(report, simulation);
        }

        private static void ObserveStep(BattleSimulation simulation, WaveEvidence wave, Replay report, List<string> leaks)
        {
            var state = simulation.State;
            var kills = state.Kills;
            var before = state.Enemies.ToArray();
            // Зафиксировать данные до Step: EnemyState/StatusInstance мутируются самим Domain.
            var beforeText = before.ToDictionary(e => e.Id.Value, EnemyText);
            var dueBurnDeaths = before.Where(e => e.Burn != null && e.Burn.NextDamageTick == state.Tick + 1 &&
                e.Health <= Math.Max(1, e.Burn.Power - Math.Max(0, e.Definition.Armor - (e.Crack?.Power ?? 0))))
                .Select(e => e.Id.Value).ToArray();
            simulation.Step();
            var directDeaths = simulation.AttackEvents.Where(a => a.WasLethal).Select(a => a.TargetEnemyId.Value).ToArray();
            var dotKills = state.Kills - kills - directDeaths.Length;
            Assert.That(dotKills, Is.EqualTo(dueBurnDeaths.Length), "Смерти без lethal direct event сверены с due Burn");
            wave.dotKills += dotKills;
            foreach (var id in dueBurnDeaths) report.Events.Add("DOT|" + state.Tick + "|" + id);
            foreach (var attack in simulation.AttackEvents)
            {
                report.Events.Add(AttackText(attack));
                if (!attack.WasLethal)
                {
                    var status = state.Towers.Single(t => t.Id == attack.AttackerTowerId).Definition.StatusEffect;
                    Assert.That(status, Is.Not.Null);
                    wave.statusApplications[(int)status.Kind]++;
                }
            }
            foreach (var enemy in before)
            {
                if (!state.Enemies.Any(e => e.Id == enemy.Id) && !directDeaths.Contains(enemy.Id.Value) &&
                    !dueBurnDeaths.Contains(enemy.Id.Value))
                {
                    Assert.That(enemy.Definition.LeakDamage, Is.EqualTo(1));
                    leaks.Add("tick=" + state.Tick + " beforeStep=" + beforeText[enemy.Id.Value]);
                }
            }
            Assert.That(state.Gold, Is.GreaterThanOrEqualTo(0));
            foreach (var enemy in state.Enemies)
                foreach (var status in enemy.Statuses) report.ObservedStatuses.Add(status.Kind.ToString());
            Capture(report, simulation);
            simulation.ClearAttackEvents();
        }

        private static void Capture(Replay report, BattleSimulation simulation) => report.Snapshots.Add(Snapshot(simulation));

        private static string Snapshot(BattleSimulation simulation)
        {
            var s = simulation.State;
            var text = new StringBuilder().Append(s.Phase).Append('|').Append(s.Tick).Append('|')
                .Append(s.WaveIndex).Append('/').Append(s.WaveCount).Append('|').Append(s.BaseHealth)
                .Append('|').Append(s.Gold).Append('|').Append(s.Kills);
            foreach (var enemy in s.Enemies) text.Append("|E:").Append(EnemyText(enemy));
            foreach (var tower in s.Towers) text.Append("|T:").Append(TowerText(tower));
            for (var y = simulation.Grid.Map.Origin.Y; y < simulation.Grid.Map.Origin.Y + simulation.Grid.Map.Height; y++)
                for (var x = simulation.Grid.Map.Origin.X; x < simulation.Grid.Map.Origin.X + simulation.Grid.Map.Width; x++)
                    text.Append('|').Append(simulation.Grid.GetState(new GridCoordinate(x, y)));
            foreach (var attack in simulation.AttackEvents) text.Append('|').Append(AttackText(attack));
            if (s.Phase != BattlePhase.Combat) text.Append('|').Append(JsonUtility.ToJson(simulation.ExportCheckpoint()));
            return text.ToString();
        }

        private static string TowerText(TowerState t) => t.Id.Value + ":" + t.Definition.Id.Value + ":L" + t.Level +
            ":cell=" + t.Coordinate.X + "," + t.Coordinate.Y + ":invested=" + t.InvestedGold + ":nextAttack=" + t.NextAttackTick;

        private static string EnemyText(EnemyState e)
        {
            var text = new StringBuilder().Append(e.Id.Value).Append(':').Append(e.Definition.Id.Value)
                .Append(":hp=").Append(e.Health).Append(":distance=").Append(e.DistanceAlongPath.ToString("R", CultureInfo.InvariantCulture));
            foreach (var status in e.Statuses)
                text.Append(':').Append(status.Kind).Append(',').Append(status.Power).Append(',').Append(status.RemainingTicks)
                    .Append(',').Append(status.NextDamageTick).Append(',').Append(status.DamageIntervalTicks).Append(',').Append(status.SourceTowerId.Value);
            return text.ToString();
        }

        private static string AttackText(AttackEvent a) => "A|" + a.Tick + "|" + a.AttackerTowerId.Value + "|" + a.TargetEnemyId.Value +
            "|" + a.TargetPosition.X.ToString("R", CultureInfo.InvariantCulture) + "|" + a.TargetPosition.Y.ToString("R", CultureInfo.InvariantCulture) +
            "|" + a.Damage + "|" + a.WasLethal;
    }
}
