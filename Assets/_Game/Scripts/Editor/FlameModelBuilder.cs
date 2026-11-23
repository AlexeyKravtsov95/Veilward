using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Veilward.Presentation;

namespace Veilward.Editor
{
    public static class FlameModelBuilder
    {
        public const string ModelFolder = "Assets/_Game/Art/Models/ART3/";
        public const string PrefabFolder = "Assets/_Game/Prefabs/ART3/";
        public const string MaterialFolder = "Assets/_Game/Art/Materials/ART3/";

        [MenuItem("VEILWARD/ART.3a/Import FLAME levels")]
        public static void Build()
        {
            if (UnityEngine.Application.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            Directory.CreateDirectory(PrefabFolder);
            Directory.CreateDirectory(MaterialFolder);
            AssetDatabase.Refresh();
            var surface = AssetDatabase.LoadAssetAtPath<Material>(ArtReferenceBuilder.MaterialFolder + "MAT_Art1_Surface.mat");
            var glow = AssetDatabase.LoadAssetAtPath<Material>(ArtReferenceBuilder.MaterialFolder + "MAT_Art1_Glow.mat");
            if (surface == null || glow == null) throw new InvalidOperationException("Shared ART1 palette materials required.");
            HealthOcclusionMaterial("HealthBackground", "MAT_OccludedHealthBackground");
            HealthOcclusionMaterial("HealthFill", "MAT_OccludedHealthFill");
            for (var level = 1; level <= 3; level++)
            {
                var name = "Tower_FlameLanternL" + level;
                var path = ModelFolder + name + ".fbx";
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                if (importer == null) throw new InvalidOperationException("Missing model: " + path);
                importer.globalScale = 1; importer.useFileScale = true; importer.bakeAxisConversion = true;
                importer.isReadable = false; importer.importAnimation = false; importer.importCameras = false;
                importer.importLights = false; importer.addCollider = false;
                importer.importNormals = ModelImporterNormals.Import; importer.importTangents = ModelImporterTangents.None;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Art1Surface"), surface);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Art1Glow"), glow);
                importer.SaveAndReimport();
                var root = new GameObject("P_" + name);
                try
                {
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
                    model.transform.SetParent(root.transform, false);
                    ShadeOcclusionView.Create(model.transform, AssetDatabase.LoadAssetAtPath<Material>(
                        ArtReferenceBuilder.MaterialFolder + "MAT_Art21_TowerMask.mat"));
                    // Anchor не участвует в Domain attack/range или generic beam.
                    var anchor = new GameObject("ShotSource").transform;
                    anchor.SetParent(root.transform, false);
                    anchor.localPosition = new Vector3(0, 1.15f, 0);
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(level));
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            var tower = PrefabUtility.LoadPrefabContents(BattleSceneBuilder.TowerPrefabPath);
            try
            {
                Bind(tower);
                PrefabUtility.SaveAsPrefabAsset(tower, BattleSceneBuilder.TowerPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(tower); }
            const string enemyPath = "Assets/_Game/Prefabs/Shade.prefab";
            var enemy = PrefabUtility.LoadPrefabContents(enemyPath);
            try { BindEnemyOcclusion(enemy); PrefabUtility.SaveAsPrefabAsset(enemy, enemyPath); }
            finally { PrefabUtility.UnloadPrefabContents(enemy); }
            AssetDatabase.SaveAssets();
        }

        private static void HealthOcclusionMaterial(string sourceName, string name)
        {
            var path = MaterialFolder + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Veilward/ShadeOcclusion"));
                AssetDatabase.CreateAsset(material, path);
            }
            var source = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/" + sourceName + ".mat");
            material.SetColor("_Tint", source.GetColor("_BaseColor"));
            EditorUtility.SetDirty(material);
        }

        // Уже проверенный stencil pass: проявляется только за production FLAME mask.
        public static void BindEnemyOcclusion(GameObject enemy)
        {
            if (enemy.GetComponentInChildren<EnemyModelView>(true) != null) EnemyModelBuilder.BindBodyOcclusion(enemy);
            else AddOcclusion(enemy.transform, "Body occlusion cue", ArtReferenceBuilder.MaterialFolder + "MAT_Art21_ShadeOcclusion.mat");
            AddOcclusion(enemy.transform.Find("Health Bar/Health Background"), "HP background occlusion cue", MaterialFolder + "MAT_OccludedHealthBackground.mat");
            AddOcclusion(enemy.transform.Find("Health Bar/Health Fill"), "HP fill occlusion cue", MaterialFolder + "MAT_OccludedHealthFill.mat");
        }

        private static void AddOcclusion(Transform target, string name, string materialPath)
        {
            if (target == null) throw new InvalidOperationException("Missing graybox enemy anchor.");
            if (target.Find(name) != null) return;
            var previous = target.Find("Occlusion presentation pass");
            if (previous != null) { previous.name = name; return; }
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) throw new InvalidOperationException("Missing occlusion material: " + materialPath);
            // Уникальные names нужны Unity SaveAsPrefabAsset для стабильного fileID matching.
            ShadeOcclusionView.Create(target, material).name = name;
        }

        public static string PrefabPath(int level) => PrefabFolder + "P_Tower_FlameLanternL" + level + ".prefab";

        public static void Bind(GameObject root)
        {
            var binding = root.GetComponent<FlameModelBinding>() ?? root.AddComponent<FlameModelBinding>();
            var serialized = new SerializedObject(binding);
            var levels = serialized.FindProperty("levels"); levels.arraySize = 3;
            for (var i = 0; i < 3; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(i + 1));
                if (prefab == null) throw new InvalidOperationException("Import FLAME production prefabs first.");
                levels.GetArrayElementAtIndex(i).objectReferenceValue = prefab.transform;
            }
            // Повторный Bind живого root не записывает production model в fallback.
            var renderers = root.GetComponentsInChildren<Renderer>()
                .Where(renderer => binding.Model == null || !renderer.transform.IsChildOf(binding.Model)).ToArray();
            var fallback = serialized.FindProperty("fallbackRenderers"); fallback.arraySize = renderers.Length;
            for (var i = 0; i < renderers.Length; i++)
                fallback.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
