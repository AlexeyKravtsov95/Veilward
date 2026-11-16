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
    public sealed class ElementProgressionControllerTests
    {
        private const string ContentId = "eight-wave-unlocks-v1";
        private string _root;
        private WaveCheckpointStore _store;
        private readonly List<GameObject> _objects = new List<GameObject>();
        [SetUp] public void SetUp()
        {
            _root = Path.GetFullPath(Path.Combine("TestResults", "ElementProgressionController", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(_root); _store = new WaveCheckpointStore(_root);
        }
        [TearDown] public void TearDown()
        {
            foreach (var go in _objects) Object.DestroyImmediate(go);
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [TestCase("flame")] [TestCase("gale")]
        public void RegBattle177_ExitBeforeAndAfterConfirm_RestoresExactlyOneWholeSnapshot(string start)
        {
            var s = BattleElementUnlockTests.Native(); var battle = Controller(s, out var cp);
            Assert.That(cp.SaveCurrentState(), Is.True);
            Assert.That(Load(s).state.elementProgression.pendingChoiceId, Is.EqualTo("start-resonance"));
            Assert.That(battle.ChooseElement(new ChooseElementCommand("start-resonance", start)).Success, Is.True);
            Assert.That(Load(s).state.elementProgression.startResonanceId, Is.EqualTo(start));
            Reach(battle, cp, 3);
            var pending = Load(s); var pendingBytes = File.ReadAllBytes(_store.PrimaryPath);
            Assert.That(pending.state.elementProgression.pendingChoiceId, Is.EqualTo("after-wave-3"));
            for (var repeat = 0; repeat < 2; repeat++)
            {
                var fresh = BattleElementUnlockTests.Native(); fresh.RestoreCheckpoint(Load(s).state);
                Assert.That(fresh.State.Gold, Is.EqualTo(pending.state.gold));
                Assert.That(fresh.State.ElementProgression.ChoicesEarned, Is.EqualTo(1));
                Assert.That(File.ReadAllBytes(_store.PrimaryPath), Is.EqualTo(pendingBytes));
            }
            Assert.That(battle.ChooseElement(new ChooseElementCommand("after-wave-3", "tide")).Success, Is.True);
            var confirmed = Load(s);
            Assert.That(confirmed.state.phase, Is.EqualTo(BattlePhase.WaveComplete));
            Assert.That(confirmed.state.elementProgression.pendingChoiceId, Is.EqualTo("none"));
            Assert.That(confirmed.state.elementProgression.choicesSpent, Is.EqualTo(1));
            Assert.That(battle.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.Continued));
            var preparation = Load(s);
            Assert.That(preparation.state.waveIndex, Is.EqualTo(3));
            Assert.That(preparation.state.phase, Is.EqualTo(BattlePhase.Preparation));
            var bytes = File.ReadAllBytes(_store.PrimaryPath);
            Assert.That(battle.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.RejectedInvalidPhase));
            Assert.That(File.ReadAllBytes(_store.PrimaryPath), Is.EqualTo(bytes));
        }

        [Test]
        public void RegBattle178_InitialSaveFailure_KeepsPendingAndBlocksStartUntilRetry()
        {
            var s = BattleElementUnlockTests.Native(); var battle = Controller(s, out var cp);
            FailNextWrite(); Assert.That(cp.SaveCurrentState(), Is.False);
            Assert.That(s.State.ElementProgression.PendingChoiceId, Is.EqualTo("start-resonance"));
            Assert.That(battle.StartWave(), Is.EqualTo(WaveTransitionResult.RejectedPendingElementChoice));
            Assert.That(File.Exists(_store.PrimaryPath), Is.False);
            Directory.Delete(_store.TempPath); Assert.That(cp.SaveCurrentState(), Is.True);
            Assert.That(Load(s).state.elementProgression.pendingChoiceId, Is.EqualTo("start-resonance"));
        }

        [TestCase(3)] [TestCase(6)]
        public void RegBattle178_CompletionAndConfirmFailures_KeepLiveChoice_RetryOnlySaves(int wave)
        {
            var s = BattleElementUnlockTests.Native(); var battle = Controller(s, out var cp);
            battle.ChooseElement(new ChooseElementCommand("start-resonance", "flame"));
            Reach(battle, cp, 3, wave == 6);
            if (wave == 6)
            {
                battle.ChooseElement(new ChooseElementCommand("after-wave-3", "tide")); battle.ContinueToNextWave();
                Reach(battle, cp, 6, false);
            }
            // Сначала доказать failure самого completion snapshot, затем Confirm без смены фазы.
            var pending = s.ExportCheckpoint();
            FailNextWrite(); Assert.That(cp.ObservePhaseTransition(), Is.False);
            Assert.That(battle.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.RejectedPendingElementChoice));
            Directory.Delete(_store.TempPath); Assert.That(cp.SaveCurrentState(), Is.True);
            var durable = File.ReadAllBytes(_store.PrimaryPath);
            var option = wave == 3 ? "tide" : "gale";
            FailNextWrite();
            Assert.That(battle.ChooseElement(new ChooseElementCommand("after-wave-" + wave, option)).Success, Is.True);
            Assert.That(cp.LastSaveSucceeded, Is.False);
            var live = JsonUtility.ToJson(s.ExportCheckpoint());
            Assert.That(s.State.ElementProgression.PendingChoiceId, Is.EqualTo("none"));
            Assert.That(File.ReadAllBytes(_store.PrimaryPath), Is.EqualTo(durable));
            LogAssert.Expect(LogType.Error, new Regex("Checkpoint save failed:.*"));
            Assert.That(battle.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.RejectedInvalidPhase));
            var restart = BattleElementUnlockTests.Native(); restart.RestoreCheckpoint(Load(s).state);
            Assert.That(restart.State.ElementProgression.PendingChoiceId, Is.EqualTo("after-wave-" + wave));
            Assert.That(restart.State.Gold, Is.EqualTo(pending.gold));
            Directory.Delete(_store.TempPath); Assert.That(cp.SaveCurrentState(), Is.True);
            Assert.That(JsonUtility.ToJson(s.ExportCheckpoint()), Is.EqualTo(live));
            Assert.That(battle.ChooseElement(new ChooseElementCommand("after-wave-" + wave, option)).Success, Is.False);
            Assert.That(JsonUtility.ToJson(Load(s).state), Is.EqualTo(live));
            Assert.That(battle.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.Continued));
        }

        [Test]
        public void RegBattle178_ReadOnlyEligibility_RejectedCommandsNeverTouchStorage()
        {
            var s = BattleElementUnlockTests.Native(); var battle = Controller(s, out _);
            Directory.CreateDirectory(_store.TempPath);
            Assert.That(battle.StartWave(), Is.EqualTo(WaveTransitionResult.RejectedPendingElementChoice));
            Assert.That(battle.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.RejectedInvalidPhase));
            Assert.That(File.Exists(_store.PrimaryPath), Is.False);
            LogAssert.NoUnexpectedReceived();
        }


        [TestCase("write")] [TestCase("readback")] [TestCase("replace")]
        public void RegBattle178_TemporaryAndReplaceFailures_KeepDurableFiles(string failure)
        {
            var writer = new FaultWriter(); _store = new WaveCheckpointStore(_root, writer);
            var s = BattleElementUnlockTests.Native(); var battle = Controller(s, out var cp);
            cp.SaveCurrentState(); battle.ChooseElement(new ChooseElementCommand("start-resonance", "flame"));
            var primary = File.ReadAllBytes(_store.PrimaryPath); var backup = File.ReadAllBytes(_store.BackupPath);
            writer.Failure = failure; writer.FailAtWrite = writer.Writes + 1;
            LogAssert.Expect(LogType.Error, new Regex(failure == "readback" ? "Checkpoint temporary file verification failed:.*" : "Checkpoint save failed:.*"));
            Assert.That(cp.SaveCurrentState(), Is.False);
            Assert.That(File.ReadAllBytes(_store.PrimaryPath), Is.EqualTo(primary));
            Assert.That(File.ReadAllBytes(_store.BackupPath), Is.EqualTo(backup));
            writer.Failure = null;
            Assert.That(cp.SaveCurrentState(), Is.True);
            Assert.That(s.State.ElementProgression.StartResonanceId, Is.EqualTo("flame"));
        }

        [Test]
        public void RegBattle178_PostNextSaveFailure_KeepsLivePreparationAndBlocksStartUntilRetry()
        {
            var writer = new FaultWriter(); _store = new WaveCheckpointStore(_root, writer);
            var s = BattleElementUnlockTests.Native(); var battle = Controller(s, out var cp);
            battle.ChooseElement(new ChooseElementCommand("start-resonance", "flame")); Reach(battle, cp, 3);
            battle.ChooseElement(new ChooseElementCommand("after-wave-3", "tide"));
            writer.FailAtWrite = writer.Writes + 2; writer.Failure = "write";
            LogAssert.Expect(LogType.Error, new Regex("Checkpoint save failed:.*"));
            Assert.That(battle.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.Continued));
            Assert.That(cp.LastSaveSucceeded, Is.False);
            Assert.That(s.State.Phase, Is.EqualTo(BattlePhase.Preparation)); Assert.That(s.State.WaveIndex, Is.EqualTo(3));
            Assert.That(Load(s).state.phase, Is.EqualTo(BattlePhase.WaveComplete));
            var before = JsonUtility.ToJson(s.ExportCheckpoint()); var bytes = File.ReadAllBytes(_store.PrimaryPath);
            LogAssert.Expect(LogType.Error, new Regex("Checkpoint save failed:.*"));
            Assert.That(battle.StartWave(), Is.EqualTo(WaveTransitionResult.RejectedInvalidPhase));
            Assert.That(battle.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.RejectedInvalidPhase));
            Assert.That(File.ReadAllBytes(_store.PrimaryPath), Is.EqualTo(bytes));
            writer.Failure = null; Assert.That(cp.SaveCurrentState(), Is.True);
            Assert.That(JsonUtility.ToJson(s.ExportCheckpoint()), Is.EqualTo(before));
            Assert.That(battle.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
        }

        [TestCase(4)] [TestCase(7)]
        public void RegBattle179_180_CombatRollbackAfterKillAndProc_RetainsUnlocksAndRepeatsPrefix(int wave)
        {
            var fixture = BattleElementUnlockTests.Native();
            BattleElementUnlockTests.Choose(fixture, "start-resonance", "flame");
            BattleElementUnlockTests.Reach(fixture, 3); BattleElementUnlockTests.Choose(fixture, "after-wave-3", "tide");
            fixture.ContinueToNextWave();
            if (wave == 7)
            {
                BattleElementUnlockTests.Reach(fixture, 6); BattleElementUnlockTests.Choose(fixture, "after-wave-6", "gale");
                fixture.ContinueToNextWave();
            }
            var s = ElementProgressionStorageTests.Config().CreateSimulation(BattleElementUnlockTests.Rules());
            s.RestoreCheckpoint(fixture.ExportCheckpoint());
            var battle = Controller(s, out var cp);
            foreach (var x in new[] { -5, -4, -3 })
                Assert.That(battle.PlaceTower(new TowerDefinitionId("steam"), new GridCoordinate(x, -3)).Success, Is.True);
            Assert.That(cp.SaveCurrentState(), Is.True);
            var safe = JsonUtility.ToJson(s.ExportCheckpoint()); var bytes = File.ReadAllBytes(_store.PrimaryPath);
            string prefix = null;
            for (var repeat = 0; repeat < 2; repeat++)
            {
                Assert.That(battle.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
                var procs = 0; var ticks = 0;
                while ((s.State.Kills == fixture.State.Kills || procs == 0) && s.State.Phase == BattlePhase.Combat && ticks++ < 2000)
                { s.Step(); procs += s.ReactionEvents.Count; s.ClearAttackEvents(); s.ClearDamageEvents(); s.ClearReactionEvents(); }
                Assert.That(s.State.Kills, Is.GreaterThan(fixture.State.Kills)); Assert.That(procs, Is.GreaterThan(0));
                var current = BattleElementUnlockTests.Snapshot(s);
                if (repeat == 0) prefix = current; else Assert.That(current, Is.EqualTo(prefix));
                Assert.That(_store.TryLoad(ContentId, s.TryValidateCheckpoint, out var load), Is.True, load.Error);
                cp.RestoreCheckpoint(load.Envelope);
                Assert.That(JsonUtility.ToJson(s.ExportCheckpoint()), Is.EqualTo(safe));
                var fresh = ElementProgressionStorageTests.Config().CreateSimulationForCheckpoint(load.Envelope);
                fresh.RestoreCheckpoint(load.Envelope.state);
                Assert.That(JsonUtility.ToJson(fresh.ExportCheckpoint()), Is.EqualTo(safe));
                Assert.That(fresh.State.Enemies, Is.Empty); Assert.That(s.AttackEvents, Is.Empty);
                Assert.That(s.DamageEvents, Is.Empty); Assert.That(s.ReactionEvents, Is.Empty);
                // Первый Start сохраняет safe snapshot ещё раз; сам Load не переписывает bytes.
                Assert.That(File.ReadAllBytes(_store.PrimaryPath), Is.EqualTo(bytes));
            }
        }

        private sealed class FaultWriter : IWaveCheckpointWriter
        {
            private readonly FileWaveCheckpointWriter _real = new FileWaveCheckpointWriter();
            public int Writes; public int FailAtWrite; public string Failure;
            public void WriteTemporary(string path, string json)
            {
                Writes++;
                if (Writes >= FailAtWrite && Failure == "write") throw new IOException("Injected temporary write failure");
                _real.WriteTemporary(path, Writes == FailAtWrite && Failure == "readback" ? "{broken" : json);
            }
            public void Commit(string temporary, string primary, string backup)
            {
                if (Writes == FailAtWrite && Failure == "replace") throw new IOException("Injected replace failure");
                _real.Commit(temporary, primary, backup);
            }
        }

        private BattleSceneController Controller(BattleSimulation s, out BattleCheckpointController cp)
        {
            var go = new GameObject("Native checkpoint test"); _objects.Add(go);
            cp = go.AddComponent<BattleCheckpointController>(); cp.Configure(_store, s, ContentId);
            var battle = go.AddComponent<BattleSceneController>();
            Set(battle, "_simulation", s); Set(battle, "checkpointController", cp);
            return battle;
        }
        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private WaveCheckpointEnvelope Load(BattleSimulation s)
        {
            Assert.That(_store.TryLoad(ContentId, s.TryValidateCheckpoint, out var result), Is.True, result.Error);
            return result.Envelope;
        }
        private void FailNextWrite()
        {
            Directory.CreateDirectory(_store.TempPath);
            LogAssert.Expect(LogType.Error, new Regex("Checkpoint save failed:.*"));
        }
        private static void Reach(BattleSceneController battle, BattleCheckpointController cp, int wave, bool saveFinal = true)
        {
            var s = battle.Simulation;
            while (true)
            {
                Assert.That(battle.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
                for (var ticks = 0; ticks < 100 && s.State.Phase == BattlePhase.Combat; ticks++) s.Step();
                if (s.State.WaveIndex + 1 == wave && !saveFinal) return;
                Assert.That(cp.ObservePhaseTransition(), Is.True);
                if (s.State.WaveIndex + 1 == wave) return;
                Assert.That(battle.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.Continued));
            }
        }
    }
}
