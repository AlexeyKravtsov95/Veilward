#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Veilward.Presentation;
using Veilward.Infrastructure;
using Veilward.Domain;

namespace Veilward.Tests.PlayMode
{
    public sealed class ArtReadabilityPlayModeTests
    {
        private GameObject _probes;
        [TearDown] public void Cleanup() { if (_probes != null) Object.DestroyImmediate(_probes); }

        [UnityTest]
        public IEnumerator DenseHp_StableNonOverlappingRatiosAndPoolReset()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_Game/Scenes/ArtReference.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var s = Object.FindFirstObjectByType<ArtReferenceShowcase>(); s.SetPaused(true);
            _probes = new GameObject("ART21 synthetic regression");
            var views = new EnemyHealthBarView[12];
            for (var i = 0; i < views.Length; i++)
            {
                var e = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/ART1/P_Enemy_Shade.prefab"), _probes.transform);
                e.transform.position = new Vector3(-.7f + (i % 4) * .48f, .14f, -.45f + (i / 4) * .48f);
                e.transform.rotation = Quaternion.LookRotation(Vector3.right);
                views[i] = e.GetComponent<EnemyHealthBarView>();
                views[i].SetNormalizedHealth(.5f + (i % 4) * .1f);
            }
            var occupied = new List<Rect>(12); var positions = new Vector3[12];
            var shifted = 0;
            var previousTarget = s.WorldCamera.targetTexture;
            foreach (var size in new[] { new Vector2Int(1600,900), new Vector2Int(1280,720), new Vector2Int(960,540) })
            {
                var target = RenderTexture.GetTemporary(size.x,size.y,24); s.WorldCamera.targetTexture = target;
                try
                {
                    for (var pass = 0; pass < 3; pass++)
            {
                occupied.Clear();
                for (var i = 0; i < views.Length; i++)
                {
                    var view = views[i]; view.Face(s.WorldCamera); view.Layout(s.WorldCamera, occupied);
                    Assert.That(view.NormalizedHealth, Is.EqualTo(.5f + (i % 4) * .1f).Within(.0001));
                    var fill = view.transform.Find("Health Bar/HealthFill");
                    Assert.That(fill.localScale.x, Is.EqualTo(.9f * view.NormalizedHealth).Within(.0001));
                    if (pass == 0) { positions[i] = view.BarPosition; if (view.LeaderVisible) shifted++; }
                    else Assert.That(view.BarPosition, Is.EqualTo(positions[i]));
                    for (var j = 0; j < i; j++) Assert.That(occupied[i].Overlaps(occupied[j]), Is.False, $"HP {i}/{j}");
                }
            }
            }
            finally { s.WorldCamera.targetTexture = previousTarget; RenderTexture.ReleaseTemporary(target); }
            }
            Assert.That(shifted, Is.GreaterThan(0), "Probe must exercise real overlap.");
            for (var i = 0; i < 12; i++)
            {
                var v = views[i]; v.gameObject.SetActive(false);
                Assert.That(v.LeaderVisible, Is.False);
                Assert.That(v.BarPosition, Is.EqualTo(v.AnchorPosition));
                v.gameObject.SetActive(true); v.Face(s.WorldCamera); occupied.Clear(); v.Layout(s.WorldCamera, occupied);
                Assert.That(v.BarPosition, Is.EqualTo(v.AnchorPosition)); Assert.That(v.LeaderVisible, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator FlameProjectile_MovingHeadArrivesBeforeImpactAndClearsWithoutNewObjects()
        {
            _probes = new GameObject("ART21 strike regression");
            var flame = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/ART1/" + "MAT_Art1_EmberStroke.mat");
            var hot = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/ART1/" + "MAT_Art1_EmberCore.mat");
            var strike = new FlameStrikeView(_probes.transform, flame, hot, 0);
            var count = _probes.GetComponentsInChildren<Transform>(true).Length;
            for (var i = 0; i < 20; i++)
            {
                var source = new Vector3(-2, 1.11f, -1); var target = new Vector3(-3, .72f, 0);
                strike.Play(source, target);
                Assert.That(strike.Source, Is.EqualTo(source)); Assert.That(strike.Target, Is.EqualTo(target));
                Assert.That(strike.IsFlying, Is.True);
                Assert.That(strike.TrailVisible, Is.False, "No tail before the head leaves the source.");
                Assert.That(strike.ImpactVisible, Is.False);
                Assert.That(strike.HeadPosition, Is.EqualTo(source));
                strike.Advance(.10f);
                Assert.That(strike.TrailVisible, Is.True);
                Assert.That(strike.HeadPosition, Is.Not.EqualTo(source));
                Assert.That(strike.HeadPosition, Is.Not.EqualTo(target));
                Assert.That(Vector3.Distance(strike.HeadPosition, strike.TailEnd), Is.LessThanOrEqualTo(.481f));
                Assert.That(strike.ImpactVisible, Is.False);
                strike.Advance(.09f); Assert.That(strike.ImpactVisible, Is.False);
                strike.Advance(.01f);
                Assert.That(strike.ImpactVisible, Is.True); Assert.That(strike.TrailVisible, Is.False);
                Assert.That(strike.HeadPosition, Is.EqualTo(target));
                Assert.That(strike.Remaining, Is.GreaterThan(0));
                strike.Advance(.17f);
                Assert.That(strike.Remaining, Is.Zero);
                foreach (var line in _probes.GetComponentsInChildren<LineRenderer>()) Assert.That(line.enabled, Is.False);
                Assert.That(_probes.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            }
            strike.Play(Vector3.zero, Vector3.one); strike.Clear();
            foreach (var line in _probes.GetComponentsInChildren<LineRenderer>()) Assert.That(line.enabled, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BattleHp_MatchesLiveEnemiesAndRestoreRemovesAllLinks()
        {
            yield return SceneManager.LoadSceneAsync("Battle", LoadSceneMode.Single); yield return null;
            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            var initial = battle.Simulation.ExportCheckpoint();
            var choice = battle.Simulation.State.ElementProgression.PendingChoiceId;
            Assert.That(battle.Simulation.ChooseElement(new ChooseElementCommand(choice, "flame")).Success, Is.True);
            Assert.That(battle.Simulation.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
            for (var i = 0; i < 100; i++)
            {
                battle.Simulation.Step();
                battle.Simulation.ClearAttackEvents(); battle.Simulation.ClearReactionEvents(); battle.Simulation.ClearDamageEvents();
            }
            battle.SendMessage("SynchronizeViews");
            var bars = battle.GetComponentsInChildren<EnemyHealthBarView>();
            Assert.That(bars.Length, Is.EqualTo(battle.Simulation.State.Enemies.Count));
            Assert.That(bars.Length, Is.GreaterThan(1));
            foreach (var bar in bars) Assert.That(bar.NormalizedHealth, Is.EqualTo(1));
            Object.FindFirstObjectByType<BattleCheckpointController>().RestoreCheckpoint(new WaveCheckpointEnvelope
            {
                schemaVersion = WaveCheckpointStore.CurrentSchemaVersion,
                scenarioContentId = "eight-wave-ability-boss-v2", state = initial
            });
            yield return null;
            Assert.That(battle.GetComponentsInChildren<EnemyHealthBarView>().Length, Is.Zero);
            foreach (var line in battle.GetComponentsInChildren<LineRenderer>())
                Assert.That(line.name, Is.Not.EqualTo("HP anchor link"));
        }
    }
}
#endif
