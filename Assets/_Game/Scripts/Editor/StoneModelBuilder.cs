using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Veilward.Presentation;

namespace Veilward.Editor
{
    public static class StoneModelBuilder
    {
        public const string ModelFolder = "Assets/_Game/Art/Models/ART3/STONE/";
        public const string PrefabFolder = "Assets/_Game/Prefabs/ART3/STONE/";

        [MenuItem("VEILWARD/ART.3d/Import STONE levels")]
        public static void Build()
        {
            if (UnityEngine.Application.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            Directory.CreateDirectory(PrefabFolder);
            AssetDatabase.Refresh();
            var surface = AssetDatabase.LoadAssetAtPath<Material>(ArtReferenceBuilder.MaterialFolder + "MAT_Art1_Surface.mat");
            var glow = AssetDatabase.LoadAssetAtPath<Material>(ArtReferenceBuilder.MaterialFolder + "MAT_Art1_Glow.mat");
            var mask = AssetDatabase.LoadAssetAtPath<Material>(ArtReferenceBuilder.MaterialFolder + "MAT_Art21_TowerMask.mat");
            if (surface == null || glow == null || mask == null)
                throw new InvalidOperationException("Shared ART1 palette and tower mask required.");
            for (var level = 1; level <= 3; level++)
            {
                var name = "Tower_StoneSealL" + level;
                var path = ModelFolder + name + ".fbx";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
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
                    ShadeOcclusionView.Create(model.transform, mask);
                    // Семантический anchor: Domain coordinates и generic beam остаются прежними.
                    var anchor = new GameObject("ShotSource").transform;
                    anchor.SetParent(root.transform, false);
                    anchor.localPosition = new Vector3(0, 1.0f, 0);
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
            AssetDatabase.SaveAssets();
        }

        public static string PrefabPath(int level) => PrefabFolder + "P_Tower_StoneSealL" + level + ".prefab";

        public static void Bind(GameObject root)
        {
            // Старый binder сохраняет stoneLevels и serialized compatibility; runtime owner остаётся один.
            GaleModelBuilder.Bind(root);
            var serialized = new SerializedObject(root.GetComponent<FlameModelBinding>());
            var levels = serialized.FindProperty("stoneLevels"); levels.arraySize = 3;
            for (var i = 0; i < 3; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(i + 1));
                if (prefab == null) throw new InvalidOperationException("Import STONE production prefabs first.");
                levels.GetArrayElementAtIndex(i).objectReferenceValue = prefab.transform;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
