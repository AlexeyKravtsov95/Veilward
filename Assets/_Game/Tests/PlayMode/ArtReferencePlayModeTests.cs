#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Veilward.Domain;
using Veilward.Presentation;

namespace Veilward.Tests.PlayMode
{
    public sealed class ArtReferencePlayModeTests
    {
        private const string Path = "Assets/_Game/Scenes/ArtReference.unity";
        private static IEnumerator Load()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(Path,new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Art11_OriginalCameraTowerScaleAndPadPositionsSurvivePlayMode()
        {
            yield return Load();var s=Object.FindFirstObjectByType<ArtReferenceShowcase>();s.SetPaused(true);s.RestartShowcase();
            Assert.That(s.WorldCamera.transform.position,Is.EqualTo(new Vector3(8,11,8)));
            Assert.That(s.WorldCamera.orthographicSize,Is.EqualTo(8.5f));
            Assert.That(s.TowerView.position,Is.EqualTo(new Vector3(-2,.16f,-1)));
            Assert.That(s.TowerView.localScale,Is.EqualTo(Vector3.one));
            Assert.That(s.FirstEnemyView.localScale,Is.EqualTo(Vector3.one));
            var tower=s.TowerView.GetComponentInChildren<MeshRenderer>().bounds;
            Assert.That(tower.size.x,Is.EqualTo(.94f).Within(.002));Assert.That(tower.size.y,Is.EqualTo(2.285f).Within(.002));
            var expected=new[]{new Vector3(-2,0,-1),new Vector3(2,0,-1),new Vector3(-3,0,1),new Vector3(2,0,3)};
            var count=0;
            foreach(Transform child in s.EnvironmentRoot)
            {
                if(child.name!="P_Environment_BuildPad")continue;
                CollectionAssert.Contains(expected,child.position);Assert.That(child.localScale,Is.EqualTo(Vector3.one));count++;
            }
            Assert.That(count,Is.EqualTo(4));
        }

        [UnityTest]
        public IEnumerator SavedCombat_DrivesHealthMovementAndAttackPool()
        {
            yield return Load();
            var s = Object.FindFirstObjectByType<ArtReferenceShowcase>();
            s.SetPaused(true); s.RestartShowcase();
            Assert.That(s.Simulation.State.Gold, Is.EqualTo(80), "Saved 150 gold minus FLAME 70.");
            Assert.That(s.Simulation.State.Towers.Count, Is.EqualTo(1));
            var first = s.FirstEnemyView.position;
            var guard = 0;
            while (s.ObservedAttacks == 0 && guard++ < 300) s.AdvanceForReview(1);
            Assert.That(guard, Is.LessThan(300));
            Assert.That(s.FirstEnemyView.position, Is.Not.EqualTo(first));
            Assert.That(s.Simulation.State.Enemies[0].Health, Is.LessThan(100));
            Assert.That(s.ActiveStrikeCount, Is.EqualTo(1));
            var attacks = s.ObservedAttacks;
            var actualTick = s.Simulation.State.Tick;
            var config = UnityEditor.AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/TestWave.asset");
            var expected = config.CreateSimulation();
            expected.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId("flame"),new GridCoordinate(-2,-1)));
            expected.StartWave();
            var expectedAttacks = 0;
            for (var i=0;i<actualTick;i++)
            {
                expected.Step(); expectedAttacks += expected.AttackEvents.Count;
                expected.ClearAttackEvents(); expected.ClearReactionEvents(); expected.ClearDamageEvents();
            }
            Assert.That(attacks, Is.EqualTo(expectedAttacks));
            Assert.That(s.Simulation.State.Enemies[0].Health, Is.EqualTo(expected.State.Enemies[0].Health));
            Assert.That(s.Simulation.State.Enemies[0].DistanceAlongPath, Is.EqualTo(expected.State.Enemies[0].DistanceAlongPath));
            s.AdvanceForReview(7);
            Assert.That(s.ActiveStrikeCount, Is.Zero, "Strike expires before the next saved cooldown.");
            Assert.That(s.ObservedAttacks, Is.EqualTo(attacks));
        }

        [UnityTest]
        public IEnumerator Pause_FreezesSimulationAndStrikeWithoutCatchUp()
        {
            yield return Load();
            var s = Object.FindFirstObjectByType<ArtReferenceShowcase>();
            s.SetPaused(true); s.RestartShowcase();
            while (s.ObservedAttacks == 0) s.AdvanceForReview(1);
            var tick = s.Simulation.State.Tick;
            yield return new WaitForSeconds(.3f);
            Assert.That(s.Simulation.State.Tick, Is.EqualTo(tick));
            Assert.That(s.ActiveStrikeCount, Is.EqualTo(1));
            s.SendMessage("OnApplicationFocus",true); s.SetPaused(false);
            yield return new WaitForSeconds(.1f);
            Assert.That(s.Simulation.State.Tick-tick, Is.InRange(1,6));
        }

        [UnityTest]
        public IEnumerator Resume_FirstLongFramePreservesTickAndStrike()
        {
            yield return Load();
            var s = Object.FindFirstObjectByType<ArtReferenceShowcase>();
            s.SetPaused(true); s.RestartShowcase();
            var guard = 0;
            while (s.ObservedAttacks == 0 && guard++ < 300) s.AdvanceForReview(1);
            Assert.That(s.ActiveStrikeCount, Is.EqualTo(1));
            var tick = s.Simulation.State.Tick;
            s.SendMessage("OnApplicationPause",true);
            s.SendMessage("OnApplicationPause",false);
            s.SendMessage("OnApplicationFocus",true);
            Assert.That(s.IsPaused, Is.True, "Lifecycle pause remains latched after foreground.");
            s.SetPaused(false);
            // Первый resumed delta включает stall/background и целиком отбрасывается.
            s.SendMessage("AdvanceFrame",.25f);
            Assert.That(s.Simulation.State.Tick, Is.EqualTo(tick));
            Assert.That(s.ActiveStrikeCount, Is.EqualTo(1));
            s.SendMessage("AdvanceFrame",1f/SimulationTiming.TicksPerSecond);
            Assert.That(s.Simulation.State.Tick, Is.EqualTo(tick+1));
            Assert.That(s.ActiveStrikeCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Restart_ReusesBoundedViewsAndClearsEffects()
        {
            yield return Load();
            var s = Object.FindFirstObjectByType<ArtReferenceShowcase>();
            s.SetPaused(true);
            var count = s.GetComponentsInChildren<Transform>(true).Length;
            for (var loop=0;loop<3;loop++)
            {
                s.RestartShowcase(); s.AdvanceForReview(300);
                Assert.That(s.ObservedAttacks, Is.GreaterThan(0));
                Assert.That(s.Simulation.State.Kills, Is.GreaterThan(0));
                Assert.That(s.Simulation.State.Gold, Is.GreaterThanOrEqualTo(80));
                Assert.That(s.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
                Assert.That(s.ActiveStrikeCount, Is.LessThanOrEqualTo(ArtReferenceShowcase.StrikeCapacity));
            }
            s.RestartShowcase();
            Assert.That(s.ObservedAttacks, Is.Zero); Assert.That(s.ActiveStrikeCount, Is.Zero);
            Assert.That(s.Simulation.State.Tick, Is.Zero); Assert.That(s.Simulation.State.Gold, Is.EqualTo(80));
        }
    }
}
#endif
