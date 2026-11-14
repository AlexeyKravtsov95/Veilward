using System.Linq;
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

        [Test]
        public void Pause_ClearsPendingPlacementAndSellConfirmation()
        {
            Assert.That(_placement.TrySelectCell(FirstCell), Is.True);
            _pause.Pause();
            Assert.That(_placement.HasSelection, Is.False);
            _pause.Resume();

            Assert.That(_battle.PlaceTower(FirstCell).Success, Is.True);
            var gold = _battle.Simulation.State.Gold;
            Assert.That(_placement.TrySelectCell(FirstCell), Is.True);
            _placement.RequestSellSelectedTower();
            Assert.That(_placement.IsSellConfirmationVisible, Is.True);

            _pause.Pause();

            Assert.That(_placement.HasSelection, Is.False);
            Assert.That(_placement.IsSellConfirmationVisible, Is.False);
            Assert.That(_battle.Simulation.State.Gold, Is.EqualTo(gold));
            Assert.That(_placement.ConfirmSellSelectedTower().FailureReason,
                Is.EqualTo(TowerOperationFailureReason.ConfirmationRequired));
        }

        [Test]
        public void Pause_DisablesGameplayButtons()
        {
            _pause.Pause();
            Invoke(_placement, "RefreshUi");

            Assert.That(_startWaveButton.interactable, Is.False);
            Assert.That(_nextWaveButton.interactable, Is.False);
            Assert.That(_placement.UpgradeButton.interactable, Is.False);
        }

        [Test]
        public void Resume_DoesNotCatchUpAndPreservesDeterministicContinuation()
        {
            _battle.StartWave();
            _battle.Simulation.Step();
            var tick = _battle.Simulation.State.Tick;
            SetField(_battle, "_accumulator", 1f);
            _pause.Pause();
            Invoke(_battle, "Update");
            Assert.That(_battle.Simulation.State.Tick, Is.EqualTo(tick));
            Assert.That(GetField<float>(_battle, "_accumulator"), Is.Zero);

            SetField(_battle, "_accumulator", 1f);
            _pause.Resume();
            Invoke(_battle, "Update");
            Assert.That(_battle.Simulation.State.Tick, Is.EqualTo(tick),
                "The resume boundary must discard the current frame delta.");
            Assert.That(GetField<float>(_battle, "_accumulator"), Is.Zero);

            // Компенсируем реальный deltaTime кадра, чтобы аккумулятор содержал ровно один tick.
            SetField(_battle, "_accumulator",
                1f / SimulationTiming.TicksPerSecond - Mathf.Min(Time.deltaTime, 0.25f) + 0.0001f);
            Invoke(_battle, "Update");
            Assert.That(_battle.Simulation.State.Tick, Is.EqualTo(tick + 1));
        }

        [Test]
        public void RegBattle112_PauseAndBackground_FreezeStatusTimersWithoutCatchUp()
        {
            Assert.That(_battle.Simulation.PlaceTower(new PlaceTowerCommand(
                new TowerDefinitionId("flame"), new GridCoordinate(-4, 0))).Success, Is.True);
            Assert.That(_battle.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
            _battle.Simulation.Step();
            var enemy = _battle.Simulation.State.Enemies.Single();
            var health = enemy.Health;
            var remainingTicks = enemy.Burn.RemainingTicks;
            var nextDamageTick = enemy.Burn.NextDamageTick;
            var tick = _battle.Simulation.State.Tick;

            _pause.Pause();
            SetField(_battle, "_accumulator", 1f);
            Invoke(_battle, "Update");
            Invoke(_battle, "Update");
            Assert.That(_battle.Simulation.State.Tick, Is.EqualTo(tick));
            Assert.That(enemy.Health, Is.EqualTo(health));
            Assert.That(enemy.Burn.RemainingTicks, Is.EqualTo(remainingTicks));
            Assert.That(enemy.Burn.NextDamageTick, Is.EqualTo(nextDamageTick));

            _pause.Resume();
            Invoke(_battle, "Update");
            Assert.That(_battle.Simulation.State.Tick, Is.EqualTo(tick),
                "The resume boundary must not catch up paused status ticks.");
            _pause.OnApplicationPause(true);
            _pause.OnApplicationPause(false);
            SetField(_battle, "_accumulator", 1f);
            Invoke(_battle, "Update");
            Assert.That(_battle.Simulation.State.Tick, Is.EqualTo(tick));
            Assert.That(enemy.Burn.RemainingTicks, Is.EqualTo(remainingTicks));
            Assert.That(enemy.Burn.NextDamageTick, Is.EqualTo(nextDamageTick));
        }

        [Test]
        public void Beams_FreezeAndResumeWithoutGlobalTimeScale()
        {
            var timeScale = Time.timeScale;
            var beamObject = new GameObject("Beam");
            beamObject.transform.SetParent(_root.transform, false);
            var beam = beamObject.AddComponent<AttackBeamView>();
            beam.Initialize(_beamMaterial, Vector3.zero, Vector3.one, _battle);
            _pause.Pause();

            Invoke(beam, "Update");

            Assert.That(GetField<float>(beam, "_remainingSeconds"), Is.EqualTo(0.12f).Within(0.0001f));
            Assert.That(Time.timeScale, Is.EqualTo(timeScale));
        }

        [TestCase(BattlePhase.Preparation)]
        [TestCase(BattlePhase.Combat)]
        [TestCase(BattlePhase.WaveComplete)]
        public void ManualPause_AllActivePhases_PreservesBattleState(BattlePhase phase)
        {
            AdvanceToPhase(phase);
            var snapshot = Snapshot();

            _pause.Pause();
            SetField(_battle, "_accumulator", 1f);
            Invoke(_battle, "Update");
            Invoke(_battle, "Update");

            Assert.That(_battle.Simulation.State.Phase, Is.EqualTo(phase));
            Assert.That(Snapshot(), Is.EqualTo(snapshot));
        }

        [TestCase(BattlePhase.Victory)]
        [TestCase(BattlePhase.Defeat)]
        public void PauseAndResume_TerminalStates_RemainTerminal(BattlePhase phase)
        {
            AdvanceToPhase(phase);
            var snapshot = Snapshot();

            _pause.Pause();
            SetField(_battle, "_accumulator", 1f);
            Invoke(_battle, "Update");
            Assert.That(_pause.Resume(), Is.True);
            Invoke(_battle, "Update");

            Assert.That(_battle.Simulation.State.Phase, Is.EqualTo(phase));
            Assert.That(Snapshot(), Is.EqualTo(snapshot));
        }

        private void AdvanceToPhase(BattlePhase phase)
        {
            if (phase == BattlePhase.Preparation)
            {
                return;
            }

            if (phase == BattlePhase.Combat)
            {
                Assert.That(_battle.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
                return;
            }

            var serialized = new SerializedObject(_config);
            serialized.FindProperty("speed").floatValue = 300f;
            if (phase == BattlePhase.Defeat)
            {
                serialized.FindProperty("baseHealth").intValue = 1;
            }
            else
            {
                serialized.FindProperty("towerDamage").intValue = 100;
            }

            var waves = serialized.FindProperty("waves");
            waves.arraySize = phase == BattlePhase.WaveComplete ? 2 : 1;
            for (var index = 0; index < waves.arraySize; index++)
            {
                var wave = waves.GetArrayElementAtIndex(index);
                wave.FindPropertyRelative("enemyCount").intValue = 1;
                wave.FindPropertyRelative("firstSpawnTick").intValue = 0;
                wave.FindPropertyRelative("spawnIntervalTicks").intValue = 30;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            Invoke(_battle, "Start");
            if (phase != BattlePhase.Defeat)
            {
                Assert.That(_battle.PlaceTower(new GridCoordinate(-4, 0)).Success, Is.True);
            }

            Assert.That(_battle.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
            for (var index = 0; index < 4 && _battle.Simulation.State.Phase == BattlePhase.Combat; index++)
            {
                _battle.Simulation.Step();
            }

            Assert.That(_battle.Simulation.State.Phase, Is.EqualTo(phase));
        }

        private string Snapshot()
        {
            var state = _battle.Simulation.State;
            return string.Join("|",
                state.Phase,
                state.Tick,
                state.WaveIndex,
                state.BaseHealth,
                state.Gold,
                state.Kills,
                state.Enemies.Count,
                state.Towers.Count);
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        private static T GetField<T>(object target, string name)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            return (T)field.GetValue(target);
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
