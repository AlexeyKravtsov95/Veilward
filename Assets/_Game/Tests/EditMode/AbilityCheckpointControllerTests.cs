using System;
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

namespace Veilward.Tests.EditMode
{
    public sealed class AbilityCheckpointControllerTests
    {
        [Test]
        public void RegBattle203_RollbackCastKillPositiveProcTwiceAndFullContinuedTrace()
        {
            var root = Path.Combine("TestResults", "AbilityRollback", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            var go = new GameObject("rollback checkpoint");
            try
            {
                var s = AbilityCheckpointTests.Create(enemyHp: 1000); AbilityCheckpointTests.Choose(s);
                s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId("flame"), new GridCoordinate(0, 1)));
                for (var wave = 1; wave <= 3; wave++) { Attempt(s, false); if (wave < 3) s.ContinueToNextWave(); }
                AbilityCheckpointTests.Choose(s); s.ContinueToNextWave();
                s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId("tide"), new GridCoordinate(1, 1)));
                var cp = s.ExportCheckpoint(); cp.ability = new AbilityCheckpoint("rift-seal-v1", "rift-seal", 421, 3); s.RestoreCheckpoint(cp);
                var store = new WaveCheckpointStore(root);
                var controller = go.AddComponent<BattleCheckpointController>(); controller.Configure(store, s, AbilityCheckpointTests.Content);
                Assert.That(controller.SaveForStartWave(), Is.True);
                var durable = File.ReadAllBytes(store.PrimaryPath); var before = AbilityCheckpointTests.Snapshot(s);
                var control = AbilityCheckpointTests.Create(enemyHp: 1000); control.RestoreCheckpoint(cp);
                var controlTrace = Attempt(control, true) + Continue(control);
                var first = Attempt(s, true, stopAfterPrefix: true);
                Assert.That(s.State.Phase, Is.EqualTo(BattlePhase.Combat));
                Assert.That(s.State.Kills, Is.GreaterThan(cp.kills)); Assert.That(s.State.Gold, Is.GreaterThan(cp.gold));
                Assert.That(s.AbilityState.NextCastSequence, Is.EqualTo(4)); Assert.That(s.AbilityEvents, Is.Not.Empty);
                Assert.That(s.DamageEvents, Is.Not.Empty); Assert.That(s.State.Enemies, Is.Not.Empty);
                Assert.That(controller.ObserveCommandCompletion(), Is.True);
                Assert.That(File.ReadAllBytes(store.PrimaryPath), Is.EqualTo(durable), "Combat must not be saved");
                for (var attempt = 0; attempt < 2; attempt++)
                {
                    Assert.That(store.TryLoad(AbilityCheckpointTests.Content, s.TryValidateCheckpoint, out var load), Is.True, load.Error);
                    if (attempt == 0)
                    {
                        var bad = load.Envelope.state.DeepCopy(); bad.ability = new AbilityCheckpoint("rift-seal-v1", "rift-seal", 601, 4);
                        var dirty = BattleAbilityTests.FullSnapshot(s);
                        Assert.Throws<ArgumentException>(() => controller.RestoreCheckpoint(new WaveCheckpointEnvelope
                        { schemaVersion = 4, scenarioContentId = AbilityCheckpointTests.Content, state = bad }));
                        Assert.That(BattleAbilityTests.FullSnapshot(s), Is.EqualTo(dirty));
                    }
                    else { s = AbilityCheckpointTests.Create(enemyHp: 1000); controller.Configure(store, s, AbilityCheckpointTests.Content); }
                    var restores = 0; void Restored() { restores++; }
                    controller.Restored += Restored; controller.RestoreCheckpoint(load.Envelope); controller.Restored -= Restored;
                    Assert.That(restores, Is.EqualTo(1)); Assert.That(AbilityCheckpointTests.Snapshot(s), Is.EqualTo(before));
                    Assert.That(s.AbilityEvents, Is.Empty); Assert.That(s.DamageEvents, Is.Empty); Assert.That(s.ReactionEvents, Is.Empty); Assert.That(s.AttackEvents, Is.Empty);
                    var prefix = Attempt(s, true, stopAfterPrefix: true); Assert.That(prefix, Is.EqualTo(first));
                    var continued = prefix + Attempt(s, false, alreadyStarted: true, castAlready: true) + Continue(s);
                    Assert.That(continued, Is.EqualTo(controlTrace)); Assert.That(s.State.Phase, Is.EqualTo(BattlePhase.Victory));
                    Assert.That(File.ReadAllBytes(store.PrimaryPath), Is.EqualTo(durable));
                }
                Directory.CreateDirectory("TestResults/P2.4c");
                File.WriteAllText("TestResults/P2.4c/rollback-prefix.trace", first);
                File.WriteAllText("TestResults/P2.4c/rollback-control.trace", controlTrace);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); Directory.Delete(root, true); }
        }
        private static string Continue(BattleSimulation s)
        {
            var trace = new System.Text.StringBuilder();
            while (s.State.Phase != BattlePhase.Victory && s.State.Phase != BattlePhase.Defeat)
            {
                if (s.State.ElementProgression.HasPendingChoice) AbilityCheckpointTests.Choose(s);
                Assert.That(s.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.Continued)); trace.Append(Attempt(s, false));
            }
            return trace.ToString();
        }
        private static string Attempt(BattleSimulation s, bool requireProc, bool stopAfterPrefix = false,
            bool alreadyStarted = false, bool castAlready = false)
        {
            var trace = new System.Text.StringBuilder(); var castKill = castAlready; var proc = castAlready;
            s.ClearAttackEvents(); s.ClearReactionEvents(); s.ClearDamageEvents(); s.ClearAbilityEvents();
            if (!alreadyStarted) Assert.That(s.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
            for (var n = 0; n < 20000 && s.State.Phase == BattlePhase.Combat; n++)
            {
                if (!castKill && s.AbilityState.CooldownRemainingTicks == 0 && s.State.Enemies.Any(e => e.Health <= 120))
                {
                    var enemy = s.State.Enemies.First(e => e.Health <= 120); var kills = s.State.Kills;
                    var command = new CastAbilityCommand(AbilityDefinitionId.RiftSeal, s.Path.GetPosition(enemy.DistanceAlongPath), s.State.WaveIndex, s.State.Tick, s.AbilityState.NextCastSequence);
                    Assert.That(s.CastAbility(command).Succeeded, Is.True); castKill = s.State.Kills > kills;
                }
                else s.Step();
                proc |= s.DamageEvents.Any(e => e.Origin == DamageOrigin.Reaction && e.AppliedHpLoss > 0);
                trace.Append(BattleAbilityTests.FullSnapshot(s)).Append('\n');
                if (stopAfterPrefix && castKill && proc)
                {
                    Assert.That(s.State.Phase, Is.EqualTo(BattlePhase.Combat), "rollback evidence must interrupt live Combat");
                    return trace.ToString();
                }
                s.ClearAttackEvents(); s.ClearReactionEvents(); s.ClearDamageEvents(); s.ClearAbilityEvents();
            }
            Assert.That(stopAfterPrefix, Is.False, "required interrupted prefix was not reached");
            Assert.That(s.State.Phase, Is.Not.EqualTo(BattlePhase.Combat)); Assert.That(castKill, Is.True, "positive ability kill required");
            if (requireProc) Assert.That(proc, Is.True, "positive reaction required"); return trace.ToString();
        }

        private sealed class FailingWriter : IWaveCheckpointWriter
        {
            public int WritesUntilFailure;
            private readonly FileWaveCheckpointWriter _files = new FileWaveCheckpointWriter();
            public void WriteTemporary(string path, string json)
            {
                if (--WritesUntilFailure == 0) throw new IOException("synthetic save failure");
                _files.WriteTemporary(path, json);
            }
            public void Commit(string temporary, string primary, string backup) => _files.Commit(temporary, primary, backup);
        }
        [TestCase(false)] [TestCase(true)]
        public void RegBattle202_FailedChooseOrSecondNextSaveRetainsWholeAbilityState(bool next)
        {
            var root = Path.Combine("TestResults", "AbilityRetry", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            var go = new GameObject("checkpoint");
            try
            {
                var s = AbilityCheckpointTests.Create(enemyHp: 1000); AbilityCheckpointTests.Choose(s);
                s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId("flame"), new GridCoordinate(0, 1)));
                for (var wave = 1; wave <= 3; wave++) { Attempt(s, false); if (wave < 3) s.ContinueToNextWave(); }
                var remaining = s.AbilityState.CooldownRemainingTicks; var sequence = s.AbilityState.NextCastSequence;
                var writer = new FailingWriter { WritesUntilFailure = next ? 2 : 1 };
                var store = new WaveCheckpointStore(root, writer); var controller = go.AddComponent<BattleCheckpointController>();
                var battle = go.AddComponent<BattleSceneController>();
                typeof(BattleSceneController).GetField("_simulation", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(battle, s);
                typeof(BattleSceneController).GetField("checkpointController", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(battle, controller);
                controller.Configure(store, s, AbilityCheckpointTests.Content);
                if (next) AbilityCheckpointTests.Choose(s);
                LogAssert.Expect(LogType.Error, new Regex("Checkpoint save failed:.*"));
                if (next) Assert.That(battle.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.Continued));
                else Assert.That(battle.ChooseElement(new ChooseElementCommand("after-wave-3", "tide")).Success, Is.True);
                Assert.That(controller.LastSaveSucceeded, Is.False);
                Assert.That(s.State.Phase, Is.EqualTo(next ? BattlePhase.Preparation : BattlePhase.WaveComplete));
                Assert.That(s.AbilityState.CooldownRemainingTicks, Is.EqualTo(remaining)); Assert.That(s.AbilityState.NextCastSequence, Is.EqualTo(sequence));
                var expected = AbilityCheckpointTests.Snapshot(s); Assert.That(controller.RetrySave(), Is.True); Assert.That(controller.RetrySave(), Is.True);
                Assert.That(AbilityCheckpointTests.Snapshot(s), Is.EqualTo(expected));
                Assert.That(store.TryLoad(AbilityCheckpointTests.Content, s.TryValidateCheckpoint, out var load), Is.True, load.Error);
                Assert.That(WaveCheckpointJson.Serialize(load.Envelope), Is.EqualTo(expected));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); Directory.Delete(root, true); }
        }

        [TestCase(false)] [TestCase(true)]
        public void RegBattle202_CommandCompletionFailedSaveRetryNeverRepeatsCastOrReward(bool observeStart)
        {
            var root = Path.Combine("TestResults", "AbilityController", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            var go = new GameObject("checkpoint");
            try
            {
                var s = AbilityCheckpointTests.Create(); AbilityCheckpointTests.Choose(s);
                var store = new WaveCheckpointStore(root); var controller = go.AddComponent<BattleCheckpointController>();
                controller.Configure(store, s, AbilityCheckpointTests.Content);
                Assert.That(controller.SaveForStartWave(), Is.True); s.StartWave(); if (observeStart) controller.ObservePhaseTransition();
                s.Step(); Directory.CreateDirectory(store.TempPath);
                var cast = s.CastAbility(AbilityCheckpointTests.Command(s)); Assert.That(cast.Succeeded, Is.True);
                Assert.That(s.State.Phase, Is.EqualTo(BattlePhase.WaveComplete));
                LogAssert.Expect(LogType.Error, new Regex("Checkpoint save failed:.*"));
                Assert.That(controller.ObserveCommandCompletion(), Is.False);
                var expected = AbilityCheckpointTests.Snapshot(s); Directory.Delete(store.TempPath);
                Assert.That(controller.RetrySave(), Is.True); Assert.That(controller.RetrySave(), Is.True);
                Assert.That(AbilityCheckpointTests.Snapshot(s), Is.EqualTo(expected));
                Assert.That(store.TryLoad(AbilityCheckpointTests.Content, s.TryValidateCheckpoint, out var load), Is.True, load.Error);
                Assert.That(load.Envelope.state.ability.NextCastSequence, Is.EqualTo(2));
                Assert.That(load.Envelope.state.ability.CooldownRemainingTicks, Is.EqualTo(600));
                Assert.That(s.State.Kills, Is.EqualTo(2)); Assert.That(s.State.Gold, Is.EqualTo(10030));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); Directory.Delete(root, true); }
        }
    }
}
