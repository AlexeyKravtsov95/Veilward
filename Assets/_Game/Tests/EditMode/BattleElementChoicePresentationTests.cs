using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Veilward.Domain;
using Object = UnityEngine.Object;
using UnityEditor;
using Veilward.Presentation;

namespace Veilward.Tests.EditMode
{
    public sealed class BattleElementChoicePresentationTests
    {
        [Test]
        public void RegBattle173_PublicEightWaveStartsWithMandatoryUnselectedChoice()
        {
            var config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWave.asset");
            var simulation = config.CreateSimulation();
            Assert.That(config.ScenarioContentId, Is.EqualTo("eight-wave-unlocks-v1"));
            Assert.That(simulation.State.ElementProgression.PendingChoiceId, Is.EqualTo("start-resonance"));
            Assert.That(simulation.State.ElementProgression.UnlockedElements, Is.Empty);
            Assert.That(simulation.State.Gold, Is.EqualTo(150));
        }

        private BattleSceneController _battle;
        private BattlePlacementController _placement;
        private Component _choice;
        private readonly GridCoordinate _cell = new GridCoordinate(-4, 0);
        private void Open()
        {
            EditorSceneManager.OpenScene("Assets/_Game/Scenes/Battle.unity");
            _battle = Object.FindFirstObjectByType<BattleSceneController>();
            _placement = Object.FindFirstObjectByType<BattlePlacementController>();
            Call(_battle, "Start"); Call(_placement, "Start");
            _choice = _battle.GetComponent("ElementChoiceController");
            Assert.That(_choice, Is.Not.Null, "Builder must wire the element choice controller");
            Call(_choice, "Start"); Call(_choice, "Refresh");
        }
        [TearDown] public void TearDown() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);

        [TestCase("flame", 70)] [TestCase("gale", 80)]
        public void RegBattle173_TapCancelConfirmAndExplicitPurchase(string start, int price)
        {
            Open(); var s = _battle.Simulation;
            Assert.That(Button("Confirm Element Button").interactable, Is.False);
            Assert.That((bool)Call(_choice, "SelectOption", "tide"), Is.False);
            Assert.That((bool)Call(_choice, "SelectOption", start), Is.True);
            Assert.That(s.State.ElementProgression.UnlockedElements, Is.Empty);
            Call(_choice, "CancelSelection");
            Button("Confirm Element Button").onClick.Invoke();
            Assert.That(s.State.ElementProgression.PendingChoiceId, Is.EqualTo("start-resonance"));
            Assert.That((bool)Call(_choice, "SelectOption", start), Is.True);
            Button("Confirm Element Button").onClick.Invoke();
            var snapshot = JsonUtility.ToJson(s.ExportCheckpoint());
            Button("Confirm Element Button").onClick.Invoke();
            Assert.That(JsonUtility.ToJson(s.ExportCheckpoint()), Is.EqualTo(snapshot));
            Assert.That(_battle.PlaceTower(_cell).Success, Is.False, "No hidden test-tower fallback");
            Assert.That(_placement.TrySelectCell(_cell), Is.True);
            Assert.That(_placement.CatalogButtons.Count, Is.EqualTo(6));
            _placement.SelectTowerType(new TowerDefinitionId(start));
            Button("Confirm Button").onClick.Invoke();
            Assert.That(s.State.Towers.Single().Definition.Id.Value, Is.EqualTo(start));
            Assert.That(s.State.Gold, Is.EqualTo(150 - price));
        }

        [TestCase("steam", "FLAME", "TIDE")]
        [TestCase("storm", "TIDE", "GALE")]
        public void RegBattle184_LockedCardShowsExactRequirementsWithoutWorldPreview(string id, string first, string second)
        {
            Open(); Choose("flame"); _placement.TrySelectCell(_cell);
            _placement.SelectTowerType(new TowerDefinitionId(id));
            var text = _placement.BuildStatsText.text;
            Assert.That(text, Does.Contain("110 gold"));
            Assert.That(text, Does.Contain("TIDE"));
            if (id == "storm") Assert.That(text, Does.Contain(second));
            else Assert.That(text, Does.Not.Contain("Missing: FLAME"));
            Assert.That(GameObject.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(t => t.name == "Tower Preview").gameObject.activeSelf, Is.False);
            Button("Confirm Button").onClick.Invoke();
            Assert.That(_battle.Simulation.State.Gold, Is.EqualTo(150));
            Assert.That(_battle.Simulation.State.Towers, Is.Empty);
        }

