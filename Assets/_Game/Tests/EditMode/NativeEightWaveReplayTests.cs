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
using Veilward.Presentation;

namespace Veilward.Tests.EditMode
{
    public sealed class NativeEightWaveReplayTests
    {
        public const string Output = "TestResults/P2.3e";
        [Serializable] public sealed class Script { public string route, start, after3, after6; public Command[] commands; }
        [Serializable] public sealed class Command { public int wave, x, y, id; public string op, type; }
        [Serializable] public sealed class Proc { public string kind, trigger; public int wave; public long tick; public int tower; }
        [Serializable] public sealed class Wave { public int wave; public BattleCheckpoint state; public int[] pair = new int[2], intrinsic = new int[2]; }
        [Serializable] public sealed class Report
        {
            public string rulesId = "wave-unlocks-v1", route, outcome, traceSha256;
            public int startingGold = 150, startingHp = 20, entries;
            public string[] receipts;
            public Wave[] waves;
            public Proc[] firstProcs;
            public string[] entitlements;
            public string[] coverage;
        }
        public sealed class Run
        {
            public BattleSimulation sim;
            public Report report = new Report();
            public List<string> trace = new List<string>(), receipts = new List<string>(), entitlements = new List<string>();
            public List<Wave> waves = new List<Wave>(); public List<Proc> first = new List<Proc>();
        }
        [TestCase("A")] [TestCase("B")] [TestCase("Stone")]
        public void RegBattle186_NaturalProduction_FullTraceDeterministic(string route)
        {
            var a = Replay(route); var b = Replay(route);
            Assert.That(b.trace, Is.EqualTo(a.trace), "Сравнение каждого tick/command/event/grid/progression");
            Assert.That(JsonUtility.ToJson(b.report), Is.EqualTo(JsonUtility.ToJson(a.report)));
            var expected = JsonUtility.FromJson<Report>(File.ReadAllText("Docs/review/P2.3e/" + route + ".summary.json"));
            Assert.That(JsonUtility.ToJson(a.report), Is.EqualTo(JsonUtility.ToJson(expected)), "Committed natural outcome/trace, включая FAIL coverage, не переписывается автоматически");
            Directory.CreateDirectory(Output);
            foreach (var pair in new[] { ("first", a), ("second", b) })
            {
                var bytes = Encoding.UTF8.GetBytes(string.Join("\n", pair.Item2.trace) + "\n");
                using (var file = File.Create(Output + "/" + route + "." + pair.Item1 + ".trace.gz"))
                using (var zip = new GZipStream(file, System.IO.Compression.CompressionLevel.Optimal)) zip.Write(bytes, 0, bytes.Length);
            }
            File.WriteAllText(Output + "/" + route + ".summary.json", JsonUtility.ToJson(a.report, true) + "\n");
        }
        public static Script LoadScript(string route) => JsonUtility.FromJson<Script>(File.ReadAllText("Docs/review/P2.3e/" + route + ".commands.json"));
        public static BattleScenarioConfig Config() => AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWave.asset");
        public static Run Replay(string route, Action<Run, string> stable = null)
        {
            var script = LoadScript(route); var s = Config().CreateSimulation(); var r = new Run { sim = s };
            r.report.route = route;
            Assert.That(s.State.Gold, Is.EqualTo(150)); Assert.That(s.State.BaseHealth, Is.EqualTo(20));
            Capture(r); Choose(r, "start-resonance", script.start); ProbeClosed(r);
            while (s.State.Phase == BattlePhase.Preparation)
            {
                var wave = new Wave { wave = s.State.WaveIndex + 1 };
                ProbeClosed(r);
                foreach (var command in script.commands.Where(c => c.wave == wave.wave)) Execute(r, command);
                stable?.Invoke(r, "preparation");
                var gold = s.State.Gold; var kills = s.State.Kills;
                Receipt(r, "Start=" + s.StartWave());
                var guard = 0;
                while (s.State.Phase == BattlePhase.Combat && guard++ < 20000)
                {
                    s.Step();
                    foreach (var e in s.ReactionEvents)
                    {
                        var index = (int)e.Kind;
                        Assert.That(s.State.ElementProgression.UnlockedElements, Does.Contain(ElementKind.Tide));
                        Assert.That(s.State.ElementProgression.UnlockedElements, Does.Contain(e.Kind == ReactionKind.Steam ? ElementKind.Flame : ElementKind.Gale));
                        Assert.That(wave.wave, Is.GreaterThanOrEqualTo(4));
                        if (e.TriggerKind == ReactionTriggerKind.Intrinsic) wave.intrinsic[index]++; else wave.pair[index]++;
                        if (!r.first.Any(p => p.kind == e.Kind.ToString() && p.trigger == e.TriggerKind.ToString()))
                            r.first.Add(new Proc { kind = e.Kind.ToString(), trigger = e.TriggerKind.ToString(), wave = wave.wave, tick = e.Tick, tower = e.SourceTowerId.Value });
                    }
                    Capture(r); Clear(s);
                }
                Assert.That(guard, Is.LessThan(20000));
                Assert.That(s.State.Gold, Is.EqualTo(gold + (s.State.Kills - kills) * 5 + (s.State.Phase == BattlePhase.Defeat ? 0 : 20)));
                wave.state = s.ExportCheckpoint(); r.waves.Add(wave); stable?.Invoke(r, "completed");
                if (s.State.Phase != BattlePhase.WaveComplete) break;
                var pending = s.State.ElementProgression.PendingChoiceId;
                if (pending != "none")
                {
                    var before = Snapshot(s); Assert.That(s.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.RejectedPendingElementChoice));
                    Assert.That(Snapshot(s), Is.EqualTo(before)); Receipt(r, "Next=pending-rejected");
                    Choose(r, pending, wave.wave == 3 ? script.after3 : script.after6);
                    stable?.Invoke(r, "confirmed"); ProbeClosed(r);
                }
                Receipt(r, "Next=" + s.ContinueToNextWave());
                var next = Snapshot(s); Assert.That(s.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.RejectedInvalidPhase));
                Assert.That(Snapshot(s), Is.EqualTo(next)); Receipt(r, "Next=repeat-rejected");
            }
            Assert.That(s.State.Phase, Is.EqualTo(BattlePhase.Defeat).Or.EqualTo(BattlePhase.Victory));
            var terminal = Snapshot(s); s.Step(); Assert.That(Snapshot(s), Is.EqualTo(terminal));
            r.report.outcome = s.State.Phase.ToString(); r.report.waves = r.waves.ToArray(); r.report.firstProcs = r.first.ToArray();
            r.report.receipts = r.receipts.ToArray(); r.report.entitlements = r.entitlements.ToArray(); r.report.entries = r.trace.Count;
            r.report.coverage = new[] { "Steam/StatusPair", "Storm/StatusPair", "Steam/Intrinsic", "Storm/Intrinsic" }.Select(key =>
                key + ":" + (r.first.Any(p => p.kind + "/" + p.trigger == key) ? "PASS" : "FAIL: zero production proc; reached wave " + (s.State.WaveIndex + 1))).ToArray();
            using (var hash = SHA256.Create()) r.report.traceSha256 = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", r.trace) + "\n"))).Replace("-", "").ToLowerInvariant();
            return r;
        }
        private static void Choose(Run r, string id, string element)
        {
            var s = r.sim; var gold = s.State.Gold;
            Assert.That(s.ChooseElement(new ChooseElementCommand(id, element)).Success, Is.True);
            Assert.That(s.State.Gold, Is.EqualTo(gold)); Receipt(r, "Choose " + id + "=" + element);
            var before = Snapshot(s);
            Assert.That(s.ChooseElement(new ChooseElementCommand(id, element)).Success, Is.False);
            Assert.That(s.ChooseElement(new ChooseElementCommand("start-resonance", element)).Success, Is.False);
            Assert.That(Snapshot(s), Is.EqualTo(before)); Receipt(r, "Confirm repeat/stale=rejected");
            foreach (var t in s.TowerCatalog.Archetypes.Where(t => t.ShowInCatalog && t.RequiredElements.All(e => s.State.ElementProgression.UnlockedElements.Contains(e))))
                if (!r.entitlements.Any(x => x.StartsWith(t.Id.Value + "|", StringComparison.Ordinal))) r.entitlements.Add(t.Id.Value + "|wave=" + (s.State.WaveIndex + 1) + "|tick=" + s.State.Tick + "|" + s.State.Phase);
        }
        private static void ProbeClosed(Run r)
        {
            var s = r.sim;
            foreach (var t in s.TowerCatalog.Archetypes.Where(t => t.RequiredElements.Any(e => !s.State.ElementProgression.UnlockedElements.Contains(e))))
            {
                var before = Snapshot(s); var result = s.PlaceTower(new PlaceTowerCommand(t.Id, new GridCoordinate(-4, 0)));
                Assert.That(result.Success, Is.False); Assert.That(Snapshot(s), Is.EqualTo(before));
                Receipt(r, "Locked Place " + t.Id.Value + "=" + result.FailureReason);
            }
        }
        public static void Execute(Run r, Command c)
        {
            var s = r.sim; var before = Snapshot(s); var gold = s.State.Gold;
            var tower = s.State.Towers.SingleOrDefault(t => t.Id.Value == c.id);
            var investment = tower?.InvestedGold ?? 0; var price = 0; var refund = 0; bool success; string result;
            if (c.op == "place")
            {
                price = s.TowerCatalog.Get(new TowerDefinitionId(c.type)).Levels[0].Price;
                var receipt = s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId(c.type), new GridCoordinate(c.x, c.y)));
                success = receipt.Success; result = receipt.FailureReason.ToString();
                if (success) { Assert.That(receipt.Tower.InvestedGold, Is.EqualTo(price)); tower = receipt.Tower; }
            }
            else
            {
                if (c.op == "upgrade" && tower != null && tower.Level < s.TowerCatalog.Get(tower.Definition.Id).Levels.Count) price = s.TowerCatalog.Get(tower.Definition.Id).Levels[tower.Level].Price;
                var receipt = c.op == "sell" ? s.SellTower(new TowerId(c.id)) : s.UpgradeTower(new TowerId(c.id));
                success = receipt.Success; result = receipt.FailureReason.ToString(); refund = receipt.Refund;
                if (success && c.op == "sell") Assert.That(refund, Is.EqualTo(investment * 70 / 100));
                if (success && c.op == "upgrade") Assert.That(tower.InvestedGold, Is.EqualTo(investment + price));
            }
            if (success) Assert.That(s.State.Gold, Is.EqualTo(gold - price + refund));
            else Assert.That(Snapshot(s), Is.EqualTo(before));
            Assert.That(s.State.Gold, Is.GreaterThanOrEqualTo(0));
            var resolvedId = tower?.Id.Value ?? c.id;
            var remaining = s.State.Towers.SingleOrDefault(t => t.Id.Value == resolvedId)?.InvestedGold ?? 0;
            Receipt(r, c.op + " " + (tower?.Definition.Id.Value ?? c.type) + " id=" + resolvedId + " (" + (tower?.Coordinate.X ?? c.x) + "," + (tower?.Coordinate.Y ?? c.y) + ")=" + success + "/" + result + " gold=" + gold + "->" + s.State.Gold + " price=" + price + " investment=" + investment + "->" + remaining + " refund=" + refund);
        }
        private static void Receipt(Run r, string value) { r.trace.Add("C|" + value); r.receipts.Add("wave=" + (r.sim.State.WaveIndex + 1) + " tick=" + r.sim.State.Tick + "|" + value); Capture(r); }
        private static void Capture(Run r) => r.trace.Add(Snapshot(r.sim));
        public static void Clear(BattleSimulation s) { s.ClearAttackEvents(); s.ClearDamageEvents(); s.ClearReactionEvents(); }
        private static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);
        private static string P(PathPosition p) => F(p.X) + "," + F(p.Y);
        public static string Snapshot(BattleSimulation s)
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
                // Полный schema3 snapshot, включая progression и следующие IDs.
                var json = JsonUtility.ToJson(s.ExportCheckpoint());
                b.Append("|CP:").Append(json);
            }
            var p = s.State.ElementProgression;
            b.Append("|P:").Append(p.RulesId).Append(',').Append(p.StartResonanceId).Append(',').Append(p.ChoicesEarned).Append(',').Append(p.ChoicesSpent).Append(',').Append(p.PendingChoiceId);
            foreach (var e in p.UnlockedElements) b.Append("|U:").Append(e);
            foreach (var h in p.SpentChoices) b.Append("|H:").Append(h.ChoiceId).Append(',').Append(h.ElementKind);
            return b.ToString();
        }
    }
}
