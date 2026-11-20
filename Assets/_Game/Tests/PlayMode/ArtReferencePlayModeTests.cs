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

        [Test]
        public void DeviceProbeRequiresArtSceneAndExplicitArgument()
        {
            Assert.That(ArtDeviceProbe.ShouldAttach("ArtReference", new[] { "app", "-art2b", "normal" }), Is.True);
            Assert.That(ArtDeviceProbe.ShouldAttach("Battle", new[] { "app", "-art2b", "dense" }), Is.False);
            Assert.That(ArtDeviceProbe.ShouldAttach("ArtReference", new[] { "app" }), Is.False);
        }

        [Test]
        public void DeviceArtProfileReceiptActivatesWhenIosOmitsManagedArguments()
        {
            var empty = new[] { "app" };
            CollectionAssert.AreEqual(empty, ArtDeviceProbe.ResolveLaunchArguments(empty, "dense", false));
            var resolved = ArtDeviceProbe.ResolveLaunchArguments(empty, "dense", true);
            Assert.That(ArtDeviceProbe.ShouldAttach("ArtReference", resolved), Is.True);
            Assert.That(ArtDeviceProbe.ShouldAttach("Battle", resolved), Is.False);
            Assert.That(resolved[2], Is.EqualTo("dense"));
            Assert.That(ArtDeviceProbe.ResolveLaunchArguments(empty, null, true)[2], Is.EqualTo("normal"));
            Assert.Throws<System.ArgumentException>(() => ArtDeviceProbe.ResolveLaunchArguments(empty, "unknown", true));
        }

        [UnityTest]
        public IEnumerator DeviceSyntheticProbeLeavesDomainUntouchedAndCleansUp()
        {
            yield return Load();
            var s = Object.FindFirstObjectByType<ArtReferenceShowcase>();
            s.SetPaused(true);
            var tick = s.Simulation.State.Tick;
            var gold = s.Simulation.State.Gold;
            var probe = s.gameObject.AddComponent<ArtDeviceProbe>();
            probe.Initialize(s);
            probe.SelectScenario("dense");
            Assert.That(probe.ProbeCount, Is.EqualTo(12));
            Assert.That(s.Simulation.State.Tick, Is.EqualTo(tick));
            Assert.That(s.Simulation.State.Gold, Is.EqualTo(gold));
            probe.SelectScenario("behind");
            yield return null;
            Assert.That(probe.ProbeCount, Is.EqualTo(1));
            probe.SelectScenario("normal");
            yield return null;
            Assert.That(probe.ProbeCount, Is.Zero);
            Object.Destroy(probe);
            yield return null;
            Assert.That(s.transform.Find("ART2B Synthetic Probes"), Is.Null);
        }

        [UnityTest]
        public IEnumerator DeviceColdStartupWaitsForShowcaseReadiness()
        {
            var output = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "art2b-cold-" + System.Guid.NewGuid().ToString("N"));
            ArtDeviceProbe probe = null;
            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> onLoaded = (scene, mode) =>
            {
                if (scene.name != "ArtReference") return;
                var showcase = Object.FindFirstObjectByType<ArtReferenceShowcase>();
                Assert.That(showcase.Simulation, Is.Null, "Exercise actual pre-Start scene-loaded boundary.");
                probe = showcase.gameObject.AddComponent<ArtDeviceProbe>();
                probe.StartCoroutine(probe.InitializeFromArguments(new[] { "app", "-art2b", "dense" }, output));
            };
            SceneManager.sceneLoaded += onLoaded;
            try
            {
                yield return Load(); yield return null;
                Assert.That(probe, Is.Not.Null);
                Assert.That(probe.ProbeCount, Is.EqualTo(12));
                Assert.That(probe.MeasurementActive, Is.True);
                Assert.That(System.IO.File.Exists(System.IO.Path.Combine(probe.MeasurementDirectory, "run.json")), Is.True);
            }
            finally
            {
                SceneManager.sceneLoaded -= onLoaded;
                if (probe != null) Object.Destroy(probe);
                if (System.IO.Directory.Exists(output)) System.IO.Directory.Delete(output, true);
            }
        }

        [UnityTest]
        public IEnumerator WarmupActivityCannotApproveFrozenSampleWindow()
        {
            yield return Load();
            var s = Object.FindFirstObjectByType<ArtReferenceShowcase>(); s.SetPaused(true); s.RestartShowcase();
            while (s.ObservedAttacks == 0) s.AdvanceForReview(1);
            s.SendMessage("OnApplicationFocus", true); s.SetPaused(false);
            var output = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "art2b-frozen-" + System.Guid.NewGuid().ToString("N"));
            var probe = s.gameObject.AddComponent<ArtDeviceProbe>(); probe.Initialize(s, output);
            try
            {
                probe.BeginMeasurements(.2, .05);
                yield return new WaitForSeconds(.1f);
                s.SetPaused(true);
                LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("InvalidOperationException: Frozen normal combat"));
                yield return new WaitForSeconds(.25f);
                Assert.That(probe.MeasurementActive, Is.False);
                Assert.That(System.IO.File.Exists(System.IO.Path.Combine(probe.MeasurementDirectory, "complete.json")), Is.False);
                Assert.That(System.IO.Directory.GetFiles(probe.MeasurementDirectory, "window-*.json"), Is.Empty);
            }
            finally { Object.Destroy(probe); if (System.IO.Directory.Exists(output)) System.IO.Directory.Delete(output, true); }
        }

        [UnityTest]
        public IEnumerator DeviceProbeDisableRestoresSettingsAndDelayedEnemyViews()
        {
            yield return Load();
            var s = Object.FindFirstObjectByType<ArtReferenceShowcase>();
            s.SetPaused(true); s.RestartShowcase();
            while (!s.LastAttack.WasLethal) s.AdvanceForReview(1);
            var target = s.LastAttack.TargetEnemyId;
            var retained = s.FindEnemyView(target);
            Assert.That(retained, Is.Not.Null, "Lethal Domain removal still has delayed live view.");
            var oldRate = UnityEngine.Application.targetFrameRate;
            var oldVsync = QualitySettings.vSyncCount;
            var probe = s.gameObject.AddComponent<ArtDeviceProbe>(); probe.Initialize(s);
            probe.SelectScenario("dense");
            Assert.That(retained.gameObject.activeSelf, Is.False);
            probe.enabled = false;
            yield return null;
            Assert.That(probe.ProbeCount, Is.Zero);
            Assert.That(s.FindEnemyView(target), Is.SameAs(retained));
            Assert.That(s.IsPaused, Is.True);
            Assert.That(UnityEngine.Application.targetFrameRate, Is.EqualTo(oldRate));
            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(oldVsync));
            probe.enabled = true;
            probe.SelectScenario("behind");
            Object.Destroy(probe);
            yield return null;
            Assert.That(s.transform.Find("ART2B Synthetic Probes"), Is.Null);
            Assert.That(s.FindEnemyView(target), Is.SameAs(retained));
            Assert.That(UnityEngine.Application.targetFrameRate, Is.EqualTo(oldRate));
        }

        [UnityTest]
        public IEnumerator DeviceMeasurementsUseSeparateRunsAndSuspendWithoutCompletion()
        {
            yield return Load();
            var s = Object.FindFirstObjectByType<ArtReferenceShowcase>(); s.SetPaused(true);
            var output = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "art2b-test-" + System.Guid.NewGuid().ToString("N"));
            var probe = s.gameObject.AddComponent<ArtDeviceProbe>(); probe.Initialize(s, output); probe.SelectScenario("dense");
            try
            {
                probe.BeginMeasurements(0, .05);
                var first = probe.MeasurementDirectory;
                var deadline = Time.realtimeSinceStartup + 3;
                while (probe.MeasurementActive && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(probe.MeasurementActive, Is.False);
                Assert.That(System.IO.File.Exists(System.IO.Path.Combine(first, "complete.json")), Is.True);
                Assert.That(System.IO.Directory.GetFiles(first, "window-*.json").Length, Is.EqualTo(3));
                probe.BeginMeasurements(0, .05);
                var second = probe.MeasurementDirectory;
                Assert.That(second, Is.Not.EqualTo(first));
                probe.SendMessage("OnApplicationPause", true);
                Assert.That(probe.MeasurementActive, Is.False);
                Assert.Throws<System.InvalidOperationException>(() => probe.BeginMeasurements());
                var line = s.transform.Find("ART2B Synthetic Probes").GetComponentInChildren<LineRenderer>();
                var head = line.GetPosition(1);
                yield return new WaitForSeconds(.1f);
                Assert.That(line.GetPosition(1), Is.EqualTo(head));
                Assert.That(System.IO.File.Exists(System.IO.Path.Combine(second, "complete.json")), Is.False);
                Assert.That(System.IO.File.Exists(System.IO.Path.Combine(first, "complete.json")), Is.True);
            }
            finally { Object.Destroy(probe); if (System.IO.Directory.Exists(output)) System.IO.Directory.Delete(output, true); }
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
            var target = s.LastAttack.TargetEnemyId;
            Assert.That(s.DisplayedHealth(target), Is.EqualTo(100), "Authoritative pre-hit snapshot stays visible during flight.");
            s.AdvanceForReview(FlameStrikeView.FlightTicks);
            Assert.That(s.FirstEnemyView.position, Is.Not.EqualTo(first));
            Assert.That(s.DisplayedHealth(target), Is.LessThan(100), "Post-hit snapshot arrives with the projectile.");
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
            s.AdvanceForReview(5);
            Assert.That(s.ActiveStrikeCount, Is.Zero, "Flight+impact expire before the unchanged next saved cooldown.");
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