        [TestCase("flame", 3)] [TestCase("gale", 3)]
        [TestCase("flame", 6)] [TestCase("gale", 6)]
        public void RegBattle185_PendingConfirmNextAndStartAreSeparate(string start, int milestone)
        {
            Open(); var s = BattleElementUnlockTests.Native();
            BattleElementUnlockTests.Choose(s, "start-resonance", start);
            BattleElementUnlockTests.Reach(s, 3);
            if (milestone == 6)
            {
                BattleElementUnlockTests.Choose(s, "after-wave-3", "tide");
                s.ContinueToNextWave(); BattleElementUnlockTests.Reach(s, 6);
            }
            Set(_battle, "_simulation", s);
            _battle.GetComponent<BattleCheckpointController>().Configure(null, s, "eight-wave-unlocks-v1"); Call(_choice, "Refresh");
            Assert.That(_battle.ContinueToNextWave(), Is.Not.EqualTo(WaveTransitionResult.Continued));
            var expected = new[] { "flame", "tide", "gale", "stone" }
                .Where(id => id != start && (milestone != 6 || id != "tide")).ToArray();
            var buttons = (System.Collections.Generic.IReadOnlyList<Button>)_choice.GetType().GetProperty("OptionButtons").GetValue(_choice);
            Assert.That(buttons.Where(b => b.gameObject.activeSelf).Select(b => b.name), Is.EqualTo(expected));
            Choose(expected[0]);
            Assert.That(s.State.Phase, Is.EqualTo(BattlePhase.WaveComplete));
            Assert.That(s.State.WaveIndex + 1, Is.EqualTo(milestone));
            Assert.That(_battle.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.Continued));
            Assert.That(s.State.Phase, Is.EqualTo(BattlePhase.Preparation));
            Assert.That(s.State.WaveIndex + 1, Is.EqualTo(milestone + 1));
            Assert.That(_battle.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
        }

        [Test]
        public void RegBattle185_PauseAndForeignModalsGuardDirectApiAndRealCallbacks()
        {
            Open(); var s = _battle.Simulation;
            Call(_choice, "SelectOption", "flame");
            var before = JsonUtility.ToJson(s.ExportCheckpoint());
            Button("Confirm Button").onClick.Invoke(); Button("Start Wave Button").onClick.Invoke();
            Assert.That(_placement.TrySelectCell(_cell), Is.False);
            Assert.That(_battle.PlaceTower(new TowerDefinitionId("flame"), _cell).Success, Is.False);
            var pause = Object.FindFirstObjectByType<BattlePauseController>();
            pause.Pause(); Button("Confirm Element Button").onClick.Invoke();
            Assert.That(((ElementChoiceResult)Call(_choice, "ConfirmChoice")).Success, Is.False);
            Assert.That(JsonUtility.ToJson(s.ExportCheckpoint()), Is.EqualTo(before));
            Assert.That(pause.Resume(), Is.True);
            Button("Confirm Element Button").onClick.Invoke();
            Assert.That(s.State.ElementProgression.PendingChoiceId, Is.EqualTo("start-resonance"));
            Choose("flame");
            _placement.TrySelectCell(_cell); _placement.SelectTowerType(new TowerDefinitionId("flame")); _placement.ConfirmSelection();
            _placement.TrySelectCell(_cell); _placement.RequestSellSelectedTower();
            before = JsonUtility.ToJson(s.ExportCheckpoint());
            Assert.That(_battle.SellTower(new TowerId(1)).Success, Is.False);
            Assert.That(_battle.UpgradeTower(new TowerId(1)).Success, Is.False);
            Assert.That(_battle.StartWave(), Is.Not.EqualTo(WaveTransitionResult.Started));
            Assert.That(_battle.ChooseElement(new ChooseElementCommand("after-wave-3", "tide")).Success, Is.False);
            Assert.That(JsonUtility.ToJson(s.ExportCheckpoint()), Is.EqualTo(before));
            Button("Confirm Sell Button").onClick.Invoke();
            Assert.That(s.State.Towers, Is.Empty);
        }


