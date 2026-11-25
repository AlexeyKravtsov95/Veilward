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
        private const string ConfigPath = "Assets/_Game/Configs/EightWaveAbilityBossV2.asset";
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
                        new[] { "scenario", "newGameButton", "continueButton", "statusText", "retryButton", "audioOwner", "audioSettings" },
                        errors);
                    RequireAssetPath(references["scenario"], ConfigPath, "Main Menu scenario", errors);
                    RequireProductionContent(references["scenario"], errors);
                }

                ValidateAudio(roots, false, errors);
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
                    if (scaler == null || scaler.referenceResolution != new Vector2(740, 400) ||
                        scaler.screenMatchMode != UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand)
                        errors.Add("Main Menu requires the adaptive 740x400 reference.");
                    if (canvases[0].GetComponent<BattleHudSafeArea>() == null)
                        errors.Add("Main Menu requires a safe area.");
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

            ValidateAudio(roots, true, errors);
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
                        "towerPanel", "towerStatsText", "upgradeButton", "sellButton", "closeTowerButton", "sellConfirmationPanel", "confirmSellButton", "cancelSellButton"
                    },
                    errors);
            }

            if (pauses.Count == 1)
            {
                RequireReferences(
                    pauses[0],
                    new[] { "battle", "placement", "pauseOverlay", "pauseButton", "resumeButton", "audioSettings" },
                    errors);
            }

            var inspectors = FindComponents<EnemyInspectorController>(roots);
            if (inspectors.Count != 1) errors.Add("Battle requires exactly one EnemyInspectorController.");
            else
            {
                var refs = RequireReferences(inspectors[0], new[] { "battle", "placement", "worldCamera", "panel", "detailsText", "closeButton" }, errors);
                if (refs["panel"] is GameObject panel && (panel.GetComponent<UnityEngine.UI.Image>() == null || !panel.GetComponent<UnityEngine.UI.Image>().raycastTarget))
                    errors.Add("Enemy inspector panel must block raycasts.");
                if (refs["closeButton"] is UnityEngine.UI.Button close && close.GetComponent<RectTransform>().rect.height < 60)
                    errors.Add("Enemy inspector closeButton must preserve its 60-unit target.");
                if (refs["detailsText"] is UnityEngine.UI.Text details && (details.fontSize < 14 || details.rectTransform.rect.height < 135))
                    errors.Add("Enemy inspector detailsText layout is too small.");
            }

            var safeAreas = FindComponents<BattleHudSafeArea>(roots);
            if (safeAreas.Count != 1) errors.Add("Battle requires exactly one BattleHudSafeArea.");
            else
            {
                var controls = new SerializedObject(safeAreas[0]).FindProperty("safeRoots");
                if (controls.arraySize != 7) errors.Add("HUD requires seven safe-area roots, including Audio settings.");
                for (var i = 0; i < controls.arraySize; i++)
                    if (controls.GetArrayElementAtIndex(i).objectReferenceValue == null) errors.Add("Missing HUD safe-area reference.");
            }
            var playerHuds = FindComponents<BattlePlayerHud>(roots);
            if (playerHuds.Count != 1) errors.Add("Battle requires exactly one BattlePlayerHud.");
            else
            {
                var refs = RequireReferences(playerHuds[0], new[] { "battle", "placement", "pause", "ability", "choice",
                    "inspector", "checkpoints", "heart", "gold", "wave", "phase", "result", "resultTitle", "resultDetails",
                    "resultSave", "resultMenu", "choiceMenu", "startWave", "nextWave", "saveWarning", "saveStatus", "retrySave",
                    "pauseSave", "diagnostics" }, errors);
                if (refs["diagnostics"] is GameObject diagnostics && diagnostics.activeSelf)
                    errors.Add("Battle diagnostics must start hidden.");
                if (refs["result"] is GameObject result &&
                    (result.GetComponent<UnityEngine.UI.Image>() == null || !result.GetComponent<UnityEngine.UI.Image>().raycastTarget))
                    errors.Add("Battle result must block raycasts.");
            }
            var choices = FindComponents<ElementChoiceController>(roots);
            if (choices.Count != 1) errors.Add("Battle requires exactly one ElementChoiceController.");
            else
            {
                var refs = RequireReferences(choices[0], new[] { "battle", "placement", "panel", "safeAreaRoot", "titleText", "summaryText", "confirmButton", "cancelButton" }, errors);
                var options = new SerializedObject(choices[0]).FindProperty("optionButtons");
                if (options.arraySize != 4) errors.Add("Choice requires four canonical buttons.");
                for (var i = 0; i < options.arraySize; i++)
                    if (options.GetArrayElementAtIndex(i).objectReferenceValue == null) errors.Add("Missing choice option reference.");
                if (refs["panel"] is GameObject choicePanel && (choicePanel.GetComponent<UnityEngine.UI.Image>() == null || !choicePanel.GetComponent<UnityEngine.UI.Image>().raycastTarget))
                    errors.Add("Choice overlay must block raycasts.");
            }
            var bosses = FindComponents<BossHudController>(roots);
            if (bosses.Count != 1) errors.Add("Battle requires exactly one BossHudController.");
            else RequireReferences(bosses[0], new[] { "battle", "placement", "panel", "noticeText", "healthText", "healthFill" }, errors);
            var abilities = FindComponents<AbilityController>(roots);
            if (abilities.Count != 1) errors.Add("Battle requires exactly one AbilityController.");
            else
            {
                var refs = RequireReferences(abilities[0], new[] { "battle", "placement", "beginButton", "confirmButton", "cancelButton", "panel", "statusText", "feedbackText", "preview" }, errors);
                if (refs["panel"] is GameObject abilityPanel && (abilityPanel.GetComponent<UnityEngine.UI.Image>() == null || !abilityPanel.GetComponent<UnityEngine.UI.Image>().raycastTarget))
                    errors.Add("Ability panel must block raycasts.");
                if (refs["preview"] is AbilityPreviewView preview && (preview.GetComponent<LineRenderer>() == null || preview.GetComponent<LineRenderer>().sharedMaterial == null))
                    errors.Add("Ability preview requires a material and line renderer.");
            }
            var checkpoints = FindComponents<BattleCheckpointController>(roots);
            if (checkpoints.Count != 1)
            {
                errors.Add($"Battle must contain exactly one BattleCheckpointController; found {checkpoints.Count}.");
            }
            else
            {
                RequireReferences(checkpoints[0], new[] { "saveStatusText", "retrySaveButton", "battle" }, errors);
            }

            foreach (var root in roots)
            {
                ValidateRenderers(root.GetComponentsInChildren<Renderer>(true), root.name, errors);
            }

            ValidatePrefabAsset(EnemyPrefabPath, errors);
            ValidatePrefabAsset(TowerPrefabPath, errors);
        }

        private static void ValidateAudio(GameObject[] roots, bool battle, List<string> errors)
        {
            var listeners = FindComponents<AudioListener>(roots);
            var owners = FindComponents<VeilwardAudio>(roots);
            var panels = FindComponents<AudioSettingsPanel>(roots);
            if (listeners.Count != 1 || owners.Count != 1 || panels.Count != 1)
            {
                errors.Add("Production audio requires one listener, playback owner and settings panel per scene.");
                return;
            }

            var owner = new SerializedObject(owners[0]);
            if (owner.FindProperty("battleMusic").boolValue != battle)
                errors.Add("Production audio is set to the wrong scene music.");
            RequireAssetPath(owner.FindProperty("catalog").objectReferenceValue, BattleSceneBuilder.AudioCatalogPath,
                "Audio catalog", errors);
            var refs = RequireReferences(panels[0], new[] { "audioOwner", "overlay", "underlyingActions", "openButton",
                "closeButton", "muteButton", "master", "music", "sfx", "masterValue", "musicValue", "sfxValue", "muteLabel" }, errors);
            if (refs["audioOwner"] != owners[0]) errors.Add("Audio settings must use the scene audio owner.");
            if (refs["overlay"] is GameObject overlay && (overlay.activeSelf ||
                !overlay.TryGetComponent<UnityEngine.UI.Image>(out var dim) || !dim.raycastTarget))
                errors.Add("Audio settings must start hidden and block raycasts when open.");
        }

        private static void ValidateBattleController(BattleSceneController controller, List<string> errors)
        {
            var references = RequireReferences(
                controller,
                new[]
                {
                    "scenario", "enemyPrefab", "towerPrefab", "hudText", "worldCamera", "attackBeamMaterial",
                    "pauseController", "placementController", "checkpointController", "enemyInspector", "elementChoice", "abilityController", "bossHud", "mainMenuButton", "reactionSummaryText", "reactionEffectMaterial"
                },
                errors);
            RequireAssetPath(references["scenario"], ConfigPath, "scenario", errors);
            RequireProductionContent(references["scenario"], errors);
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
            var flameData = new SerializedObject(controller);
            ValidateMaterial(flameData.FindProperty("flameProjectileMaterial").objectReferenceValue as Material, "flameProjectileMaterial", errors);
            ValidateMaterial(flameData.FindProperty("flameProjectileCoreMaterial").objectReferenceValue as Material, "flameProjectileCoreMaterial", errors);
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

        private static void RequireProductionContent(UnityEngine.Object asset, List<string> errors)
        {
            if (asset is BattleScenarioConfig config &&
                config.ScenarioContentId != Veilward.Infrastructure.WaveCheckpointCompatibility.BalancedAbilityBossContentId)
                errors.Add("Production content must be eight-wave-ability-boss-v2.");
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
                    var refs = RequireReferences(status, new[] { "firstSlot", "secondSlot", "firstCue", "secondCue" }, errors);
                    if (refs["firstSlot"] == refs["secondSlot"] || prefab.GetComponentsInChildren<UnityEngine.UI.Text>(true).Length != 2)
                        errors.Add("EnemyStatusView must have exactly two distinct overhead slots.");
                    if (refs["firstCue"] == refs["secondCue"] || prefab.GetComponentsInChildren<StatusCueGraphic>(true).Length != EnemyStatusView.CueCapacity)
                        errors.Add("EnemyStatusView requires exactly two prebuilt cue graphics.");
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
