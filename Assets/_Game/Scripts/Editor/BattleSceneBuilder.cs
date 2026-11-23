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
            FlameModelBuilder.BindEnemyOcclusion(enemy);
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

            BattleEnvironmentBuilder.Create(config);
            var battleCamera = CreateCamera();
            CreateLight();
            BattleEnvironmentBuilder.ApplyAtmosphere(battleCamera, Object.FindFirstObjectByType<Light>());
            var cellSize = config.CreateSimulation().Grid.Map.CellSize;
            var selection = CreateSelection(selectionMaterial, cellSize);
            var preview = CreatePreview(towerPrefab, previewMaterial, cellSize);
            var rangePreview = CreateRangePreview(rangeMaterial);
            var hud = CreateHud(config.CreateSimulation().TowerDefinition.Price);
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
            BuildMainMenu(config);
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

        private static void BuildMainMenu(BattleScenarioConfig config)
        {
            var scene = EditorSceneManager.OpenScene(MainMenuScenePath);
            foreach (var root in scene.GetRootGameObjects())
            {
                Object.DestroyImmediate(root);
            }

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 1f, -10f);
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.05f, 0.08f);

            var canvasObject = new GameObject(
                "Main Menu Canvas",
                typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler),
                typeof(UnityEngine.UI.GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var background = CreateUiObject("Background", canvasObject.transform);
            var backgroundImage = background.AddComponent<UnityEngine.UI.Image>();
            backgroundImage.color = new Color(0.04f, 0.05f, 0.08f, 1f);
            backgroundImage.raycastTarget = false;
            backgroundImage.rectTransform.anchorMin = Vector2.zero;
            backgroundImage.rectTransform.anchorMax = Vector2.one;
            backgroundImage.rectTransform.offsetMin = Vector2.zero;
            backgroundImage.rectTransform.offsetMax = Vector2.zero;

            var title = CreateText("Title", canvasObject.transform, 64, TextAnchor.MiddleCenter);
            title.text = "VEILWARD";
            title.raycastTarget = false;
            SetRect(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 220f), new Vector2(900f, 100f));
            var subtitle = CreateText("Subtitle", canvasObject.transform, 28, TextAnchor.MiddleCenter);
            subtitle.text = "Spirit Defense";
            subtitle.raycastTarget = false;
            SetRect(subtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 140f), new Vector2(700f, 60f));

            var newGame = CreateButton("New Game Button", canvasObject.transform, "New Game", Vector2.zero);
            SetRect(newGame.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(360f, 72f));
            var continueButton = CreateButton("Continue Button", canvasObject.transform, "Continue", Vector2.zero);
            SetRect(continueButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(360f, 72f));
            var status = CreateText("Status Text", canvasObject.transform, 24, TextAnchor.MiddleCenter);
            status.raycastTarget = false;
            SetRect(status.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -220f), new Vector2(900f, 100f));

            CreateEventSystem();
            var controllerObject = new GameObject("Main Menu Controller", typeof(MainMenuController));
            var menu = controllerObject.GetComponent<MainMenuController>();
            var menuConfig = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>(ConfigPath);
            var serialized = new SerializedObject(menu);
            serialized.FindProperty("scenario").objectReferenceValue = menuConfig;
            serialized.FindProperty("newGameButton").objectReferenceValue = newGame;
            serialized.FindProperty("continueButton").objectReferenceValue = continueButton;
            serialized.FindProperty("statusText").objectReferenceValue = status;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
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
            serialized.ApplyModifiedPropertiesWithoutUndo();
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
            panel.raycastTarget = true;
            SetRect(panel.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(440f, 280f));
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
                new Vector2(1f, 0f),
                new Vector2(-24f, 24f),
                new Vector2(1080f, 540f));
            var catalog = CreateUiObject("Tower Catalog", confirmation.transform).GetComponent<RectTransform>();
            SetRect(catalog, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(1040f, 140f));
            var layout = catalog.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            var catalogTemplate = CreateButton("Catalog Button Template", catalog, "Type", Vector2.zero);
            catalogTemplate.GetComponentInChildren<UnityEngine.UI.Text>().fontSize = 22;
            catalogTemplate.gameObject.SetActive(false);
            var buildStats = CreateText("Build Stats", confirmation.transform, 24, TextAnchor.UpperLeft);
            buildStats.text = "Choose a tower type";
            buildStats.raycastTarget = false;
            SetRect(buildStats.rectTransform, new Vector2(0f, 1f), new Vector2(20f, -180f), new Vector2(1040f, 260f));
            var confirm = CreateButton(
                "Confirm Button",
                confirmation.transform,
                "Confirm",
                new Vector2(-160f, -225f));
            var cancel = CreateButton(
                "Cancel Button",
                confirmation.transform,
                "Cancel",
                new Vector2(160f, -225f));
            confirmation.SetActive(false);

            var towerPanel = CreateUiObject("Tower Panel", canvasObject.transform);
            var towerPanelImage = towerPanel.AddComponent<UnityEngine.UI.Image>();
            towerPanelImage.color = new Color(0.02f, 0.03f, 0.05f, 0.92f);
            SetRect(
                towerPanelImage.rectTransform,
                new Vector2(1f, 0f),
                new Vector2(-24f, 24f),
                new Vector2(720f, 350f));
            var towerStats = CreateText("Tower Stats", towerPanel.transform, 22, TextAnchor.UpperLeft);
            towerStats.raycastTarget = false;
            towerStats.rectTransform.anchorMin = Vector2.zero;
            towerStats.rectTransform.anchorMax = Vector2.one;
            towerStats.rectTransform.offsetMin = new Vector2(20f, 90f);
            towerStats.rectTransform.offsetMax = new Vector2(-20f, -20f);
            var upgrade = CreateButton(
                "Upgrade Button",
                towerPanel.transform,
                "Upgrade",
                new Vector2(-160f, -125f));
            var sell = CreateButton(
                "Sell Button",
                towerPanel.transform,
                "Sell",
                new Vector2(160f, -125f));
            towerPanel.SetActive(false);

            var sellConfirmation = CreateUiObject("Sell Confirmation Panel", canvasObject.transform);
            var sellConfirmationImage = sellConfirmation.AddComponent<UnityEngine.UI.Image>();
            sellConfirmationImage.color = new Color(0.02f, 0.03f, 0.05f, 0.96f);
            SetRect(
                sellConfirmationImage.rectTransform,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(600f, 190f));
            var sellLabel = CreateText(
                "Sell Confirmation Label",
                sellConfirmation.transform,
                24,
                TextAnchor.MiddleCenter);
            sellLabel.text = "Sell this tower?";
            sellLabel.raycastTarget = false;
            SetRect(
                sellLabel.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0f, -16f),
                new Vector2(560f, 60f));
            var confirmSell = CreateButton(
                "Confirm Sell Button",
                sellConfirmation.transform,
                "Confirm Sell",
                new Vector2(-140f, -42f));
            var cancelSell = CreateButton(
                "Cancel Sell Button",
                sellConfirmation.transform,
                "Cancel",
                new Vector2(140f, -42f));
            sellConfirmation.SetActive(false);

            var startWave = CreateButton("Start Wave Button", canvasObject.transform, "Start Wave", Vector2.zero);
            SetRect(
                startWave.GetComponent<RectTransform>(),
                new Vector2(1f, 1f),
                new Vector2(-24f, -24f),
                new Vector2(230f, 68f));
            var nextWave = CreateButton("Next Wave Button", canvasObject.transform, "Next Wave", Vector2.zero);
            SetRect(
                nextWave.GetComponent<RectTransform>(),
                new Vector2(1f, 1f),
                new Vector2(-24f, -100f),
                new Vector2(230f, 68f));
            var pause = CreateButton("Pause Button", canvasObject.transform, "Pause", Vector2.zero);
            SetRect(
                pause.GetComponent<RectTransform>(),
                new Vector2(1f, 1f),
                new Vector2(-24f, -176f),
                new Vector2(230f, 68f));

            var pauseOverlay = CreateUiObject("Pause Overlay", canvasObject.transform);
            var overlayImage = pauseOverlay.AddComponent<UnityEngine.UI.Image>();
            overlayImage.color = new Color(0.02f, 0.03f, 0.05f, 0.88f);
            overlayImage.raycastTarget = true;
            var overlayRect = overlayImage.rectTransform;
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            var pausedLabel = CreateText("Paused Label", pauseOverlay.transform, 42, TextAnchor.MiddleCenter);
            pausedLabel.text = "Paused";
            pausedLabel.raycastTarget = false;
            SetRect(
                pausedLabel.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 70f),
                new Vector2(400f, 70f));
            var resume = CreateButton("Resume Button", pauseOverlay.transform, "Resume", new Vector2(0f, -30f));
            pauseOverlay.SetActive(false);

            var inspectorPanel = CreateUiObject("Enemy Inspector Panel", canvasObject.transform);
            inspectorPanel.AddComponent<UnityEngine.UI.Image>().color = new Color(.02f, .03f, .05f, .94f);
            SetRect(inspectorPanel.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(-24, 24), new Vector2(720, 390));
            var inspectorText = CreateText("Enemy Inspector Details", inspectorPanel.transform, 22, TextAnchor.UpperLeft);
            inspectorText.raycastTarget = false;
            SetRect(inspectorText.rectTransform, new Vector2(0, 1), new Vector2(20, -16), new Vector2(680, 300));
            var closeInspector = CreateButton("Close Inspector Button", inspectorPanel.transform, "Закрыть", new Vector2(0, -155));
            inspectorPanel.SetActive(false);
            var reactionSummary = CreateText("Reaction Summary", canvasObject.transform, 26, TextAnchor.MiddleCenter);
            reactionSummary.raycastTarget = false;
            SetRect(reactionSummary.rectTransform, new Vector2(.5f, 1), new Vector2(0, -24), new Vector2(680, 48));

            var saveStatus = CreateText("Save Status", canvasObject.transform, 18, TextAnchor.UpperRight);
            saveStatus.raycastTarget = false;
            SetRect(
                saveStatus.rectTransform,
                new Vector2(1f, 1f),
                new Vector2(-24f, -260f),
                new Vector2(440f, 90f));
            var progressionSummary = CreateText("Progression Summary", canvasObject.transform, 24, TextAnchor.UpperLeft);
            progressionSummary.raycastTarget = false;
            SetRect(progressionSummary.rectTransform, new Vector2(0, 0), new Vector2(24, 24), new Vector2(480, 90));
            var choiceOverlay = CreateUiObject("Element Choice Overlay", canvasObject.transform);
            choiceOverlay.AddComponent<UnityEngine.UI.Image>().color = new Color(.02f, .03f, .05f, .92f);
            var overlay = choiceOverlay.GetComponent<RectTransform>();
            overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            var safeRoot = CreateUiObject("Choice Safe Area", choiceOverlay.transform).GetComponent<RectTransform>();
            safeRoot.anchorMin = Vector2.zero; safeRoot.anchorMax = Vector2.one;
            safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
            var choiceTitle = CreateText("Choice Title", safeRoot, 32, TextAnchor.MiddleCenter);
            choiceTitle.raycastTarget = false;
            SetRect(choiceTitle.rectTransform, new Vector2(.5f, .5f), new Vector2(0, 180), new Vector2(1160, 90));
            var choices = new UnityEngine.UI.Button[4];
            var ids = new[] { "flame", "tide", "gale", "stone" };
            for (var i = 0; i < choices.Length; i++)
            {
                choices[i] = CreateButton(ids[i], safeRoot, ids[i].ToUpperInvariant(), new Vector2((i - 1.5f) * 270, 20));
                choices[i].GetComponent<RectTransform>().sizeDelta = new Vector2(240, 100);
            }
            var confirmChoice = CreateButton("Confirm Element Button", safeRoot, "Confirm", new Vector2(-150, -150));
            var cancelChoice = CreateButton("Cancel Element Button", safeRoot, "Cancel preview", new Vector2(150, -150));
            var choiceHelp = CreateText("Choice Help", safeRoot, 24, TextAnchor.MiddleCenter);
            choiceHelp.raycastTarget = false;
            choiceHelp.text = "Выбор обязателен. Tap — preview; Confirm — открыть. Cancel очищает preview.";
            SetRect(choiceHelp.rectTransform, new Vector2(.5f,.5f), new Vector2(0, -65), new Vector2(1160, 90));
            choiceOverlay.SetActive(false);
            var retrySave = CreateButton("Retry Save Button", canvasObject.transform, "Retry Save", Vector2.zero);
            SetRect(retrySave.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(-24, -360), new Vector2(230, 64));
            retrySave.gameObject.SetActive(false);
            var menu = CreateButton("Main Menu Button", canvasObject.transform, "Main Menu", Vector2.zero);
            SetRect(menu.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(24, 115), new Vector2(230, 64));
            var abilityBegin = CreateButton("Ability Button", canvasObject.transform, "Печать — Ready", Vector2.zero);
            SetRect(abilityBegin.GetComponent<RectTransform>(), new Vector2(.5f, 0), new Vector2(0, 55), new Vector2(300, 70));
            abilityBegin.gameObject.SetActive(false);
            var abilityPanel = CreateUiObject("Ability Panel", canvasObject.transform);
            SetRect(abilityPanel.GetComponent<RectTransform>(), new Vector2(.5f, 0), new Vector2(0, 170), new Vector2(600, 150));
            var abilityBlocker = abilityPanel.AddComponent<UnityEngine.UI.Image>(); abilityBlocker.color = new Color(.06f, .08f, .12f, .95f);
            abilityBlocker.raycastTarget = true;
            var abilityStatus = CreateText("Ability Status", abilityPanel.transform, 24, TextAnchor.MiddleCenter);
            SetRect(abilityStatus.rectTransform, new Vector2(.5f, .5f), new Vector2(0, 35), new Vector2(580, 65));
            var abilityConfirm = CreateButton("Confirm Ability Button", abilityPanel.transform, "Confirm", new Vector2(-145, -35));
            var abilityCancel = CreateButton("Cancel Ability Button", abilityPanel.transform, "Cancel", new Vector2(145, -35));
            abilityPanel.SetActive(false);
            var bossPanel = CreateUiObject("Boss Panel", canvasObject.transform);
            SetRect(bossPanel.GetComponent<RectTransform>(), new Vector2(.5f, 1), new Vector2(105, -24), new Vector2(720, 205));
            var bossBackground = bossPanel.AddComponent<UnityEngine.UI.Image>();
            bossBackground.color = new Color(.08f,.04f,.14f,.95f); bossBackground.raycastTarget = true;
            var bossNotice = CreateText("Boss Notice", bossPanel.transform, 22, TextAnchor.UpperCenter);
            SetRect(bossNotice.rectTransform, new Vector2(.5f, 1), new Vector2(0,-10), new Vector2(700,130));
            bossNotice.raycastTarget = false; bossNotice.resizeTextForBestFit = true; bossNotice.resizeTextMinSize = 14; bossNotice.resizeTextMaxSize = 22;
            var bossHealth = CreateText("Boss Health", bossPanel.transform, 27, TextAnchor.MiddleCenter);
            SetRect(bossHealth.rectTransform, new Vector2(.5f, 0), new Vector2(0,30), new Vector2(700,45));
            bossHealth.raycastTarget = false; bossHealth.resizeTextForBestFit = true; bossHealth.resizeTextMinSize = 14; bossHealth.resizeTextMaxSize = 27;
            var bar = CreateUiObject("Boss HP Bar", bossPanel.transform);
            SetRect(bar.GetComponent<RectTransform>(), new Vector2(.5f,0), new Vector2(0,10), new Vector2(680,14));
            bar.AddComponent<UnityEngine.UI.Image>().color = new Color(.15f,.1f,.2f);
            var fill = CreateUiObject("Boss HP Fill", bar.transform).AddComponent<UnityEngine.UI.Image>();
            fill.rectTransform.anchorMin=Vector2.zero; fill.rectTransform.anchorMax=Vector2.one;
            fill.rectTransform.offsetMin=Vector2.zero; fill.rectTransform.offsetMax=Vector2.zero;
            fill.color=new Color(.9f,.5f,.75f); fill.raycastTarget=false;
            bossPanel.SetActive(false);
            var safeHud = canvasObject.AddComponent<BattleHudSafeArea>();
            var safeSerialized = new SerializedObject(safeHud);
            var edgeControls = new[] { panel.rectTransform, startWave.GetComponent<RectTransform>(), nextWave.GetComponent<RectTransform>(),
                pause.GetComponent<RectTransform>(), saveStatus.rectTransform, retrySave.GetComponent<RectTransform>(),
                menu.GetComponent<RectTransform>(), progressionSummary.rectTransform,
                abilityBegin.GetComponent<RectTransform>(), abilityPanel.GetComponent<RectTransform>(), bossPanel.GetComponent<RectTransform>() };
            var safeRefs = safeSerialized.FindProperty("edgeControls"); safeRefs.arraySize = edgeControls.Length;
            for (var i = 0; i < edgeControls.Length; i++) safeRefs.GetArrayElementAtIndex(i).objectReferenceValue = edgeControls[i];
            safeSerialized.ApplyModifiedPropertiesWithoutUndo();
            saveStatus.transform.SetAsLastSibling();
            pause.transform.SetAsLastSibling();
            menu.transform.SetAsLastSibling();
            pauseOverlay.transform.SetAsLastSibling();

            return new HudReferences
            {
                BossPanel = bossPanel, BossNotice = bossNotice, BossHealth = bossHealth, BossFill = fill,
                AbilityBegin = abilityBegin, AbilityConfirm = abilityConfirm, AbilityCancel = abilityCancel,
                AbilityPanel = abilityPanel, AbilityStatus = abilityStatus,
                ChoicePanel = choiceOverlay, ChoiceSafeArea = safeRoot, ChoiceTitle = choiceTitle,
                ProgressionSummary = progressionSummary, ChoiceOptions = choices, ConfirmChoice = confirmChoice,
                CancelChoice = cancelChoice, RetrySave = retrySave, MainMenuButton = menu,
                InspectorPanel = inspectorPanel,
                InspectorText = inspectorText,
                CloseInspectorButton = closeInspector,
                ReactionSummaryText = reactionSummary,
                StatsText = stats,
                SaveStatusText = saveStatus,
                CatalogRoot = catalog,
                CatalogButtonTemplate = catalogTemplate,
                BuildStatsText = buildStats,
                SellConfirmationText = sellLabel,
                ConfirmationPanel = confirmation,
                ConfirmButton = confirm,
                CancelButton = cancel,
                StartWaveButton = startWave,
                NextWaveButton = nextWave,
                PauseButton = pause,
                PauseOverlay = pauseOverlay,
                ResumeButton = resume,
                TowerPanel = towerPanel,
                TowerStatsText = towerStats,
                UpgradeButton = upgrade,
                SellButton = sell,
                SellConfirmationPanel = sellConfirmation,
                ConfirmSellButton = confirmSell,
                CancelSellButton = cancelSell
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
            serialized.FindProperty("towerPrefab").objectReferenceValue = towerPrefab;
            serialized.FindProperty("hudText").objectReferenceValue = hud.StatsText;
            serialized.FindProperty("worldCamera").objectReferenceValue = battleCamera;
            serialized.FindProperty("attackBeamMaterial").objectReferenceValue = beamMaterial;
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
        }

        private sealed class HudReferences
        {
            public GameObject BossPanel;
            public UnityEngine.UI.Text BossNotice, BossHealth;
            public UnityEngine.UI.Image BossFill;
            public UnityEngine.UI.Button AbilityBegin, AbilityConfirm, AbilityCancel;
            public GameObject AbilityPanel;
            public UnityEngine.UI.Text AbilityStatus;
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
            public UnityEngine.UI.Button UpgradeButton;
            public UnityEngine.UI.Button SellButton;
            public GameObject SellConfirmationPanel;
            public UnityEngine.UI.Button ConfirmSellButton;
            public UnityEngine.UI.Button CancelSellButton;
        }
    }
}
