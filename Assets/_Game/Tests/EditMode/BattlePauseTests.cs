using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Veilward.Domain;
using Veilward.Presentation;

namespace Veilward.Tests.EditMode
{
    public sealed class BattlePauseTests
    {
        private static readonly GridCoordinate FirstCell = new GridCoordinate(-3, 1);

        private GameObject _root;
        private BattleScenarioConfig _config;
        private BattleSceneController _battle;
        private BattlePlacementController _placement;
        private BattlePauseController _pause;
        private Material _beamMaterial;
        private Camera _camera;
        private Button _startWaveButton;
        private Button _nextWaveButton;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Pause Tests");
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
            CreatePlacement();
            CreatePause();
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
        public void LifecycleCallbacks_OverlapWithoutAutomaticResume()
        {
            _pause.Pause();
            _pause.OnApplicationFocus(false);
            _pause.OnApplicationPause(true);
            _pause.OnApplicationFocus(true);
            _pause.OnApplicationPause(false);

            Assert.That(_pause.IsPaused, Is.True);
            Assert.That(_battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.Preparation));
            Assert.That(_pause.Resume(), Is.True);
            Assert.That(_pause.IsPaused, Is.False);
            Assert.That(_pause.Resume(), Is.False);
        }

        [Test]
        public void Resume_RequiresActiveFocusedApplication()
        {
            _pause.OnApplicationPause(true);
            Assert.That(_pause.Resume(), Is.False);
            _pause.OnApplicationPause(false);
            _pause.OnApplicationFocus(false);
            Assert.That(_pause.Resume(), Is.False);
            _pause.OnApplicationFocus(true);
            Assert.That(_pause.Resume(), Is.True);
        }

        [Test]
        public void PausedCommands_AreRejectedWithoutMutation()
        {
            var gold = _battle.Simulation.State.Gold;
            _pause.Pause();

            Assert.That(
                _battle.PlaceTower(FirstCell).FailureReason,
                Is.EqualTo(PlaceTowerFailureReason.InvalidPhase));
            Assert.That(
                _battle.UpgradeTower(new TowerId(1)).FailureReason,
                Is.EqualTo(TowerOperationFailureReason.InvalidPhase));
            Assert.That(
                _battle.SellTower(new TowerId(1)).FailureReason,
                Is.EqualTo(TowerOperationFailureReason.InvalidPhase));
            Assert.That(_battle.StartWave(), Is.EqualTo(WaveTransitionResult.RejectedInvalidPhase));
            Assert.That(_battle.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.RejectedInvalidPhase));
            Assert.That(_battle.Simulation.State.Gold, Is.EqualTo(gold));
            Assert.That(_battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.Preparation));
            Assert.That(_placement.TrySelectCell(FirstCell), Is.False);
        }

        private void CreatePlacement()
        {
            var selection = new GameObject("Selection").transform;
            selection.SetParent(_root.transform, false);
            var preview = new GameObject("Tower Preview").transform;
            preview.SetParent(_root.transform, false);
            var panel = new GameObject("Placement Panel", typeof(RectTransform));
            panel.transform.SetParent(_root.transform, false);
            var rangeObject = new GameObject("Range Preview", typeof(LineRenderer));
            rangeObject.transform.SetParent(_root.transform, false);
            var towerPanel = new GameObject("Tower Panel", typeof(RectTransform));
            towerPanel.transform.SetParent(_root.transform, false);
            var towerStats = new GameObject("Tower Stats", typeof(RectTransform), typeof(Text));
            towerStats.transform.SetParent(_root.transform, false);
            var sellConfirmationPanel = new GameObject("Sell Confirmation Panel", typeof(RectTransform));
            sellConfirmationPanel.transform.SetParent(_root.transform, false);
            _startWaveButton = CreateButton("Start Wave");
            _nextWaveButton = CreateButton("Next Wave");

            _placement = _battle.gameObject.AddComponent<BattlePlacementController>();
            var serialized = new SerializedObject(_placement);
            serialized.FindProperty("battle").objectReferenceValue = _battle;
            serialized.FindProperty("worldCamera").objectReferenceValue = _camera;
            serialized.FindProperty("selectionHighlight").objectReferenceValue = selection;
            serialized.FindProperty("towerPreview").objectReferenceValue = preview;
            serialized.FindProperty("confirmationPanel").objectReferenceValue = panel;
            serialized.FindProperty("confirmButton").objectReferenceValue = CreateButton("Confirm");
            serialized.FindProperty("cancelButton").objectReferenceValue = CreateButton("Cancel");
            serialized.FindProperty("startWaveButton").objectReferenceValue = _startWaveButton;
            serialized.FindProperty("nextWaveButton").objectReferenceValue = _nextWaveButton;
            serialized.FindProperty("rangePreview").objectReferenceValue = rangeObject.GetComponent<LineRenderer>();
            serialized.FindProperty("towerPanel").objectReferenceValue = towerPanel;
            serialized.FindProperty("towerStatsText").objectReferenceValue = towerStats.GetComponent<Text>();
            serialized.FindProperty("upgradeButton").objectReferenceValue = CreateButton("Upgrade");
            serialized.FindProperty("sellButton").objectReferenceValue = CreateButton("Sell");
            serialized.FindProperty("sellConfirmationPanel").objectReferenceValue = sellConfirmationPanel;
            serialized.FindProperty("confirmSellButton").objectReferenceValue = CreateButton("Confirm Sell");
            serialized.FindProperty("cancelSellButton").objectReferenceValue = CreateButton("Cancel Sell");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Invoke(_placement, "Start");
        }

        private void CreatePause()
        {
            var overlay = new GameObject("Pause Overlay", typeof(RectTransform), typeof(Image));
            overlay.transform.SetParent(_root.transform, false);
            var pauseButton = CreateButton("Pause");
            var resumeButton = CreateButton("Resume");
            _pause = _battle.gameObject.AddComponent<BattlePauseController>();
            var serialized = new SerializedObject(_pause);
            serialized.FindProperty("battle").objectReferenceValue = _battle;
            serialized.FindProperty("placement").objectReferenceValue = _placement;
            serialized.FindProperty("pauseOverlay").objectReferenceValue = overlay;
            serialized.FindProperty("pauseButton").objectReferenceValue = pauseButton;
            serialized.FindProperty("resumeButton").objectReferenceValue = resumeButton;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var serializedBattle = new SerializedObject(_battle);
            serializedBattle.FindProperty("pauseController").objectReferenceValue = _pause;
            serializedBattle.ApplyModifiedPropertiesWithoutUndo();
            Invoke(_pause, "Start");
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
