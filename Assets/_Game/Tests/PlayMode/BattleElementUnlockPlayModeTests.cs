using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Veilward.Application;
using Veilward.Domain;
using Veilward.Infrastructure;
using Veilward.Presentation;
using Object = UnityEngine.Object;

namespace Veilward.Tests.PlayMode
{
    public sealed class BattleElementUnlockPlayModeTests
    {
        private string _root;
        private WaveCheckpointStore _store;
        private BattleSceneController Battle => Object.FindFirstObjectByType<BattleSceneController>();
        private ElementChoiceController Choice => Object.FindFirstObjectByType<ElementChoiceController>();
        [SetUp] public void SetUp()
        {
            _root = Path.GetFullPath(Path.Combine("TestResults", "NativePresentation", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(_root); _store = new WaveCheckpointStore(_root);
            MainMenuController.CheckpointStoreFactory = () => _store; BattleSessionContext.Clear();
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            BattleSessionContext.Clear(); yield return SceneManager.LoadSceneAsync("Battle"); yield return null;
            MainMenuController.CheckpointStoreFactory = null;
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
        [UnityTest] public IEnumerator RegBattle185_NewGameFlameBuildPauseAndContinue() { yield return NewGame("flame", 80); }
        [UnityTest] public IEnumerator RegBattle185_NewGameGaleBuildPauseAndContinue() { yield return NewGame("gale", 70); }
        private IEnumerator NewGame(string start, int remaining)
        {
            yield return SceneManager.LoadSceneAsync("MainMenu"); yield return null;
            Object.FindFirstObjectByType<MainMenuController>().NewGameButton.onClick.Invoke(); yield return null; yield return null;
            Assert.That(Battle.Simulation.State.ElementProgression.PendingChoiceId, Is.EqualTo("start-resonance"));
            Assert.That(Choice.PreviewOptionId, Is.Null);
            Assert.That(Choice.OptionButtons.Where(b => b.gameObject.activeSelf).Select(b => b.name), Is.EqualTo(new[] { "flame", "gale" }));
            Button("flame").onClick.Invoke(); Button("Cancel Element Button").onClick.Invoke();
            Assert.That(Choice.PreviewOptionId, Is.Null);
            Assert.That(Choice.SelectOption(start), Is.True);
            Button("Pause Button").onClick.Invoke();
            var snapshot = JsonUtility.ToJson(Battle.Simulation.ExportCheckpoint());
            Button("Confirm Element Button").onClick.Invoke(); Button("Start Wave Button").onClick.Invoke();
            Assert.That(Choice.SelectOption(start), Is.False);
            yield return new WaitForSeconds(.15f);
            Assert.That(JsonUtility.ToJson(Battle.Simulation.ExportCheckpoint()), Is.EqualTo(snapshot));
            Button("Resume Button").onClick.Invoke(); Button("Confirm Element Button").onClick.Invoke();
            Assert.That(Choice.PreviewOptionId, Is.Null);
            Assert.That(Battle.Simulation.State.ElementProgression.PendingChoiceId, Is.EqualTo("start-resonance"));
            // Настоящий Continue pending; preview не передаётся через session.
            Button("Main Menu Button").onClick.Invoke(); yield return null; yield return null;
            Object.FindFirstObjectByType<MainMenuController>().ContinueButton.onClick.Invoke(); yield return null; yield return null;
            Assert.That(Choice.PreviewOptionId, Is.Null);
            Assert.That(Choice.SelectOption(start), Is.True); Button("Confirm Element Button").onClick.Invoke();
            var placement = Object.FindFirstObjectByType<BattlePlacementController>();
            Assert.That(placement.TrySelectCell(new GridCoordinate(-4, 0)), Is.True);
            placement.SelectTowerType(new TowerDefinitionId("storm")); Button("Confirm Button").onClick.Invoke();
            Assert.That(Battle.Simulation.State.Towers, Is.Empty);
            Assert.That(placement.BuildStatsText.text, Does.Contain("Missing: TIDE"));
            placement.SelectTowerType(new TowerDefinitionId(start)); Button("Confirm Button").onClick.Invoke();
            Assert.That(Battle.Simulation.State.Gold, Is.EqualTo(remaining));
            Button("Main Menu Button").onClick.Invoke(); yield return null; yield return null;
            Object.FindFirstObjectByType<MainMenuController>().ContinueButton.onClick.Invoke(); yield return null; yield return null;
            Assert.That(Battle.Simulation.State.ElementProgression.StartResonanceId, Is.EqualTo(start));
            Assert.That(Battle.Simulation.State.ElementProgression.PendingChoiceId, Is.EqualTo("none"));
            Assert.That(Choice.PreviewOptionId, Is.Null);
            Assert.That(Battle.Simulation.State.Towers.Single().Definition.Id.Value, Is.EqualTo(start));
        }
        [UnityTest] public IEnumerator RegBattle185_ContinuePending3Confirmed3AndPreparation4() { yield return Milestone(3); }
        [UnityTest] public IEnumerator RegBattle185_ContinuePending6Confirmed6AndPreparation7() { yield return Milestone(6); }
        private IEnumerator Milestone(int wave)
        {
            yield return SceneManager.LoadSceneAsync("Battle"); yield return null;
            var config = (BattleScenarioConfig)typeof(BattleSceneController).GetField("scenario", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(Battle);
            var s = config.CreateSimulation(); s.ChooseElement(new ChooseElementCommand("start-resonance", "flame"));
            // Synthetic safe snapshot для UI границ; это не natural replay и не баланс.
            var cp = s.ExportCheckpoint(); cp.phase = BattlePhase.WaveComplete; cp.waveIndex = wave - 1; cp.tick = wave * 100;
            cp.elementProgression.choicesEarned = wave == 3 ? 1 : 2;
            cp.elementProgression.pendingChoiceId = "after-wave-" + wave;
            if (wave == 6)
            {
                cp.elementProgression.choicesSpent = 1;
                cp.elementProgression.unlockedElementIds = new[] { "flame", "tide" };
                cp.elementProgression.spentChoices = new[] { new SpentElementChoiceCheckpoint { choiceId = "after-wave-3", elementId = "tide" } };
            }
            Assert.That(_store.TrySave(new WaveCheckpointEnvelope { schemaVersion = 3, scenarioContentId = config.ScenarioContentId, state = cp }, s.TryValidateCheckpoint, out var error, config.CreateSimulationForCheckpoint), Is.True, error);
            yield return SceneManager.LoadSceneAsync("MainMenu"); yield return null;
            Object.FindFirstObjectByType<MainMenuController>().ContinueButton.onClick.Invoke(); yield return null; yield return null;
            Assert.That(Choice.PreviewOptionId, Is.Null);
            Assert.That(Battle.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.RejectedPendingElementChoice));
            Assert.That(Choice.SelectOption(wave == 3 ? "tide" : "gale"), Is.True);
            Button("Confirm Element Button").onClick.Invoke();
            Assert.That(Battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.WaveComplete));
            var confirmed = JsonUtility.ToJson(Battle.Simulation.ExportCheckpoint());
            Button("Main Menu Button").onClick.Invoke(); yield return null; yield return null;
            Object.FindFirstObjectByType<MainMenuController>().ContinueButton.onClick.Invoke(); yield return null; yield return null;
            Assert.That(JsonUtility.ToJson(Battle.Simulation.ExportCheckpoint()), Is.EqualTo(confirmed));
            Button("Confirm Element Button").onClick.Invoke();
            Button("Next Wave Button").onClick.Invoke();
            Assert.That(Battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.Preparation));
            Assert.That(Battle.Simulation.State.WaveIndex + 1, Is.EqualTo(wave + 1));
            Button("Start Wave Button").onClick.Invoke();
            Assert.That(Battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.Combat));
        }
        [UnityTest] public IEnumerator RegBattle185_SaveFailureRetryOnlySavesAndStartCanRecover()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu"); yield return null;
            Object.FindFirstObjectByType<MainMenuController>().NewGameButton.onClick.Invoke(); yield return null; yield return null;
            Directory.CreateDirectory(_store.TempPath);
            LogAssert.Expect(LogType.Error, new Regex("Checkpoint save failed:.*"));
            Choice.SelectOption("gale"); Button("Confirm Element Button").onClick.Invoke();
            var checkpoint = Object.FindFirstObjectByType<BattleCheckpointController>();
            Assert.That(checkpoint.LastSaveSucceeded, Is.False);
            Assert.That(checkpoint.StatusText.text, Does.Contain("unsaved"));
            var before = JsonUtility.ToJson(Battle.Simulation.ExportCheckpoint());
            Object.FindFirstObjectByType<BattlePauseController>().OnApplicationPause(true);
            Button("Retry Save Button").onClick.Invoke(); Assert.That(checkpoint.LastSaveSucceeded, Is.False);
            Object.FindFirstObjectByType<BattlePauseController>().OnApplicationPause(false);
            Button("Resume Button").onClick.Invoke();
            Directory.Delete(_store.TempPath);
            Button("Retry Save Button").onClick.Invoke();
            Assert.That(checkpoint.LastSaveSucceeded, Is.True);
            Assert.That(JsonUtility.ToJson(Battle.Simulation.ExportCheckpoint()), Is.EqualTo(before));
            Assert.That(Battle.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
        }
        [UnityTest] public IEnumerator RegBattle185_NextWithSelectedTowerClosesPanelIncludingMilestone()
        {
            foreach (var wave in new[] { 1, 3 })
            {
                yield return SceneManager.LoadSceneAsync("Battle"); yield return null;
                Choice.SelectOption("flame"); Choice.ConfirmChoice();
                var placement = Object.FindFirstObjectByType<BattlePlacementController>();
                var cell = new GridCoordinate(-4, 0);
                placement.TrySelectCell(cell); placement.SelectTowerType(new TowerDefinitionId("flame")); placement.ConfirmSelection();
                placement.TrySelectCell(cell);
                var state = Battle.Simulation.ExportCheckpoint(); state.phase = BattlePhase.WaveComplete; state.waveIndex = wave - 1;
                if (wave == 3) { state.elementProgression.choicesEarned = 1; state.elementProgression.pendingChoiceId = "after-wave-3"; }
                Battle.Simulation.RestoreCheckpoint(state); yield return null;
                if (wave == 3) { Choice.SelectOption("tide"); Button("Confirm Element Button").onClick.Invoke(); }
                yield return null;
                Assert.That(placement.SelectedTowerId.HasValue, Is.True);
                Assert.That(Button("Next Wave Button").interactable, Is.True);
                Button("Next Wave Button").onClick.Invoke();
                Assert.That(Battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.Preparation));
                Assert.That(Battle.Simulation.State.WaveIndex + 1, Is.EqualTo(wave + 1));
                Assert.That(placement.HasSelection, Is.False);
            }
        }

        private static Button Button(string name) => Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(b => b.name == name);
    }
}
