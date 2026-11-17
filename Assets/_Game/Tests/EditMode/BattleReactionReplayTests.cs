using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Veilward.Domain;
using Veilward.Infrastructure;
using Veilward.Presentation;

namespace Veilward.Tests.EditMode
{
    // Измерительный observer существует только в tests; не добавляет combat log в production.
    public sealed class BattleReactionReplayTests
    {
        private const string ConfigPath = "Assets/_Game/Configs/EightWave.asset";
        private const string Output = "TestResults/P2.2f";
        private const string Evidence = "Docs/review/P2.2f/replay-summary.json";
        private const int Guard = 20000;

        [TestCase("historical")]
        [TestCase("matched")]
        [TestCase("steam")]
        [TestCase("storm")]
        public void RegBattle161_FullTimelines_AreDeterministic(string strategy)
        {
            var first = Run(strategy, false);
            var second = Run(strategy, false);
            Assert.That(second.lines, Is.EqualTo(first.lines), "Каждый tick/receipt/event, а не только terminal");
            Assert.That(JsonUtility.ToJson(second.summary), Is.EqualTo(JsonUtility.ToJson(first.summary)));
            Assert.That(first.summary.terminal, Is.EqualTo("Defeat").Or.EqualTo("Victory"));
            if (strategy == "steam" || strategy == "storm")
            {
                var kind = strategy == "steam" ? 0 : 1;
                Assert.That(first.summary.waves.Sum(w => w.intrinsicProcs[kind]), Is.GreaterThan(0));
                Assert.That(first.summary.receipts, Has.Some.Contains("Sell"));
                Assert.That(first.summary.receipts, Has.Some.Contains("Upgrade"));
            }
            WriteTrace(strategy, first.lines);
        }

        [Test]
        public void RegBattle160_FileRollbackAfterReactionKill_ReplaysPrefixAndFullMatch()
        {
            var control = Run("matched", false);
            var continued = Run("matched", true);
            Assert.That(continued.interrupted, Is.True, "Нужен настоящий reaction kill, не direct/Burn");
            Assert.That(continued.roundTrips, Is.GreaterThan(2));
            Assert.That(continued.lines, Is.EqualTo(control.lines));
            Assert.That(JsonUtility.ToJson(continued.summary), Is.EqualTo(JsonUtility.ToJson(control.summary)));
        }

        [Test]
        public void RegBattle162_HistoricalAndImpactEvidence_MatchesCommittedResults()
        {
            // Этот тест сравнивает весь прежний JSON byte-for-byte, включая прежние числа/команды.
            new BattleElementalReplayTests().RegBattle126_CommittedBalanceEvidence_MatchesReplay();
            var runs = new[] { "historical", "matched", "steam", "storm" }.Select(s => Run(s, false)).ToArray();
            foreach (var r in runs) WriteTrace(r.summary.strategy, r.lines);
            Assert.That(runs[1].summary.waves.Sum(w => w.procs.Sum()), Is.GreaterThan(0), "Zero-proc impact coverage FAIL");
            var historical = JsonUtility.FromJson<BattleElementalReplayTests.Evidence>(File.ReadAllText("Docs/review/P2.1e/replay-summary.json"));
            Assert.That(runs[0].summary.waves.Length, Is.EqualTo(historical.waves.Length));
            for (var i = 0; i < historical.waves.Length; i++)
            {
                var a = runs[0].summary.waves[i]; var b = historical.waves[i];
                Assert.That(new long[] { a.tick, a.health, a.gold, a.kills, a.leaks },
                    Is.EqualTo(new long[] { b.tick, b.health, b.gold, b.kills, b.leaks }));
            }
            var common = runs[0].summary.receipts.Zip(runs[1].summary.receipts, (a, b) => a == b).TakeWhile(x => x).Count();
            var report = new Report { series = runs.Select(r => r.summary).ToArray(),
                identicalReceiptPrefix = common, historicalReceiptCount = runs[0].summary.receipts.Length,
                matchedReceiptCount = runs[1].summary.receipts.Length,
                identicalAvailabilityPrefix = runs[0].summary.receipts.Zip(runs[1].summary.receipts, (a, b) => Availability(a) == Availability(b)).TakeWhile(x => x).Count(),
                firstReceiptDivergence = runs[0].summary.receipts.Skip(common).Take(1).Concat(runs[1].summary.receipts.Skip(common).Take(1)).ToArray(),
                configHashes = new[] { HashFile(ConfigPath), HashFile("Assets/_Game/Configs/TestWave.asset") } };
            var json = JsonUtility.ToJson(report, true) + "\n";
            Directory.CreateDirectory(Output); File.WriteAllText(Output + "/replay-summary.actual.json", json);
            Assert.That(File.Exists(Evidence), Is.True, "Сначала сохранить фактические измерения, без изменения исхода");
            AssertMeasurementsEqual(JsonUtility.FromJson<Report>(File.ReadAllText(Evidence)), report);
        }

