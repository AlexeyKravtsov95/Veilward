#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Veilward.Domain;
using Veilward.Infrastructure;
using Veilward.Presentation;
using Veilward.Tests.Shared;

namespace Veilward.Tests.PlayMode
{
    public sealed class BasicProjectilePlayModeTests
    {
        private BattleSceneController Battle => Object.FindFirstObjectByType<BattleSceneController>();
        private WaveCheckpointEnvelope _earned;
        private static void Call(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

        private IEnumerator StartEarnedProduction()
        {
            Veilward.Application.BattleSessionContext.Clear();
            yield return SceneManager.LoadSceneAsync("Battle"); yield return null;
            var config = UnityEditor.AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWaveAbilityBossV2.asset");
            var run = new AbilityBossTrace.Run(config.CreateSimulation(), BossMissionScript.Load("gale"));
            run.stable = (r, stage) =>
            {
                if (stage == "preparation" && r.sim.State.WaveIndex == 6) _earned = AbilityBossTrace.Envelope(r.sim);
            };
            run.Play(); Assert.That(_earned, Is.Not.Null, "Earned GALE→STONE→TIDE, no resource grants");
            Battle.GetComponent<BattleCheckpointController>().RestoreCheckpoint(_earned);
            yield return null;
        }

        private static object[] Geometry(BattleSceneController battle)
        {
            var values = new List<object>();
            foreach (var line in battle.GetComponentsInChildren<LineRenderer>(true).Where(l => l.transform.parent.name.StartsWith("Basic Projectile Pool")))
            {
                values.Add(line.enabled); values.Add(line.startWidth); values.Add(line.endWidth);
                for(var i=0;i<line.positionCount;i++) values.Add(line.GetPosition(i));
            }
            foreach (var view in battle.GetComponentsInChildren<EnemyStatusView>())
            {
                foreach (var badge in view.GetComponentsInChildren<UnityEngine.UI.Image>())
                {
                    values.Add(badge.enabled);
                    values.Add(badge.sprite);
                    values.Add(badge.rectTransform.position);
                }
            }
            return values.ToArray();
        }

        [UnityTearDown] public IEnumerator Cleanup() { yield return SceneManager.LoadSceneAsync("MainMenu"); yield return null; }

        [UnityTest]
        public IEnumerator Vfx1B05_RealUpdateMovesEveryFamilyAndFreezesShotAndRenderedStatusGeometry()
        {
            yield return StartEarnedProduction();
            Assert.That(Battle.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
            var seen = new HashSet<BasicShotKind>(); var moved = new HashSet<BasicShotKind>(); var impacts = new HashSet<BasicShotKind>();
            var initial = new Dictionary<BasicShotKind,Vector3>();
            var paused = false; var deadline = Time.realtimeSinceStartup + 25;
            while ((seen.Count < 3 || moved.Count < 3 || impacts.Count < 3) && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                for(var i=0;i<BasicProjectilePool.Capacity;i++)
                {
                    var view=Battle.BasicProjectiles.GetView(i);if(view.Remaining<=0)continue;
                    if(seen.Add(view.Kind))initial[view.Kind]=view.HeadPosition;
                    if(view.IsFlying && view.HeadPosition!=initial[view.Kind] && view.HeadPosition!=view.Target) moved.Add(view.Kind);
                    if(view.ImpactVisible)impacts.Add(view.Kind);
                }
                if(!paused && Battle.GetComponentsInChildren<EnemyStatusView>().Any(view => view.VisibleSlotCount > 0))
                {
                    var pause=Battle.GetComponent<BattlePauseController>();pause.Pause();
                    yield return new WaitForEndOfFrame();
                    var frozen=Geometry(Battle);var tick=Battle.Simulation.State.Tick;
                    yield return new WaitForSecondsRealtime(.25f);
                    CollectionAssert.AreEqual(frozen,Geometry(Battle));Assert.That(Battle.Simulation.State.Tick,Is.EqualTo(tick));
                    pause.OnApplicationPause(true);pause.OnApplicationFocus(false);Assert.That(pause.Resume(),Is.False);
                    pause.OnApplicationPause(false);pause.OnApplicationFocus(true);
                    Battle.enabled=false;Assert.That(pause.Resume(),Is.True);Call(Battle,"Update");
                    CollectionAssert.AreEqual(frozen,Geometry(Battle));Assert.That(Battle.Simulation.State.Tick,Is.EqualTo(tick));
                    Battle.enabled=true;paused=true;
                }
            }
            Assert.That(seen.Count,Is.EqualTo(3));Assert.That(moved.Count,Is.EqualTo(3));Assert.That(impacts.Count,Is.EqualTo(3));Assert.That(paused,Is.True);
            Assert.That(Battle.GetComponentsInChildren<AttackBeamView>(),Is.Empty);
            // Успешный Restore очищает и полёт, и impact, и status owners синхронно.
            var pool=Battle.BasicProjectiles;
            Battle.GetComponent<BattleCheckpointController>().RestoreCheckpoint(_earned);
            Assert.That(Battle.BasicProjectiles,Is.SameAs(pool));
            Assert.That(Enumerable.Range(0,12).All(i=>pool.GetView(i).Remaining==0&&pool.GetEvent(i).TargetEnemyId.Value==0),Is.True);
            Assert.That(Battle.GetComponentsInChildren<EnemyStatusView>(),Is.Empty);
            Assert.That(Battle.GetComponent<BattleCheckpointController>().RetrySave(),Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Vfx1B05_ExpiryAndMenuUnloadNeverCarryStatusOrShotsIntoNewIds()
        {
            yield return StartEarnedProduction();Battle.StartWave();
            var deadline=Time.realtimeSinceStartup+10;
            while(!Enumerable.Range(0,12).Any(i=>Battle.BasicProjectiles.GetView(i).Remaining>0)&&Time.realtimeSinceStartup<deadline)yield return null;
            var pool=Battle.BasicProjectiles;
            Assert.That(Enumerable.Range(0,12).Any(i=>pool.GetView(i).Remaining>0),Is.True);
            Battle.enabled=false;
            // Production Update outside Combat also drains cosmetics; no new simulation Steps.
            Battle.GetComponent<BattleCheckpointController>().RestoreCheckpoint(_earned);
            pool.Play(BasicShotKind.Stone,new AttackEvent(new TowerId(1),new EnemyId(999),new PathPosition(1,1),1,10,true),1,Vector3.zero);
            Battle.enabled=true;yield return new WaitForSecondsRealtime(.7f);
            Assert.That(Enumerable.Range(0,12).All(i=>pool.GetView(i).Remaining==0&&pool.GetEvent(i).TargetEnemyId.Value==0),Is.True);
            var old=Battle.GetComponentsInChildren<LineRenderer>(true).First(l=>l.transform.parent.name.StartsWith("Basic Projectile Pool"));
            Battle.ReturnToMenu();yield return null;yield return null;
            Assert.That(old==null,Is.True);
            yield return SceneManager.LoadSceneAsync("Battle");yield return null;
            Assert.That(Enumerable.Range(0,12).All(i=>Battle.BasicProjectiles.GetView(i).Remaining==0),Is.True);
            Assert.That(Battle.GetComponentsInChildren<EnemyStatusView>(),Is.Empty);
        }
    }
}
#endif
