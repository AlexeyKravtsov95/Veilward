using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Veilward.Presentation;

namespace Veilward.Editor
{
    public static class BattleAssetValidator
    {
        private const string BattleScenePath = "Assets/_Game/Scenes/Battle.unity";
        private const string MainMenuScenePath = "Assets/_Game/Scenes/MainMenu.unity";
        private const string ConfigPath = "Assets/_Game/Configs/EightWave.asset";
        private const string EnemyPrefabPath = "Assets/_Game/Prefabs/Shade.prefab";
        private const string TowerPrefabPath = "Assets/_Game/Prefabs/TestTower.prefab";

        public static IReadOnlyList<string> ValidateSavedBattle()
        {
            var errors = new List<string>();
            var scene = SceneManager.GetSceneByPath(BattleScenePath);
            var openedByValidator = !scene.IsValid() || !scene.isLoaded;
            try
            {
                if (openedByValidator)
                {
                    scene = EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Additive);
                }

                ValidateScene(scene, errors);
            }
            finally
            {
                if (openedByValidator && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            return errors;
        }

        public static IReadOnlyList<string> ValidateSavedMainMenu()
        {
            var errors = new List<string>();
            var scene = SceneManager.GetSceneByPath(MainMenuScenePath);
            var openedByValidator = !scene.IsValid() || !scene.isLoaded;
            try
            {
                if (openedByValidator)
                {
                    scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Additive);
                }

                var roots = scene.GetRootGameObjects();
                foreach (var root in roots)
                {
                    ValidateMissingScripts(root, $"Main Menu {root.name}", errors);
                }

                var menus = FindComponents<MainMenuController>(roots);
                if (menus.Count != 1)
                {
                    errors.Add($"Main Menu must contain exactly one MainMenuController; found {menus.Count}.");
                }
                else
                {
                    var references = RequireReferences(
                        menus[0],
                        new[] { "scenario", "newGameButton", "continueButton", "statusText" },
                        errors);
                    RequireAssetPath(references["scenario"], ConfigPath, "Main Menu scenario", errors);
                }

                var canvases = FindComponents<Canvas>(roots);
                var raycasters = FindComponents<UnityEngine.UI.GraphicRaycaster>(roots);
                var eventSystems = FindComponents<UnityEngine.EventSystems.EventSystem>(roots);
                if (canvases.Count != 1 || raycasters.Count != 1 || eventSystems.Count != 1)
                {
                    errors.Add("Main Menu requires exactly one Canvas, GraphicRaycaster and EventSystem.");
                }

                if (canvases.Count == 1)
                {
                    var scaler = canvases[0].GetComponent<UnityEngine.UI.CanvasScaler>();
                    if (scaler == null || scaler.referenceResolution != new Vector2(1920f, 1080f))
                    {
                        errors.Add("Main Menu CanvasScaler must use a 1920x1080 reference resolution.");
                    }
                }
            }
            finally
            {
                if (openedByValidator && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            return errors;
        }

        private static void ValidateScene(Scene scene, List<string> errors)
        {
            var roots = scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                ValidateMissingScripts(root, $"Scene {root.name}", errors);
            }

            var controllers = FindComponents<BattleSceneController>(roots);
            if (controllers.Count != 1)
            {
                errors.Add($"Battle must contain exactly one BattleSceneController; found {controllers.Count}.");
            }

            var placements = FindComponents<BattlePlacementController>(roots);
            if (placements.Count != 1)
            {
                errors.Add($"Battle must contain exactly one BattlePlacementController; found {placements.Count}.");
            }

            var pauses = FindComponents<BattlePauseController>(roots);
            if (pauses.Count != 1)
            {
                errors.Add($"Battle must contain exactly one BattlePauseController; found {pauses.Count}.");
            }

            if (controllers.Count == 1)
            {
                ValidateBattleController(controllers[0], errors);
            }

            if (placements.Count == 1)
            {
                RequireReferences(
                    placements[0],
                    new[]
                    {
                        "catalogRoot", "catalogButtonTemplate", "buildStatsText", "sellConfirmationText", "enemyInspector",
                        "battle", "worldCamera", "selectionHighlight", "towerPreview",
                        "confirmationPanel", "confirmButton", "cancelButton", "startWaveButton", "nextWaveButton", "rangePreview",
                        "towerPanel", "towerStatsText", "upgradeButton", "sellButton", "sellConfirmationPanel", "confirmSellButton", "cancelSellButton"
                    },
                    errors);
            }

            if (pauses.Count == 1)
            {
                RequireReferences(
                    pauses[0],
                    new[] { "battle", "placement", "pauseOverlay", "pauseButton", "resumeButton" },
                    errors);
            }

            var choices = FindComponents<ElementChoiceController>(roots);
            if (choices.Count != 1) errors.Add("Battle requires exactly one ElementChoiceController.");
            else
            {
                var refs = RequireReferences(choices[0], new[] { "battle", "panel", "safeContent", "titleText", "descriptionText",
                    "progressionText", "confirmButton", "cancelButton", "menuButton" }, errors);
                if (refs["panel"] is GameObject panel && (panel.GetComponent<UnityEngine.UI.Image>() == null || !panel.GetComponent<UnityEngine.UI.Image>().raycastTarget))
                    errors.Add("Element choice overlay must block raycasts.");
                if (refs["safeContent"] is RectTransform safe && refs["panel"] is GameObject parent && safe.parent != parent.transform)
                    errors.Add("Choice safe content must belong to its overlay.");
                if (refs["titleText"] is UnityEngine.UI.Text title && title.fontSize < 30)
                    errors.Add("Choice title must remain readable.");
                var options = new SerializedObject(choices[0]).FindProperty("optionButtons");
                if (options.arraySize != 4) errors.Add("Element choice needs four canonical buttons.");
                for (var i = 0; i < options.arraySize; i++)
                    if (!(options.GetArrayElementAtIndex(i).objectReferenceValue is UnityEngine.UI.Button button) || button.GetComponent<RectTransform>().rect.height < 56)
                        errors.Add("Element option must preserve 56px target.");
            }

            var inspectors = FindComponents<EnemyInspectorController>(roots);
            if (inspectors.Count != 1) errors.Add("Battle requires exactly one EnemyInspectorController.");
            else
            {
                var refs = RequireReferences(inspectors[0], new[] { "battle", "placement", "worldCamera", "panel", "detailsText", "closeButton" }, errors);
                if (refs["panel"] is GameObject panel && (panel.GetComponent<UnityEngine.UI.Image>() == null || !panel.GetComponent<UnityEngine.UI.Image>().raycastTarget))
                    errors.Add("Enemy inspector panel must block raycasts.");
                if (refs["closeButton"] is UnityEngine.UI.Button close && close.GetComponent<RectTransform>().rect.height < 56)
                    errors.Add("Enemy inspector closeButton must preserve 56px target.");
                if (refs["detailsText"] is UnityEngine.UI.Text details && (details.fontSize < 22 || details.rectTransform.rect.height < 300))
                    errors.Add("Enemy inspector detailsText layout is too small.");
            }

            var checkpoints = FindComponents<BattleCheckpointController>(roots);
            if (checkpoints.Count != 1)
            {
                errors.Add($"Battle must contain exactly one BattleCheckpointController; found {checkpoints.Count}.");
            }
            else
            {
                RequireReferences(checkpoints[0], new[] { "saveStatusText" }, errors);
            }

            foreach (var root in roots)
            {
                ValidateRenderers(root.GetComponentsInChildren<Renderer>(true), root.name, errors);
            }

            ValidatePrefabAsset(EnemyPrefabPath, errors);
            ValidatePrefabAsset(TowerPrefabPath, errors);
        }

        private static void ValidateBattleController(BattleSceneController controller, List<string> errors)
        {
            var references = RequireReferences(
                controller,
                new[]
                {
                    "scenario", "enemyPrefab", "towerPrefab", "hudText", "worldCamera", "attackBeamMaterial",
                    "pauseController", "placementController", "checkpointController", "retrySaveButton", "enemyInspector", "elementChoice", "reactionSummaryText", "reactionEffectMaterial"
                },
                errors);
            RequireAssetPath(references["scenario"], ConfigPath, "scenario", errors);
            RequireAssetPath(references["enemyPrefab"], EnemyPrefabPath, "enemyPrefab", errors);
            RequireAssetPath(references["towerPrefab"], TowerPrefabPath, "towerPrefab", errors);

            if (references["scenario"] is BattleScenarioConfig config)
            {
                try
                {
                    var serialized = new SerializedObject(config);
                    RequireAssetPath(serialized.FindProperty("sharedScenario").objectReferenceValue,
                        "Assets/_Game/Configs/TestWave.asset", "sharedScenario", errors);
                    if (serialized.FindProperty("towerArchetypes").arraySize != 0 ||
                        serialized.FindProperty("reactions").arraySize != 0)
                        errors.Add("EightWave must use the shared tower/reaction catalog without local copies.");
                    if (config.ScenarioContentId != "eight-wave-unlocks-v1") errors.Add("EightWave requires native progression content ID.");
                    var simulation = config.CreateSimulation();
                    if (simulation == null)
                    {
                        errors.Add("Battle scenario returned a null simulation.");
                    }
                }
                catch (Exception exception)
                {
                    errors.Add($"Battle scenario cannot create a simulation: {exception.GetType().Name}: {exception.Message}");
                }
            }

            ValidateMaterial(references["attackBeamMaterial"] as Material, "attackBeamMaterial", errors);
            ValidateMaterial(references["reactionEffectMaterial"] as Material, "reactionEffectMaterial", errors);
        }

        private static Dictionary<string, UnityEngine.Object> RequireReferences(
            Component component,
            IEnumerable<string> propertyNames,
            List<string> errors)
        {
            var references = new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);
            var serialized = new SerializedObject(component);
            foreach (var propertyName in propertyNames)
            {
                var property = serialized.FindProperty(propertyName);
                var value = property == null ? null : property.objectReferenceValue;
                references[propertyName] = value;
                if (value == null)
                {
                    errors.Add($"{component.GetType().Name} is missing required reference: {propertyName}.");
                }
            }

            return references;
        }

