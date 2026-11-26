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
        public IEnumerator UiRework102_DecoratedControlsReceivePointerAndModalBlocksWorld()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            var newGame = Find<Button>("New Game Button");
            Assert.That(((Image)newGame.targetGraphic).sprite, Is.Not.Null, "Production actions have authored faces.");
            Click(newGame, .84f);
            yield return null;
            yield return null;
            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            ConfirmStartingElement(battle);
            yield return null;
            Click(Find<Button>("Pause Button"), .76f);
            yield return null;
            Assert.That(battle.IsPaused, Is.True);
            var placement = battle.GetComponent<BattlePlacementController>();
            var cell = battle.GetCellCenter(FirstCell);
            var point = (Vector2)Camera.main.WorldToScreenPoint(new Vector3(cell.x, 0, cell.z));
            Assert.That(Hits(point), Does.Contain("Pause Overlay"));
            Send(placement, point, TouchPhase.Began);
            Send(placement, point, TouchPhase.Ended);
            Assert.That(placement.HasSelection, Is.False);
            Click(Find<Button>("Resume Button"), .83f);
            yield return null;
            Assert.That(battle.IsPaused, Is.False);
            Click(Find<Button>("Pause Button"));
            yield return null;
            Click(Find<Button>("Main Menu Button"));
            yield return null;
            yield return null;
            Click(Find<Button>("Continue Button"), .82f);
            yield return null;
            yield return null;
            Assert.That(Object.FindFirstObjectByType<BattleSceneController>().Simulation.State.Gold, Is.EqualTo(150));
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
        public IEnumerator UiRework103_CardReopenDoesNotDuplicateActions()
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
            var tile = Find<Button>("Catalog flame");
            var face = tile.GetComponentInChildren<Text>().rectTransform;
            var initialFacePosition = face.anchoredPosition;
            Assert.That(initialFacePosition.y, Is.EqualTo(-43), "A cloned inactive template preserves its authored inset.");
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            tile.OnPointerDown(pointer);
            Assert.That(face.anchoredPosition, Is.EqualTo(initialFacePosition + Vector2.down * 2));
            tile.OnPointerUp(pointer);
            Assert.That(face.anchoredPosition, Is.EqualTo(initialFacePosition));
            yield return null;
            Click(Find<Button>("Cancel Button"));
            Assert.That(placement.TrySelectCell(FirstCell), Is.True);
            yield return null;
            Click(tile);
            var build = Find<Button>("Confirm Button");
            Click(build);
            build.onClick.Invoke();
            Assert.That(sim.State.Towers.Count, Is.EqualTo(1));
            Assert.That(sim.State.Gold, Is.EqualTo(500 - sim.State.Towers.Single().InvestedGold));
            Assert.That(placement.TrySelectCell(FirstCell), Is.True);
            placement.enabled = false;
            placement.enabled = true;
            yield return null;
            var tower = sim.State.Towers.Single();
            var upgradeCost = sim.TowerCatalog.Get(tower.Definition.Id).Levels[1].Price;
            var before = sim.State.Gold;
            Click(Find<Button>("Upgrade Button"));
            Find<Button>("Upgrade Button").onClick.Invoke();
            Assert.That(tower.Level, Is.EqualTo(2));
            Assert.That(sim.State.Gold, Is.EqualTo(before - upgradeCost));

            Click(Find<Button>("Sell Button"));
            Assert.That(placement.IsSellConfirmationVisible, Is.True);
            Assert.That(Find<Text>("Sell Confirmation Label").text, Does.Contain("Refund:"));
            Assert.That(placement.TrySelectCell(SecondCell), Is.False);
            yield return null;
            Click(Find<Button>("Cancel Sell Button"));
            Assert.That(sim.State.Towers.Count, Is.EqualTo(1));
            Click(Find<Button>("Sell Button"));
            var refund = tower.InvestedGold * 70 / 100;
            var confirm = Find<Button>("Confirm Sell Button");
            yield return null;
            Click(confirm);
            confirm.onClick.Invoke();
            Assert.That(sim.State.Towers, Is.Empty);
            Assert.That(sim.State.Gold, Is.EqualTo(before - upgradeCost + refund));
        }

        [UnityTest]
        public IEnumerator UiRework107_CatalogLeavesSelectedCentralBuildCellVisible()
        {
            yield return SceneManager.LoadSceneAsync("Battle");
            yield return null;
            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            ConfirmStartingElement(battle);
            var placement = battle.GetComponent<BattlePlacementController>();
            var coordinate = new GridCoordinate(3, 3);
            Assert.That(placement.TrySelectCell(coordinate), Is.True);
            yield return null;

            Canvas.ForceUpdateCanvases();
            var point = (Vector2)Camera.main.WorldToScreenPoint(battle.GetCellCenter(coordinate));
            var drawer = Find<RectTransform>("Placement Panel");
            var hover = RectTransformUtility.RectangleContainsScreenPoint(drawer, point);
            Assert.That(hover, Is.False, $"The build drawer covers selected cell {coordinate} at {point}.");
            Click(Find<Button>("Catalog flame"));
            Assert.That(Find<Button>("Confirm Button").interactable, Is.True);
            Click(Find<Button>("Cancel Button"));
            Assert.That(placement.TrySelectCell(FirstCell), Is.True);
            yield return null;
            Assert.That(Find<RectTransform>("Tower Catalog").GetComponent<GridLayoutGroup>().constraintCount, Is.EqualTo(6));
        }

        [UnityTest]
        public IEnumerator UiRework104_LockedSchoolAndPricesRefreshInDecoratedCatalog()
        {
            yield return SceneManager.LoadSceneAsync("Battle");
            yield return null;
            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            ConfirmStartingElement(battle);
            var placement = battle.GetComponent<BattlePlacementController>();
            var simulation = battle.Simulation;
            var poor = simulation.ExportCheckpoint();
            poor.gold = 10;
            simulation.RestoreCheckpoint(poor);
            Assert.That(placement.TrySelectCell(FirstCell), Is.True);

            var tide = Find<Button>("Catalog tide");
            var glyph = tide.GetComponentInChildren<VeilwardUiGlyph>();
            var lockBadge = tide.transform.Find("Lock Badge").GetComponent<VeilwardUiGlyph>();
            Assert.That(glyph.Glyph, Is.EqualTo(VeilwardGlyph.Tide));
            Assert.That(lockBadge.Glyph, Is.EqualTo(VeilwardGlyph.Lock));
            Assert.That(lockBadge.gameObject.activeSelf, Is.True);
            yield return null;
            Click(tide);
            Assert.That(tide.GetComponent<Outline>().enabled, Is.True);
            Assert.That(placement.BuildStatsText.text, Does.Contain("Price: 60 gold")
                .And.Contain("Damage ").And.Contain("Missing: TIDE"));
            var build = Find<Button>("Confirm Button");
            Assert.That(build.interactable, Is.False);
            Click(build);
            Assert.That(simulation.State.Towers, Is.Empty);

            poor.phase = BattlePhase.WaveComplete;
            poor.waveIndex = 2;
            poor.elementProgression.choicesEarned = 1;
            poor.elementProgression.pendingChoiceId = "after-wave-3";
            simulation.RestoreCheckpoint(poor);
            yield return null;
            yield return null;
            Click(Find<Button>("tide"));
            Click(Find<Button>("Confirm Element Button"));
            yield return null;
            Click(Find<Button>("Next Wave Button"));
            Assert.That(placement.TrySelectCell(FirstCell), Is.True);
            yield return null;
            Click(tide);
            Assert.That(tide.GetComponentInChildren<VeilwardUiGlyph>(), Is.SameAs(glyph));
            Assert.That(glyph.Glyph, Is.EqualTo(VeilwardGlyph.Tide));
            Assert.That(lockBadge.gameObject.activeSelf, Is.False);
            Assert.That(placement.BuildStatsText.text, Does.Contain("Price: 60 gold")
                .And.Contain("Damage ").And.Contain("Not enough gold"));
            Assert.That(build.interactable, Is.False);
            Click(build);
            Assert.That(simulation.State.Towers, Is.Empty);
            Assert.That(simulation.State.Gold, Is.EqualTo(10));
        }

        [UnityTest]
        public IEnumerator UiRework105_SaveRetryRemainsReachableAcrossModals()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            Click(Find<Button>("New Game Button"));
            yield return null;
            yield return null;
            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            var checkpoint = battle.GetComponent<BattleCheckpointController>();
            var retry = Find<Button>("Retry Save Button");
            var original = File.ReadAllBytes(_store.PrimaryPath);
            Directory.CreateDirectory(_store.TempPath);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Checkpoint save failed:.*"));
            Assert.That(checkpoint.SaveCurrentState(), Is.False);
            yield return null;
            Assert.That(Find<RectTransform>("Element Choice Overlay").gameObject.activeInHierarchy, Is.True);
            Assert.That(retry.gameObject.activeInHierarchy && retry.interactable, Is.True);
            Directory.Delete(_store.TempPath);
            Click(retry);
            Assert.That(File.ReadAllBytes(_store.PrimaryPath), Is.EqualTo(original));
            Assert.That(battle.Simulation.State.ElementProgression.PendingChoiceId, Is.EqualTo("start-resonance"));
            yield return null;

            Click(Find<Button>("flame"));
            Directory.CreateDirectory(_store.TempPath);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Checkpoint save failed:.*"));
            Click(Find<Button>("Confirm Element Button"));
            yield return null;
            Assert.That(checkpoint.LastSaveSucceeded, Is.False);
            Assert.That(battle.Simulation.State.ElementProgression.UnlockedElements.Count, Is.EqualTo(1));
            Assert.That(retry.gameObject.activeInHierarchy && retry.interactable, Is.True);
            Directory.Delete(_store.TempPath);
            var placement = battle.GetComponent<BattlePlacementController>();
            Assert.That(placement.TrySelectCell(new GridCoordinate(3, 1)), Is.True);
            yield return null;
            var drawer = Find<RectTransform>("Placement Panel");
            Assert.That(drawer.anchorMin.x, Is.EqualTo(.5f));
            Assert.That(Find<RectTransform>("Tower Catalog").GetComponent<GridLayoutGroup>().constraintCount, Is.EqualTo(6));
            Assert.That(retry.gameObject.activeInHierarchy && !retry.interactable, Is.True);
            Assert.That(Find<RectTransform>("Save Warning").anchorMin.y, Is.EqualTo(1 - drawer.anchorMin.y),
                "The warning must stay on the opposite edge from the catalog.");
            foreach (var school in new[] { "flame", "tide", "gale" })
                Click(Find<Button>("Catalog " + school));
            Click(retry);
            Assert.That(checkpoint.LastSaveSucceeded, Is.False);
            Click(Find<Button>("Cancel Button"));
            yield return null;
            Assert.That(retry.interactable, Is.True);
            Click(retry);
            yield return null;
            Assert.That(checkpoint.LastSaveSucceeded, Is.True);
            Assert.That(retry.gameObject.activeInHierarchy, Is.False);
            Assert.That(battle.Simulation.State.ElementProgression.UnlockedElements.Count, Is.EqualTo(1));
            Assert.That(battle.Simulation.State.Gold, Is.EqualTo(150));
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
            Click(retry);
            var written = WaveCheckpointJson.Deserialize(File.ReadAllText(_store.PrimaryPath));
            Assert.That(WaveCheckpointJson.Serialize(written), Is.EqualTo(checkpoint));
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

        private static void Click(Button button, float horizontal = .5f)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var local = new Vector2(Mathf.Lerp(rect.rect.xMin, rect.rect.xMax, horizontal), rect.rect.center.y);
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(local))
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, $"{button.name} at {pointer.position} on {Screen.width}×{Screen.height}, " +
                $"active={button.gameObject.activeInHierarchy}, depth={button.targetGraphic.depth}");
            Assert.That(hits[0].gameObject, Is.EqualTo(button.gameObject), button.name);
            pointer.pointerCurrentRaycast = hits[0];
            ExecuteEvents.Execute(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(hits[0].gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

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
