#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Veilward.Application;
using Veilward.Domain;
using Veilward.Infrastructure;
using Veilward.Presentation;
using Veilward.Tests.Shared;
using Object = UnityEngine.Object;

namespace Veilward.Tests.PlayMode
{
    public sealed class HybridProjectilePlayModeTests
    {
        [UnityTest]
        public IEnumerator Vfx1C03_ProductionUpdateMovesBothAttacksAndFreezesGeometryThenFileContinueClearsIt()
        {
            var store = new WaveCheckpointStore(Path.Combine("TestResults", "Vfx1c", Guid.NewGuid().ToString("N")));
            var config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWaveAbilityBossV2.asset");
            var run = new AbilityBossTrace.Run(config.CreateSimulation(), BossMissionScript.Load("flame"));
            WaveCheckpointEnvelope earned = null;
            run.stable = (current, stage) =>
            {
                if (stage == "preparation" && current.sim.State.WaveIndex == 7)
                    earned = AbilityBossTrace.Envelope(current.sim);
            };
            run.Play();
            Assert.That(run.sim.State.Phase, Is.EqualTo(BattlePhase.Victory));
            Assert.That(earned, Is.Not.Null);
            Assert.That(store.TrySave(earned, run.sim.TryValidateCheckpoint, out var error, config.CreateSimulationForCheckpoint), Is.True, error);
            MainMenuController.CheckpointStoreFactory = () => store;
            BattleSessionContext.Clear();
            var reference = config.CreateSimulationForCheckpoint(earned);
            reference.RestoreCheckpoint(earned.state);
            var safe = AbilityBossTrace.Snapshot(reference);

            yield return OpenEarnedBattle();
            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            Assert.That(AbilityBossTrace.Snapshot(battle.Simulation), Is.EqualTo(safe));
            Assert.That(battle.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
            var file = File.ReadAllBytes(store.PrimaryPath);
            var paused = false;
            var flying = new HashSet<HybridShotKind>();
            var arrived = new HashSet<HybridShotKind>();
            var reactions = new HashSet<ReactionKind>();
            var deadline = Time.realtimeSinceStartup + 20;
            while ((flying.Count < 2 || arrived.Count < 2 || reactions.Count < 2 || !paused) && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                var shotInFlight = false;
                for (var i = 0; i < HybridProjectilePool.Capacity; i++)
                {
                    var view = battle.HybridProjectiles.GetView(i);
                    if (view.Remaining <= 0) continue;
                    if (!view.IsFlying) arrived.Add(view.Kind);
                    else if (view.HeadPosition != view.Source && view.HeadPosition != view.Target)
                    {
                        shotInFlight = true;
                        flying.Add(view.Kind);
                    }
                }
                var active = battle.GetComponentsInChildren<ReactionEffectView>();
                foreach (var effect in active.Where(e => e.HitCount > 0))
                    reactions.Add(effect.Event.Kind);
                if (paused || !shotInFlight || active.Length == 0) continue;

                var pause = battle.GetComponent<BattlePauseController>();
                pause.Pause();
                var frozen = Geometry(battle);
                var tick = battle.Simulation.State.Tick;
                yield return new WaitForSecondsRealtime(.20f);
                CollectionAssert.AreEqual(frozen, Geometry(battle));
                Assert.That(battle.Simulation.State.Tick, Is.EqualTo(tick));
                pause.OnApplicationPause(true);
                pause.OnApplicationFocus(false);
                Assert.That(pause.Resume(), Is.False);
                yield return new WaitForSecondsRealtime(.12f);
                CollectionAssert.AreEqual(frozen, Geometry(battle));
                pause.OnApplicationPause(false);
                pause.OnApplicationFocus(true);
                battle.enabled = false;
                Assert.That(pause.Resume(), Is.True);
                typeof(BattleSceneController).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(battle, null);
                CollectionAssert.AreEqual(frozen, Geometry(battle));
                Assert.That(battle.Simulation.State.Tick, Is.EqualTo(tick));
                battle.enabled = true;
                paused = true;
            }

            CollectionAssert.AreEquivalent(new[] { HybridShotKind.Steam, HybridShotKind.Storm }, flying);
            CollectionAssert.AreEquivalent(flying, arrived);
            CollectionAssert.AreEquivalent(new[] { ReactionKind.Steam, ReactionKind.Storm }, reactions);
            Assert.That(paused, Is.True);
            Assert.That(battle.GetComponentsInChildren<AttackBeamView>(), Is.Empty);
            var old = battle.GetComponentsInChildren<ReactionEffectView>(true)[0];
            battle.ReturnToMenu();
            yield return null;
            yield return null;
            yield return OpenEarnedBattle();
            battle = Object.FindFirstObjectByType<BattleSceneController>();
            Assert.That(old == null, Is.True, "The unloaded scene releases all pooled objects.");
            Assert.That(AbilityBossTrace.Snapshot(battle.Simulation), Is.EqualTo(safe));
            CollectionAssert.AreEqual(file, File.ReadAllBytes(store.PrimaryPath));
            Assert.That(battle.GetComponentsInChildren<EnemyStatusView>(), Is.Empty);
            Assert.That(battle.GetComponentsInChildren<ReactionEffectView>(), Is.Empty);
            Assert.That(Enumerable.Range(0, HybridProjectilePool.Capacity).Select(battle.HybridProjectiles.GetView)
                .All(v => v.Kind == HybridShotKind.None && v.Event.Tick == 0 && v.Remaining == 0), Is.True);
        }

        private static IEnumerator OpenEarnedBattle()
        {
            if (SceneManager.GetActiveScene().name != "MainMenu")
                yield return SceneManager.LoadSceneAsync("MainMenu");
            var menu = Object.FindFirstObjectByType<MainMenuController>();
            Assert.That(menu.ContinueButton.interactable, Is.True);
            menu.ContinueButton.onClick.Invoke();
            yield return null;
            yield return null;
        }

        private static object[] Geometry(BattleSceneController battle)
        {
            var values = new List<object>();
            foreach (var line in battle.GetComponentsInChildren<LineRenderer>(true).Where(l =>
                l.transform.parent.name.StartsWith("Hybrid Projectile Pool", StringComparison.Ordinal) ||
                l.GetComponentInParent<ReactionEffectView>(true) != null))
            {
                values.Add(line.enabled);
                values.Add(line.startWidth);
                values.Add(line.endWidth);
                for (var i = 0; i < line.positionCount; i++) values.Add(line.GetPosition(i));
            }
            return values.ToArray();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            MainMenuController.CheckpointStoreFactory = null;
            BattleSessionContext.Clear();
            yield return SceneManager.LoadSceneAsync("MainMenu");
        }
    }
}
#endif