        private static void RequireAssetPath(
            UnityEngine.Object reference,
            string expectedPath,
            string label,
            List<string> errors)
        {
            if (reference == null)
            {
                return;
            }

            var actualPath = AssetDatabase.GetAssetPath(reference);
            if (!string.Equals(actualPath, expectedPath, StringComparison.Ordinal))
            {
                errors.Add($"{label} must reference {expectedPath}; actual: {actualPath}.");
            }
        }

        private static void ValidatePrefabAsset(string path, List<string> errors)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                errors.Add($"Required prefab asset is missing: {path}.");
                return;
            }

            if (path == EnemyPrefabPath)
            {
                var status = prefab.GetComponent<EnemyStatusView>();
                if (status == null) errors.Add("Enemy prefab requires EnemyStatusView.");
                else
                {
                    var refs = RequireReferences(status, new[] { "firstSlot", "secondSlot" }, errors);
                    if (refs["firstSlot"] == refs["secondSlot"] || prefab.GetComponentsInChildren<UnityEngine.UI.Text>(true).Length != 2)
                        errors.Add("EnemyStatusView must have exactly two distinct overhead slots.");
                }
            }
            ValidateMissingScripts(prefab, path, errors);
            ValidateRenderers(prefab.GetComponentsInChildren<Renderer>(true), path, errors);
        }

        private static void ValidateMissingScripts(GameObject root, string label, List<string> errors)
        {
            foreach (var gameObject in root.GetComponentsInChildren<Transform>(true))
            {
                var missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject.gameObject);
                if (missingCount > 0)
                {
                    errors.Add($"{label}/{gameObject.name} contains {missingCount} missing script component(s).");
                }
            }
        }

        private static void ValidateRenderers(IEnumerable<Renderer> renderers, string label, List<string> errors)
        {
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                if (materials == null || materials.Length == 0)
                {
                    errors.Add($"{label}/{renderer.name} has no materials.");
                    continue;
                }

                foreach (var material in materials)
                {
                    ValidateMaterial(material, $"{label}/{renderer.name}", errors);
                }
            }
        }

        private static void ValidateMaterial(Material material, string label, List<string> errors)
        {
            if (material == null)
            {
                errors.Add($"{label} contains a missing material.");
                return;
            }

            if (material.shader == null)
            {
                errors.Add($"{label} uses material {material.name} with a missing shader.");
            }
        }

        private static List<T> FindComponents<T>(IEnumerable<GameObject> roots) where T : Component
        {
            var components = new List<T>();
            foreach (var root in roots)
            {
                components.AddRange(root.GetComponentsInChildren<T>(true));
            }

            return components;
        }
    }
}
