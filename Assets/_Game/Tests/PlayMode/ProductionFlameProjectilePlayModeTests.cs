#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Veilward.Domain;
using Veilward.Infrastructure;
using Veilward.Presentation;

namespace Veilward.Tests.PlayMode
{
    public sealed class ProductionFlameProjectilePlayModeTests
    {
        private BattleSceneController Battle => Object.FindFirstObjectByType<BattleSceneController>();
        private static void Call(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        private static object[] Geometry(BattleSceneController battle) => battle.GetComponentsInChildren<LineRenderer>(true)
            .Where(l => l.transform.parent.name.StartsWith("Flame Projectile Pool"))
            .SelectMany(l => new object[] { l.enabled, l.GetPosition(0), l.GetPosition(1), l.GetPosition(2), l.widthMultiplier, l.startWidth, l.endWidth }).ToArray();

        private IEnumerator StartProduction()
        {
            Veilward.Application.BattleSessionContext.Clear();
            yield return SceneManager.LoadSceneAsync("Battle"); yield return null;
            Assert.That(Battle.ChooseElement(new ChooseElementCommand(Battle.Simulation.State.ElementProgression.PendingChoiceId, "flame")).Success, Is.True);
            Assert.That(Battle.PlaceTower(new TowerDefinitionId("flame"), new GridCoordinate(-4, -1)).Success, Is.True);
            yield return null;
        }

        private IEnumerator WaitForShot()
        {
            var deadline = Time.realtimeSinceStartup + 5;
            while (Battle.FlameProjectiles.GetView(0).Remaining <= 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Battle.FlameProjectiles.GetView(0).Remaining, Is.GreaterThan(0), "actual production Update must consume a shot");
        }

        [UnityTearDown] public IEnumerator Cleanup() { yield return SceneManager.LoadSceneAsync("MainMenu"); yield return null; }

        [UnityTest]
        public IEnumerator Vfx1A05_ProductionUpdateMovesHeadFreezesEveryLineOnPauseAndBackgroundThenExpires()
        {
            yield return StartProduction(); Assert.That(Battle.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
            yield return WaitForShot();
            var view = Battle.FlameProjectiles.GetView(0); var initial = view.HeadPosition;
            var pause = Battle.GetComponent<BattlePauseController>(); pause.Pause();
            var geometry = Geometry(Battle); var tick = Battle.Simulation.State.Tick; var remaining = view.Remaining;
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(Battle.Simulation.State.Tick, Is.EqualTo(tick)); Assert.That(view.Remaining, Is.EqualTo(remaining));
            CollectionAssert.AreEqual(geometry, Geometry(Battle));
            pause.OnApplicationPause(true); pause.OnApplicationFocus(false);
            Assert.That(pause.Resume(), Is.False);
            pause.OnApplicationPause(false); pause.OnApplicationFocus(true);
            Battle.enabled = false; Assert.That(pause.Resume(), Is.True); Call(Battle, "Update");
            CollectionAssert.AreEqual(geometry, Geometry(Battle)); Assert.That(Battle.Simulation.State.Tick, Is.EqualTo(tick), "first resumed delta skipped");
            Battle.enabled = true;
            var moved = false; var impact = false; var deadline = Time.realtimeSinceStartup + 5;
            while (view.Remaining > 0 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                moved |= view.IsFlying && view.HeadPosition != initial && view.HeadPosition != view.Target;
                impact |= view.ImpactVisible;
            }
            Assert.That(moved, Is.True, "intermediate motion through real Update"); Assert.That(impact, Is.True);
            Assert.That(view.Remaining, Is.Zero); Assert.That(view.TargetEnemyId.Value, Is.Zero);
            Assert.That(Battle.FlameProjectiles.GetEvent(0).TargetEnemyId.Value, Is.Zero);
            Assert.That(Battle.GetComponentsInChildren<AttackBeamView>(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator Vfx1A05_RestoreClearsFlightFlashAndImpactSynchronouslyWithoutExtraPool()
        {
            yield return StartProduction();
            var s = Battle.Simulation;
            var safe = new WaveCheckpointEnvelope { schemaVersion = WaveCheckpointStore.CurrentSchemaVersion, scenarioContentId = "eight-wave-ability-boss-v2", state = s.ExportCheckpoint() };
            var pool = Battle.FlameProjectiles; var renderers = Battle.GetComponentsInChildren<LineRenderer>(true).Count(l => l.name == "Fire head layer");
            Battle.StartWave(); yield return WaitForShot();
            var checkpoint = Battle.GetComponent<BattleCheckpointController>();
            checkpoint.RestoreCheckpoint(safe);
            Assert.That(Battle.FlameProjectiles, Is.SameAs(pool));
            Assert.That(Enumerable.Range(0, 8).All(i => pool.GetView(i).Remaining == 0 && pool.GetEvent(i).TargetEnemyId.Value == 0), Is.True);
            Assert.That(Geometry(Battle).Where((o, i) => i % 7 == 0).Cast<bool>().All(v => !v), Is.True);
            Assert.That(s.State.Phase, Is.EqualTo(BattlePhase.Preparation)); Assert.That(s.State.Gold, Is.EqualTo(80)); Assert.That(s.State.Kills, Is.Zero);
            yield return null;
            Assert.That(Battle.GetComponentsInChildren<LineRenderer>(true).Count(l => l.name == "Fire head layer"), Is.EqualTo(renderers));
            Battle.StartWave(); yield return WaitForShot();
            pool.Advance(.21f); Assert.That(pool.GetView(0).ImpactVisible, Is.True);
            Battle.ResetPresentation(); Assert.That(pool.GetView(0).ImpactVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator Vfx1A05_MenuUnloadDestroysOldPoolAndNewGameHasNoGhostShots()
        {
            yield return StartProduction(); Battle.StartWave(); yield return WaitForShot();
            var old = Battle.GetComponentsInChildren<LineRenderer>(true).First(l => l.name == "Fire head layer");
            Battle.ReturnToMenu(); yield return null; yield return null;
            Assert.That(old == null, Is.True);
            Assert.That(Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None).Any(l => l.name == "Fire head layer"), Is.False);
            yield return StartProduction();
            Assert.That(Enumerable.Range(0, 8).All(i => Battle.FlameProjectiles.GetView(i).Remaining == 0), Is.True);
        }
    }
}
#endif
