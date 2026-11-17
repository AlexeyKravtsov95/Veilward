using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using Veilward.Application;
using Veilward.Domain;
using Veilward.Presentation;
using Object = UnityEngine.Object;

namespace Veilward.Tests.EditMode
{
    public sealed class BattleElementChoicePresentationTests
    {
        private BattleSceneController _battle;
        private BattlePlacementController _placement;
        private MonoBehaviour _choice;
        private static readonly GridCoordinate Cell = new GridCoordinate(-4, 0);

        [SetUp] public void SetUp()
        {
            BattleSessionContext.Clear();
            EditorSceneManager.OpenScene("Assets/_Game/Scenes/Battle.unity");
            _battle = Object.FindFirstObjectByType<BattleSceneController>();
            _placement = Object.FindFirstObjectByType<BattlePlacementController>();
            Invoke(_battle, "Start"); Invoke(_placement, "Start");
            _choice = _battle.GetComponents<MonoBehaviour>().FirstOrDefault(c => c.GetType().Name == "ElementChoiceController");
            if (_choice != null) Invoke(_choice, "Start");
        }
        [TearDown] public void TearDown() { BattleSessionContext.Clear(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene); }

        [Test] public void RegBattle184_PublicNewGame_IsMandatoryWithoutPreselection()
        {
            Assert.That(_battle.Simulation.State.ElementProgression.PendingChoiceId, Is.EqualTo("start-resonance"));
            RequireChoice();
            Assert.That(Button("Confirm Element Button").interactable, Is.False);
            Assert.That(_placement.TrySelectCell(Cell), Is.False);
            Assert.That(_battle.PlaceTower(Cell).Success, Is.False);
            Assert.That(_battle.Simulation.State.Gold, Is.EqualTo(150));
            Assert.That(_battle.Simulation.State.Towers, Is.Empty);
        }

        [TestCase("flame")] [TestCase("gale")]
        public void RegBattle184_TapCancelConfirm_IsPreviewThenExactlyOneCommand(string option)
        {
            RequireChoice();
            Assert.That((bool)Invoke(_choice, "SelectOption", option), Is.True);
            Assert.That(_battle.Simulation.State.ElementProgression.UnlockedElements, Is.Empty);
            Invoke(_choice, "CancelSelection");
            Assert.That(Button("Confirm Element Button").interactable, Is.False);
            Button("Confirm Element Button").onClick.Invoke();
            Assert.That(_battle.Simulation.State.ElementProgression.PendingChoiceId, Is.EqualTo("start-resonance"));
            Invoke(_choice, "SelectOption", option);
            Button("Confirm Element Button").onClick.Invoke();
            Button("Confirm Element Button").onClick.Invoke();
            Assert.That(_battle.Simulation.State.ElementProgression.StartResonanceId, Is.EqualTo(option));
            Assert.That(_battle.Simulation.State.ElementProgression.UnlockedElements.Count, Is.EqualTo(1));
            Assert.That(_battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.Preparation));
            Assert.That(_battle.PlaceTower(Cell).Success, Is.False, "native parameterless path cannot select hidden test-tower");
            Assert.That(_placement.TrySelectCell(Cell), Is.True);
            _placement.SelectTowerType(new TowerDefinitionId(option));
            Button("Confirm Button").onClick.Invoke();
            Assert.That(_battle.Simulation.State.Towers.Single().Definition.Id.Value, Is.EqualTo(option));
        }

        [Test] public void RegBattle185_LockedCatalog_IsReadOnlyWithExactMissingElementsAndGold()
        {
            RequireChoice(); Invoke(_choice, "SelectOption", "flame"); Invoke(_choice, "ConfirmChoice");
            _placement.TrySelectCell(Cell);
            Assert.That(_placement.CatalogButtons.Count, Is.EqualTo(6));
            _placement.SelectTowerType(new TowerDefinitionId("storm"));
            Assert.That(_placement.BuildStatsText.text, Does.Contain("TIDE").And.Contain("GALE").And.Contain("110"));
            Assert.That(Button("Confirm Button").interactable, Is.False);
            Assert.That(Find("Tower Preview").activeSelf, Is.False);
            Assert.That(Find("Range Preview").activeSelf, Is.False);
            Button("Confirm Button").onClick.Invoke();
            _placement.SelectTowerType(new TowerDefinitionId("steam"));
            Assert.That(_placement.BuildStatsText.text, Does.Contain("TIDE").And.Not.Contain("Missing: FLAME"));
            Assert.That(_battle.Simulation.State.Towers, Is.Empty);
            _placement.SelectTowerType(new TowerDefinitionId("flame"));
            _placement.ConfirmSelection();
            _placement.TrySelectCell(new GridCoordinate(-3, 0));
            _placement.SelectTowerType(new TowerDefinitionId("flame")); _placement.ConfirmSelection();
            _placement.TrySelectCell(new GridCoordinate(-2, 0)); _placement.SelectTowerType(new TowerDefinitionId("flame"));
            Assert.That(_placement.BuildStatsText.text, Does.Contain("Gold").And.Not.Contain("Missing:"));
            Assert.That(Button("Confirm Button").interactable, Is.False);
        }