        [Test]
        public void RegBattle162_EvidenceComparison_AllowsHostOsButRejectsGameplayDrift()
        {
            var expected = new Report { series = new[] { Run("matched", false).summary } };
            var actual = JsonUtility.FromJson<Report>(JsonUtility.ToJson(expected));
            actual.os = "Another macOS runner";
            AssertMeasurementsEqual(expected, actual);
            Assert.That(actual.os, Is.EqualTo("Another macOS runner"), "Comparison must not rewrite actual host metadata");
            actual.series[0].waves[0].health--;
            Assert.Throws<AssertionException>(() => AssertMeasurementsEqual(expected, actual), "HP drift must still fail");
        }

        private static void AssertMeasurementsEqual(Report expected, Report actual)
        {
            Assert.That(actual.os, Is.Not.Empty, "Actual evidence must retain the measured host OS");
            // ОС — metadata измерения, не часть deterministic combat contract. Копии сохраняют actual report.
            var expectedCopy = JsonUtility.FromJson<Report>(JsonUtility.ToJson(expected));
            var actualCopy = JsonUtility.FromJson<Report>(JsonUtility.ToJson(actual));
            expectedCopy.os = actualCopy.os = string.Empty;
            Assert.That(JsonUtility.ToJson(actualCopy, true), Is.EqualTo(JsonUtility.ToJson(expectedCopy, true)));
        }

        [TestCase("steam")]
        [TestCase("storm")]
        public void RegBattle162_SuppressionObserver_DistinguishesCooldownAndNoNeighbor(string strategy)
        {
            var production = Config().CreateHistoricalAllOpenSimulation();
            var enemy = new EnemyDefinition(new EnemyDefinitionId("synthetic-armored"), 1000, .1f, 1, 5, 7);
            var s = new BattleSimulation(new GridMapDefinition(10, 3, new GridCoordinate(0, -1), 1),
                new PathDefinition(new[] { new GridCoordinate(0, 0), new GridCoordinate(100, 0) }),
                new WaveDefinition(enemy, 3, 0, 30), production.TowerCatalog, 20, 150, production.ReactionCatalog);
            var r = new Replay(); var w = new Wave { enemyHp = 1000 };
            Place(s, strategy, 0, 1, w, r); Transition(s, true, r);
            var losses = new Dictionary<int, int>(); var dead = new HashSet<int>();
            for (var i = 0; i < 100; i++) ObserveStep(s, w, r, losses, dead);
            var k = strategy == "steam" ? 0 : 1;
            Assert.That(w.procs[k], Is.GreaterThan(0)); Assert.That(w.targets[k], Is.GreaterThan(0));
            Assert.That(w.cooldownSuppressions[k], Is.GreaterThan(0));
            Assert.That(w.noNeighborSuppressions[1], Is.EqualTo(strategy == "storm" ? 2 : 0));
            Assert.That(w.applied[0], Is.EqualTo(strategy == "steam" ? 33 : 10));
            Assert.That(w.applied[2], Is.EqualTo(strategy == "steam" ? 44 : 1));
            w.wave = 1; w.tick = s.State.Tick; w.ticks = w.tick; w.outcome = s.State.Phase.ToString();
            w.health = s.State.BaseHealth; w.gold = s.State.Gold; w.kills = s.State.Kills;
            w.spawned = 3; w.live = s.State.Enemies.Count; w.liveRemainingHp = s.State.Enemies.Sum(e => e.Health);
            w.investment = s.State.Towers.Sum(t => t.InvestedGold); w.commands = r.receipts.ToArray();
            Assert.That(w.applied.Sum() + w.liveRemainingHp, Is.EqualTo(3000));
            WriteTrace("synthetic-armor-" + strategy, r.lines);
            File.WriteAllText(Output + "/synthetic-armor-" + strategy + ".json", JsonUtility.ToJson(w, true) + "\n");
        }

        [Serializable] public sealed class Report
        {
            public string rules = "p2.2-steam-storm-v1";
            public string baseCommit = "8159f4c530bea98ec8bc495f3168299342e12831";
            public string os = SystemInfo.operatingSystem;
            public string buildTarget = "iOS; Editor Mono tests; player configured IL2CPP/ARM64, not executed";
            public string[] originOrder = { "Direct", "Burn", "Reaction" };
            public string[] reactionOrder = { "Steam", "Storm" };
            public string unity = "6000.3.28f1 (8f082d2fe476)";
            public string rng = "Absent; fixed 30 Hz, stable IDs, First";
            public string commandSource = "BattleReactionReplayTests.Prepare; historical/matched = BattleElementalReplayTests.Prepare commands including repeated Sell";
            public string[] configHashes;
            public int identicalReceiptPrefix, identicalAvailabilityPrefix, historicalReceiptCount, matchedReceiptCount;
            public string[] firstReceiptDivergence;
            public Series[] series;
        }
        [Serializable] public sealed class Series
        {
            public string strategy, terminal, timelineSha256;
            public int startingGold = 150, startingHealth = 20, timelineEntries;
            public string[] receipts;
            public Wave[] waves;
        }
        [Serializable] public sealed class Wave
        {
            public int wave, health, gold, kills, leaks, spawned, live, investment, purchases, refunds;
            public long startTick, tick, ticks;
            public string outcome;
            public string[] commands;
            public long[] applied = new long[3], overkill = new long[3]; // Direct/Burn/Reaction
            public int[] lethal = new int[3], procs = new int[2], targets = new int[2], intrinsicProcs = new int[2];
            public int[] cooldownSuppressions = new int[2], noNeighborSuppressions = new int[2], reactionLethal = new int[2];
            public long[] reactionApplied = new long[2], reactionOverkill = new long[2];
            public int leakedRemainingHp, liveRemainingHp, enemyHp = 100;
        }
        private sealed class Replay
        {
            public Series summary = new Series();
            public List<string> lines = new List<string>();
            public List<string> receipts = new List<string>();
            public bool interrupted;
            public int roundTrips;
        }