        [TestCase("{\"rulesId\":\"wrong\"}")]
        [TestCase("{\"startingOptions\":[0]}")]
        [TestCase("{\"startingOptions\":[2,0]}")]
        [TestCase("{\"choiceWaves\":[3,7]}")]
        [TestCase("{\"choiceWaves\":[]}")]
        public void RegBattle184_MalformedNativeSettingsRejectWithoutAllOpenFallback(string mutation)
        {
            var source = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWave.asset");
            var config = Object.Instantiate(source);
            try
            {
                JsonUtility.FromJsonOverwrite("{\"progression\":" + mutation + "}", config);
                Assert.Throws<ArgumentException>(() => config.CreateSimulation());
            }
            finally { Object.DestroyImmediate(config); }
        }
        [TestCase("progression")] [TestCase("waves")] [TestCase("scenarioContentId")]
        public void RegBattle184_MissingOrLegacyNativeConfigCannotStartAllOpen(string field)
        {
            var config = Object.Instantiate(AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWave.asset"));
            try
            {
                var serialized = new SerializedObject(config); var property = serialized.FindProperty(field);
                if (field == "scenarioContentId") property.stringValue = "eight-wave-v1";
                else if (field == "waves") property.arraySize = 0;
                else property.FindPropertyRelative("rulesId").stringValue = string.Empty;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.Throws<ArgumentException>(() => config.CreateSimulation());
            }
            finally { Object.DestroyImmediate(config); }
        }
        [Test]
        public void RegBattle184_UnlockedUnaffordableCardExplainsGoldSeparately()
        {
            Open(); Choose("flame");
            _placement.TrySelectCell(_cell); _placement.SelectTowerType(new TowerDefinitionId("flame")); _placement.ConfirmSelection();
            _placement.TrySelectCell(new GridCoordinate(-3, 0)); _placement.SelectTowerType(new TowerDefinitionId("flame")); _placement.ConfirmSelection();
            _placement.TrySelectCell(new GridCoordinate(-2, 0)); _placement.SelectTowerType(new TowerDefinitionId("flame"));
            Assert.That(_placement.BuildStatsText.text, Does.Contain("Недостаточно Gold").And.Not.Contain("Missing:"));
            Assert.That(Button("Confirm Button").interactable, Is.False);
            Button("Confirm Button").onClick.Invoke();
            Assert.That(_battle.Simulation.State.Gold, Is.EqualTo(10));
            Assert.That(_battle.Simulation.State.Towers.Count, Is.EqualTo(2));
        }
        [Test]
        public void RegBattle173_StalePreviewCannotSpendAnotherPendingChoice()
        {
            Open(); var s = BattleElementUnlockTests.Native();
            BattleElementUnlockTests.Choose(s, "start-resonance", "flame"); BattleElementUnlockTests.Reach(s, 3);
            Set(_battle, "_simulation", s); _battle.GetComponent<BattleCheckpointController>().Configure(null, s, "eight-wave-unlocks-v1");
            Call(_choice, "Refresh"); Call(_choice, "SelectOption", "tide");
            Assert.That(_battle.ChooseElement(new ChooseElementCommand("after-wave-3", "stone")).Success, Is.True);
            s.ContinueToNextWave(); BattleElementUnlockTests.Reach(s, 6);
            Assert.That(((ElementChoiceResult)Call(_choice, "ConfirmChoice")).Success, Is.False);
            Assert.That(s.State.ElementProgression.PendingChoiceId, Is.EqualTo("after-wave-6"));
            Assert.That(s.State.ElementProgression.ChoicesSpent, Is.EqualTo(1));
            Assert.That(ChoicePreview(), Is.Null);
        }
        private string ChoicePreview() => (string)_choice.GetType().GetProperty("PreviewOptionId").GetValue(_choice);
        [Test]
        public void RegBattle185_HudSafeAreaKeepsEdgeControlsInsideInsets()
        {
            Open(); var safe = Object.FindFirstObjectByType<Canvas>().GetComponent("BattleHudSafeArea");
            Assert.That(safe, Is.Not.Null);
            var pause = Button("Pause Button").GetComponent<RectTransform>();
            var menu = Button("Main Menu Button").GetComponent<RectTransform>();
            var right = pause.anchoredPosition; var left = menu.anchoredPosition;
            Call(safe, "ApplySafeArea", new Rect(60, 30, 1800, 1000), new Vector2(1920,1080), 1f);
            Assert.That(pause.anchoredPosition, Is.EqualTo(right + new Vector2(-60, -50)));
            Assert.That(menu.anchoredPosition, Is.EqualTo(left + new Vector2(60, 30)));
        }

        [TestCase(1)] [TestCase(3)]
        public void RegBattle185_NextButtonClosesOwnTowerSelectionAfterOrdinaryOrChoiceWave(int wave)
        {
            Open(); Choose("flame");
            _placement.TrySelectCell(_cell); _placement.SelectTowerType(new TowerDefinitionId("flame")); _placement.ConfirmSelection();
            _placement.TrySelectCell(_cell);
            var state = _battle.Simulation.ExportCheckpoint(); state.phase = BattlePhase.WaveComplete; state.waveIndex = wave - 1;
            if (wave == 3) { state.elementProgression.choicesEarned = 1; state.elementProgression.pendingChoiceId = "after-wave-3"; }
            _battle.Simulation.RestoreCheckpoint(state);
            Call(_placement, "RefreshUi"); Call(_choice, "Refresh");
            if (wave == 3) Choose("tide");
            Assert.That(_placement.SelectedTowerId.HasValue, Is.True);
            Call(_placement, "RefreshUi");
            Assert.That(Button("Next Wave Button").interactable, Is.True);
            Button("Next Wave Button").onClick.Invoke();
            Assert.That(_battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.Preparation));
            Assert.That(_battle.Simulation.State.WaveIndex + 1, Is.EqualTo(wave + 1));
            Assert.That(_placement.HasSelection, Is.False);
        }

        private void Choose(string id) { Call(_choice, "Refresh"); Assert.That(Call(_choice, "SelectOption", id), Is.EqualTo(true)); Assert.That(((ElementChoiceResult)Call(_choice, "ConfirmChoice")).Success, Is.True); }
        private static Button Button(string name) => Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(b => b.name == name);
        private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
