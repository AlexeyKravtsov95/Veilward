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
    public sealed class BattleCombatVisualTests
    {
        private GameObject _root;
        private BattleScenarioConfig _config;
        private BattleSceneController _battle;
        private Material _beamMaterial;
        private Text _hud;
        private Camera _camera;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Combat Visual Tests");
            _config = BattleScenarioConfig.CreateDefault();
            var serializedConfig = new SerializedObject(_config);
            serializedConfig.FindProperty("towerRange").floatValue = 3f;
            serializedConfig.FindProperty("towerFireRate").floatValue = 2f;
            serializedConfig.FindProperty("towerDamage").intValue = 25;
            serializedConfig.ApplyModifiedPropertiesWithoutUndo();
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Shade.prefab");
            var towerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/TestTower.prefab");
            Assert.That(enemyPrefab, Is.Not.Null);
            Assert.That(towerPrefab, Is.Not.Null);

            var hudObject = new GameObject("HUD", typeof(Text));
            hudObject.transform.SetParent(_root.transform, false);
            _hud = hudObject.GetComponent<Text>();
            var cameraObject = new GameObject("Battle Camera", typeof(Camera));
            cameraObject.transform.SetParent(_root.transform, false);
            _camera = cameraObject.GetComponent<Camera>();
            _beamMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));

            var battleObject = new GameObject("Battle", typeof(BattleSceneController));
            battleObject.transform.SetParent(_root.transform, false);
            _battle = battleObject.GetComponent<BattleSceneController>();
            var serialized = new SerializedObject(_battle);
            serialized.FindProperty("scenario").objectReferenceValue = _config;
            serialized.FindProperty("enemyPrefab").objectReferenceValue = enemyPrefab.transform;
            serialized.FindProperty("towerPrefab").objectReferenceValue = towerPrefab.transform;
            serialized.FindProperty("hudText").objectReferenceValue = _hud;
            serialized.FindProperty("worldCamera").objectReferenceValue = _camera;
            serialized.FindProperty("attackBeamMaterial").objectReferenceValue = _beamMaterial;
            serialized.ApplyModifiedPropertiesWithoutUndo();
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
        public void AttackEvent_CreatesBeamAndClearsProcessedEvents()
        {
            Assert.That(_battle.PlaceTower(new GridCoordinate(-4, 0)).Success, Is.True);
            Invoke(_battle, "LateUpdate");
            _battle.StartWave();
            _battle.Simulation.Step();

            Invoke(_battle, "ProcessAttackEvents");

            Assert.That(_battle.Simulation.AttackEvents, Is.Empty);
            var beam = _battle.GetComponentsInChildren<LineRenderer>().SingleOrDefault();
            Assert.That(beam, Is.Not.Null);
            Assert.That(beam.positionCount, Is.EqualTo(2));
            Assert.That(Vector3.Distance(beam.GetPosition(0), beam.GetPosition(1)), Is.GreaterThan(0.1f));
        }

        [Test]
        public void AttackEvent_LethalBeforeFirstEnemyView_CreatesBeamAtEventPosition()
        {
            var serializedConfig = new SerializedObject(_config);
            serializedConfig.FindProperty("towerDamage").intValue = 100;
            serializedConfig.ApplyModifiedPropertiesWithoutUndo();
            Invoke(_battle, "Start");
            Assert.That(_battle.PlaceTower(new GridCoordinate(-4, 0)).Success, Is.True);
            _battle.StartWave();
            _battle.Simulation.Step();
            Assert.That(_battle.Simulation.State.Enemies, Is.Empty);

            Invoke(_battle, "ProcessAttackEvents");

            var beam = _battle.GetComponentsInChildren<LineRenderer>().SingleOrDefault();
            Assert.That(beam, Is.Not.Null);
            Assert.That(Vector3.Distance(beam.GetPosition(1), new Vector3(-4.95f, 0.6f, -2f)), Is.LessThan(0.0001f));
            Assert.That(_battle.Simulation.AttackEvents, Is.Empty);
        }

        [Test]
        public void AttackEvents_MultipleFixedStepsBeforeRender_UsePerAttackPositions()
        {
            Assert.That(_battle.PlaceTower(new GridCoordinate(-4, 0)).Success, Is.True);
            _battle.StartWave();
            Invoke(_battle, "LateUpdate");
            for (var index = 0; index < 31; index++)
            {
                _battle.Simulation.Step();
                Invoke(_battle, "ProcessAttackEvents");
            }

            var beams = _battle.GetComponentsInChildren<LineRenderer>()
                .OrderBy(beam => beam.transform.GetSiblingIndex())
                .ToArray();
            var expectedEnds = new[]
            {
                new Vector3(-4.95f, 0.6f, -2f),
                new Vector3(-4.2f, 0.6f, -2f),
                new Vector3(-3.45f, 0.6f, -2f)
            };
            Assert.That(beams.Length, Is.EqualTo(expectedEnds.Length));
            for (var index = 0; index < expectedEnds.Length; index++)
            {
                Assert.That(
                    Vector3.Distance(beams[index].GetPosition(1), expectedEnds[index]),
                    Is.LessThan(0.0001f),
                    $"Beam {index} must use the position captured at its attack tick.");
            }

            Assert.That(_battle.Simulation.AttackEvents, Is.Empty);
        }

        [Test]
        public void LateUpdate_HealthBarMatchesDomainDamage()
        {
            Assert.That(_battle.PlaceTower(new GridCoordinate(-4, 0)).Success, Is.True);
            _battle.StartWave();
            _battle.Simulation.Step();

            Invoke(_battle, "LateUpdate");

            var fill = FindChild("Health Fill");
            var background = FindChild("Health Background");
            Assert.That(fill, Is.Not.Null);
            Assert.That(background, Is.Not.Null);
            Assert.That(fill.localScale.x / background.localScale.x, Is.EqualTo(0.75f).Within(0.0001f));
        }

        [Test]
        public void LateUpdate_LethalDamageIncreasesKillsWithoutBaseDamage()
        {
            Assert.That(_battle.PlaceTower(new GridCoordinate(-4, 0)).Success, Is.True);
            _battle.StartWave();
            for (var index = 0; index < 91; index++)
            {
                _battle.Simulation.Step();
            }

            Invoke(_battle, "LateUpdate");

            Assert.That(_battle.Simulation.State.Kills, Is.EqualTo(1));
            Assert.That(_battle.Simulation.State.BaseHealth, Is.EqualTo(20));
            Assert.That(_hud.text, Does.Contain("Kills: 1"));
        }

        [Test]
        public void LateUpdate_DisplaysCurrentWaveAndWaveCount()
        {
            Invoke(_battle, "LateUpdate");

            Assert.That(_hud.text, Does.Contain("Wave: 1 / 1"));
        }

        [Test]
        public void LateUpdate_Defeat_DisplaysResultFeedback()
        {
            var serializedConfig = new SerializedObject(_config);
            serializedConfig.FindProperty("baseHealth").intValue = 1;
            serializedConfig.FindProperty("speed").floatValue = 300f;
            serializedConfig.ApplyModifiedPropertiesWithoutUndo();
            Invoke(_battle, "Start");
            _battle.StartWave();
            _battle.Simulation.Step();
            _battle.Simulation.Step();

            Invoke(_battle, "LateUpdate");

            Assert.That(_battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.Defeat));
            Assert.That(_hud.text, Does.Contain("Result: Defeat"));
        }

        [Test]
        public void NextWaveButton_WaveComplete_TransitionsToSecondPreparation()
        {
            var serializedConfig = new SerializedObject(_config);
            serializedConfig.FindProperty("speed").floatValue = 300f;
            var waves = serializedConfig.FindProperty("waves");
            waves.arraySize = 2;
            for (var index = 0; index < waves.arraySize; index++)
            {
                var wave = waves.GetArrayElementAtIndex(index);
                wave.FindPropertyRelative("enemyCount").intValue = 1;
                wave.FindPropertyRelative("firstSpawnTick").intValue = 0;
                wave.FindPropertyRelative("spawnIntervalTicks").intValue = 30;
            }

            serializedConfig.ApplyModifiedPropertiesWithoutUndo();
            Invoke(_battle, "Start");
            var selection = new GameObject("Selection").transform;
            selection.SetParent(_root.transform, false);
            var preview = new GameObject("Tower Preview").transform;
            preview.SetParent(_root.transform, false);
            var panel = new GameObject("Placement Panel", typeof(RectTransform));
            panel.transform.SetParent(_root.transform, false);
            var rangeObject = new GameObject("Range Preview", typeof(LineRenderer));
            rangeObject.transform.SetParent(_root.transform, false);
            var nextWave = CreateButton("Next Wave");
            var placement = _battle.gameObject.AddComponent<BattlePlacementController>();
            var serialized = new SerializedObject(placement);
            serialized.FindProperty("battle").objectReferenceValue = _battle;
            serialized.FindProperty("worldCamera").objectReferenceValue = _camera;
            serialized.FindProperty("selectionHighlight").objectReferenceValue = selection;
            serialized.FindProperty("towerPreview").objectReferenceValue = preview;
            serialized.FindProperty("confirmationPanel").objectReferenceValue = panel;
            serialized.FindProperty("confirmButton").objectReferenceValue = CreateButton("Confirm");
            serialized.FindProperty("cancelButton").objectReferenceValue = CreateButton("Cancel");
            serialized.FindProperty("startWaveButton").objectReferenceValue = CreateButton("Start Wave");
            var nextWaveProperty = serialized.FindProperty("nextWaveButton");
            Assert.That(nextWaveProperty, Is.Not.Null, "Next Wave button reference is required.");
            nextWaveProperty.objectReferenceValue = nextWave;
            serialized.FindProperty("rangePreview").objectReferenceValue = rangeObject.GetComponent<LineRenderer>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Invoke(placement, "Start");
            Invoke(placement, "RefreshUi");
            Assert.That(nextWave.interactable, Is.False);

            _battle.StartWave();
            _battle.Simulation.Step();
            _battle.Simulation.Step();
            Invoke(placement, "RefreshUi");
            Assert.That(_battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.WaveComplete));
            Assert.That(nextWave.interactable, Is.True);

            nextWave.onClick.Invoke();

            Assert.That(_battle.Simulation.State.WaveIndex, Is.EqualTo(1));
            Assert.That(_battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.Preparation));
            Assert.That(nextWave.interactable, Is.False);
        }

        [Test]
        public void Selection_ShowsConfiguredRangeAndCancelHidesIt()
        {
            var selection = new GameObject("Selection").transform;
            selection.SetParent(_root.transform, false);
            var preview = new GameObject("Tower Preview").transform;
            preview.SetParent(_root.transform, false);
            var panel = new GameObject("Placement Panel", typeof(RectTransform));
            panel.transform.SetParent(_root.transform, false);
            var rangeObject = new GameObject("Range Preview", typeof(LineRenderer));
            rangeObject.transform.SetParent(_root.transform, false);
            var range = rangeObject.GetComponent<LineRenderer>();
            range.useWorldSpace = false;
            range.loop = true;
            var placement = _battle.gameObject.AddComponent<BattlePlacementController>();
            var serialized = new SerializedObject(placement);
            serialized.FindProperty("battle").objectReferenceValue = _battle;
            serialized.FindProperty("worldCamera").objectReferenceValue = _camera;
            serialized.FindProperty("selectionHighlight").objectReferenceValue = selection;
            serialized.FindProperty("towerPreview").objectReferenceValue = preview;
            serialized.FindProperty("confirmationPanel").objectReferenceValue = panel;
            serialized.FindProperty("confirmButton").objectReferenceValue = CreateButton("Confirm");
            serialized.FindProperty("cancelButton").objectReferenceValue = CreateButton("Cancel");
            serialized.FindProperty("startWaveButton").objectReferenceValue = CreateButton("Start Wave");
            serialized.FindProperty("nextWaveButton").objectReferenceValue = CreateButton("Next Wave");
            serialized.FindProperty("rangePreview").objectReferenceValue = range;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Invoke(placement, "Start");
            Assert.That(placement.TrySelectCell(new GridCoordinate(-3, 1)), Is.True);

            Assert.That(range.gameObject.activeSelf, Is.True);
            Assert.That(range.positionCount, Is.EqualTo(64));
            for (var index = 0; index < range.positionCount; index++)
            {
                var position = range.GetPosition(index);
                Assert.That(new Vector2(position.x, position.z).magnitude, Is.EqualTo(3f).Within(0.0001f));
            }

            placement.CancelSelection();
            Assert.That(range.gameObject.activeSelf, Is.False);
        }

        private Button CreateButton(string name)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Button));
            buttonObject.transform.SetParent(_root.transform, false);
            return buttonObject.GetComponent<Button>();
        }

        private Transform FindChild(string name)
        {
            return _battle.GetComponentsInChildren<Transform>(true).FirstOrDefault(child => child.name == name);
        }

        private static void Invoke(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, null);
        }
    }
}
