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
                        "battle", "worldCamera", "selectionHighlight", "towerPreview",
                        "confirmationPanel", "confirmButton", "cancelButton", "startWaveButton", "nextWaveButton", "rangePreview",
                        "towerPanel", "towerStatsText", "upgradeButton", "sellButton", "sellConfirmationPanel", "confirmSellButton", "cancelSellButton"
                    },
                    errors);
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
                new[] { "scenario", "enemyPrefab", "towerPrefab", "hudText", "worldCamera", "attackBeamMaterial" },
                errors);
            RequireAssetPath(references["scenario"], ConfigPath, "scenario", errors);
            RequireAssetPath(references["enemyPrefab"], EnemyPrefabPath, "enemyPrefab", errors);
            RequireAssetPath(references["towerPrefab"], TowerPrefabPath, "towerPrefab", errors);

            if (references["scenario"] is BattleScenarioConfig config)
            {
                try
                {
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