        private static string Availability(string receipt) => receipt.Split('|')[3];

        private static BattleScenarioConfig Config() => AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>(ConfigPath);
        private static BattleSimulation Create(string strategy) => strategy == "historical"
            ? Config().CreateHistoricalAllOpenSimulation(ReactionCatalog.Empty) : Config().CreateHistoricalAllOpenSimulation();

        private static Replay Run(string strategy, bool restore)
        {
            var s = Create(strategy); var r = new Replay(); r.summary.strategy = strategy;
            Assert.That(s.State.Gold, Is.EqualTo(150)); Assert.That(s.State.BaseHealth, Is.EqualTo(20));
            Assert.That(s.State.WaveCount, Is.EqualTo(8));
            var root = Path.Combine(Path.GetTempPath(), "veilward-p22f-" + Guid.NewGuid().ToString("N"));
            var store = new WaveCheckpointStore(root);
            var owner = new GameObject("P2.2f file checkpoint");
            var cp = owner.AddComponent<BattleCheckpointController>(); cp.Configure(store, s, "eight-wave-v1");
            var waves = new List<Wave>();
            try
            {
                Capture(r, s);
                while (s.State.Phase == BattlePhase.Preparation)
                {
                    var w = new Wave { wave = s.State.WaveIndex + 1, startTick = s.State.Tick };
                    var commandStart = r.receipts.Count;
                    Prepare(s, strategy, w, r);
                    Assert.That(cp.SaveForStartWave(), Is.True);
                    if (restore)
                    {
                        s = RoundTrip(s, strategy, cp, store, r);
                        if (!r.interrupted) TryInterrupt(ref s, strategy, cp, store, r);
                    }
                    var oldHealth = s.State.BaseHealth; var oldGold = s.State.Gold; var oldKills = s.State.Kills;
                    var oldNextEnemy = s.ExportCheckpoint().nextEnemyId;
                    var investmentBeforeCombat = s.State.Towers.Sum(t => t.InvestedGold);
                    Transition(s, true, r); Assert.That(cp.ObservePhaseTransition(), Is.True);
                    var losses = new Dictionary<int, int>(); var dead = new HashSet<int>();
                    var guard = 0;
                    while (s.State.Phase == BattlePhase.Combat && guard++ < Guard) ObserveStep(s, w, r, losses, dead);
                    Assert.That(guard, Is.LessThan(Guard)); Assert.That(cp.ObservePhaseTransition(), Is.True);
                    w.tick = s.State.Tick; w.ticks = w.tick - w.startTick; w.outcome = s.State.Phase.ToString();
                    w.health = s.State.BaseHealth; w.gold = s.State.Gold; w.kills = s.State.Kills;
                    w.leaks = oldHealth - w.health; w.investment = s.State.Towers.Sum(t => t.InvestedGold);
                    Assert.That(w.investment, Is.EqualTo(investmentBeforeCombat));
                    w.live = s.State.Enemies.Count; w.liveRemainingHp = s.State.Enemies.Sum(e => e.Health);
                    w.spawned = s.ExportCheckpoint().nextEnemyId - oldNextEnemy;
                    Assert.That(w.spawned, Is.EqualTo(w.leaks + dead.Count + w.live));
                    Assert.That(w.applied.Sum() + w.leakedRemainingHp + w.liveRemainingHp, Is.EqualTo((long)w.spawned * w.enemyHp), "HP conservation");
                    Assert.That(w.lethal.Sum(), Is.EqualTo(s.State.Kills - oldKills));
                    Assert.That(w.gold, Is.EqualTo(oldGold + dead.Count * 5 + (w.outcome == "Defeat" ? 0 : 20)), "Единственная kill reward + bonus");
                    w.commands = r.receipts.Skip(commandStart).ToArray(); waves.Add(w);
                    if (restore && s.State.Phase == BattlePhase.WaveComplete)
                    {
                        s = RoundTrip(s, strategy, cp, store, r); s = RoundTrip(s, strategy, cp, store, r);
                    }
                    if (s.State.Phase != BattlePhase.WaveComplete) break;
                    Transition(s, false, r); Transition(s, false, r);
                }
                r.summary.terminal = s.State.Phase.ToString(); r.summary.waves = waves.ToArray();
                r.summary.receipts = r.receipts.ToArray(); r.summary.timelineEntries = r.lines.Count;
                var terminal = Snapshot(s); s.Step(); Assert.That(Snapshot(s), Is.EqualTo(terminal));
                r.summary.timelineSha256 = Hash(Encoding.UTF8.GetBytes(string.Join("\n", r.lines) + "\n"));
                return r;
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        private static void Prepare(BattleSimulation s, string strategy, Wave w, Replay r)
        {
            if (strategy == "historical" || strategy == "matched")
            {
                switch (w.wave)
                {
                    case 1: Place(s, "flame", -2, -1, w, r); Place(s, "gale", 1, 0, w, r); break;
                    case 3: Place(s, "tide", -1, 0, w, r); break;
                    case 5: Place(s, "stone", -3, -1, w, r); break;
                    case 7: Sell(s, 3, w, r); Sell(s, 3, w, r); Place(s, "tide", -1, 0, w, r); Upgrade(s, 5, w, r); break;
                }
            }
            else
            {
                // Отдельная фиксированная стратегия: обычные цены, без выдачи Gold/HP.
                if (w.wave == 1) Place(s, strategy, -2, -1, w, r);
                if (w.wave == 3) Upgrade(s, 2, w, r);
                if (w.wave == 2) { Sell(s, 1, w, r); Sell(s, 1, w, r); Place(s, strategy, -2, -1, w, r); }
            }
        }
        private static void Place(BattleSimulation s, string type, int x, int y, Wave w, Replay r)
        {
            var before = Snapshot(s); var gold = s.State.Gold; var next = s.ExportCheckpoint().nextTowerId;
            var result = s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId(type), new GridCoordinate(x, y)));
            if (result.Success)
            {
                var price = s.TowerCatalog.Get(new TowerDefinitionId(type)).Levels[0].Price;
                Assert.That(s.State.Gold, Is.EqualTo(gold - price)); Assert.That(result.Tower.InvestedGold, Is.EqualTo(price));
                Assert.That(result.Tower.Id.Value, Is.EqualTo(next)); w.purchases += price;
            }
            else Assert.That(Snapshot(s), Is.EqualTo(before));
            Receipt(r, s, "Place " + type + " (" + x + "," + y + ")=" + (result.Success ? "accepted id=" + result.Tower.Id.Value : "rejected " + result.FailureReason));
        }
        private static void Upgrade(BattleSimulation s, int id, Wave w, Replay r)
        {
            var before = Snapshot(s); var gold = s.State.Gold; var tower = s.State.Towers.SingleOrDefault(t => t.Id.Value == id);
            var invested = tower?.InvestedGold ?? 0; var cd = tower?.NextAttackTick ?? 0;
            var result = s.UpgradeTower(new TowerId(id));
            if (result.Success)
            {
                var price = tower.Definition.Price; w.purchases += price;
                Assert.That(s.State.Gold, Is.EqualTo(gold - price)); Assert.That(tower.InvestedGold, Is.EqualTo(invested + price));
                Assert.That(tower.NextAttackTick, Is.EqualTo(cd));
            }
            else Assert.That(Snapshot(s), Is.EqualTo(before));
            Receipt(r, s, "Upgrade " + id + "=" + (result.Success ? "accepted" : "rejected " + result.FailureReason));
        }
        private static void Sell(BattleSimulation s, int id, Wave w, Replay r)
        {
            var before = Snapshot(s); var gold = s.State.Gold; var tower = s.State.Towers.SingleOrDefault(t => t.Id.Value == id);
            var refund = (tower?.InvestedGold ?? 0) * 70 / 100;
            var result = s.SellTower(new TowerId(id));
            if (result.Success)
            {
                Assert.That(result.Refund, Is.EqualTo(refund)); Assert.That(s.State.Gold, Is.EqualTo(gold + refund)); w.refunds += refund;
                Assert.That(s.Grid.GetState(tower.Coordinate), Is.EqualTo(GridCellState.Available));
            }
            else Assert.That(Snapshot(s), Is.EqualTo(before));
            Receipt(r, s, "Sell " + id + "=" + (result.Success ? "accepted refund=" + refund : "rejected " + result.FailureReason));
        }
        private static void Transition(BattleSimulation s, bool start, Replay r)
        {
            var before = Snapshot(s); var gold = s.State.Gold; var kills = s.State.Kills;
            var result = start ? s.StartWave() : s.ContinueToNextWave();
            Assert.That(s.State.Gold, Is.EqualTo(gold)); Assert.That(s.State.Kills, Is.EqualTo(kills));
            if (result == WaveTransitionResult.RejectedInvalidPhase) Assert.That(Snapshot(s), Is.EqualTo(before));
            Receipt(r, s, (start ? "Start" : "Next") + "=" + result);
        }
        private static void Receipt(Replay r, BattleSimulation s, string command)
        {
            var text = "C|w=" + (s.State.WaveIndex + 1) + "|t=" + s.State.Tick + "|" + command + "|gold=" + s.State.Gold;
            r.receipts.Add(text); r.lines.Add(text); Capture(r, s);
        }

