using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Veilward.Application;
using Veilward.Domain;
using Veilward.Infrastructure;
using Veilward.Presentation;
using Object = UnityEngine.Object;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Veilward.Tests.PlayMode
{
    public sealed class ProductionUiPlayModeTests
    {
        private static readonly GridCoordinate FirstCell = new GridCoordinate(-4, 0);
        private static readonly GridCoordinate SecondCell = new GridCoordinate(-3, 0);
        private static readonly MethodInfo Dispatch = typeof(BattlePlacementController).GetMethod(
            "DispatchPointer", BindingFlags.Instance | BindingFlags.NonPublic);
        private string _directory;
        private WaveCheckpointStore _store;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.GetFullPath(Path.Combine("TestResults", "Ui1Tests", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(_directory);
            _store = new WaveCheckpointStore(_directory);
            MainMenuController.CheckpointStoreFactory = () => _store;
            BattleSessionContext.Clear();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            MainMenuController.CheckpointStoreFactory = null;
            BattleSessionContext.Clear();
            yield return SceneManager.LoadSceneAsync("Battle");
            Directory.Delete(_directory, true);
        }

        [UnityTest]
        public IEnumerator Ui101_MenuCorruptRetryNewGameAndContinueUseOneSession()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            var menu = Object.FindFirstObjectByType<MainMenuController>();
            var retry = Find<Button>("Retry Load Button");
            Assert.That(menu.ContinueButton.interactable, Is.False);
            Assert.That(menu.StatusText.text, Is.EqualTo("No saved battle."));
            File.WriteAllText(_store.PrimaryPath, "{fixture-corrupt");
            menu.RefreshCheckpointStatus();
            Assert.That(menu.StatusText.text, Is.EqualTo("The save is damaged."));
            Assert.That(retry.gameObject.activeInHierarchy, Is.True);
            Assert.That(menu.ContinueButton.interactable, Is.False);

            File.Delete(_store.PrimaryPath);
            retry.onClick.Invoke();
            Assert.That(menu.StatusText.text, Is.EqualTo("No saved battle."));
            Assert.That(retry.gameObject.activeInHierarchy, Is.False);
            menu.enabled = false;
            menu.enabled = true;
            menu.NewGameButton.onClick.Invoke();
            menu.NewGameButton.onClick.Invoke();
            yield return null;
            yield return null;

            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            Assert.That(battle, Is.Not.Null);
            ConfirmStartingElement(battle);
            var bytes = File.ReadAllBytes(_store.PrimaryPath);
            Find<Button>("Pause Button").onClick.Invoke();
            yield return null;
            Assert.That(Find<Button>("Main Menu Button").gameObject.activeInHierarchy, Is.True);
            Find<Button>("Main Menu Button").onClick.Invoke();
            yield return null;
            yield return null;
            var reloadedMenu = Object.FindFirstObjectByType<MainMenuController>();
            Assert.That(reloadedMenu.ContinueButton.interactable, Is.True);
            Assert.That(reloadedMenu.StatusText.text, Does.Contain("wave 1"));
            reloadedMenu.ContinueButton.onClick.Invoke();
            reloadedMenu.ContinueButton.onClick.Invoke();
            yield return null;
            yield return null;
            var continued = Object.FindFirstObjectByType<BattleSceneController>();
            Assert.That(continued.Simulation.State.ElementProgression.UnlockedElements.Count, Is.EqualTo(1));
            Assert.That(continued.Simulation.State.Gold, Is.EqualTo(150));
            Assert.That(File.ReadAllBytes(_store.PrimaryPath), Is.EqualTo(bytes));
        }

        [UnityTest]
        public IEnumerator Ui102_UpgradeRepeatAndSellConfirmationKeepOneCharge()
        {
            yield return SceneManager.LoadSceneAsync("Battle");
            yield return null;
            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            ConfirmStartingElement(battle);
            var sim = battle.Simulation;
            var rich = sim.ExportCheckpoint();
            rich.gold = 500;
            sim.RestoreCheckpoint(rich);
            var placement = battle.GetComponent<BattlePlacementController>();
            Assert.That(placement.TrySelectCell(FirstCell), Is.True);
            placement.SelectTowerType(new TowerDefinitionId("flame"));
            Find<Button>("Confirm Button").onClick.Invoke();
            Assert.That(placement.TrySelectCell(FirstCell), Is.True);
            placement.enabled = false;
            placement.enabled = true;
            var tower = sim.State.Towers.Single();
            var upgradeCost = sim.TowerCatalog.Get(tower.Definition.Id).Levels[1].Price;
            var before = sim.State.Gold;
            Find<Button>("Upgrade Button").onClick.Invoke();
            Find<Button>("Upgrade Button").onClick.Invoke();
            Assert.That(tower.Level, Is.EqualTo(2));
            Assert.That(sim.State.Gold, Is.EqualTo(before - upgradeCost));

            Find<Button>("Sell Button").onClick.Invoke();
            Assert.That(placement.IsSellConfirmationVisible, Is.True);
            Assert.That(Find<Text>("Sell Confirmation Label").text, Does.Contain("Refund:"));
            Assert.That(placement.TrySelectCell(SecondCell), Is.False);
            Find<Button>("Cancel Sell Button").onClick.Invoke();
            Assert.That(sim.State.Towers.Count, Is.EqualTo(1));
            Find<Button>("Sell Button").onClick.Invoke();
            var refund = tower.InvestedGold * 70 / 100;
            var confirm = Find<Button>("Confirm Sell Button");
            confirm.onClick.Invoke();
            confirm.onClick.Invoke();
            Assert.That(sim.State.Towers, Is.Empty);
            Assert.That(sim.State.Gold, Is.EqualTo(before - upgradeCost + refund));
        }

        [UnityTest]
        public IEnumerator Ui104_HudPressThenDragAndReleaseOverWorldDoesNotSelect()
        {
            yield return SceneManager.LoadSceneAsync("Battle");
            yield return null;
            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            ConfirmStartingElement(battle);
            yield return null;
            var placement = battle.GetComponent<BattlePlacementController>();
            Canvas.ForceUpdateCanvases();
            var hud = ScreenPoint(Find<RectTransform>("Stats Panel"));
            var cell = battle.GetCellCenter(FirstCell);
            var world = (Vector2)Camera.main.WorldToScreenPoint(new Vector3(cell.x, 0, cell.z));
            Assert.That(Hits(hud), Does.Contain("Stats Panel"));
            Assert.That(Hits(world), Is.Empty);
            Send(placement, hud, TouchPhase.Began);
            Send(placement, world, TouchPhase.Moved);
            Send(placement, world, TouchPhase.Ended);
            Assert.That(placement.HasSelection, Is.False);
            Send(placement, world, TouchPhase.Began);
            Send(placement, world, TouchPhase.Ended);
            Assert.That(placement.SelectedCell, Is.EqualTo(FirstCell));
            Assert.That(placement.HasSelection, Is.True);
            Assert.That(battle.Simulation.State.Towers, Is.Empty);
            Assert.That(battle.Simulation.State.Gold, Is.EqualTo(150));
        }

        [UnityTest]
        public IEnumerator Ui103_DefeatUsesActualTotalsAndBlocksField()
        {
            yield return SceneManager.LoadSceneAsync("Battle");
            yield return null;
            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            ConfirmStartingElement(battle);
            var sim = battle.Simulation;
            for (var wave = 0; wave < sim.State.WaveCount && sim.State.Phase != BattlePhase.Defeat; wave++)
            {
                Assert.That(battle.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
                for (var tick = 0; tick < 10000 && sim.State.Phase == BattlePhase.Combat; tick++) sim.Step();
                if (sim.State.Phase == BattlePhase.Defeat) break;
                var choice = battle.GetComponent<ElementChoiceController>();
                if (choice.SelectOption("tide")) Assert.That(choice.ConfirmChoice().Success, Is.True);
                Assert.That(battle.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.Continued));
            }
            yield return null;
            Assert.That(sim.State.Phase, Is.EqualTo(BattlePhase.Defeat));
            Assert.That(Find<Text>("Result Title").text, Is.EqualTo("Defeat"));
            Assert.That(Find<Text>("Result Details").text, Does.Contain($"Wave {sim.State.WaveIndex + 1} of {sim.State.WaveCount}")
                .And.Contain($"{sim.State.Kills} defeated").And.Contain($"Heart 0   ·   {sim.State.Gold} gold"));
            Assert.That(Find<RectTransform>("Battle Result Overlay").gameObject.activeInHierarchy, Is.True);
            Canvas.ForceUpdateCanvases();
            Assert.That(Hits(new Vector2(Screen.width - 4, 4)).First(), Is.EqualTo("Battle Result Overlay"));
        }

        [UnityTest]
        public IEnumerator Ui103_TerminalWriteFailureCanRetryWithPreviouslySelectedTower()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            Find<Button>("New Game Button").onClick.Invoke();
            yield return null;
            yield return null;
            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            ConfirmStartingElement(battle);
            var sim = battle.Simulation;
            var fixture = sim.ExportCheckpoint();
            fixture.baseHealth = 1;
            sim.RestoreCheckpoint(fixture);
            var placement = battle.GetComponent<BattlePlacementController>();
            Assert.That(placement.TrySelectCell(FirstCell), Is.True);
            placement.SelectTowerType(new TowerDefinitionId("flame"));
            Find<Button>("Confirm Button").onClick.Invoke();
            Assert.That(battle.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
            Assert.That(placement.TrySelectCell(FirstCell), Is.True);
            var durable = File.ReadAllBytes(_store.PrimaryPath);
            Directory.CreateDirectory(_store.TempPath);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".+"));
            for (var tick = 0; tick < 10000 && sim.State.Phase == BattlePhase.Combat; tick++) sim.Step();
            yield return null;
            yield return null;
            Assert.That(sim.State.Phase, Is.EqualTo(BattlePhase.Defeat));
            Assert.That(placement.SelectedTowerId, Is.Null);
            Assert.That(File.ReadAllBytes(_store.PrimaryPath), Is.EqualTo(durable));
            var retry = Find<Button>("Retry Save Button");
            Assert.That(retry.gameObject.activeInHierarchy && retry.interactable, Is.True);
            var checkpoint = WaveCheckpointJson.Serialize(new WaveCheckpointEnvelope
            {
                schemaVersion = WaveCheckpointStore.CurrentSchemaVersion,
                scenarioContentId = WaveCheckpointCompatibility.BalancedAbilityBossContentId,
                state = sim.ExportCheckpoint()
            });
            Directory.Delete(_store.TempPath);
            retry.onClick.Invoke();
            Assert.That(File.ReadAllText(_store.PrimaryPath), Is.EqualTo(checkpoint));
            Assert.That(battle.GetComponent<BattleCheckpointController>().LastSaveSucceeded, Is.True);
        }

        private static void ConfirmStartingElement(BattleSceneController battle)
        {
            var choice = battle.GetComponent<ElementChoiceController>();
            Assert.That(choice.SelectOption("flame"), Is.True);
            var unlock = Find<Button>("Confirm Element Button");
            unlock.onClick.Invoke();
            unlock.onClick.Invoke();
            Assert.That(battle.Simulation.State.ElementProgression.UnlockedElements.Count, Is.EqualTo(1));
        }

        private static T Find<T>(string name) where T : Component =>
            Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(view => view.name == name);

        private static Vector2 ScreenPoint(RectTransform view) =>
            RectTransformUtility.WorldToScreenPoint(null, view.TransformPoint(view.rect.center));

        private static string[] Hits(Vector2 point)
        {
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
            return hits.Select(hit => hit.gameObject.name).ToArray();
        }

        private static void Send(BattlePlacementController placement, Vector2 position, TouchPhase phase) =>
            Dispatch.Invoke(placement, new object[] { position, 876, phase });
    }
}
