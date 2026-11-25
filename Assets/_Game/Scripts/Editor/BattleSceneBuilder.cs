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
    public static partial class BattleSceneBuilder
    {
        private const string BattleScenePath = "Assets/_Game/Scenes/Battle.unity";
        private const string MainMenuScenePath = "Assets/_Game/Scenes/MainMenu.unity";
        private const string LegacyConfigPath = "Assets/_Game/Configs/TestWave.asset";
        private const string HistoricalConfigPath = "Assets/_Game/Configs/EightWave.asset";
        private const string ConfigPath = "Assets/_Game/Configs/EightWaveAbilityBossV2.asset";
        private const string EnemyPrefabPath = "Assets/_Game/Prefabs/Shade.prefab";
        internal const string TowerPrefabPath = "Assets/_Game/Prefabs/TestTower.prefab";
        private const string MaterialsFolder = "Assets/_Game/Art/Materials";

        [MenuItem("VEILWARD/P1.4b Build")]
        public static void Build()
        {
            Directory.CreateDirectory("Assets/_Game/Configs");
            Directory.CreateDirectory("Assets/_Game/Prefabs");
            Directory.CreateDirectory(MaterialsFolder);

            var legacyConfig = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>(LegacyConfigPath);
            if (legacyConfig == null)
            {
                legacyConfig = BattleScenarioConfig.CreateDefault();
                var serializedLegacy = new SerializedObject(legacyConfig);
                serializedLegacy.FindProperty("towerRange").floatValue = 3f;
                serializedLegacy.FindProperty("towerFireRate").floatValue = 2f;
                serializedLegacy.FindProperty("towerDamage").intValue = 25;
                serializedLegacy.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(legacyConfig, LegacyConfigPath);
            }

            EnsureElementalCatalog(legacyConfig);
            var config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>(HistoricalConfigPath);
            if (config == null)
            {
                config = BattleScenarioConfig.CreateDefault();
                var serializedConfig = new SerializedObject(config);
                serializedConfig.FindProperty("sharedScenario").objectReferenceValue = legacyConfig;
                serializedConfig.FindProperty("enemyReward").intValue = 5;
                serializedConfig.FindProperty("waveCompletionBonus").intValue = 20;
                var waves = serializedConfig.FindProperty("waves");
                waves.arraySize = 8;
                for (var index = 0; index < waves.arraySize; index++)
                {
                    var wave = waves.GetArrayElementAtIndex(index);
                    wave.FindPropertyRelative("enemyCount").intValue = 5 + index;
                    wave.FindPropertyRelative("firstSpawnTick").intValue = 0;
                    wave.FindPropertyRelative("spawnIntervalTicks").intValue = 30;
                }

                SetNativeProgression(serializedConfig);
                serializedConfig.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(config, HistoricalConfigPath);
            }

            ConfigureNativeProgression(config);
            EnsureSharedCatalogReference(config, legacyConfig);
            config = EnsureAbilityBossConfig(config);

            SetScenarioContentId(legacyConfig, "legacy-single-wave-v1");

            var enemyMaterial = CreateOrUpdateMaterial("GrayboxShade", new Color(0.35f, 0.25f, 0.65f));
            var towerMaterial = CreateOrUpdateMaterial("GrayboxTower", new Color(0.95f, 0.7f, 0.2f));
            var selectionMaterial = CreateOrUpdateMaterial("GrayboxSelection", new Color(1f, 0.9f, 0.2f));
            var previewMaterial = CreateOrUpdateMaterial("GrayboxTowerPreview", new Color(0.3f, 0.9f, 0.95f));
            var beamMaterial = CreateOrUpdateUnlitMaterial("AttackBeam", new Color(1f, 0.85f, 0.25f));
            CreateOrUpdateUnlitMaterial("ReactionEffect", Color.white);
            var healthBackgroundMaterial = CreateOrUpdateUnlitMaterial("HealthBackground", new Color(0.12f, 0.03f, 0.04f));
            var healthFillMaterial = CreateOrUpdateUnlitMaterial("HealthFill", new Color(0.25f, 0.9f, 0.35f));
            var rangeMaterial = CreateOrUpdateUnlitMaterial("RangePreview", new Color(0.3f, 0.9f, 0.95f));

            var enemy = CreatePrimitive(PrimitiveType.Capsule, "Shade");
            enemy.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
            enemy.GetComponent<Renderer>().sharedMaterial = enemyMaterial;
            AddHealthBar(enemy, healthBackgroundMaterial, healthFillMaterial);
            AddStatusSlots(enemy);
            EnemyModelBuilder.Bind(enemy);
            var enemyPrefab = PrefabUtility.SaveAsPrefabAsset(enemy, EnemyPrefabPath);
            EnemyModelBuilder.NormalizeSerializedWhitespace(EnemyPrefabPath);
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

            BattleEnvironmentBuilder.Create(config);
            var battleCamera = CreateCamera();
            CreateLight();
            BattleEnvironmentBuilder.ApplyAtmosphere(battleCamera, Object.FindFirstObjectByType<Light>());
            var cellSize = config.CreateSimulation().Grid.Map.CellSize;
            var selection = CreateSelection(selectionMaterial, cellSize);
            var preview = CreatePreview(towerPrefab, previewMaterial, cellSize);
            var rangePreview = CreateRangePreview(rangeMaterial);
            var hud = CreateHud();
            CreateEventSystem();
            CreateController(
                config,
                enemyPrefab.transform,
                towerPrefab.transform,
                battleCamera,
                selection,
                preview,
                rangePreview,
                beamMaterial,
                hud);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateRequiredReferences();
            BuildMainMenu();
        }

        private static BattleScenarioConfig EnsureAbilityBossConfig(BattleScenarioConfig historical)
        {
            var config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>(ConfigPath);
            if (config == null)
            {
                config = Object.Instantiate(historical); config.name = "EightWaveAbilityBossV2";
                var data = new SerializedObject(config);
                data.FindProperty("scenarioContentId").stringValue = "eight-wave-ability-boss-v2";
                data.FindProperty("historicalScenario").objectReferenceValue = historical;
                var previous = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWaveAbilityBoss.asset");
                if (previous == null) throw new System.InvalidOperationException("Missing historical boss v1 source.");
                data.FindProperty("historicalBossScenario").objectReferenceValue = previous;
                data.FindProperty("abilityRulesId").stringValue = "rift-seal-v1";
                var boss = data.FindProperty("boss");
                boss.FindPropertyRelative("id").stringValue = "rift-warden";
                boss.FindPropertyRelative("maxHealth").intValue = 600;
                boss.FindPropertyRelative("speed").floatValue = 1;
                boss.FindPropertyRelative("armor").intValue = 5;
                boss.FindPropertyRelative("leakDamage").intValue = 20;
                boss.FindPropertyRelative("reward").intValue = 50;
                var groups = data.FindProperty("waves").GetArrayElementAtIndex(7).FindPropertyRelative("groups");
                groups.arraySize = 2;
                for (var i = 0; i < 2; i++)
                {
                    var g = groups.GetArrayElementAtIndex(i);
                    g.FindPropertyRelative("enemyId").stringValue = i == 0 ? "shade" : "rift-warden";
                    g.FindPropertyRelative("count").intValue = i == 0 ? 11 : 1;
                    g.FindPropertyRelative("firstSpawnTick").intValue = i == 0 ? 0 : 330;
                    g.FindPropertyRelative("spawnIntervalTicks").intValue = 30;
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                config.CreateSimulation();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }
            config.CreateSimulation();
            return config;
        }

        private static void ConfigureNativeProgression(BattleScenarioConfig config)
        {
            var serialized = new SerializedObject(config);
            var content = serialized.FindProperty("scenarioContentId");
            // Одноразовое обновление только прежнего wrapper; native malformed config не чинится молча.
            if (content.stringValue == "eight-wave-v1")
            {
                SetNativeProgression(serialized);
                serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(config);
            }
            config.CreateSimulation();
        }

        private static void SetNativeProgression(SerializedObject serialized)
        {
            serialized.FindProperty("scenarioContentId").stringValue = "eight-wave-unlocks-v1";
            var settings = serialized.FindProperty("progression");
            settings.FindPropertyRelative("rulesId").stringValue = "wave-unlocks-v1";
            var options = settings.FindPropertyRelative("startingOptions"); options.arraySize = 2;
            options.GetArrayElementAtIndex(0).intValue = (int)ElementKind.Flame;
            options.GetArrayElementAtIndex(1).intValue = (int)ElementKind.Gale;
            var milestones = settings.FindPropertyRelative("choiceWaves"); milestones.arraySize = 2;
            milestones.GetArrayElementAtIndex(0).intValue = 3; milestones.GetArrayElementAtIndex(1).intValue = 6;
        }

        private static void EnsureElementalCatalog(BattleScenarioConfig config)
        {
            var serialized = new SerializedObject(config);
            var catalog = serialized.FindProperty("towerArchetypes");
            if (catalog.arraySize != 4)
            {
                // Пустые/частичные данные не являются запросом на reset.
                config.CreateSimulation();
                return;
            }

            var candidate = Object.Instantiate(config);
            candidate.name = config.name;
            var defaults = BattleScenarioConfig.CreateDefault();
            try
            {
                var candidateSerialized = new SerializedObject(candidate);
                candidateSerialized.FindProperty("towerArchetypes").arraySize = 6;
                candidateSerialized.ApplyModifiedPropertiesWithoutUndo();
                var defaultCatalog = new SerializedObject(defaults).FindProperty("towerArchetypes");
                for (var index = 4; index < 6; index++)
                    candidateSerialized.CopyFromSerializedProperty(defaultCatalog.GetArrayElementAtIndex(index));
                candidateSerialized.ApplyModifiedPropertiesWithoutUndo();
                candidate.CreateSimulation();
                // Публикуем расширение только после validation полного набора, старые уровни сохранены.
                EditorUtility.CopySerialized(candidate, config);
                EditorUtility.SetDirty(config);
            }
            finally
            {
                Object.DestroyImmediate(candidate);
                Object.DestroyImmediate(defaults);
            }
        }

        private static void EnsureSharedCatalogReference(BattleScenarioConfig config, BattleScenarioConfig shared)
        {
            var serialized = new SerializedObject(config);
            if (serialized.FindProperty("sharedScenario").objectReferenceValue != shared)
                throw new System.ArgumentException("EightWave must reference the shared TestWave scenario.");
            config.CreateSimulation();
            var catalog = serialized.FindProperty("towerArchetypes");
            var reactions = serialized.FindProperty("reactions");
            if (catalog.arraySize == 0 && reactions.arraySize == 0) return;
            var defaults = BattleScenarioConfig.CreateDefault();
            try
            {
                var defaultSerialized = new SerializedObject(defaults);
                // Допустимая миграция только неизменённых initializer copies старого shared asset.
                if (!SerializedProperty.DataEquals(catalog, defaultSerialized.FindProperty("towerArchetypes")) ||
                    !SerializedProperty.DataEquals(reactions, defaultSerialized.FindProperty("reactions")))
                    throw new System.ArgumentException("EightWave local catalogs are malformed or customized; expected empty shared data or complete default copies.");
            }
            finally
            {
                Object.DestroyImmediate(defaults);
            }
            catalog.arraySize = 0;
            reactions.arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }

        private static void SetScenarioContentId(BattleScenarioConfig config, string contentId)
        {
            var serialized = new SerializedObject(config);
            var property = serialized.FindProperty("scenarioContentId");
            if (property.stringValue == contentId) return;
            property.stringValue = contentId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
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
            var simulation = config.CreateSimulation();
            var path = simulation.Path;
            var pathWidth = simulation.Grid.Map.CellSize * 0.92f;
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
                segment.transform.localScale = new Vector3(pathWidth, 0.1f, delta.magnitude);
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
            StormModelBuilder.Bind(root);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, TowerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void AddHealthBar(
            GameObject enemy,
            Material backgroundMaterial,
            Material fillMaterial)
        {
            var healthBar = enemy.AddComponent<EnemyHealthBarView>();
            var barRoot = new GameObject("Health Bar").transform;
            barRoot.SetParent(enemy.transform, false);
            // Полная HP quad выше projected capsule head и между двумя существующими status glyph slots.
            barRoot.localPosition = new Vector3(0f, 2f, 0f);

            var background = CreatePrimitive(PrimitiveType.Quad, "Health Background");
            background.transform.SetParent(barRoot, false);
            background.transform.localScale = new Vector3(0.9f, 0.12f, 1f);
            background.GetComponent<Renderer>().sharedMaterial = backgroundMaterial;

            var fill = CreatePrimitive(PrimitiveType.Quad, "Health Fill");
            fill.transform.SetParent(barRoot, false);
            fill.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            fill.transform.localScale = new Vector3(0.9f, 0.1f, 1f);
            fill.GetComponent<Renderer>().sharedMaterial = fillMaterial;

            var serialized = new SerializedObject(healthBar);
            serialized.FindProperty("barRoot").objectReferenceValue = barRoot;
            serialized.FindProperty("fill").objectReferenceValue = fill.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddStatusSlots(GameObject enemy)
        {
            var status = enemy.AddComponent<EnemyStatusView>();
            var root = new GameObject("Status slots", typeof(RectTransform), typeof(Canvas));
            root.transform.SetParent(enemy.transform, false);
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            root.transform.localPosition = new Vector3(0, 2.2f, 0);
            root.transform.localScale = Vector3.one * .014f;
            var first = CreateText("Status slot 1", root.transform, 28, TextAnchor.MiddleCenter);
            var second = CreateText("Status slot 2", root.transform, 28, TextAnchor.MiddleCenter);
            SetRect(first.rectTransform, new Vector2(.5f, .5f), new Vector2(0, 22), new Vector2(540, 76));
            SetRect(second.rectTransform, new Vector2(.5f, .5f), new Vector2(0, -38), new Vector2(240, 40));
            foreach (var text in new[] { first, second })
            {
                text.raycastTarget = false;
                var outline = text.gameObject.AddComponent<UnityEngine.UI.Outline>();
                outline.effectColor = new Color(.02f, .03f, .04f, 1);
                outline.effectDistance = new Vector2(1.5f, -1.5f);
                text.gameObject.SetActive(false);
            }
            var serialized = new SerializedObject(status);
            serialized.FindProperty("firstSlot").objectReferenceValue = first;
            serialized.FindProperty("secondSlot").objectReferenceValue = second;
            serialized.FindProperty("firstCue").objectReferenceValue = AddStatusCue(first);
            serialized.FindProperty("secondCue").objectReferenceValue = AddStatusCue(second);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static StatusCueGraphic AddStatusCue(UnityEngine.UI.Text slot)
        {
            var go = new GameObject(slot.name + " cue", typeof(RectTransform), typeof(CanvasRenderer), typeof(StatusCueGraphic));
            go.transform.SetParent(slot.transform, false);
            var cue = go.GetComponent<StatusCueGraphic>();
            cue.rectTransform.sizeDelta = Vector2.one * StatusCueGraphic.Size;
            cue.raycastTarget = false; cue.enabled = false;
            return cue;
        }

        private static Transform CreateSelection(Material material, float cellSize)
        {
            var selection = CreatePrimitive(PrimitiveType.Cube, "Selection Highlight");
            selection.transform.localScale = new Vector3(0.98f * cellSize, 0.04f, 0.98f * cellSize);
            selection.GetComponent<Renderer>().sharedMaterial = material;
            selection.SetActive(false);
            return selection.transform;
        }

        private static Transform CreatePreview(GameObject towerPrefab, Material material, float cellSize)
        {
            var preview = Object.Instantiate(towerPrefab);
            // Preview сохраняет текущую полупрозрачную graybox форму и footprint.
            Object.DestroyImmediate(preview.GetComponent<FlameModelBinding>());
            preview.name = "Tower Preview";
            preview.transform.localScale = Vector3.one * cellSize;
            foreach (var renderer in preview.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterial = material;
            }

            preview.SetActive(false);
            return preview.transform;
        }

        private static LineRenderer CreateRangePreview(Material material)
        {
            var rangeObject = new GameObject("Range Preview", typeof(LineRenderer));
            var line = rangeObject.GetComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.startWidth = 0.04f;
            line.endWidth = 0.04f;
            line.sharedMaterial = material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            rangeObject.SetActive(false);
            return line;
        }

        private static void ValidateRequiredReferences()
        {
            var controller = Object.FindFirstObjectByType<BattleSceneController>();
            var serialized = new SerializedObject(controller);
            var propertyNames = new[]
            {
                "scenario", "enemyPrefab", "towerPrefab", "hudText", "worldCamera", "attackBeamMaterial",
                "pauseController", "placementController", "checkpointController", "enemyInspector", "reactionSummaryText", "reactionEffectMaterial", "abilityController", "bossHud"
            };
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
                "catalogRoot", "catalogButtonTemplate", "buildStatsText", "sellConfirmationText", "enemyInspector",
                "battle", "worldCamera", "selectionHighlight", "towerPreview",
                "confirmationPanel", "confirmButton", "cancelButton", "startWaveButton", "nextWaveButton", "rangePreview",
                "towerPanel", "towerStatsText", "upgradeButton", "sellButton", "sellConfirmationPanel", "confirmSellButton", "cancelSellButton"
            };
            foreach (var propertyName in placementProperties)
            {
                if (placementSerialized.FindProperty(propertyName).objectReferenceValue == null)
                {
                    throw new System.InvalidOperationException(
                        $"Battle Placement is missing required reference: {propertyName}.");
                }
            }

            var pause = Object.FindFirstObjectByType<BattlePauseController>();
            var pauseSerialized = new SerializedObject(pause);
            var pauseProperties = new[] { "battle", "placement", "pauseOverlay", "pauseButton", "resumeButton" };
            foreach (var propertyName in pauseProperties)
            {
                if (pauseSerialized.FindProperty(propertyName).objectReferenceValue == null)
                {
                    throw new System.InvalidOperationException(
                        $"Battle Pause is missing required reference: {propertyName}.");
                }
            }

            var checkpoint = Object.FindFirstObjectByType<BattleCheckpointController>();
            var checkpointSerialized = new SerializedObject(checkpoint);
            if (checkpointSerialized.FindProperty("saveStatusText").objectReferenceValue == null)
            {
                throw new System.InvalidOperationException("Battle Checkpoint is missing Save Status Text.");
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

            SetMaterialColors(material, color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateOrUpdateUnlitMaterial(string name, Color color)
        {
            var path = $"{MaterialsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }

            SetMaterialColors(material, color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetMaterialColors(Material material, Color color)
        {
            material.SetColor("_BaseColor", color);

            // SetColor("_Color") меняет serialized cache без dirty-флага; пишем legacy color напрямую.
            var serializedMaterial = new SerializedObject(material);
            var colors = serializedMaterial.FindProperty("m_SavedProperties.m_Colors");
            for (var index = 0; index < colors.arraySize; index++)
            {
                var entry = colors.GetArrayElementAtIndex(index);
                if (entry.FindPropertyRelative("first").stringValue == "_Color")
                {
                    entry.FindPropertyRelative("second").colorValue = color;
                }
            }

            serializedMaterial.ApplyModifiedPropertiesWithoutUndo();
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
            LineRenderer rangePreview,
            Material beamMaterial,
            HudReferences hud)
        {
            var controllerObject = new GameObject(
                "Battle Controller",
                typeof(BattleSceneController),
                typeof(BattlePlacementController),
                typeof(BattlePauseController),
                typeof(BattleCheckpointController),
                typeof(EnemyInspectorController), typeof(ElementChoiceController), typeof(AbilityController), typeof(BossHudController));
            var bossHud = controllerObject.GetComponent<BossHudController>();
            var bossData = new SerializedObject(bossHud);
            bossData.FindProperty("battle").objectReferenceValue = controllerObject.GetComponent<BattleSceneController>();
            bossData.FindProperty("panel").objectReferenceValue = hud.BossPanel;
            bossData.FindProperty("placement").objectReferenceValue = controllerObject.GetComponent<BattlePlacementController>();
            bossData.FindProperty("noticeText").objectReferenceValue = hud.BossNotice;
            bossData.FindProperty("healthText").objectReferenceValue = hud.BossHealth;
            bossData.FindProperty("healthFill").objectReferenceValue = hud.BossFill;
            bossData.ApplyModifiedPropertiesWithoutUndo();
            var ability = controllerObject.GetComponent<AbilityController>();
            var choice = controllerObject.GetComponent<ElementChoiceController>();
            var inspector = controllerObject.GetComponent<EnemyInspectorController>();
            var battle = controllerObject.GetComponent<BattleSceneController>();
            var placement = controllerObject.GetComponent<BattlePlacementController>();
            var pause = controllerObject.GetComponent<BattlePauseController>();
            var checkpoint = controllerObject.GetComponent<BattleCheckpointController>();
            var serialized = new SerializedObject(battle);
            serialized.FindProperty("scenario").objectReferenceValue = config;
            serialized.FindProperty("enemyPrefab").objectReferenceValue = enemyPrefab;
            serialized.FindProperty("bossPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyModelBuilder.BossPath).transform;
            serialized.FindProperty("towerPrefab").objectReferenceValue = towerPrefab;
            serialized.FindProperty("hudText").objectReferenceValue = hud.StatsText;
            serialized.FindProperty("worldCamera").objectReferenceValue = battleCamera;
            serialized.FindProperty("attackBeamMaterial").objectReferenceValue = beamMaterial;
            serialized.FindProperty("flameProjectileMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/ART1/MAT_Art1_EmberStroke.mat");
            serialized.FindProperty("flameProjectileCoreMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/ART1/MAT_Art1_EmberCore.mat");
            serialized.FindProperty("reactionEffectMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/ReactionEffect.mat");
            serialized.FindProperty("pauseController").objectReferenceValue = pause;
            serialized.FindProperty("placementController").objectReferenceValue = placement;
            serialized.FindProperty("checkpointController").objectReferenceValue = checkpoint;
            serialized.FindProperty("enemyInspector").objectReferenceValue = inspector;
            serialized.FindProperty("elementChoice").objectReferenceValue = choice;
            serialized.FindProperty("abilityController").objectReferenceValue = ability;
            serialized.FindProperty("bossHud").objectReferenceValue = bossHud;
            serialized.FindProperty("mainMenuButton").objectReferenceValue = hud.MainMenuButton;
            serialized.FindProperty("reactionSummaryText").objectReferenceValue = hud.ReactionSummaryText;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var checkpointSerialized = new SerializedObject(checkpoint);
            checkpointSerialized.FindProperty("saveStatusText").objectReferenceValue = hud.SaveStatusText;
            checkpointSerialized.FindProperty("retrySaveButton").objectReferenceValue = hud.RetrySave;
            checkpointSerialized.FindProperty("battle").objectReferenceValue = battle;
            checkpointSerialized.ApplyModifiedPropertiesWithoutUndo();

            var placementSerialized = new SerializedObject(placement);
            placementSerialized.FindProperty("battle").objectReferenceValue = battle;
            placementSerialized.FindProperty("enemyInspector").objectReferenceValue = inspector;
            placementSerialized.FindProperty("worldCamera").objectReferenceValue = battleCamera;
            placementSerialized.FindProperty("selectionHighlight").objectReferenceValue = selection;
            placementSerialized.FindProperty("towerPreview").objectReferenceValue = preview;
            placementSerialized.FindProperty("rangePreview").objectReferenceValue = rangePreview;
            placementSerialized.FindProperty("catalogRoot").objectReferenceValue = hud.CatalogRoot;
            placementSerialized.FindProperty("catalogButtonTemplate").objectReferenceValue = hud.CatalogButtonTemplate;
            placementSerialized.FindProperty("buildStatsText").objectReferenceValue = hud.BuildStatsText;
            placementSerialized.FindProperty("sellConfirmationText").objectReferenceValue = hud.SellConfirmationText;
            placementSerialized.FindProperty("confirmationPanel").objectReferenceValue = hud.ConfirmationPanel;
            placementSerialized.FindProperty("confirmButton").objectReferenceValue = hud.ConfirmButton;
            placementSerialized.FindProperty("cancelButton").objectReferenceValue = hud.CancelButton;
            placementSerialized.FindProperty("startWaveButton").objectReferenceValue = hud.StartWaveButton;
            placementSerialized.FindProperty("nextWaveButton").objectReferenceValue = hud.NextWaveButton;
            placementSerialized.FindProperty("towerPanel").objectReferenceValue = hud.TowerPanel;
            placementSerialized.FindProperty("towerStatsText").objectReferenceValue = hud.TowerStatsText;
            placementSerialized.FindProperty("upgradeButton").objectReferenceValue = hud.UpgradeButton;
            placementSerialized.FindProperty("closeTowerButton").objectReferenceValue = hud.CloseTowerButton;
            placementSerialized.FindProperty("sellButton").objectReferenceValue = hud.SellButton;
            placementSerialized.FindProperty("sellConfirmationPanel").objectReferenceValue = hud.SellConfirmationPanel;
            placementSerialized.FindProperty("confirmSellButton").objectReferenceValue = hud.ConfirmSellButton;
            placementSerialized.FindProperty("cancelSellButton").objectReferenceValue = hud.CancelSellButton;
            placementSerialized.ApplyModifiedPropertiesWithoutUndo();

            var inspectorSerialized = new SerializedObject(inspector);
            inspectorSerialized.FindProperty("battle").objectReferenceValue = battle;
            inspectorSerialized.FindProperty("placement").objectReferenceValue = placement;
            inspectorSerialized.FindProperty("worldCamera").objectReferenceValue = battleCamera;
            inspectorSerialized.FindProperty("panel").objectReferenceValue = hud.InspectorPanel;
            inspectorSerialized.FindProperty("detailsText").objectReferenceValue = hud.InspectorText;
            inspectorSerialized.FindProperty("closeButton").objectReferenceValue = hud.CloseInspectorButton;
            inspectorSerialized.ApplyModifiedPropertiesWithoutUndo();

            var abilityPreviewObject = new GameObject("Ability Preview", typeof(LineRenderer), typeof(AbilityPreviewView));
            abilityPreviewObject.transform.SetParent(controllerObject.transform, false);
            var abilityLine = abilityPreviewObject.GetComponent<LineRenderer>();
            abilityLine.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/ReactionEffect.mat");
            abilityLine.startWidth = abilityLine.endWidth = .04f;
            abilityLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; abilityLine.receiveShadows = false;
            abilityPreviewObject.SetActive(false);
            var abilitySerialized = new SerializedObject(ability);
            abilitySerialized.FindProperty("battle").objectReferenceValue = battle;
            abilitySerialized.FindProperty("placement").objectReferenceValue = placement;
            abilitySerialized.FindProperty("beginButton").objectReferenceValue = hud.AbilityBegin;
            abilitySerialized.FindProperty("confirmButton").objectReferenceValue = hud.AbilityConfirm;
            abilitySerialized.FindProperty("cancelButton").objectReferenceValue = hud.AbilityCancel;
            abilitySerialized.FindProperty("panel").objectReferenceValue = hud.AbilityPanel;
            abilitySerialized.FindProperty("statusText").objectReferenceValue = hud.AbilityStatus;
            abilitySerialized.FindProperty("feedbackText").objectReferenceValue = hud.AbilityFeedback;
            abilitySerialized.FindProperty("preview").objectReferenceValue = abilityPreviewObject.GetComponent<AbilityPreviewView>();
            abilitySerialized.ApplyModifiedPropertiesWithoutUndo();

            var choiceSerialized = new SerializedObject(choice);
            choiceSerialized.FindProperty("battle").objectReferenceValue = battle;
            choiceSerialized.FindProperty("placement").objectReferenceValue = placement;
            choiceSerialized.FindProperty("panel").objectReferenceValue = hud.ChoicePanel;
            choiceSerialized.FindProperty("safeAreaRoot").objectReferenceValue = hud.ChoiceSafeArea;
            choiceSerialized.FindProperty("titleText").objectReferenceValue = hud.ChoiceTitle;
            choiceSerialized.FindProperty("summaryText").objectReferenceValue = hud.ProgressionSummary;
            var optionRefs = choiceSerialized.FindProperty("optionButtons"); optionRefs.arraySize = 4;
            for (var i = 0; i < 4; i++) optionRefs.GetArrayElementAtIndex(i).objectReferenceValue = hud.ChoiceOptions[i];
            choiceSerialized.FindProperty("confirmButton").objectReferenceValue = hud.ConfirmChoice;
            choiceSerialized.FindProperty("cancelButton").objectReferenceValue = hud.CancelChoice;
            choiceSerialized.ApplyModifiedPropertiesWithoutUndo();

            var pauseSerialized = new SerializedObject(pause);
            pauseSerialized.FindProperty("battle").objectReferenceValue = battle;
            pauseSerialized.FindProperty("placement").objectReferenceValue = placement;
            pauseSerialized.FindProperty("pauseOverlay").objectReferenceValue = hud.PauseOverlay;
            pauseSerialized.FindProperty("pauseButton").objectReferenceValue = hud.PauseButton;
            pauseSerialized.FindProperty("resumeButton").objectReferenceValue = hud.ResumeButton;
            pauseSerialized.ApplyModifiedPropertiesWithoutUndo();
            Bind(hud.PlayerHud, ("battle", battle), ("placement", placement), ("pause", pause),
                ("ability", ability), ("choice", choice), ("inspector", inspector), ("checkpoints", checkpoint));
        }

        private sealed class HudReferences
        {
            public BattlePlayerHud PlayerHud;
            public GameObject BossPanel;
            public UnityEngine.UI.Text BossNotice, BossHealth;
            public UnityEngine.UI.Image BossFill;
            public UnityEngine.UI.Button AbilityBegin, AbilityConfirm, AbilityCancel;
            public GameObject AbilityPanel;
            public UnityEngine.UI.Text AbilityStatus, AbilityFeedback;
            public GameObject ChoicePanel;
            public RectTransform ChoiceSafeArea;
            public UnityEngine.UI.Text ChoiceTitle, ProgressionSummary;
            public UnityEngine.UI.Button[] ChoiceOptions;
            public UnityEngine.UI.Button ConfirmChoice, CancelChoice, RetrySave, MainMenuButton;
            public GameObject InspectorPanel;
            public UnityEngine.UI.Text InspectorText;
            public UnityEngine.UI.Button CloseInspectorButton;
            public UnityEngine.UI.Text ReactionSummaryText;
            public UnityEngine.UI.Text StatsText;
            public UnityEngine.UI.Text SaveStatusText;
            public RectTransform CatalogRoot;
            public UnityEngine.UI.Button CatalogButtonTemplate;
            public UnityEngine.UI.Text BuildStatsText;
            public UnityEngine.UI.Text SellConfirmationText;
            public GameObject ConfirmationPanel;
            public UnityEngine.UI.Button ConfirmButton;
            public UnityEngine.UI.Button CancelButton;
            public UnityEngine.UI.Button StartWaveButton;
            public UnityEngine.UI.Button NextWaveButton;
            public UnityEngine.UI.Button PauseButton;
            public GameObject PauseOverlay;
            public UnityEngine.UI.Button ResumeButton;
            public GameObject TowerPanel;
            public UnityEngine.UI.Text TowerStatsText;
            public UnityEngine.UI.Button UpgradeButton, CloseTowerButton;
            public UnityEngine.UI.Button SellButton;
            public GameObject SellConfirmationPanel;
            public UnityEngine.UI.Button ConfirmSellButton;
            public UnityEngine.UI.Button CancelSellButton;
        }
    }
}
