using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Veilward.Domain;
using Veilward.Infrastructure;
using Veilward.Presentation;
using Object = UnityEngine.Object;

namespace Veilward.Tests.EditMode
{
    public sealed class NativeEightWaveFileTests
    {
        private const string Native = "eight-wave-unlocks-v1";
        private string AuditOutput => TestContext.CurrentContext.Test.Name.StartsWith("RegBattle189", StringComparison.Ordinal) ? NativeStrategyReplayTests.Output : NativeEightWaveReplayTests.Output;
        private string root;
        private WaveCheckpointStore store;
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<string> audit = new List<string>();
        private BattleScenarioConfig Config => NativeEightWaveReplayTests.Config();
        [SetUp] public void SetUp() { root = Path.Combine(Path.GetTempPath(), "veilward-p23e-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); store = new WaveCheckpointStore(root); }
        [TearDown] public void TearDown()
        {
            Directory.CreateDirectory(AuditOutput);
            File.WriteAllLines(AuditOutput + "/" + TestContext.CurrentContext.Test.Name + ".audit.txt", audit);
            foreach (var go in objects) Object.DestroyImmediate(go); objects.Clear(); audit.Clear();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
        [TestCase("A")] [TestCase("B")]
        public void RegBattle187_NaturalMilestones_FileRestoreFailuresAndCombatRollback(string route)
        {
            var reached = new HashSet<string>();
            NativeEightWaveReplayTests.Replay(route, (run, point) =>
            {
                var s = run.sim; var wave = s.State.WaveIndex + 1;
                Save(s); var expected = Wire(s.ExportCheckpoint());
                for (var repeat = 0; repeat < 2; repeat++)
                {
                    var restored = Load(); Assert.That(Wire(restored.ExportCheckpoint()), Is.EqualTo(expected));
                    audit.Add("PASS full snapshot Load/no write " + route + " wave=" + wave + " point=" + point + " repeat=" + repeat + " state=" + expected);
                }
                if (point == "completed" && s.State.Phase == BattlePhase.WaveComplete && (wave == 3 || wave == 6))
                {
                    FailedConfirm(s, wave == 3 ? NativeEightWaveReplayTests.LoadScript(route).after3 : NativeEightWaveReplayTests.LoadScript(route).after6);
                    reached.Add("pending" + wave);
                }
                if (point == "confirmed") reached.Add("confirmed" + wave);
                if (point == "preparation" && (wave == 4 || wave == 7))
                {
                    if (Rollback(s)) reached.Add("rollback" + wave);
                    else audit.Add("BLOCKED Combat" + wave + " kill+proc prefix absent before natural terminal");
                }
            });
            foreach (var key in new[] { "pending3", "confirmed3", "pending6", "confirmed6", "rollback4", "rollback7" })
                audit.Add((reached.Contains(key) ? "PASS " : "BLOCKED natural route did not cover ") + key);
        }
        [TestCase("A")] [TestCase("B")]
        public void RegBattle187_FileContinuedFullReplay_EqualsUninterrupted(string route)
        {
            var control = NativeEightWaveReplayTests.Replay(route);
            var continued = NativeEightWaveReplayTests.Replay(route, (run, point) =>
            {
                var s = run.sim;
                if (s.State.Phase == BattlePhase.Defeat || s.State.Phase == BattlePhase.Victory) return;
                Save(s); var e = LoadEnvelope();
                var battle = Controller(s, out var cp); cp.RestoreCheckpoint(e);
                audit.Add("PASS actual safe restore into replay wave=" + (s.State.WaveIndex + 1) + " point=" + point);
            });
            Assert.That(continued.trace, Is.EqualTo(control.trace));
            Assert.That(Wire(continued.report), Is.EqualTo(Wire(control.report)));
            audit.Add("PASS full continued trace SHA256=" + continued.report.traceSha256);
        }
        [Test] public void RegBattle186_NaturalEconomyUpgradeSell_UsesActualInvestment()
        {
            var covered = false;
            NativeEightWaveReplayTests.Replay("A", (run, point) =>
            {
                if (point != "completed" || run.sim.State.WaveIndex != 2) return;
                Save(run.sim); var s = Load();
                Assert.That(s.ChooseElement(new ChooseElementCommand("after-wave-3", "tide")).Success, Is.True);
                Assert.That(s.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.Continued));
                var r = new NativeEightWaveReplayTests.Run { sim = s };
                NativeEightWaveReplayTests.Execute(r, new NativeEightWaveReplayTests.Command { op = "sell", id = 2 });
                NativeEightWaveReplayTests.Execute(r, new NativeEightWaveReplayTests.Command { op = "upgrade", id = 1 });
                Assert.That(s.State.Towers.Single().Level, Is.EqualTo(2));
                NativeEightWaveReplayTests.Execute(r, new NativeEightWaveReplayTests.Command { op = "sell", id = 1 });
                var before = Wire(s.ExportCheckpoint());
                NativeEightWaveReplayTests.Execute(r, new NativeEightWaveReplayTests.Command { op = "sell", id = 1 });
                Assert.That(Wire(s.ExportCheckpoint()), Is.EqualTo(before));
                audit.AddRange(r.receipts); covered = true;
            });
            Assert.That(covered, Is.True);
        }
        private void FailedConfirm(BattleSimulation source, string option)
        {
            var s = Load(); var battle = Controller(s, out var cp);
            var durable = File.ReadAllBytes(store.PrimaryPath); var old = Wire(source.ExportCheckpoint());
            Directory.CreateDirectory(store.TempPath);
            LogAssert.Expect(LogType.Error, new Regex("Checkpoint save failed:.*"));
            Assert.That(battle.ChooseElement(new ChooseElementCommand(s.State.ElementProgression.PendingChoiceId, option)).Success, Is.True);
            Assert.That(cp.LastSaveSucceeded, Is.False);
            Assert.That(File.ReadAllBytes(store.PrimaryPath), Is.EqualTo(durable));
            Assert.That(Wire(Load().ExportCheckpoint()), Is.EqualTo(old), "Restart получает полный durable pending");
            var live = Wire(s.ExportCheckpoint());
            LogAssert.Expect(LogType.Error, new Regex("Checkpoint save failed:.*"));
            Assert.That(battle.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.RejectedInvalidPhase));
            Directory.Delete(store.TempPath); Assert.That(cp.RetrySave(), Is.True);
            Assert.That(Wire(s.ExportCheckpoint()), Is.EqualTo(live));
            Assert.That(Wire(Load().ExportCheckpoint()), Is.EqualTo(live));
            Assert.That(battle.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.Continued));
            var next = Wire(s.ExportCheckpoint()); var bytes = File.ReadAllBytes(store.PrimaryPath);
            Assert.That(battle.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.RejectedInvalidPhase));
            Assert.That(Wire(s.ExportCheckpoint()), Is.EqualTo(next));
            Assert.That(File.ReadAllBytes(store.PrimaryPath), Is.EqualTo(bytes));
            audit.Add("PASS failed Confirm/restart/retry-only/Next-once wave=" + (source.State.WaveIndex + 1) + " durable=" + old + " live=" + live);
            Save(source);
        }
        private bool Rollback(BattleSimulation preparation)
        {
            var s = Load(); var battle = Controller(s, out var cp); var expected = Wire(s.ExportCheckpoint());
            var envelope = LoadEnvelope(); var first = new List<string>(); var second = new List<string>();
            bool covered = false;
            for (var repeat = 0; repeat < 2; repeat++)
            {
                Assert.That(battle.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
                var bytes = File.ReadAllBytes(store.PrimaryPath); var sawProc = false; var trace = repeat == 0 ? first : second;
                trace.Add(NativeEightWaveReplayTests.Snapshot(s));
                while (s.State.Phase == BattlePhase.Combat && s.State.Tick < preparation.State.Tick + 20000)
                {
                    s.Step(); sawProc |= s.ReactionEvents.Count > 0 && s.DamageEvents.Any(d => d.Origin == DamageOrigin.Reaction && d.AppliedHpLoss > 0);
                    trace.Add(NativeEightWaveReplayTests.Snapshot(s));
                    if (s.State.Kills > preparation.State.Kills && sawProc && s.State.Phase == BattlePhase.Combat) { covered = true; break; }
                    NativeEightWaveReplayTests.Clear(s);
                }
                if (!covered) return false;
                Assert.That(cp.ObservePhaseTransition(), Is.True); Assert.That(File.ReadAllBytes(store.PrimaryPath), Is.EqualTo(bytes));
                cp.RestoreCheckpoint(envelope);
                Assert.That(Wire(s.ExportCheckpoint()), Is.EqualTo(expected));
                Assert.That(s.State.Enemies, Is.Empty); Assert.That(s.AttackEvents, Is.Empty); Assert.That(s.DamageEvents, Is.Empty); Assert.That(s.ReactionEvents, Is.Empty);
                Assert.That(Wire(Load().ExportCheckpoint()), Is.EqualTo(expected));
            }
            Assert.That(second, Is.EqualTo(first));
            audit.Add("PASS Combat rollback wave=" + (s.State.WaveIndex + 1) + " prefix entries=" + first.Count + " two identical attempts; whole state=" + expected);
            Directory.CreateDirectory(AuditOutput);
            File.WriteAllLines(AuditOutput + "/" + TestContext.CurrentContext.Test.Name + ".rollback" + (s.State.WaveIndex + 1) + ".txt", first);
            return true;
        }
        [TestCase("A-coverage")] [TestCase("B-coverage")]
        public void RegBattle189_NaturalLateMilestonesAndWholeContinuedTrace(string route)
        {
            var reached = new HashSet<string>();
            var script = NativeStrategyReplayTests.Load(route);
            var earlyWave = route.StartsWith("A", StringComparison.Ordinal) ? 4 : 5;
            var control = NativeEightWaveReplayTests.ReplayScript(script);
            var continued = NativeEightWaveReplayTests.ReplayScript(script, (run, point) =>
            {
                var s = run.sim; var wave = s.State.WaveIndex + 1;
                if (s.State.Phase == BattlePhase.Victory) return;
                Save(s); var expected = Wire(s.ExportCheckpoint());
                for (var repeat=0; repeat<2; repeat++)
                    Assert.That(Wire(Load().ExportCheckpoint()), Is.EqualTo(expected));
                audit.Add("PASS no-write whole snapshot wave="+wave+" point="+point+" state="+expected);
                if (point == "completed" && (wave == 3 || wave == 6))
                {
                    FailedConfirm(s,wave==3?script.after3:script.after6); reached.Add("pending"+wave);
                }
                if (point == "confirmed") reached.Add("confirmed"+wave);
                if (point == "preparation" && (wave == earlyWave || wave == 7))
                {
                    Assert.That(Rollback(s), Is.True, "Обязательный natural kill+positive proc prefix");
                    reached.Add("rollback"+wave);
                }
                var envelope=LoadEnvelope(); var battle=Controller(s,out var cp); cp.RestoreCheckpoint(envelope);
                Assert.That(Wire(s.ExportCheckpoint()),Is.EqualTo(expected));
            });
            Assert.That(continued.trace,Is.EqualTo(control.trace));
            foreach(var key in new[]{"pending3","confirmed3","pending6","confirmed6","rollback"+earlyWave,"rollback7"})
            { Assert.That(reached,Does.Contain(key)); audit.Add("PASS required natural "+key); }
            audit.Add("PASS full continued trace SHA256="+continued.report.traceSha256);
        }
        [TestCase(1)] [TestCase(2)]
        public void RegBattle187_LegacyMigrationAndBidirectionalBackup_PreserveWholeSnapshot(int schema)
        {
            // Synthetic historical input: legacy army is a migration fixture, never production playable evidence.
            var seed = Config.CreateSimulation().ExportCheckpoint();
            var e = new WaveCheckpointEnvelope { schemaVersion = schema, scenarioContentId = "eight-wave-v1", state = seed };
            var legacy = Config.CreateHistoricalAllOpenSimulation();
            var state = legacy.ExportCheckpoint(); state.gold = 1000; legacy.RestoreCheckpoint(state);
            foreach (var type in schema == 1 ? new[] { "test-tower" } : new[] { "steam", "storm" })
                Assert.That(legacy.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId(type), new GridCoordinate(-5 + legacy.State.Towers.Count, -3))).Success, Is.True);
            Assert.That(legacy.UpgradeTower(new TowerId(1)).Success, Is.True);
            var expected = Wire(legacy.ExportCheckpoint());
            e.state = legacy.ExportCheckpoint(); e.state.elementProgression = null;
            if (schema == 1) foreach (var t in e.state.towers) t.towerTypeId = null;
            File.WriteAllText(store.PrimaryPath, Wire(e));
            Assert.That(Wire(Load().ExportCheckpoint()), Is.EqualTo(expected));
            var native = Config.CreateSimulation(); native.ChooseElement(new ChooseElementCommand("start-resonance", "gale"));
            foreach (var backupNative in new[] { false, true })
            {
                var backup = backupNative ? Envelope(native) : e;
                File.WriteAllText(store.BackupPath, Wire(backup));
                var primary = WaveCheckpointJson.Deserialize(Wire(backupNative ? e : Envelope(native)));
                primary.state.gold = -1; File.WriteAllText(store.PrimaryPath, Wire(primary));
                var load = LoadEnvelope(); Assert.That(load.scenarioContentId, Is.EqualTo(backup.scenarioContentId));
                Assert.That(Wire(Load().ExportCheckpoint()), Is.EqualTo(backupNative ? Wire(native.ExportCheckpoint()) : expected));
                audit.Add("PASS synthetic schema=" + schema + " corrupt primary -> " + (backupNative ? "native" : "legacy") + " whole backup=" + Wire(load));
            }
        }
        private static string Wire(object value)
        {
            if (!(value is WaveCheckpointEnvelope e)) return JsonUtility.ToJson(value);
            if (e.schemaVersion == 4) return WaveCheckpointJson.Serialize(e);
            var json = JsonUtility.ToJson(e);
            // Реальный legacy shape: нет progression, а не inline defaults от Unity null serialization.
            if (e.schemaVersion < 3 && e.state.elementProgression == null)
                json = json.Substring(0, json.IndexOf(",\"elementProgression\":", StringComparison.Ordinal)) + "}}";
            return json;
        }
        private WaveCheckpointEnvelope Envelope(BattleSimulation s) => new WaveCheckpointEnvelope { schemaVersion = WaveCheckpointStore.CurrentSchemaVersion, scenarioContentId = s.State.ElementProgression.RulesId == "wave-unlocks-v1" ? Native : "eight-wave-v1", state = s.ExportCheckpoint() };
        private void Save(BattleSimulation s) => Assert.That(store.TrySave(Envelope(s), s.TryValidateCheckpoint, out var error, Config.CreateSimulationForCheckpoint), Is.True, error);
        private WaveCheckpointEnvelope LoadEnvelope()
        {
            var primary = File.ReadAllBytes(store.PrimaryPath); var backup = File.Exists(store.BackupPath) ? File.ReadAllBytes(store.BackupPath) : null;
            Assert.That(store.TryLoad(Native, Config.CreateSimulationForCheckpoint, out var load), Is.True, load.Error);
            Assert.That(File.ReadAllBytes(store.PrimaryPath), Is.EqualTo(primary));
            if (backup != null) Assert.That(File.ReadAllBytes(store.BackupPath), Is.EqualTo(backup));
            return load.Envelope;
        }
        private BattleSimulation Load() { var e = LoadEnvelope(); var s = Config.CreateSimulationForCheckpoint(e); s.RestoreCheckpoint(e.state); return s; }
        private BattleSceneController Controller(BattleSimulation s, out BattleCheckpointController cp)
        {
            var go = new GameObject("P2.3e file controller"); objects.Add(go);
            var battle = go.AddComponent<BattleSceneController>(); cp = go.AddComponent<BattleCheckpointController>();
            cp.Configure(store, s, Native, Config.CreateSimulationForCheckpoint);
            Set(battle, "_simulation", s); Set(battle, "checkpointController", cp); return battle;
        }
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
