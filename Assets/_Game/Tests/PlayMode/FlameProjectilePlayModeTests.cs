#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Veilward.Domain;
using Veilward.Presentation;

namespace Veilward.Tests.PlayMode
{
    public sealed class FlameProjectilePlayModeTests
    {
        private static IEnumerator Load()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_Game/Scenes/ArtReference.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var s = Object.FindFirstObjectByType<ArtReferenceShowcase>(); s.SetPaused(true); s.RestartShowcase();
        }

        [UnityTest]
        public IEnumerator AuthoritativeTimeline_NonlethalLethalMovingTargetsAndPoolReuseDoNotChangeDomain()
        {
            yield return Load();
            var s = Object.FindFirstObjectByType<ArtReferenceShowcase>();
            var config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/TestWave.asset");
            var control = config.CreateSimulation();
            control.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId("flame"), new GridCoordinate(-2,-1))); control.StartWave();
            var pending = false; var attack = default(AttackEvent); var flightStart = 0; var strikeIndex = 0;
            var hpAfter = 0; Transform oldView = null; var observed = 0; var nonlethal = 0; var lethal = 0;
            var objects = s.GetComponentsInChildren<Transform>(true).Length;
            for (var tick = 1; tick <= 350; tick++)
            {
                if (control.State.Phase == BattlePhase.Combat) control.Step();
                s.AdvanceForReview(1);
                Assert.That(s.Simulation.State.Tick, Is.EqualTo(control.State.Tick));
                Assert.That(s.Simulation.State.Gold, Is.EqualTo(control.State.Gold));
                Assert.That(s.Simulation.State.Kills, Is.EqualTo(control.State.Kills));
                Assert.That(s.Simulation.State.BaseHealth, Is.EqualTo(control.State.BaseHealth));
                Assert.That(s.Simulation.State.Phase, Is.EqualTo(control.State.Phase));
                Assert.That(s.Simulation.State.Towers[0].NextAttackTick, Is.EqualTo(control.State.Towers[0].NextAttackTick));
                Assert.That(s.Simulation.State.Enemies.Count, Is.EqualTo(control.State.Enemies.Count));
                for (var i = 0; i < control.State.Enemies.Count; i++)
                {
                    var actual = s.Simulation.State.Enemies[i]; var expected = control.State.Enemies[i];
                    Assert.That(actual.Id, Is.EqualTo(expected.Id)); Assert.That(actual.Health, Is.EqualTo(expected.Health));
                    Assert.That(actual.DistanceAlongPath, Is.EqualTo(expected.DistanceAlongPath));
                }
                foreach (var e in control.AttackEvents)
                {
                    attack = e; pending = true; flightStart = tick; strikeIndex = observed++ % ArtReferenceShowcase.StrikeCapacity;
                    hpAfter = 0;
                    foreach (var enemy in control.State.Enemies) if (enemy.Id == e.TargetEnemyId) hpAfter = enemy.Health;
                    var strike = s.GetStrike(strikeIndex);
                    Assert.That(strike.TargetEnemyId, Is.EqualTo(e.TargetEnemyId)); Assert.That(strike.WasLethal, Is.EqualTo(e.WasLethal));
                    Assert.That(s.LastAttack.Tick, Is.EqualTo(e.Tick)); Assert.That(s.LastAttack.Damage, Is.EqualTo(e.Damage));
                    Assert.That(strike.Target, Is.EqualTo(new Vector3(e.TargetPosition.X,.72f,e.TargetPosition.Y)));
                    oldView = s.FindEnemyView(e.TargetEnemyId);
                    Assert.That(oldView, Is.Not.Null, "A lethal Domain removal must not remove the delayed visual target on launch.");
                    Assert.That(s.DisplayedHealth(e.TargetEnemyId), Is.GreaterThan(0));
                    Assert.That(strike.ImpactVisible, Is.False);
                }
                Assert.That(s.ObservedAttacks, Is.EqualTo(observed));
                Assert.That(s.Simulation.AttackEvents, Is.Empty); Assert.That(s.Simulation.DamageEvents, Is.Empty);
                Assert.That(s.Simulation.ReactionEvents, Is.Empty);
                if (pending)
                {
                    var elapsed = tick - flightStart; var strike = s.GetStrike(strikeIndex);
                    if (elapsed < FlameStrikeView.FlightTicks)
                    {
                        Assert.That(s.FindEnemyView(attack.TargetEnemyId), Is.SameAs(oldView));
                        Assert.That(strike.ImpactVisible, Is.False);
                        Assert.That(strike.TargetEnemyId, Is.EqualTo(attack.TargetEnemyId));
                    }
                    if (elapsed == FlameStrikeView.FlightTicks)
                    {
                        Assert.That(s.PresentedTick, Is.EqualTo(attack.Tick));
                        Assert.That(strike.ImpactVisible, Is.True); Assert.That(strike.HeadPosition, Is.EqualTo(strike.Target));
                        if (attack.WasLethal)
                        { lethal++; Assert.That(s.FindEnemyView(attack.TargetEnemyId), Is.Null); Assert.That(s.DisplayedHealth(attack.TargetEnemyId), Is.Null); }
                        else
                        {
                            nonlethal++; var view = s.FindEnemyView(attack.TargetEnemyId); Assert.That(view, Is.SameAs(oldView));
                            Assert.That(s.DisplayedHealth(attack.TargetEnemyId), Is.EqualTo(hpAfter));
                            Assert.That(view.position.x, Is.EqualTo(attack.TargetPosition.X).Within(.0001f));
                            Assert.That(view.position.z, Is.EqualTo(attack.TargetPosition.Y).Within(.0001f));
                            Assert.That(view.GetComponent<EnemyHealthBarView>().NormalizedHealth, Is.EqualTo(hpAfter / 100f).Within(.0001f));
                        }
                    }
                }
                control.ClearAttackEvents(); control.ClearReactionEvents(); control.ClearDamageEvents();
            }
            Assert.That(nonlethal, Is.GreaterThan(0)); Assert.That(lethal, Is.GreaterThan(0));
            Assert.That(s.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objects));
        }

        [UnityTest]
        public IEnumerator PauseAndRestart_FreezeFlightAndPendingDeathAndClearBothFlightAndImpact()
        {
            yield return Load(); var s = Object.FindFirstObjectByType<ArtReferenceShowcase>();
            var guard=0; while (!s.LastAttack.WasLethal && guard++<500) s.AdvanceForReview(1);
            Assert.That(s.LastAttack.WasLethal,Is.True);
            var id = s.LastAttack.TargetEnemyId; var head = s.GetStrike((s.ObservedAttacks-1) % ArtReferenceShowcase.StrikeCapacity).HeadPosition;
            var tick = s.Simulation.State.Tick; var presented = s.PresentedTick; var view = s.FindEnemyView(id);
            Assert.That(view, Is.Not.Null);
            s.SendMessage("AdvanceFrame", .25f); yield return new WaitForSeconds(.1f);
            Assert.That(s.Simulation.State.Tick, Is.EqualTo(tick)); Assert.That(s.PresentedTick, Is.EqualTo(presented));
            Assert.That(s.FindEnemyView(id), Is.SameAs(view));
            var strike = s.GetStrike((s.ObservedAttacks-1) % ArtReferenceShowcase.StrikeCapacity);
            Assert.That(strike.HeadPosition, Is.EqualTo(head));
            s.SendMessage("OnApplicationFocus",true); s.SetPaused(false); s.SendMessage("AdvanceFrame",.25f);
            Assert.That(s.Simulation.State.Tick, Is.EqualTo(tick)); Assert.That(strike.HeadPosition, Is.EqualTo(head));
            s.SetPaused(true); s.AdvanceForReview(FlameStrikeView.FlightTicks);
            Assert.That(strike.ImpactVisible, Is.True); Assert.That(s.FindEnemyView(id), Is.Null);
            s.RestartShowcase();
            Assert.That(s.ActiveStrikeCount, Is.Zero); Assert.That(s.PresentedTick, Is.Zero);
            Assert.That(s.ObservedAttacks, Is.Zero); Assert.That(s.Simulation.State.Gold, Is.EqualTo(80));
            while (s.ObservedAttacks == 0) s.AdvanceForReview(1);
            Assert.That(s.ActiveStrikeCount, Is.GreaterThan(0)); s.RestartShowcase();
            for (var i=0;i<ArtReferenceShowcase.StrikeCapacity;i++)
            { Assert.That(s.GetStrike(i).Remaining, Is.Zero); Assert.That(s.GetStrike(i).TargetEnemyId.Value, Is.Zero); Assert.That(s.GetStrike(i).ImpactVisible, Is.False); }
        }

        [UnityTest]
        public IEnumerator TerminalDrainAndSceneReload_DoNotLeaveDelayedEnemiesOrEffects()
        {
            yield return Load(); var s = Object.FindFirstObjectByType<ArtReferenceShowcase>();
            var guard=0; while(s.Simulation.State.Phase==BattlePhase.Combat && guard++<2000) s.AdvanceForReview(1);
            Assert.That(guard,Is.LessThan(2000)); var tick=s.Simulation.State.Tick;
            s.AdvanceForReview(12);
            Assert.That(s.Simulation.State.Tick,Is.EqualTo(tick)); Assert.That(s.ActiveStrikeCount,Is.Zero);
            Assert.That(s.FirstEnemyView,Is.Null); Assert.That(s.GetComponentsInChildren<EnemyHealthBarView>().Length,Is.Zero);
            s.RestartShowcase(); while(s.ObservedAttacks==0)s.AdvanceForReview(1);
            var old=s.GetComponentsInChildren<LineRenderer>(true)[0];
            yield return Load(); Assert.That(old==null,Is.True);
            s=Object.FindFirstObjectByType<ArtReferenceShowcase>(); Assert.That(s.ActiveStrikeCount,Is.Zero); Assert.That(s.PresentedTick,Is.Zero);
        }
        [UnityTest]
        public IEnumerator FlightCannotOverpaintHealthBarPixels()
        {
            yield return Load(); var s=Object.FindFirstObjectByType<ArtReferenceShowcase>();
            var guard=0;while(s.ObservedAttacks==0&&guard++<300)s.AdvanceForReview(1);
            Assert.That(s.ObservedAttacks,Is.GreaterThan(0));s.AdvanceForReview(3);
            var strike=s.GetStrike(0);Assert.That(strike.IsFlying,Is.True);
            var probe=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/ART1/P_Enemy_Shade.prefab"));
            var camera=s.WorldCamera;var previous=camera.targetTexture;var rt=RenderTexture.GetTemporary(1600,900,24);
            try
            {
                camera.targetTexture=rt;
                probe.transform.position=strike.HeadPosition-Vector3.up*1.53f;
                var health=probe.GetComponent<EnemyHealthBarView>();s.ConfigureHealthView(health);
                health.SetNormalizedHealth(.6f);health.Face(camera);
                var center=camera.WorldToScreenPoint(health.BarPosition);
                var withProjectile=HealthPixels(camera,center);
                strike.Clear();var withoutProjectile=HealthPixels(camera,center);
                Assert.That(withProjectile.Length,Is.EqualTo(135));
                var green=0;var background=0;
                for(var i=0;i<withProjectile.Length;i++)
                {
                    Assert.That(withProjectile[i],Is.EqualTo(withoutProjectile[i]),"Projectile must not replace HP pixels: "+i);
                    if(withoutProjectile[i].g>withoutProjectile[i].r)green++;
                    if(withoutProjectile[i].r>withoutProjectile[i].g)background++;
                }
                Assert.That(green,Is.GreaterThan(0));Assert.That(background,Is.GreaterThan(0));
            }
            finally{camera.targetTexture=previous;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(probe);}
        }

        private static Color32[] HealthPixels(Camera camera, Vector3 center)
        {
            var active=RenderTexture.active;var texture=new Texture2D(45,3,TextureFormat.RGB24,false);
            try
            {
                camera.Render();RenderTexture.active=camera.targetTexture;
                texture.ReadPixels(new Rect(Mathf.RoundToInt(center.x)-22,Mathf.RoundToInt(center.y)-1,45,3),0,0);texture.Apply();
                return texture.GetPixels32();
            }
            finally{RenderTexture.active=active;Object.DestroyImmediate(texture);}
        }
    }
}
#endif