        private static void ObserveStep(BattleSimulation s, Wave w, Replay r, Dictionary<int, int> losses, HashSet<int> dead)
        {
            var before = s.State.Enemies.ToDictionary(e => e.Id.Value, e => new Shadow(e));
            var refs = s.State.Enemies.ToArray(); var kills = s.State.Kills; var gold = s.State.Gold;
            s.Step();
            var allRefs = refs.Concat(s.State.Enemies).GroupBy(e => e.Id.Value).ToDictionary(g => g.Key, g => g.First());
            foreach (var e in allRefs.Values)
            {
                Assert.That(e.Definition.MaxHealth, Is.EqualTo(w.enemyHp)); Assert.That(e.Definition.LeakDamage, Is.EqualTo(1));
                Assert.That(e.Definition.Reward, Is.EqualTo(5));
                if (!before.ContainsKey(e.Id.Value)) before[e.Id.Value] = new Shadow(e, true);
            }
            // Новый spawn может умереть в том же Step и отсутствовать в обоих state snapshots.
            foreach (var d in s.DamageEvents)
                if (!before.ContainsKey(d.TargetEnemyId.Value)) before[d.TargetEnemyId.Value] = new Shadow(d.TargetEnemyId.Value, w.enemyHp, d.TargetPosition);
            var lethalIds = s.DamageEvents.Where(d => d.WasLethal).Select(d => d.TargetEnemyId.Value).ToHashSet();
            foreach (var e in refs.Where(e => e.Health > 0 && !s.State.Enemies.Any(a => a.Id == e.Id)))
            {
                Assert.That(lethalIds.Contains(e.Id.Value), Is.False); w.leakedRemainingHp += e.Health; before.Remove(e.Id.Value);
            }
            foreach (var e in allRefs.Values) if (before.TryGetValue(e.Id.Value, out var shadow)) shadow.position = s.Path.GetPosition(e.DistanceAlongPath);
            var events = s.DamageEvents.OrderBy(d => d.SequenceInTick).ToArray();
            foreach (var d in events)
            {
                var shadow = before[d.TargetEnemyId.Value];
                Assert.That(d.AppliedHpLoss, Is.EqualTo(Math.Min(shadow.hp, d.ResolvedDamage)));
                Assert.That(d.HealthAfter, Is.EqualTo(shadow.hp - d.AppliedHpLoss));
                Assert.That(d.WasLethal, Is.EqualTo(d.HealthAfter == 0));
                shadow.hp = d.HealthAfter;
                var id = d.TargetEnemyId.Value; losses.TryGetValue(id, out var previous); losses[id] = previous + d.AppliedHpLoss;
                Assert.That(losses[id], Is.LessThanOrEqualTo(w.enemyHp));
                if (d.Origin == DamageOrigin.Reaction)
                {
                    Assert.That(d.ReactionKind.HasValue, Is.True); var k = (int)d.ReactionKind.Value;
                    w.reactionApplied[k] += d.AppliedHpLoss; w.reactionOverkill[k] += d.ResolvedDamage - d.AppliedHpLoss;
                    if (d.WasLethal) w.reactionLethal[k]++;
                }
                var origin = (int)d.Origin; w.applied[origin] += d.AppliedHpLoss; w.overkill[origin] += d.ResolvedDamage - d.AppliedHpLoss;
                if (d.WasLethal) { Assert.That(dead.Add(id), Is.True, "Один lethal/reward на ID"); Assert.That(losses[id], Is.EqualTo(w.enemyHp)); w.lethal[origin]++; }
                if (d.Origin == DamageOrigin.Direct)
                {
                    var attack = s.AttackEvents.Single(a => a.AttackerTowerId == d.SourceTowerId);
                    Assert.That(attack.WasLethal, Is.EqualTo(d.WasLethal)); Assert.That(attack.TargetEnemyId, Is.EqualTo(d.TargetEnemyId));
                    if (!d.WasLethal) ObserveCandidates(s, d, shadow, before, w);
                }
            }
            Assert.That(s.AttackEvents.Count, Is.EqualTo(events.Count(d => d.Origin == DamageOrigin.Direct)));
            foreach (var e in s.State.Enemies)
            {
                Assert.That(before[e.Id.Value].hp, Is.EqualTo(e.Health));
                Assert.That(before[e.Id.Value].steam, Is.EqualTo(e.LastSteamTick)); Assert.That(before[e.Id.Value].storm, Is.EqualTo(e.LastStormTick));
            }
            Assert.That(s.State.Kills - kills, Is.EqualTo(lethalIds.Count));
            Assert.That(s.State.Gold - gold, Is.EqualTo(lethalIds.Count * 5 + (s.State.Phase == BattlePhase.WaveComplete || s.State.Phase == BattlePhase.Victory ? 20 : 0)));
            Assert.That(s.State.Gold, Is.GreaterThanOrEqualTo(0));
            Capture(r, s); Clear(s);
        }

