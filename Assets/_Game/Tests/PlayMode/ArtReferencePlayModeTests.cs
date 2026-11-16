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
