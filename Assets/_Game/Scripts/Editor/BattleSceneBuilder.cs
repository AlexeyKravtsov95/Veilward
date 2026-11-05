using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Veilward.Presentation;

namespace Veilward.Editor
{
    public static class BattleSceneBuilder
    {
        private const string BattleScenePath = "Assets/_Game/Scenes/Battle.unity";
        private const string ConfigPath = "Assets/_Game/Configs/TestWave.asset";
        private const string PrefabPath = "Assets/_Game/Prefabs/Shade.prefab";
        private const string MaterialsFolder = "Assets/_Game/Art/Materials";

        [MenuItem("Veilward/Build P1.1 Battle Scene")]
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

            var enemy = CreatePrimitive(PrimitiveType.Capsule, "Shade");
            enemy.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
            enemy.GetComponent<Renderer>().sharedMaterial = enemyMaterial;
            var enemyPrefab = PrefabUtility.SaveAsPrefabAsset(enemy, PrefabPath);
            Object.DestroyImmediate(enemy);
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

            var points = new[]
            {
                new Vector2Int(-5, -2),
                new Vector2Int(0, -2),
                new Vector2Int(0, 2),
                new Vector2Int(5, 2)
            };
            for (var index = 0; index < points.Length - 1; index++)
            {
                var start = ToWorld(points[index]);
                var end = ToWorld(points[index + 1]);
                var delta = end - start;
                var segment = CreatePrimitive(PrimitiveType.Cube, $"Path Segment {index}");
                segment.transform.position = (start + end) * 0.5f;
                segment.transform.rotation = Quaternion.LookRotation(delta.normalized);
                segment.transform.localScale = new Vector3(1.2f, 0.1f, delta.magnitude);
                segment.GetComponent<Renderer>().sharedMaterial = pathMaterial;
            }

            CreateMarker("Spawn Marker", points[0], spawnMaterial);
            CreateMarker("Seal Heart", points[points.Length - 1], heartMaterial);
            CreateCamera();
            CreateLight();
            var hudText = CreateHud();
            CreateController(config, enemyPrefab.transform, hudText);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateRequiredReferences();
        }

        private static void ValidateRequiredReferences()
        {
            var controller = Object.FindFirstObjectByType<BattleSceneController>();
            var serialized = new SerializedObject(controller);
            var propertyNames = new[] { "scenario", "enemyPrefab", "hudText" };
            foreach (var propertyName in propertyNames)
            {
                if (serialized.FindProperty(propertyName).objectReferenceValue == null)
                {
                    throw new System.InvalidOperationException(
                        $"Battle Controller is missing required reference: {propertyName}.");
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

        private static void CreateMarker(string name, Vector2Int point, Material material)
        {
            var marker = CreatePrimitive(PrimitiveType.Cylinder, name);
            marker.transform.position = new Vector3(point.x, 0.25f, point.y);
            marker.transform.localScale = new Vector3(0.8f, 0.25f, 0.8f);
            marker.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("Battle Camera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(8f, 10f, 8f);
            cameraObject.transform.LookAt(Vector3.zero);
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 8f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.05f, 0.08f);
        }

        private static void CreateLight()
        {
            var lightObject = new GameObject("Directional Light", typeof(Light));
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
        }

        private static UnityEngine.UI.Text CreateHud()
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

            var panelObject = new GameObject("Stats Panel", typeof(UnityEngine.UI.Image));
            panelObject.transform.SetParent(canvasObject.transform, false);
            var panel = panelObject.GetComponent<UnityEngine.UI.Image>();
            panel.color = new Color(0.02f, 0.03f, 0.05f, 0.85f);
            panel.raycastTarget = false;
            var panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(24f, -24f);
            panelRect.sizeDelta = new Vector2(420f, 150f);

            var textObject = new GameObject("Stats", typeof(UnityEngine.UI.Text));
            textObject.transform.SetParent(panelObject.transform, false);
            var text = textObject.GetComponent<UnityEngine.UI.Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.color = Color.white;
            text.raycastTarget = false;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(16f, 10f);
            text.rectTransform.offsetMax = new Vector2(-16f, -10f);
            return text;
        }

        private static void CreateController(
            BattleScenarioConfig config,
            Transform enemyPrefab,
            UnityEngine.UI.Text hudText)
        {
            var controllerObject = new GameObject("Battle Controller", typeof(BattleSceneController));
            var serialized = new SerializedObject(controllerObject.GetComponent<BattleSceneController>());
            serialized.FindProperty("scenario").objectReferenceValue = config;
            serialized.FindProperty("enemyPrefab").objectReferenceValue = enemyPrefab;
            serialized.FindProperty("hudText").objectReferenceValue = hudText;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