        private sealed class Shadow
        {
            public int id, hp;
            public PathPosition position;
            public bool burn, wet, gust;
            public long? steam, storm;
            public Shadow(int id, int hp, PathPosition p) { this.id = id; this.hp = hp; position = p; }
            public Shadow(EnemyState e, bool spawned = false) : this(e.Id.Value, spawned ? e.Definition.MaxHealth : e.Health, default)
            {
                burn = !spawned && e.Burn != null && e.Burn.RemainingTicks > 1;
                wet = !spawned && e.Wet != null && e.Wet.RemainingTicks > 1;
                gust = !spawned && e.Gust != null && e.Gust.RemainingTicks > 1;
                steam = spawned ? null : e.LastSteamTick; storm = spawned ? null : e.LastStormTick;
            }
        }
        private static bool Ready(Shadow e, ReactionKind k, long tick, int cooldown)
        {
            var last = k == ReactionKind.Steam ? e.steam : e.storm;
            return !last.HasValue || tick >= last.Value && tick - last.Value >= cooldown;
        }
        private static void ObserveCandidates(BattleSimulation s, DamageResolvedEvent direct, Shadow primary, Dictionary<int, Shadow> enemies, Wave w)
        {
            var tower = s.State.Towers.Single(t => t.Id == direct.SourceTowerId); var def = tower.Definition;
            var status = def.StatusEffect?.Kind;
            if (status == StatusKind.Burn) primary.burn = true;
            if (status == StatusKind.Wet) primary.wet = true;
            if (status == StatusKind.Gust) primary.gust = true;
            var intrinsic = def.IntrinsicReaction;
            var candidates = intrinsic != null ? new[] { intrinsic.Kind } : new[] { ReactionKind.Steam, ReactionKind.Storm }.Where(k =>
                k == ReactionKind.Steam ? (status == StatusKind.Burn || status == StatusKind.Wet) && primary.burn && primary.wet :
                (status == StatusKind.Wet || status == StatusKind.Gust) && primary.wet && primary.gust).ToArray();
            ReactionKind? predicted = null; var selected = new List<Shadow>();
            foreach (var kind in candidates)
            {
                if (!s.ReactionCatalog.TryGet(kind, out var global)) continue;
                if (!Ready(primary, kind, s.State.Tick, global.CooldownTicks)) { w.cooldownSuppressions[(int)kind]++; continue; }
                var payload = intrinsic ?? global.Payload;
                selected.Clear(); selected.Add(primary);
                var limit = kind == ReactionKind.Steam ? payload.TargetLimit : payload.TargetLimit + 1;
                while (selected.Count < limit)
                {
                    var origin = kind == ReactionKind.Steam ? primary : selected.Last();
                    var nearest = enemies.Values.Where(e => e.hp > 0 && !selected.Contains(e) && Ready(e, kind, s.State.Tick, global.CooldownTicks))
                        .Select(e => new { enemy = e, distance = Distance(origin.position, e.position) })
                        .Where(e => e.distance <= (double)payload.Range * payload.Range)
                        .OrderBy(e => e.distance).ThenBy(e => e.enemy.id).FirstOrDefault();
                    if (nearest == null) break;
                    selected.Add(nearest.enemy);
                }
                if (kind == ReactionKind.Storm && selected.Count == 1) { w.noNeighborSuppressions[1]++; continue; }
                predicted = kind; break;
            }
            var actual = s.ReactionEvents.Where(e => e.SourceTowerId == tower.Id).ToArray();
            if (!predicted.HasValue) { Assert.That(actual, Is.Empty); return; }
            Assert.That(actual.Length, Is.EqualTo(1)); var reaction = actual[0]; var k = (int)predicted.Value;
            Assert.That(reaction.Kind, Is.EqualTo(predicted.Value));
            Assert.That(reaction.TriggerKind, Is.EqualTo(intrinsic == null ? ReactionTriggerKind.StatusPair : ReactionTriggerKind.Intrinsic));
            var hits = s.DamageEvents.Where(d => d.ReactionSequenceInTick == reaction.SequenceInTick).OrderBy(d => d.HitIndex).ToArray();
            var victims = predicted == ReactionKind.Steam ? selected : selected.Skip(1).ToList();
            Assert.That(hits.Select(d => d.TargetEnemyId.Value), Is.EqualTo(victims.Select(e => e.id)), "Сверка геометрии/окон с captured events");
            foreach (var e in selected) { if (predicted == ReactionKind.Steam) e.steam = s.State.Tick; else e.storm = s.State.Tick; }
            w.procs[k]++; w.targets[k] += hits.Length; if (intrinsic != null) w.intrinsicProcs[k]++;
        }
        private static double Distance(PathPosition a, PathPosition b) { var x = (double)a.X - b.X; var y = (double)a.Y - b.Y; return x * x + y * y; }