        [Test] public void RegBattle173_PauseAndCallbacks_DoNotChooseOrBuild()
        {
            RequireChoice(); Invoke(_choice, "SelectOption", "flame");
            var pause = Object.FindFirstObjectByType<BattlePauseController>();
            pause.Pause();
            Button("Confirm Element Button").onClick.Invoke();
            Assert.That((bool)Invoke(_choice, "SelectOption", "gale"), Is.False);
            Assert.That(_battle.ChooseElement(new ChooseElementCommand("start-resonance", "flame")).Success, Is.False);
            _placement.ConfirmSelection(); _placement.StartWave(); _placement.ContinueToNextWave();
            Assert.That(_battle.Simulation.State.Tick, Is.Zero);
            Assert.That(_battle.Simulation.State.ElementProgression.PendingChoiceId, Is.EqualTo("start-resonance"));
            Assert.That(pause.Resume(), Is.True);
            Invoke(_choice, "Refresh");
            Assert.That(Button("Confirm Element Button").interactable, Is.False);
            Button("Confirm Element Button").onClick.Invoke();
            Assert.That(_battle.Simulation.State.ElementProgression.UnlockedElements, Is.Empty);
        }

        [TestCase("missing-start")] [TestCase("wrong-start")] [TestCase("duplicate-start")]
        [TestCase("missing-waves")] [TestCase("wrong-waves")] [TestCase("duplicate-waves")]
        [TestCase("seven-waves")]
        public void RegBattle184_MalformedProgressionSettings_RejectsWithoutAllOpenFallback(string malformed)
        {
            var source = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWave.asset");
            var clone = Object.Instantiate(source);
            try
            {
                var so = new SerializedObject(clone);
                var settings = so.FindProperty("progressionStartingOptions");
                Assert.That(settings, Is.Not.Null, "serialized native settings required");
                if (malformed == "missing-start") settings.arraySize = 0;
                if (malformed == "wrong-start") settings.GetArrayElementAtIndex(1).intValue = (int)ElementKind.Tide;
                if (malformed == "duplicate-start") settings.GetArrayElementAtIndex(1).intValue = (int)ElementKind.Flame;
                var milestones = so.FindProperty("progressionChoiceWaves");
                if (malformed == "missing-waves") milestones.arraySize = 0;
                if (malformed == "wrong-waves") milestones.GetArrayElementAtIndex(1).intValue = 7;
                if (malformed == "duplicate-waves") milestones.GetArrayElementAtIndex(1).intValue = 3;
                if (malformed == "seven-waves") so.FindProperty("waves").arraySize = 7;
                so.ApplyModifiedPropertiesWithoutUndo();
                Assert.Throws<ArgumentException>(() => clone.CreateSimulation());
            }
            finally { Object.DestroyImmediate(clone); }
        }
        [TestCase(3)] [TestCase(6)]
        public void RegBattle184_Milestones_CanonicalClosedOptionsAndSeparateNextStart(int wave)
        {
            var choice = (ElementChoiceController)_choice;
            choice.SelectOption("flame"); Assert.That(choice.ConfirmChoice().Success, Is.True);
            var cp = _battle.Simulation.ExportCheckpoint();
            cp.phase = BattlePhase.WaveComplete; cp.waveIndex = wave - 1;
            cp.elementProgression.choicesEarned = wave == 3 ? 1 : 2;
            if (wave == 6)
            {
                cp.elementProgression.unlockedElementIds = new[] { "flame", "tide" };
                cp.elementProgression.choicesSpent = 1;
                cp.elementProgression.spentChoices = new[] { new SpentElementChoiceCheckpoint { choiceId = "after-wave-3", elementId = "tide" } };
            }
            cp.elementProgression.pendingChoiceId = "after-wave-" + wave;
            _battle.Simulation.RestoreCheckpoint(cp); choice.Refresh();
            Assert.That(choice.SelectedOptionId, Is.Null);
            Assert.That(choice.Options, Is.EqualTo(wave == 3 ? new[] { "tide", "gale", "stone" } : new[] { "gale", "stone" }));
            Assert.That(_battle.StartWave(), Is.EqualTo(WaveTransitionResult.RejectedInvalidPhase));
            Button("Next Wave Button").onClick.Invoke();
            Assert.That(_battle.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.RejectedPendingElementChoice));
            Assert.That(choice.SelectOption("gale"), Is.True);
            Button("Confirm Element Button").onClick.Invoke();
            Assert.That(_battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.WaveComplete));
            Assert.That(_battle.Simulation.State.ElementProgression.ChoicesSpent, Is.EqualTo(wave == 3 ? 1 : 2));
            Button("Confirm Element Button").onClick.Invoke();
            Assert.That(_battle.Simulation.State.WaveIndex, Is.EqualTo(wave - 1));
            Button("Next Wave Button").onClick.Invoke();
            Assert.That(_battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.Preparation));
            Assert.That(_battle.Simulation.State.WaveIndex, Is.EqualTo(wave));
            Button("Start Wave Button").onClick.Invoke();
            Assert.That(_battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.Combat));
        }

        [Test]
        public void RegBattle173_SellModal_DirectCommandsAndForeignButtonsCannotBypassOwner()
        {
            var choice = (ElementChoiceController)_choice;
            choice.SelectOption("flame"); choice.ConfirmChoice();
            Assert.That(_battle.PlaceTower(new TowerDefinitionId("flame"), Cell).Success, Is.True);
            _placement.TrySelectCell(Cell); _placement.RequestSellSelectedTower();
            var before = JsonUtility.ToJson(_battle.Simulation.ExportCheckpoint());
            Assert.That(_battle.SellTower(new TowerId(1)).Success, Is.False);
            Assert.That(_battle.UpgradeTower(new TowerId(1)).Success, Is.False);
            Assert.That(_battle.PlaceTower(new TowerDefinitionId("flame"), new GridCoordinate(-3, 0)).Success, Is.False);
            Assert.That(_battle.ChooseElement(new ChooseElementCommand("start-resonance", "gale")).Success, Is.False);
            Button("Cancel Button").onClick.Invoke(); Button("Start Wave Button").onClick.Invoke();
            Button("Confirm Element Button").onClick.Invoke();
            Assert.That(_placement.IsSellConfirmationVisible, Is.True);
            Assert.That(JsonUtility.ToJson(_battle.Simulation.ExportCheckpoint()), Is.EqualTo(before));
            Button("Confirm Sell Button").onClick.Invoke();
            Assert.That(_battle.Simulation.State.Towers, Is.Empty);
            Assert.That(_battle.Simulation.State.Gold, Is.EqualTo(129));
        }

        [Test]
        public void RegBattle184_RestoreInvalidatesPreviewEvenWithSamePendingId()
        {
            var choice = (ElementChoiceController)_choice;
            choice.SelectOption("flame");
            var cp = _battle.Simulation.ExportCheckpoint();
            _battle.Simulation.RestoreCheckpoint(cp);
            Assert.That(choice.ConfirmChoice().Success, Is.False);
            Assert.That(_battle.Simulation.State.ElementProgression.PendingChoiceId, Is.EqualTo("start-resonance"));
            Assert.That(choice.SelectedOptionId, Is.Null);
        }

        [Test]
        public void RegBattle184_RetrySaveInCombat_IsRejectedWithoutChangingDurableStatus()
        {
            var choice = (ElementChoiceController)_choice;
            choice.SelectOption("flame"); choice.ConfirmChoice();
            var checkpoint = Object.FindFirstObjectByType<BattleCheckpointController>();
            var status = checkpoint.StatusText.text;
            _battle.StartWave();
            Assert.That(_battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.Combat));
            Button("Retry Save Button").onClick.Invoke();
            Assert.That(_battle.RetryCheckpointSave(), Is.False);
            Assert.That(checkpoint.LastSaveSucceeded, Is.True);
            Assert.That(checkpoint.StatusText.text, Is.EqualTo(status));
            LogAssert.NoUnexpectedReceived();
        }

        private void RequireChoice() => Assert.That(_choice, Is.Not.Null, "builder must wire ElementChoiceController");
        private static GameObject Find(string name) => Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(x => x.name == name).gameObject;
        private static Button Button(string name) => Find(name).GetComponent<Button>();
        private static object Invoke(object target, string method, params object[] args)
        {
            var found = target.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(found, Is.Not.Null, method); return found.Invoke(target, args);
        }
    }
}
