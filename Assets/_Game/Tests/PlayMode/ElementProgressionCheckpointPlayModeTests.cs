using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Veilward.Application;
using Veilward.Domain;
using Veilward.Infrastructure;
using Veilward.Presentation;
using Object = UnityEngine.Object;

namespace Veilward.Tests.PlayMode
{
    public sealed class ElementProgressionCheckpointPlayModeTests
    {
        private string _root;
        private WaveCheckpointStore _store;
        private BattleScenarioConfig _nativeConfig;
        [SetUp] public void SetUp()
        {
            _root = Path.GetFullPath(Path.Combine("TestResults", "ProgressionPlayMode", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(_root); _store = new WaveCheckpointStore(_root);
            MainMenuController.CheckpointStoreFactory = () => _store; BattleSessionContext.Clear();
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            SceneManager.sceneLoaded -= ConfigureTestScenario; BattleSessionContext.Clear();
            yield return SceneManager.LoadSceneAsync("Battle", LoadSceneMode.Single);
            MainMenuController.CheckpointStoreFactory = null;
            if (_nativeConfig != null) Object.Destroy(_nativeConfig);
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
        [UnityTest] public IEnumerator RegBattle177_NativeContinue_UsesCheckpointFactoryAndSavesConfirm()
        { yield return MenuContinue(native: true); }
        [UnityTest] public IEnumerator RegBattle182_LegacyHybridBackup_UnderNativeScenarioRestoresLegacyRules()
        { yield return MenuContinue(native: false); }

        private IEnumerator MenuContinue(bool native)
        {
            // Direct Battle не читает диск. Клонируется только test config, production asset остаётся all-open.
            yield return SceneManager.LoadSceneAsync("Battle", LoadSceneMode.Single); yield return null;
            var original = (BattleScenarioConfig)Get(Object.FindFirstObjectByType<BattleSceneController>(), "scenario");
            _nativeConfig = Object.Instantiate(original);
            JsonUtility.FromJsonOverwrite("{\"scenarioContentId\":\"eight-wave-unlocks-v1\"}", _nativeConfig);
            var s = original.CreateSimulation();
            var e = new WaveCheckpointEnvelope { schemaVersion = 3, scenarioContentId = native ? "eight-wave-unlocks-v1" : "eight-wave-v1", state = s.ExportCheckpoint() };
            if (native)
            {
                e.state.elementProgression = new ElementProgressionCheckpoint
                {
                    rulesId = "wave-unlocks-v1", startResonanceId = "unselected", unlockedElementIds = Array.Empty<string>(),
                    choicesEarned = 0, choicesSpent = 0, spentChoices = Array.Empty<SpentElementChoiceCheckpoint>(), pendingChoiceId = "start-resonance"
                };
            }
            else
            {
                Assert.That(s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId("storm"), new GridCoordinate(-5, -3))).Success, Is.True);
                e.state = s.ExportCheckpoint(); e.schemaVersion = 2;
            }
            File.WriteAllText(native ? _store.PrimaryPath : _store.BackupPath, JsonUtility.ToJson(e));
            if (!native) File.WriteAllText(_store.PrimaryPath, "{broken-native");
            var bytes = File.ReadAllBytes(native ? _store.PrimaryPath : _store.BackupPath);
            SceneManager.sceneLoaded += ConfigureTestScenario;
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single); yield return null;
            var menu = Object.FindFirstObjectByType<MainMenuController>();
            Assert.That(menu.ContinueButton.interactable, Is.True); menu.ContinueButton.onClick.Invoke();
            yield return null; yield return null;
            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            Assert.That(battle.Simulation.State.ElementProgression.RulesId, Is.EqualTo(native ? "wave-unlocks-v1" : "legacy-all-open-v1"));
            Assert.That(File.ReadAllBytes(native ? _store.PrimaryPath : _store.BackupPath), Is.EqualTo(bytes));
            if (native)
            {
                Assert.That(battle.Simulation.State.ElementProgression.PendingChoiceId, Is.EqualTo("start-resonance"));
                Assert.That(battle.StartWave(), Is.EqualTo(WaveTransitionResult.RejectedPendingElementChoice));
                Assert.That(battle.ChooseElement(new ChooseElementCommand("start-resonance", "gale")).Success, Is.True);
                Assert.That(_store.TryLoad("eight-wave-unlocks-v1", _nativeConfig.CreateSimulationForCheckpoint, out var saved), Is.True, saved.Error);
                Assert.That(saved.Envelope.state.elementProgression.startResonanceId, Is.EqualTo("gale"));
                Assert.That(saved.Envelope.state.elementProgression.pendingChoiceId, Is.EqualTo("none"));
            }
            else
            {
                Assert.That(battle.Simulation.State.Towers[0].Definition.Id.Value, Is.EqualTo("storm"));
                Assert.That(battle.Simulation.State.ElementProgression.ChoicesEarned, Is.Zero);
                Assert.That(battle.Simulation.State.ElementProgression.UnlockedElements.Count, Is.EqualTo(4));
            }
        }
        private void ConfigureTestScenario(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "Battle") Set(Object.FindFirstObjectByType<BattleSceneController>(), "scenario", _nativeConfig);
            if (scene.name == "MainMenu") Set(Object.FindFirstObjectByType<MainMenuController>(), "scenario", _nativeConfig);
        }
        private static object Get(object target, string field) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
