using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Veilward.Domain;
using Veilward.Presentation;

namespace Veilward.Tests.EditMode
{
    public sealed class BattleSellConfirmationTests
    {
        private static readonly GridCoordinate FirstCell = new GridCoordinate(-3, 1);
        private static readonly GridCoordinate SecondCell = new GridCoordinate(-2, 1);

        private GameObject _root;
        private BattleScenarioConfig _config;
        private BattleSceneController _battle;
        private Material _beamMaterial;
        private Camera _camera;
        private Button _confirmButton;
        private Button _cancelButton;
        private Button _startWaveButton;
        private Button _nextWaveButton;
        private Button _sellButton;
        private Button _confirmSellButton;
        private Button _cancelSellButton;
        private GameObject _towerPanel;
        private GameObject _sellConfirmationPanel;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Sell Confirmation Tests");
            _config = BattleScenarioConfig.CreateDefault();
            var serializedConfig = new SerializedObject(_config);
            serializedConfig.FindProperty("startingGold").intValue = 500;
            serializedConfig.ApplyModifiedPropertiesWithoutUndo();

            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Shade.prefab");
            var towerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/TestTower.prefab");
            Assert.That(enemyPrefab, Is.Not.Null);
            Assert.That(towerPrefab, Is.Not.Null);
            var hud = new GameObject("HUD", typeof(Text));
            hud.transform.SetParent(_root.transform, false);
            var cameraObject = new GameObject("Battle Camera", typeof(Camera));
            cameraObject.transform.SetParent(_root.transform, false);
            _camera = cameraObject.GetComponent<Camera>();
            _beamMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));

            var battleObject = new GameObject("Battle", typeof(BattleSceneController));
            battleObject.transform.SetParent(_root.transform, false);
            _battle = battleObject.GetComponent<BattleSceneController>();
            var serializedBattle = new SerializedObject(_battle);
            serializedBattle.FindProperty("scenario").objectReferenceValue = _config;
            serializedBattle.FindProperty("enemyPrefab").objectReferenceValue = enemyPrefab.transform;
            serializedBattle.FindProperty("towerPrefab").objectReferenceValue = towerPrefab.transform;
            serializedBattle.FindProperty("hudText").objectReferenceValue = hud.GetComponent<Text>();
            serializedBattle.FindProperty("worldCamera").objectReferenceValue = _camera;
            serializedBattle.FindProperty("attackBeamMaterial").objectReferenceValue = _beamMaterial;
            serializedBattle.ApplyModifiedPropertiesWithoutUndo();
            Invoke(_battle, "Start");
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }

            if (_config != null)
            {
                Object.DestroyImmediate(_config);
            }

            if (_beamMaterial != null)
            {
                Object.DestroyImmediate(_beamMaterial);
            }
        }

        [Test]
        public void ConfirmSell_WithoutRequest_DoesNotMutateDomain()
        {
            var placement = CreatePlacement();
            PlaceAndSelect(placement, FirstCell);

            var result = placement.ConfirmSellSelectedTower();

            Assert.That(result.Success, Is.False);
            Assert.That(_battle.Simulation.State.Gold, Is.EqualTo(440));
            Assert.That(_battle.Simulation.State.Towers.Count, Is.EqualTo(1));
            Assert.That(_battle.Simulation.Grid.GetState(FirstCell), Is.EqualTo(GridCellState.Occupied));
        }

        [Test]
        public void SellConfirmation_BlocksConflictingActionsAndButtons()
        {
            var placement = CreatePlacement();
            PlaceAndSelect(placement, FirstCell);
            placement.RequestSellSelectedTower();

            var upgrade = placement.UpgradeSelectedTower();
            placement.RequestSellSelectedTower();
            placement.ConfirmSelection();
            placement.StartWave();
            placement.ContinueToNextWave();

            Assert.That(upgrade.Success, Is.False);
            Assert.That(_battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.Preparation));
            Assert.That(_battle.Simulation.State.Gold, Is.EqualTo(440));
            Assert.That(_battle.Simulation.State.Towers[0].Level, Is.EqualTo(1));
            Assert.That(placement.UpgradeButton.interactable, Is.False);
            Assert.That(_sellButton.interactable, Is.False);
            Assert.That(_startWaveButton.interactable, Is.False);
            Assert.That(_nextWaveButton.interactable, Is.False);
            Assert.That(_confirmButton.interactable, Is.False);
            Assert.That(_cancelButton.interactable, Is.False);
            Assert.That(_confirmSellButton.interactable, Is.True);
            Assert.That(_cancelSellButton.interactable, Is.True);
            Assert.That(_sellConfirmationPanel.activeSelf, Is.True);
        }

        [Test]
        public void ConfirmSell_AfterSelectionChange_DoesNotSellAnotherTower()
        {
            var placement = CreatePlacement();
            Assert.That(_battle.PlaceTower(FirstCell).Success, Is.True);
            Assert.That(_battle.PlaceTower(SecondCell).Success, Is.True);
            Assert.That(placement.TrySelectCell(FirstCell), Is.True);
            var firstId = placement.SelectedTowerId;
            placement.RequestSellSelectedTower();
            Assert.That(placement.TrySelectCell(SecondCell), Is.True);
            var secondId = placement.SelectedTowerId;

            var result = placement.ConfirmSellSelectedTower();

            Assert.That(result.Success, Is.False);
            Assert.That(firstId, Is.Not.EqualTo(secondId));
            Assert.That(placement.SelectedTowerId, Is.EqualTo(secondId));
            Assert.That(_battle.Simulation.State.Gold, Is.EqualTo(380));
            Assert.That(_battle.Simulation.State.Towers.Count, Is.EqualTo(2));
            Assert.That(_battle.Simulation.Grid.GetState(FirstCell), Is.EqualTo(GridCellState.Occupied));
            Assert.That(_battle.Simulation.Grid.GetState(SecondCell), Is.EqualTo(GridCellState.Occupied));
        }

        [Test]
        public void CancelSellConfirmation_RestoresActionsAndPreservesCombatStats()
        {
            var placement = CreatePlacement();
            PlaceAndSelect(placement, FirstCell);
            placement.RequestSellSelectedTower();

            placement.CancelSellConfirmation();

            Assert.That(placement.IsSellConfirmationVisible, Is.False);
            Assert.That(_battle.Simulation.State.Gold, Is.EqualTo(440));
            Assert.That(_battle.Simulation.State.Towers.Count, Is.EqualTo(1));
            Assert.That(_battle.Simulation.Grid.GetState(FirstCell), Is.EqualTo(GridCellState.Occupied));
            Assert.That(placement.UpgradeButton.interactable, Is.True);
            Assert.That(_sellButton.interactable, Is.True);
            Assert.That(_startWaveButton.interactable, Is.True);
            Assert.That(placement.UpgradeSelectedTower().Success, Is.True);

            placement.StartWave();

            Assert.That(_battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.Combat));
            Assert.That(placement.TrySelectCell(FirstCell), Is.True);
            Assert.That(_towerPanel.activeSelf, Is.True);
            Assert.That(placement.TowerStatsText.text, Does.Contain("Level: 2"));
            Assert.That(placement.UpgradeButton.interactable, Is.False);
            Assert.That(_sellButton.interactable, Is.False);
            Assert.That(placement.IsSellConfirmationVisible, Is.False);
        }

        [Test]
        public void ConfirmedSell_ClearsSelectionAndConfirmation()
        {
            var placement = CreatePlacement();
            PlaceAndSelect(placement, FirstCell);
            placement.RequestSellSelectedTower();

            var result = placement.ConfirmSellSelectedTower();

            Assert.That(result.Success, Is.True, result.FailureReason.ToString());
            Assert.That(result.Refund, Is.EqualTo(42));
            Assert.That(_battle.Simulation.State.Gold, Is.EqualTo(482));
            Assert.That(_battle.Simulation.State.Towers, Is.Empty);
            Assert.That(_battle.Simulation.Grid.GetState(FirstCell), Is.EqualTo(GridCellState.Available));
            Assert.That(placement.HasSelection, Is.False);
            Assert.That(placement.SelectedTowerId.HasValue, Is.False);
            Assert.That(placement.IsSellConfirmationVisible, Is.False);
            Assert.That(_sellConfirmationPanel.activeSelf, Is.False);
        }

        private BattlePlacementController CreatePlacement()
        {
            var selection = new GameObject("Selection").transform;
            selection.SetParent(_root.transform, false);
            var preview = new GameObject("Tower Preview").transform;
            preview.SetParent(_root.transform, false);
            var panel = new GameObject("Placement Panel", typeof(RectTransform));
            panel.transform.SetParent(_root.transform, false);
            var rangeObject = new GameObject("Range Preview", typeof(LineRenderer));
            rangeObject.transform.SetParent(_root.transform, false);
            _towerPanel = new GameObject("Tower Panel", typeof(RectTransform));
            _towerPanel.transform.SetParent(_root.transform, false);
            var towerStats = new GameObject("Tower Stats", typeof(RectTransform), typeof(Text));
            towerStats.transform.SetParent(_root.transform, false);
            _sellConfirmationPanel = new GameObject("Sell Confirmation Panel", typeof(RectTransform));
            _sellConfirmationPanel.transform.SetParent(_root.transform, false);
            _confirmButton = CreateButton("Confirm");
            _cancelButton = CreateButton("Cancel");
            _startWaveButton = CreateButton("Start Wave");
            _nextWaveButton = CreateButton("Next Wave");
            _sellButton = CreateButton("Sell");
            _confirmSellButton = CreateButton("Confirm Sell");
            _cancelSellButton = CreateButton("Cancel Sell");

            var placement = _battle.gameObject.AddComponent<BattlePlacementController>();
            var serialized = new SerializedObject(placement);
            serialized.FindProperty("battle").objectReferenceValue = _battle;
            serialized.FindProperty("worldCamera").objectReferenceValue = _camera;
            serialized.FindProperty("selectionHighlight").objectReferenceValue = selection;
            serialized.FindProperty("towerPreview").objectReferenceValue = preview;
            serialized.FindProperty("confirmationPanel").objectReferenceValue = panel;
            serialized.FindProperty("confirmButton").objectReferenceValue = _confirmButton;
            serialized.FindProperty("cancelButton").objectReferenceValue = _cancelButton;
            serialized.FindProperty("startWaveButton").objectReferenceValue = _startWaveButton;
            serialized.FindProperty("nextWaveButton").objectReferenceValue = _nextWaveButton;
            serialized.FindProperty("rangePreview").objectReferenceValue = rangeObject.GetComponent<LineRenderer>();
            serialized.FindProperty("towerPanel").objectReferenceValue = _towerPanel;
            serialized.FindProperty("towerStatsText").objectReferenceValue = towerStats.GetComponent<Text>();
            serialized.FindProperty("upgradeButton").objectReferenceValue = CreateButton("Upgrade");
            serialized.FindProperty("sellButton").objectReferenceValue = _sellButton;
            serialized.FindProperty("sellConfirmationPanel").objectReferenceValue = _sellConfirmationPanel;
            serialized.FindProperty("confirmSellButton").objectReferenceValue = _confirmSellButton;
            serialized.FindProperty("cancelSellButton").objectReferenceValue = _cancelSellButton;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Invoke(placement, "Start");
            return placement;
        }

        private void PlaceAndSelect(BattlePlacementController placement, GridCoordinate coordinate)
        {
            Assert.That(_battle.PlaceTower(coordinate).Success, Is.True);
            Assert.That(placement.TrySelectCell(coordinate), Is.True);
        }

        private Button CreateButton(string name)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Button));
            buttonObject.transform.SetParent(_root.transform, false);
            return buttonObject.GetComponent<Button>();
        }

        private static void Invoke(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, null);
        }
    }
}
