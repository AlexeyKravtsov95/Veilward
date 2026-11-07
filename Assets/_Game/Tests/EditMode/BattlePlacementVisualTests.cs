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
    public sealed class BattlePlacementVisualTests
    {
        private GameObject _root;
        private BattleScenarioConfig _config;
        private BattleSceneController _battle;
        private GameObject _towerPrefab;
        private Camera _camera;
        private Material _beamMaterial;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Half Cell Visual Tests");
            _config = BattleScenarioConfig.CreateDefault();
            var serializedConfig = new SerializedObject(_config);
            serializedConfig.FindProperty("cellSize").floatValue = 0.5f;
            var waypoints = serializedConfig.FindProperty("waypoints");
            waypoints.arraySize = 2;
            waypoints.GetArrayElementAtIndex(0).vector2IntValue = new Vector2Int(-2, 0);
            waypoints.GetArrayElementAtIndex(1).vector2IntValue = new Vector2Int(2, 0);
            serializedConfig.ApplyModifiedPropertiesWithoutUndo();

            var towerAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/TestTower.prefab");
            Assert.That(towerAsset, Is.Not.Null);
            _towerPrefab = Object.Instantiate(towerAsset, _root.transform);
            _towerPrefab.name = "Tower Prefab";
            var enemyPrefab = new GameObject("Enemy Prefab");
            enemyPrefab.transform.SetParent(_root.transform, false);
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
            serializedBattle.FindProperty("towerPrefab").objectReferenceValue = _towerPrefab.transform;
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
        public void Placement_HalfSizeCells_ScalesSelectionAndPreview()
        {
            var selection = GameObject.CreatePrimitive(PrimitiveType.Cube);
            selection.name = "Selection Highlight";
            selection.transform.SetParent(_root.transform, false);
            selection.transform.localScale = new Vector3(0.98f, 0.04f, 0.98f);
            var preview = Object.Instantiate(_towerPrefab, _root.transform);
            preview.name = "Tower Preview";
            var panel = new GameObject("Placement Panel", typeof(RectTransform));
            panel.transform.SetParent(_root.transform, false);
            var confirm = CreateButton("Confirm");
            var cancel = CreateButton("Cancel");
            var startWave = CreateButton("Start Wave");
            var rangeObject = new GameObject("Range Preview", typeof(LineRenderer));
            rangeObject.transform.SetParent(_root.transform, false);
            var range = rangeObject.GetComponent<LineRenderer>();

            var placement = _battle.gameObject.AddComponent<BattlePlacementController>();
            var serializedPlacement = new SerializedObject(placement);
            serializedPlacement.FindProperty("battle").objectReferenceValue = _battle;
            serializedPlacement.FindProperty("worldCamera").objectReferenceValue = _camera;
            serializedPlacement.FindProperty("selectionHighlight").objectReferenceValue = selection.transform;
            serializedPlacement.FindProperty("towerPreview").objectReferenceValue = preview.transform;
            serializedPlacement.FindProperty("confirmationPanel").objectReferenceValue = panel;
            serializedPlacement.FindProperty("confirmButton").objectReferenceValue = confirm;
            serializedPlacement.FindProperty("cancelButton").objectReferenceValue = cancel;
            serializedPlacement.FindProperty("startWaveButton").objectReferenceValue = startWave;
            serializedPlacement.FindProperty("rangePreview").objectReferenceValue = range;
            serializedPlacement.ApplyModifiedPropertiesWithoutUndo();
            Invoke(placement, "Start");

            Assert.That(placement.TrySelectCell(new GridCoordinate(-3, 1)), Is.True);
            AssertVector(selection.transform.localScale, new Vector3(0.49f, 0.04f, 0.49f));
            AssertVector(preview.transform.localScale, Vector3.one * 0.5f);
        }

        [Test]
        public void Towers_HalfSizeCells_AdjacentFootprintsDoNotIntersect()
        {
            Assert.That(_battle.PlaceTower(new GridCoordinate(-3, 1)).Success, Is.True);
            Assert.That(_battle.PlaceTower(new GridCoordinate(-2, 1)).Success, Is.True);
            Invoke(_battle, "LateUpdate");

            var towers = _battle.transform.Cast<Transform>()
                .Where(child => child.name.StartsWith("Tower "))
                .ToArray();
            Assert.That(towers.Length, Is.EqualTo(2));
            AssertVector(towers[0].localScale, Vector3.one * 0.5f);
            AssertVector(towers[1].localScale, Vector3.one * 0.5f);
            Assert.That(
                GetBounds(towers[0]).Intersects(GetBounds(towers[1])),
                Is.False,
                "Towers on adjacent cells must not overlap.");
        }

        private Button CreateButton(string name)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Button));
            buttonObject.transform.SetParent(_root.transform, false);
            return buttonObject.GetComponent<Button>();
        }

        private static Bounds GetBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            Assert.That(renderers.Length, Is.GreaterThan(0));
            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds;
        }

        private static void Invoke(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, null);
        }

        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.0001f));
        }
    }
}
