using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
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
    public sealed class BattleTowerCatalogPlayModeTests : HistoricalAllOpenPlayModeFixture
    {
        private string _root;
        private WaveCheckpointStore _store;
        private static readonly GridCoordinate Cell = new GridCoordinate(-4, 0);

        [SetUp]
        public void SetUp()
        {
            _root = Path.GetFullPath(Path.Combine("TestResults", "TowerCatalog", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(_root);
            _store = new WaveCheckpointStore(_root);
            MainMenuController.CheckpointStoreFactory = () => _store;
            BattleSessionContext.Clear();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            BattleSessionContext.Clear();
            yield return SceneManager.LoadSceneAsync("Battle");
            MainMenuController.CheckpointStoreFactory = null;
            Directory.Delete(_root, true);
        }

        [UnityTest]
        public IEnumerator RegBattle150_HybridPauseBlocksAllEconomyAndRepeatedSellPaysOnce()
        {
            foreach (var id in new[] { "steam", "storm" })
            {
                yield return SceneManager.LoadSceneAsync("Battle");
                yield return null;
                var battle = Object.FindFirstObjectByType<BattleSceneController>();
                var placement = Object.FindFirstObjectByType<BattlePlacementController>();
                Assert.That(placement.CatalogButtons.Select(b => b.GetComponentInChildren<Text>().text.Split('\n')[0]),
                    Is.EqualTo(new[] { "FLAME", "TIDE", "GALE", "STONE", "STEAM", "STORM" }));
                placement.TrySelectCell(Cell); placement.SelectTowerType(new TowerDefinitionId(id));
                Button("Pause Button").onClick.Invoke();
                placement.ConfirmSelection();
                Assert.That(battle.Simulation.State.Towers, Is.Empty);
                Button("Resume Button").onClick.Invoke(); yield return null;
                placement.TrySelectCell(Cell); placement.SelectTowerType(new TowerDefinitionId(id));
                placement.ConfirmSelection(); yield return null;
                Assert.That(battle.Simulation.State.Gold, Is.EqualTo(40));
                placement.TrySelectCell(Cell);
                Button("Pause Button").onClick.Invoke();
                Assert.That(battle.UpgradeTower(new TowerId(1)).Success, Is.False);
                Assert.That(battle.SellTower(new TowerId(1)).Success, Is.False);
                Assert.That(battle.PlaceTower(new TowerDefinitionId(id), new GridCoordinate(-3, 0)).Success, Is.False);
                Assert.That(battle.Simulation.State.Gold, Is.EqualTo(40));
                Assert.That(battle.Simulation.State.Towers.Single().Level, Is.EqualTo(1));
                Assert.That(battle.Simulation.AttackEvents, Is.Empty);
                Assert.That(battle.Simulation.ReactionEvents, Is.Empty);
                Assert.That(battle.Simulation.DamageEvents, Is.Empty);
                Button("Resume Button").onClick.Invoke(); yield return null;
                placement.TrySelectCell(Cell); placement.RequestSellSelectedTower();
                Assert.That(placement.ConfirmSellSelectedTower().Refund, Is.EqualTo(77));
                Assert.That(placement.ConfirmSellSelectedTower().Success, Is.False);
                Assert.That(battle.Simulation.State.Gold, Is.EqualTo(117));
            }
        }

        [UnityTest]
        public IEnumerator RegBattle120_CombatPauseAndPointers_BlockMutationAndClickThrough()
        {
            yield return SceneManager.LoadSceneAsync("Battle");
            yield return null;
            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            var placement = Object.FindFirstObjectByType<BattlePlacementController>();
            placement.TrySelectCell(Cell);
            placement.SelectTowerType(new TowerDefinitionId("gale"));
            Button("Pause Button").onClick.Invoke();
            Assert.That(placement.SelectedTowerTypeId.Value, Is.Null.Or.Empty);
            placement.SelectTowerType(new TowerDefinitionId("gale"));
            placement.ConfirmSelection();
            Assert.That(battle.Simulation.State.Towers, Is.Empty);
            Button("Resume Button").onClick.Invoke();
            Pointer(placement, Camera.main.WorldToScreenPoint(battle.GetCellCenter(Cell)));
            Assert.That(placement.HasSelection, Is.False);
            yield return null;
            // Panel background, catalog and Confirm must win current-position raycasts before world selection.
            placement.TrySelectCell(Cell);
            placement.SelectTowerType(new TowerDefinitionId("gale"));
            yield return null;
            var panelPoint = RectTransformUtility.WorldToScreenPoint(null, Button("Confirm Button").transform.position);
            Pointer(placement, panelPoint);
            Assert.That(placement.SelectedCell, Is.EqualTo(Cell));
            Button("Confirm Button").onClick.Invoke();
            Pointer(placement, Camera.main.WorldToScreenPoint(battle.GetCellCenter(new GridCoordinate(-3, 0))));
            Assert.That(placement.HasSelection, Is.False);
            Assert.That(battle.Simulation.State.Gold, Is.EqualTo(70));
            placement.TrySelectCell(Cell);
            placement.RequestSellSelectedTower();
            Pointer(placement, Camera.main.WorldToScreenPoint(battle.GetCellCenter(new GridCoordinate(-3, 0))));
            Assert.That(placement.SelectedTowerId, Is.EqualTo(battle.Simulation.State.Towers[0].Id));
            Assert.That(placement.UpgradeSelectedTower().Success, Is.False);
            placement.StartWave();
            Assert.That(battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.Preparation));
            placement.CancelSellConfirmation();
            placement.StartWave();
            Assert.That(placement.SelectedTowerTypeId.Value, Is.Null.Or.Empty);
            placement.TrySelectCell(Cell);
            Assert.That(placement.TowerStatsText.text, Does.Contain("GALE"));
            Assert.That(placement.UpgradeButton.interactable, Is.False);
            Assert.That(Button("Sell Button").interactable, Is.False);
            Assert.That(placement.UpgradeSelectedTower().Success, Is.False);
            placement.RequestSellSelectedTower();
            Assert.That(placement.IsSellConfirmationVisible, Is.False);
            Assert.That(placement.TrySelectCell(new GridCoordinate(-3, 0)), Is.False);
            Assert.That(battle.Simulation.State.Gold, Is.EqualTo(70));
        }

        [UnityTest]
        public IEnumerator RegBattle123_CatalogStatusEconomyAndContinue_RestoresEveryType()
        {
            foreach (var id in new[] { "flame", "tide", "gale", "stone" })
            {
                yield return SceneManager.LoadSceneAsync("MainMenu");
                yield return null;
                Object.FindFirstObjectByType<MainMenuController>().NewGameButton.onClick.Invoke();
                yield return null;
                yield return null;
                var battle = Object.FindFirstObjectByType<BattleSceneController>();
                var placement = Object.FindFirstObjectByType<BattlePlacementController>();
                Assert.That(placement.TrySelectCell(Cell), Is.True);
                placement.CatalogButtons.Single(b => b.GetComponentInChildren<Text>().text.Split('\n')[0] == id.ToUpperInvariant()).onClick.Invoke();
                var price = battle.Simulation.TowerCatalog.Get(new TowerDefinitionId(id)).Levels[0].Price;
                Button("Confirm Button").onClick.Invoke();
                Button("Confirm Button").onClick.Invoke();
                yield return null;
                Assert.That(battle.Simulation.State.Gold, Is.EqualTo(150 - price));
                Assert.That(battle.Simulation.State.Towers.Single().Definition.Id.Value, Is.EqualTo(id));
                Assert.That(GameObject.Find("Tower 1"), Is.Not.Null);
                // Real frame updates must advance the live fixed simulation and apply configured status.
                placement.StartWave();
                var deadline = Time.realtimeSinceStartup + 6f;
                while (!battle.Simulation.State.Enemies.Any(e => e.Statuses.Count > 0) && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That(battle.Simulation.State.Tick, Is.GreaterThan(0));
                Assert.That(battle.Simulation.State.Enemies.Any(e => e.Statuses.Any(s =>
                    s.Kind == battle.Simulation.State.Towers[0].Definition.StatusEffect.Kind)), Is.True, id);
                yield return SceneManager.LoadSceneAsync("MainMenu");
                yield return null;
                var menu = Object.FindFirstObjectByType<MainMenuController>();
                Assert.That(menu.ContinueButton.interactable, Is.True);
                menu.ContinueButton.onClick.Invoke();
                yield return null;
                yield return null;
                battle = Object.FindFirstObjectByType<BattleSceneController>();
                placement = Object.FindFirstObjectByType<BattlePlacementController>();
                Assert.That(battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.Preparation));
                Assert.That(battle.Simulation.State.Tick, Is.EqualTo(0));
                Assert.That(battle.Simulation.State.Gold, Is.EqualTo(150 - price));
                Assert.That(battle.Simulation.State.Towers.Single().Definition.Id.Value, Is.EqualTo(id));
                Assert.That(GameObject.Find("Tower 1"), Is.Not.Null);
                placement.TrySelectCell(Cell);
                Assert.That(placement.TowerStatsText.text, Does.Contain(id.ToUpperInvariant()));
                placement.RequestSellSelectedTower();
                Assert.That(placement.ConfirmSellSelectedTower().Success, Is.True);
                Assert.That(placement.ConfirmSellSelectedTower().Success, Is.False);
                Assert.That(battle.Simulation.State.Gold, Is.EqualTo(150 - price + price * 70 / 100));
                Assert.That(battle.Simulation.State.Towers, Is.Empty);
                yield return null;
                Assert.That(GameObject.Find("Tower 1"), Is.Null);
            }
        }

        [UnityTest]
        public IEnumerator RegBattle121_LegacyContinue_MigratesAndKeepsHiddenCatalogAndEconomy()
        {
            yield return SceneManager.LoadSceneAsync("Battle");
            yield return null;
            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            Assert.That(battle.PlaceTower(new TowerDefinitionId("test-tower"), Cell).Success, Is.True);
            var state = battle.Simulation.ExportCheckpoint();
            state.towers[0].towerTypeId = null;
            File.WriteAllText(_store.PrimaryPath, JsonUtility.ToJson(new WaveCheckpointEnvelope
            {
                schemaVersion = 1, scenarioContentId = "eight-wave-v1", state = state
            }));
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            var menu = Object.FindFirstObjectByType<MainMenuController>();
            Assert.That(menu.ContinueButton.interactable, Is.True);
            menu.ContinueButton.onClick.Invoke();
            yield return null;
            yield return null;
            battle = Object.FindFirstObjectByType<BattleSceneController>();
            var placement = Object.FindFirstObjectByType<BattlePlacementController>();
            Assert.That(GameObject.Find("Tower 1"), Is.Not.Null);
            Assert.That(battle.Simulation.State.Towers.Single().Definition.Id.Value, Is.EqualTo("test-tower"));
            Assert.That(placement.CatalogButtons.Any(b => b.GetComponentInChildren<Text>(true).text == "Test Tower"), Is.False);
            placement.TrySelectCell(Cell);
            Assert.That(placement.TowerStatsText.text, Does.Contain("Status: None").And.Contain("Upgrade: 50 gold"));
            Assert.That(placement.UpgradeSelectedTower().Success, Is.True);
            Assert.That(placement.TowerStatsText.text, Does.Contain("Damage: 35").And.Contain("Sell refund: 77 gold"));
            placement.RequestSellSelectedTower();
            Assert.That(placement.ConfirmSellSelectedTower().Refund, Is.EqualTo(77));
            Assert.That(placement.ConfirmSellSelectedTower().Success, Is.False);
            Assert.That(battle.Simulation.State.Gold, Is.EqualTo(117));
        }

        private static Button Button(string name) => Object.FindObjectsByType<Button>(
            FindObjectsInactive.Include, FindObjectsSortMode.None).Single(b => b.name == name);
        private static void Pointer(BattlePlacementController placement, Vector2 position) => typeof(BattlePlacementController)
            .GetMethod("TryHandlePointer", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(placement, new object[] { position, null });
    }
}