        private static BattleSimulation RoundTrip(BattleSimulation s, string strategy, BattleCheckpointController cp, WaveCheckpointStore store, Replay r)
        {
            var expected = Snapshot(s); var bytes = File.ReadAllBytes(store.PrimaryPath);
            var restored = Create(strategy);
            Assert.That(store.TryLoad("eight-wave-v1", restored.TryValidateCheckpoint, out var loaded), Is.True);
            cp.Configure(store, restored, "eight-wave-v1"); cp.RestoreCheckpoint(loaded.Envelope);
            Assert.That(Snapshot(restored), Is.EqualTo(expected));
            Assert.That(File.ReadAllBytes(store.PrimaryPath), Is.EqualTo(bytes)); r.roundTrips++;
            return restored;
        }
        private static void TryInterrupt(ref BattleSimulation s, string strategy, BattleCheckpointController cp, WaveCheckpointStore store, Replay r)
        {
            var preparation = Snapshot(s); var saved = s.ExportCheckpoint(); var bytes = File.ReadAllBytes(store.PrimaryPath);
            var prefix = new List<string>(); var sawTimestamp = false; var reactionKill = false;
            Assert.That(s.StartWave(), Is.EqualTo(WaveTransitionResult.Started)); Assert.That(cp.ObservePhaseTransition(), Is.True);
            prefix.Add(Snapshot(s));
            for (var i = 0; i < Guard && s.State.Phase == BattlePhase.Combat; i++)
            {
                s.Step(); prefix.Add(Snapshot(s));
                sawTimestamp = s.State.Enemies.Any(e => e.LastSteamTick.HasValue || e.LastStormTick.HasValue);
                reactionKill = s.DamageEvents.Any(d => d.Origin == DamageOrigin.Reaction && d.WasLethal);
                if (reactionKill && sawTimestamp && s.State.Phase == BattlePhase.Combat) break;
                Clear(s);
            }
            // Пилот без подходящего kill не меняет контрольный матч: откатывается к тому же файлу.
            if (reactionKill && sawTimestamp && s.State.Phase == BattlePhase.Combat)
            {
                Assert.That(s.State.Gold, Is.GreaterThan(saved.gold)); Assert.That(s.State.Kills, Is.GreaterThan(saved.kills));
                Assert.That(s.AttackEvents, Is.Not.Empty); Assert.That(s.ReactionEvents, Is.Not.Empty); Assert.That(s.DamageEvents, Is.Not.Empty);
                Assert.That(cp.ObservePhaseTransition(), Is.True); Assert.That(File.ReadAllBytes(store.PrimaryPath), Is.EqualTo(bytes));
                var interruptedAt = s.State.Tick;
                // Restore на грязной simulation должен очистить все stream и transient enemies.
                Assert.That(store.TryLoad("eight-wave-v1", s.TryValidateCheckpoint, out var loaded), Is.True);
                cp.RestoreCheckpoint(loaded.Envelope); Assert.That(Snapshot(s), Is.EqualTo(preparation)); AssertTransientClean(s);
                for (var repeat = 0; repeat < 2; repeat++)
                {
                    Assert.That(s.StartWave(), Is.EqualTo(WaveTransitionResult.Started)); Assert.That(cp.ObservePhaseTransition(), Is.True);
                    Assert.That(Snapshot(s), Is.EqualTo(prefix[0]));
                    for (var i = 1; i < prefix.Count; i++)
                    {
                        s.Step(); Assert.That(Snapshot(s), Is.EqualTo(prefix[i])); Clear(s);
                    }
                    cp.RestoreCheckpoint(loaded.Envelope); Assert.That(Snapshot(s), Is.EqualTo(preparation)); AssertTransientClean(s);
                }
                r.interrupted = true;
                Directory.CreateDirectory(Output);
                File.WriteAllText(Output + "/rollback.txt", "wave=" + (s.State.WaveIndex + 1) + " interruptedTick=" + interruptedAt +
                    " prefixEntries=" + prefix.Count + " repeats=2\npreparation=" + preparation + "\n");
                WriteTrace("rollback-prefix", prefix);
            }
            else
            {
                Assert.That(store.TryLoad("eight-wave-v1", s.TryValidateCheckpoint, out var loaded), Is.True);
                cp.RestoreCheckpoint(loaded.Envelope); Assert.That(Snapshot(s), Is.EqualTo(preparation));
            }
        }
        private static void AssertTransientClean(BattleSimulation s)
        {
            Assert.That(s.State.Phase, Is.EqualTo(BattlePhase.Preparation)); Assert.That(s.State.Enemies, Is.Empty);
            Assert.That(s.AttackEvents, Is.Empty); Assert.That(s.ReactionEvents, Is.Empty); Assert.That(s.DamageEvents, Is.Empty);
        }
        private static void Clear(BattleSimulation s) { s.ClearAttackEvents(); s.ClearReactionEvents(); s.ClearDamageEvents(); }
        private static void Capture(Replay r, BattleSimulation s) => r.lines.Add(Snapshot(s));
        private static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);
        private static string P(PathPosition p) => F(p.X) + "," + F(p.Y);
        private static string Snapshot(BattleSimulation s)
        {
            var b = new StringBuilder("S|").Append(s.State.Phase).Append('|').Append(s.State.WaveIndex).Append('|').Append(s.State.Tick)
                .Append('|').Append(s.State.BaseHealth).Append('|').Append(s.State.Gold).Append('|').Append(s.State.Kills);
            foreach (var t in s.State.Towers) b.Append("|T:").Append(t.Id.Value).Append(',').Append(t.Definition.Id.Value).Append(',')
                .Append(t.Level).Append(',').Append(t.Coordinate.X).Append(',').Append(t.Coordinate.Y).Append(',').Append(t.InvestedGold).Append(',').Append(t.NextAttackTick);
            foreach (var e in s.State.Enemies)
            {
                b.Append("|E:").Append(e.Id.Value).Append(',').Append(e.Definition.Id.Value).Append(',').Append(e.Health).Append(',').Append(F(e.DistanceAlongPath))
                    .Append(",steam=").Append(e.LastSteamTick).Append(",storm=").Append(e.LastStormTick);
                foreach (var st in e.Statuses) b.Append(";status=").Append(st.Kind).Append(',').Append(st.Power).Append(',').Append(st.RemainingTicks)
                    .Append(',').Append(st.NextDamageTick).Append(',').Append(st.DamageIntervalTicks).Append(',').Append(st.SourceTowerId.Value);
            }
            for (var y = s.Grid.Map.Origin.Y; y < s.Grid.Map.Origin.Y + s.Grid.Map.Height; y++)
                for (var x = s.Grid.Map.Origin.X; x < s.Grid.Map.Origin.X + s.Grid.Map.Width; x++) b.Append('|').Append((int)s.Grid.GetState(new GridCoordinate(x, y)));
            foreach (var a in s.AttackEvents) b.Append("|A:").Append(a.Tick).Append(',').Append(a.AttackerTowerId.Value).Append(',').Append(a.TargetEnemyId.Value)
                .Append(',').Append(P(a.TargetPosition)).Append(',').Append(a.Damage).Append(',').Append(a.WasLethal);
            foreach (var e in s.ReactionEvents) b.Append("|R:").Append(e.Tick).Append(',').Append(e.SequenceInTick).Append(',').Append(e.Kind).Append(',').Append(e.TriggerKind)
                .Append(',').Append(e.SourceTowerId.Value).Append(',').Append(e.PrimaryEnemyId.Value).Append(',').Append(P(e.PrimaryPosition)).Append(',').Append(e.CauseFirstKind)
                .Append(',').Append(e.CauseSecondKind).Append(',').Append(F(e.RadiusOrHopRange)).Append(',').Append(e.TargetLimit);
            foreach (var d in s.DamageEvents) b.Append("|D:").Append(d.Tick).Append(',').Append(d.SequenceInTick).Append(',').Append(d.Origin).Append(',').Append(d.SourceTowerId.Value)
                .Append(',').Append(d.TargetEnemyId.Value).Append(',').Append(P(d.FromPosition)).Append(',').Append(P(d.TargetPosition)).Append(',').Append(d.ConfiguredBaseDamage)
                .Append(',').Append(d.RawDamage).Append(',').Append(d.ResolvedDamage).Append(',').Append(d.AppliedHpLoss).Append(',').Append(d.HealthAfter).Append(',').Append(d.WasLethal)
                .Append(',').Append(d.ReactionKind).Append(',').Append(d.ReactionSequenceInTick).Append(',').Append(d.HitIndex);
            if (s.State.Phase != BattlePhase.Combat)
            {
                // Исторический trace хранит schema 2 shape; progression проверяется отдельным suite P2.3c.
                var json = JsonUtility.ToJson(s.ExportCheckpoint());
                b.Append("|CP:").Append(json.Substring(0, json.IndexOf(",\"elementProgression\":", StringComparison.Ordinal))).Append('}');
            }
            return b.ToString();
        }
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        private static string HashFile(string path)
        {
            var bytes = File.ReadAllBytes(path);
            if (path == ConfigPath)
            {
                // Исторический combat hash исключает только новую progression metadata/content ID.
                var yaml = Encoding.UTF8.GetString(bytes).Replace("eight-wave-unlocks-v1", "eight-wave-v1");
                var progression = yaml.IndexOf("  progression:\n", StringComparison.Ordinal);
                if (progression >= 0) yaml = yaml.Substring(0, progression);
                bytes = Encoding.UTF8.GetBytes(yaml);
            }
            return path + ":" + Hash(bytes);
        }
        private static void WriteTrace(string strategy, List<string> lines)
        {
            Directory.CreateDirectory(Output);
            using (var file = File.Create(Output + "/" + strategy + ".timeline.txt.gz"))
            using (var zip = new GZipStream(file, System.IO.Compression.CompressionLevel.Optimal))
            {
                var bytes = Encoding.UTF8.GetBytes(string.Join("\n", lines) + "\n"); zip.Write(bytes, 0, bytes.Length);
            }
        }
    }
}
