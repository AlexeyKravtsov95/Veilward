using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Veilward.Domain;
using Veilward.Presentation;

namespace Veilward.Editor
{
    public static class BattleSceneBuilder
    {
        private const string BattleScenePath = "Assets/_Game/Scenes/Battle.unity";
        private const string ConfigPath = "Assets/_Game/Configs/TestWave.asset";
        private const string EnemyPrefabPath = "Assets/_Game/Prefabs/Shade.prefab";
        private const string TowerPrefabPath = "Assets/_Game/Prefabs/TestTower.prefab";
        private const string MaterialsFolder = "Assets/_Game/Art/Materials";

        [MenuItem("Veilward/Build P1.2 Battle Scene")]
        public static void Build()
        {
            Directory.CreateDirectory("Assets/_Game/Configs");
            Directory.CreateDirectory("Assets/_Game/Prefabs");
            Directory.CreateDirectory(MaterialsFolder);

            var config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>(ConfigPath);
            if (config == null)
            {
                config = BattleScenarioConfig.CreateDefault();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            var groundMaterial = CreateOrUpdateMaterial("GrayboxGround", new Color(0.12f, 0.16f, 0.2f));
            var pathMaterial = CreateOrUpdateMaterial("GrayboxPath", new Color(0.25f, 0.55f, 0.5f));
            var spawnMaterial = CreateOrUpdateMaterial("GrayboxSpawn", new Color(0.2f, 0.8f, 0.9f));
            var heartMaterial = CreateOrUpdateMaterial("GrayboxHeart", new Color(0.95f, 0.35f, 0.3f));
            var enemyMaterial = CreateOrUpdateMaterial("GrayboxShade", new Color(0.35f, 0.25f, 0.65f));
            var availableMaterial = CreateOrUpdateMaterial("GrayboxCellAvailable", new Color(0.18f, 0.3f, 0.34f));
            var blockedMaterial = CreateOrUpdateMaterial("GrayboxCellBlocked", new Color(0.1f, 0.12f, 0.15f));
            var towerMaterial = CreateOrUpdateMaterial("GrayboxTower", new Color(0.95f, 0.7f, 0.2f));
            var selectionMaterial = CreateOrUpdateMaterial("GrayboxSelection", new Color(1f, 0.9f, 0.2f));
            var previewMaterial = CreateOrUpdateMaterial("GrayboxTowerPreview", new Color(0.3f, 0.9f, 0.95f));

            var enemy = CreatePrimitive(PrimitiveType.Capsule, "Shade");
            enemy.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
            enemy.GetComponent<Renderer>().sharedMaterial = enemyMaterial;
            var enemyPrefab = PrefabUtility.SaveAsPrefabAsset(enemy, EnemyPrefabPath);
            Object.DestroyImmediate(enemy);
            var towerPrefab = CreateTowerPrefab(towerMaterial);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.OpenScene(BattleScenePath);
            config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>(ConfigPath);
            foreach (var root in scene.GetRootGameObjects())
            {
                Object.DestroyImmediate(root);
            }

            var ground = CreatePrimitive(PrimitiveType.Cube, "Ground");
            ground.transform.localScale = new Vector3(16f, 0.2f, 10f);
            ground.transform.position = new Vector3(0f, -0.15f, 0f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

            CreateGridGeometry(config, availableMaterial, blockedMaterial);
            CreatePathGeometry(config, pathMaterial, spawnMaterial, heartMaterial);
            var battleCamera = CreateCamera();
            CreateLight();
            var selection = CreateSelection(selectionMaterial);
            var preview = CreatePreview(towerPrefab, previewMaterial);
            var hud = CreateHud(config.CreateSimulation().TowerDefinition.Price);
            CreateEventSystem();
            CreateController(config, enemyPrefab.transform, towerPrefab.transform, battleCamera, selection, preview, hud);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateRequiredReferences();
        }

        public static GameObject CreateGridGeometry(
            BattleScenarioConfig config,
            Material availableMaterial,
            Material blockedMaterial)
        {
            var simulation = config.CreateSimulation();
            var map = simulation.Grid.Map;
            var root = new GameObject("Build Grid");
            for (var y = map.Origin.Y; y < map.Origin.Y + map.Height; y++)
            {
                for (var x = map.Origin.X; x < map.Origin.X + map.Width; x++)
                {
                    var coordinate = new GridCoordinate(x, y);
                    var center = map.GetCellCenter(coordinate);
                    var cell = CreatePrimitive(PrimitiveType.Cube, $"Cell_{x}_{y}");
                    cell.transform.SetParent(root.transform, false);
                    cell.transform.position = new Vector3(center.X, 0f, center.Y);
                    cell.transform.localScale = new Vector3(
                        map.CellSize * 0.92f,
                        0.04f,
                        map.CellSize * 0.92f);
                    cell.GetComponent<Renderer>().sharedMaterial =
                        simulation.Grid.GetState(coordinate) == GridCellState.Blocked
                            ? blockedMaterial
                            : availableMaterial;
                }
            }

            return root;
        }

        public static GameObject CreatePathGeometry(
            BattleScenarioConfig config,
            Material pathMaterial,
            Material spawnMaterial,
            Material heartMaterial)
        {
            var path = config.CreateSimulation().Path;
            var root = new GameObject("Path Geometry");
            for (var index = 0; index < path.Points.Count - 1; index++)
            {
                var startPoint = path.Points[index];
                var endPoint = path.Points[index + 1];
                var start = ToWorld(new Vector2Int(startPoint.X, startPoint.Y));
                var end = ToWorld(new Vector2Int(endPoint.X, endPoint.Y));
                var delta = end - start;
                var segment = CreatePrimitive(PrimitiveType.Cube, $"Path Segment {index}");
                segment.transform.SetParent(root.transform, false);
                segment.transform.position = (start + end) * 0.5f;
                segment.transform.rotation = Quaternion.LookRotation(delta.normalized);
                segment.transform.localScale = new Vector3(1.2f, 0.1f, delta.magnitude);
                segment.GetComponent<Renderer>().sharedMaterial = pathMaterial;
            }

            var spawn = path.Points[0];
            var heart = path.Points[path.Points.Count - 1];
            CreateMarker("Spawn Marker", new Vector2Int(spawn.X, spawn.Y), spawnMaterial)
                .transform.SetParent(root.transform, false);
            CreateMarker("Seal Heart", new Vector2Int(heart.X, heart.Y), heartMaterial)
                .transform.SetParent(root.transform, false);
            return root;
        }

        private static GameObject CreateTowerPrefab(Material material)
        {
            var root = new GameObject("TestTower");
            var foundation = CreatePrimitive(PrimitiveType.Cylinder, "Foundation");
            foundation.transform.SetParent(root.transform, false);
            foundation.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            foundation.transform.localScale = new Vector3(0.65f, 0.18f, 0.65f);
            foundation.GetComponent<Renderer>().sharedMaterial = material;
            var spirit = CreatePrimitive(PrimitiveType.Sphere, "Spirit");
            spirit.transform.SetParent(root.transform, false);
            spirit.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            spirit.transform.localScale = new Vector3(0.55f, 0.7f, 0.55f);
            spirit.GetComponent<Renderer>().sharedMaterial = material;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, TowerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static Transform CreateSelection(Material material)
        {
            var selection = CreatePrimitive(PrimitiveType.Cube, "Selection Highlight");
            selection.transform.localScale = new Vector3(0.98f, 0.04f, 0.98f);
            selection.GetComponent<Renderer>().sharedMaterial = material;
            selection.SetActive(false);
            return selection.transform;
        }

        private static Transform CreatePreview(GameObject towerPrefab, Material material)
        {
            var preview = Object.Instantiate(towerPrefab);
            preview.name = "Tower Preview";
            foreach (var renderer in preview.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterial = material;
            }

            preview.SetActive(false);
            return preview.transform;
        }

        private static void ValidateRequiredReferences()
        {
            var controller = Object.FindFirstObjectByType<BattleSceneController>();
            var serialized = new SerializedObject(controller);
            var propertyNames = new[] { "scenario", "enemyPrefab", "towerPrefab", "hudText" };
            foreach (var propertyName in propertyNames)
            {
                if (serialized.FindProperty(propertyName).objectReferenceValue == null)
                {
                    throw new System.InvalidOperationException(
                        $"Battle Controller is missing required reference: {propertyName}.");
                }
            }

            var placement = Object.FindFirstObjectByType<BattlePlacementController>();
            var placementSerialized = new SerializedObject(placement);
            var placementProperties = new[]
            {
                "battle", "worldCamera", "selectionHighlight", "towerPreview",
                "confirmationPanel", "confirmButton", "cancelButton", "startWaveButton"
            };
            foreach (var propertyName in placementProperties)
            {
                if (placementSerialized.FindProperty(propertyName).objectReferenceValue == null)
                {
                    throw new System.InvalidOperationException(
                        $"Battle Placement is missing required reference: {propertyName}.");
                }
            }
        }

        private static Material CreateOrUpdateMaterial(string name, Color color)
        {
            var path = $"{MaterialsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreatePrimitive(PrimitiveType type, string name)
        {
            var gameObject = GameObject.CreatePrimitive(type);
            gameObject.name = name;
            Object.DestroyImmediate(gameObject.GetComponent<Collider>());
            return gameObject;
        }

        private static Vector3 ToWorld(Vector2Int point)
        {
            return new Vector3(point.x, 0.05f, point.y);
        }

        private static GameObject CreateMarker(string name, Vector2Int point, Material material)
        {
            var marker = CreatePrimitive(PrimitiveType.Cylinder, name);
            marker.transform.position = new Vector3(point.x, 0.25f, point.y);
            marker.transform.localScale = new Vector3(0.8f, 0.25f, 0.8f);
            marker.GetComponent<Renderer>().sharedMaterial = material;
            return marker;
        }

        private static Camera CreateCamera()
        {
            var cameraObject = new GameObject("Battle Camera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(8f, 11f, 8f);
            cameraObject.transform.LookAt(Vector3.zero);
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 8.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.05f, 0.08f);
            return camera;
        }

        private static void CreateLight()
        {
            var lightObject = new GameObject("Directional Light", typeof(Light));
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
        }

        private static HudReferences CreateHud(int towerPrice)
        {
            var canvasObject = new GameObject(
                "HUD",
                typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler),
                typeof(UnityEngine.UI.GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var panelObject = CreateUiObject("Stats Panel", canvasObject.transform);
            var panel = panelObject.AddComponent<UnityEngine.UI.Image>();
            panel.color = new Color(0.02f, 0.03f, 0.05f, 0.85f);
            panel.raycastTarget = false;
            SetRect(panel.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(440f, 210f));
            var stats = CreateText("Stats", panelObject.transform, 24, TextAnchor.UpperLeft);
            SetRect(stats.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero);
            stats.rectTransform.anchorMin = Vector2.zero;
            stats.rectTransform.anchorMax = Vector2.one;
            stats.rectTransform.offsetMin = new Vector2(16f, 10f);
            stats.rectTransform.offsetMax = new Vector2(-16f, -10f);
            stats.raycastTarget = false;

            var confirmation = CreateUiObject("Placement Panel", canvasObject.transform);
            var confirmationImage = confirmation.AddComponent<UnityEngine.UI.Image>();
            confirmationImage.color = new Color(0.02f, 0.03f, 0.05f, 0.92f);
            SetRect(
                confirmationImage.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0f, 24f),
                new Vector2(540f, 122f));
            var placementLabel = CreateText(
                "Placement Label",
                confirmation.transform,
                22,
                TextAnchor.MiddleCenter);
            placementLabel.text = $"Test Tower — {towerPrice} gold";
            placementLabel.raycastTarget = false;
            SetRect(
                placementLabel.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0f, -12f),
                new Vector2(500f, 38f));
            var confirm = CreateButton(
                "Confirm Button",
                confirmation.transform,
                "Confirm",
                new Vector2(-130f, -38f));
            var cancel = CreateButton(
                "Cancel Button",
                confirmation.transform,
                "Cancel",
                new Vector2(130f, -38f));
            confirmation.SetActive(false);

            var startWave = CreateButton("Start Wave Button", canvasObject.transform, "Start Wave", Vector2.zero);
            SetRect(
                startWave.GetComponent<RectTransform>(),
                new Vector2(1f, 1f),
                new Vector2(-24f, -24f),
                new Vector2(230f, 68f));
            return new HudReferences
            {
                StatsText = stats,
                ConfirmationPanel = confirmation,
                ConfirmButton = confirm,
                CancelButton = cancel,
                StartWaveButton = startWave
            };
        }

        private static UnityEngine.UI.Button CreateButton(
            string name,
            Transform parent,
            string label,
            Vector2 anchoredPosition)
        {
            var buttonObject = CreateUiObject(name, parent);
            var image = buttonObject.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.16f, 0.36f, 0.42f, 1f);
            var button = buttonObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            SetRect(image.rectTransform, new Vector2(0.5f, 0.5f), anchoredPosition, new Vector2(220f, 56f));
            var text = CreateText("Label", buttonObject.transform, 24, TextAnchor.MiddleCenter);
            text.text = label;
            text.raycastTarget = false;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        private static UnityEngine.UI.Text CreateText(
            string name,
            Transform parent,
            int fontSize,
            TextAnchor alignment)
        {
            var textObject = CreateUiObject(name, parent);
            var text = textObject.AddComponent<UnityEngine.UI.Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            return text;
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 anchor,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static void CreateEventSystem()
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private static void CreateController(
            BattleScenarioConfig config,
            Transform enemyPrefab,
            Transform towerPrefab,
            Camera battleCamera,
            Transform selection,
            Transform preview,
            HudReferences hud)
        {
            var controllerObject = new GameObject(
                "Battle Controller",
                typeof(BattleSceneController),
                typeof(BattlePlacementController));
            var serialized = new SerializedObject(controllerObject.GetComponent<BattleSceneController>());
            serialized.FindProperty("scenario").objectReferenceValue = config;
            serialized.FindProperty("enemyPrefab").objectReferenceValue = enemyPrefab;
            serialized.FindProperty("towerPrefab").objectReferenceValue = towerPrefab;
            serialized.FindProperty("hudText").objectReferenceValue = hud.StatsText;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var placementSerialized = new SerializedObject(controllerObject.GetComponent<BattlePlacementController>());
            placementSerialized.FindProperty("battle").objectReferenceValue =
                controllerObject.GetComponent<BattleSceneController>();
            placementSerialized.FindProperty("worldCamera").objectReferenceValue = battleCamera;
            placementSerialized.FindProperty("selectionHighlight").objectReferenceValue = selection;
            placementSerialized.FindProperty("towerPreview").objectReferenceValue = preview;
            placementSerialized.FindProperty("confirmationPanel").objectReferenceValue = hud.ConfirmationPanel;
            placementSerialized.FindProperty("confirmButton").objectReferenceValue = hud.ConfirmButton;
            placementSerialized.FindProperty("cancelButton").objectReferenceValue = hud.CancelButton;
            placementSerialized.FindProperty("startWaveButton").objectReferenceValue = hud.StartWaveButton;
            placementSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class HudReferences
        {
            public UnityEngine.UI.Text StatsText;
            public GameObject ConfirmationPanel;
            public UnityEngine.UI.Button ConfirmButton;
            public UnityEngine.UI.Button CancelButton;
            public UnityEngine.UI.Button StartWaveButton;
        }
    }
}
