using System;
using System.Collections;
using System.IO;
using System.Linq;
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
        private static BattleSceneController Battle => Object.FindFirstObjectByType<BattleSceneController>();
        private static ElementChoiceController Choice => Object.FindFirstObjectByType<ElementChoiceController>();
        private static BattlePlacementController Placement => Object.FindFirstObjectByType<BattlePlacementController>();
        private static BattleCheckpointController Checkpoint => Object.FindFirstObjectByType<BattleCheckpointController>();
        [SetUp] public void SetUp()
        {
            _root = Path.Combine(UnityEngine.Application.temporaryCachePath, "p23d-" + Guid.NewGuid().ToString("N"));
            _store = new WaveCheckpointStore(_root);
            MainMenuController.CheckpointStoreFactory = () => _store;
            BattleSessionContext.Clear();
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            BattleSessionContext.Clear(); MainMenuController.CheckpointStoreFactory = null;
            yield return SceneManager.LoadSceneAsync("Battle"); yield return null;
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
        [UnityTest] public IEnumerator RegBattle184_NewGameBothStarts_BuildCombatAndContinueUseNativeRules()
        {
            foreach (var start in new[] { "flame", "gale" })
            {
                yield return NewGame();
                Assert.That(Battle.Simulation.State.ElementProgression.PendingChoiceId, Is.EqualTo("start-resonance"));
                Assert.That(Choice.Options, Is.EqualTo(new[] { "flame", "gale" }));
                Assert.That(Choice.SelectedOptionId, Is.Null);
                Assert.That(Button("Confirm Element Button").interactable, Is.False);
                Assert.That(Placement.TrySelectCell(new GridCoordinate(-4, 0)), Is.False);
                Assert.That(File.Exists(_store.PrimaryPath), Is.True);
                Button("Element Option " + start.ToUpperInvariant()).onClick.Invoke();
                Button("Cancel Element Button").onClick.Invoke();
                Button("Confirm Element Button").onClick.Invoke();
                Assert.That(Battle.Simulation.State.ElementProgression.UnlockedElements, Is.Empty);
                Button("Element Option " + start.ToUpperInvariant()).onClick.Invoke();
                Button("Pause Button").onClick.Invoke();
                var before = JsonUtility.ToJson(Battle.Simulation.ExportCheckpoint());
                Button("Confirm Element Button").onClick.Invoke();
                Choice.SelectOption(start); Placement.StartWave();
                yield return new WaitForSecondsRealtime(.1f);
                Assert.That(JsonUtility.ToJson(Battle.Simulation.ExportCheckpoint()), Is.EqualTo(before));
                Button("Resume Button").onClick.Invoke(); yield return null;
                Assert.That(Choice.SelectedOptionId, Is.Null);
                Choice.SelectOption(start); Button("Confirm Element Button").onClick.Invoke();
                Button("Confirm Element Button").onClick.Invoke();
                Assert.That(Battle.Simulation.State.ElementProgression.UnlockedElements.Count, Is.EqualTo(1));
                Placement.TrySelectCell(new GridCoordinate(-4, 0));
                foreach (var locked in new[] { "steam", "storm" })
                {
                    Placement.SelectTowerType(new TowerDefinitionId(locked));
                    Assert.That(Placement.BuildStatsText.text, Does.Contain("Missing:").And.Contain("TIDE"));
                    Assert.That(Button("Confirm Button").interactable, Is.False);
                    Button("Confirm Button").onClick.Invoke();
                    Assert.That(Battle.Simulation.State.Towers, Is.Empty);
                }
                Placement.SelectTowerType(new TowerDefinitionId(start)); Button("Confirm Button").onClick.Invoke();
                var saved = JsonUtility.ToJson(Battle.Simulation.ExportCheckpoint());
                Button("Start Wave Button").onClick.Invoke();
                var diskBeforeRetry = File.ReadAllBytes(_store.PrimaryPath);
                var statusBeforeRetry = Checkpoint.StatusText.text;
                Button("Retry Save Button").onClick.Invoke();
                Assert.That(Battle.RetryCheckpointSave(), Is.False);
                Assert.That(Checkpoint.LastSaveSucceeded, Is.True);
                Assert.That(Checkpoint.StatusText.text, Is.EqualTo(statusBeforeRetry));
                Assert.That(File.ReadAllBytes(_store.PrimaryPath), Is.EqualTo(diskBeforeRetry));
                var tick = Battle.Simulation.State.Tick;
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(Battle.Simulation.State.Tick, Is.GreaterThan(tick));
                yield return Continue();
                Assert.That(JsonUtility.ToJson(Battle.Simulation.ExportCheckpoint()), Is.EqualTo(saved));
                Assert.That(Choice.IsVisible, Is.False);
                Assert.That(Battle.Simulation.State.Towers.Single().Definition.Id.Value, Is.EqualTo(start));
            }
        }

        [UnityTest] public IEnumerator RegBattle184_185_PendingAndConfirmedMilestones_RestoreWithoutPreviewOrDuplicateReward()
        {
            yield return NewGame(); Choice.SelectOption("flame"); Choice.ConfirmChoice();
            foreach (var wave in new[] { 3, 6 })
            {
                // Synthetic safe boundary на настоящем production scenario: не natural replay/баланс.
                var cp = Battle.Simulation.ExportCheckpoint();
                cp.phase = BattlePhase.WaveComplete; cp.waveIndex = wave - 1;
                cp.elementProgression.choicesEarned = wave == 3 ? 1 : 2;
                cp.elementProgression.pendingChoiceId = "after-wave-" + wave;
                Battle.Simulation.RestoreCheckpoint(cp); Assert.That(Checkpoint.SaveCurrentState(), Is.True);
                Choice.Refresh(); Choice.SelectOption(wave == 3 ? "tide" : "gale");
                yield return Continue();
                Assert.That(Choice.SelectedOptionId, Is.Null);
                Assert.That(Choice.Options, Is.EqualTo(wave == 3 ? new[] { "tide", "gale", "stone" } : new[] { "gale", "stone" }));
                var gold = Battle.Simulation.State.Gold;
                Button("Next Wave Button").onClick.Invoke();
                Assert.That(Battle.Simulation.State.WaveIndex, Is.EqualTo(wave - 1));
                Choice.SelectOption(wave == 3 ? "tide" : "gale");
                Button("Confirm Element Button").onClick.Invoke();
                Assert.That(Battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.WaveComplete));
                yield return Continue();
                Assert.That(Choice.IsVisible, Is.False);
                Assert.That(Battle.Simulation.State.ElementProgression.ChoicesSpent, Is.EqualTo(wave == 3 ? 1 : 2));
                Assert.That(Battle.Simulation.State.Gold, Is.EqualTo(gold));
                if (wave == 6) Assert.That(Text("Element Progression"), Does.Not.Contain("после"));
                Button("Next Wave Button").onClick.Invoke();
                Assert.That(Battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.Preparation));
                Assert.That(Battle.Simulation.State.WaveIndex, Is.EqualTo(wave));
            }
        }
        [UnityTest] public IEnumerator RegBattle173_BackgroundPause_ClearsPreviewFreezesAndRequiresExplicitResume()
        {
            yield return NewGame(); Choice.SelectOption("gale");
            var pause = Object.FindFirstObjectByType<BattlePauseController>();
            pause.OnApplicationPause(true);
            var before = JsonUtility.ToJson(Battle.Simulation.ExportCheckpoint());
            Button("Confirm Element Button").onClick.Invoke();
            Assert.That(Choice.ConfirmChoice().Success, Is.False);
            Assert.That(Choice.SelectOption("flame"), Is.False);
            Assert.That(pause.Resume(), Is.False);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(JsonUtility.ToJson(Battle.Simulation.ExportCheckpoint()), Is.EqualTo(before));
            pause.OnApplicationPause(false);
            Assert.That(pause.IsPaused, Is.True);
            Button("Resume Button").onClick.Invoke(); yield return null;
            Assert.That(Choice.SelectedOptionId, Is.Null);
            Button("Confirm Element Button").onClick.Invoke();
            Assert.That(Battle.Simulation.State.ElementProgression.PendingChoiceId, Is.EqualTo("start-resonance"));
        }

        [UnityTest] public IEnumerator RegBattle184_ConfirmSaveFailure_RetryButtonOnlySavesLiveSuccess()
        {
            yield return NewGame();
            var durable = File.ReadAllBytes(_store.PrimaryPath);
            Directory.CreateDirectory(_store.TempPath);
            Choice.SelectOption("flame");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Checkpoint save failed:.*"));
            Button("Confirm Element Button").onClick.Invoke();
            Assert.That(Checkpoint.LastSaveSucceeded, Is.False);
            Assert.That(Checkpoint.StatusText.text, Does.Contain("unsaved"));
            Assert.That(Battle.Simulation.State.ElementProgression.StartResonanceId, Is.EqualTo("flame"));
            Assert.That(File.ReadAllBytes(_store.PrimaryPath), Is.EqualTo(durable));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Checkpoint save failed:.*"));
            Button("Start Wave Button").onClick.Invoke();
            Assert.That(Battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.Preparation));
            Directory.Delete(_store.TempPath);
            var success = JsonUtility.ToJson(Battle.Simulation.ExportCheckpoint());
            Button("Retry Save Button").onClick.Invoke();
            Assert.That(Checkpoint.LastSaveSucceeded, Is.True);
            Assert.That(JsonUtility.ToJson(Battle.Simulation.ExportCheckpoint()), Is.EqualTo(success));
            yield return Continue();
            Assert.That(JsonUtility.ToJson(Battle.Simulation.ExportCheckpoint()), Is.EqualTo(success));
            Assert.That(Choice.SelectedOptionId, Is.Null);
        }

        private IEnumerator NewGame()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu"); yield return null;
            Object.FindFirstObjectByType<MainMenuController>().NewGameButton.onClick.Invoke();
            yield return null; yield return null;
        }
        private IEnumerator Continue()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu"); yield return null;
            var menu = Object.FindFirstObjectByType<MainMenuController>();
            Assert.That(menu.ContinueButton.interactable, Is.True, menu.StatusText.text);
            menu.ContinueButton.onClick.Invoke(); yield return null; yield return null;
        }
        private static string Text(string name) => Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(t => t.name == name).text;
        private static Button Button(string name) => Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(b => b.name == name);
    }
}
