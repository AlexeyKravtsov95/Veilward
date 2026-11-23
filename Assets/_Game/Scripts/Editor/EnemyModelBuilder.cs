using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Veilward.Presentation;

namespace Veilward.Editor
{
    public static class EnemyModelBuilder
    {
        public const string Folder = "Assets/_Game/Art/Models/ART5/";
        public const string ShadePath = "Assets/_Game/Prefabs/Shade.prefab";
        public const string BossPath = "Assets/_Game/Prefabs/ART5/P_Enemy_RiftWarden.prefab";

        [MenuItem("VEILWARD/ART.5/Import enemies")]
        public static void Import()
        {
            if (UnityEngine.Application.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            Directory.CreateDirectory("Assets/_Game/Prefabs/ART5"); AssetDatabase.Refresh();
            foreach (var name in new[] { "Enemy_Shade", "Enemy_RiftWarden" })
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(Folder + name + ".fbx");
                if (importer == null) throw new InvalidOperationException("Missing ART5 source export " + name);
                importer.globalScale = 1; importer.useFileScale = true; importer.bakeAxisConversion = true;
                importer.importAnimation = true; importer.animationType = ModelImporterAnimationType.Legacy;
                importer.importCameras = false; importer.importLights = false; importer.addCollider = false;
                importer.isReadable = false; importer.importNormals = ModelImporterNormals.Import;
                importer.importTangents = ModelImporterTangents.None; importer.animationCompression = ModelImporterAnimationCompression.Off;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                foreach (var material in new[] { "Surface", "Glow" })
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Art1" + material),
                        AssetDatabase.LoadAssetAtPath<Material>(ArtReferenceBuilder.MaterialFolder + "MAT_Art1_" + material + ".mat"));
                var take = importer.defaultClipAnimations.First().takeName;
                var names = new[] { "Idle", "Move", "Hit", "Death", "Leak" };
                var starts = new[] { 0, 31, 62, 75, 94 }; var ends = new[] { 30, 61, 74, 93, 112 };
                importer.clipAnimations = names.Select((clip, i) => new ModelImporterClipAnimation
                { name = clip, takeName = take, firstFrame = starts[i], lastFrame = ends[i], loopTime = i < 2, wrapMode = i < 2 ? WrapMode.Loop : WrapMode.ClampForever }).ToArray();
                importer.SaveAndReimport();
            }
            var shade = PrefabUtility.LoadPrefabContents(ShadePath);
            try
            {
                Bind(shade, false); PrefabUtility.SaveAsPrefabAsset(shade, ShadePath);
                shade.name = "Rift Warden"; shade.transform.localScale = Vector3.one;
                Bind(shade, true); PrefabUtility.SaveAsPrefabAsset(shade, BossPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(shade); }
            AssetDatabase.SaveAssets();
            NormalizeSerializedWhitespace(ShadePath); NormalizeSerializedWhitespace(BossPath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        public static void NormalizeSerializedWhitespace(string path)
        {
            // Unity пишет пробел после пустого YAML scalar; удаляем только trailing whitespace собственных prefabs.
            File.WriteAllLines(path, File.ReadAllLines(path).Select(line => line.TrimEnd()).ToArray());
        }

        public static void Bind(GameObject enemy, bool boss = false)
        {
            var previous = enemy.GetComponentInChildren<EnemyModelView>(true);
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var capsule = enemy.GetComponent<Renderer>(); if (capsule != null) capsule.enabled = false;
            var oldCue = enemy.transform.Find("Body occlusion cue");
            if (oldCue != null) UnityEngine.Object.DestroyImmediate(oldCue.gameObject);
            var orientation = new GameObject("Authored enemy orientation"); orientation.transform.SetParent(enemy.transform, false);
            // UI/root scale сохранён; геометрия в meters компенсируется только здесь.
            var inverse = 1f / enemy.transform.localScale.x;
            orientation.transform.localPosition = new Vector3(0, -.6f * inverse, 0);
            orientation.transform.localScale = Vector3.one * inverse;
            var path = Folder + (boss ? "Enemy_RiftWarden" : "Enemy_Shade") + ".fbx";
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            model.transform.SetParent(orientation.transform, false);
            // Plain hierarchy сохраняет стабильные localIDs при полном повторном Build; mesh/clips остаются FBX assets.
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (var animation in model.GetComponentsInChildren<Animation>(true))
            { animation.playAutomatically = false; animation.enabled = false; }
            var view = orientation.AddComponent<EnemyModelView>(); var serialized = new SerializedObject(view);
            serialized.FindProperty("model").objectReferenceValue = model.transform;
            var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().ToArray();
            foreach (var name in new[] { "Idle", "Move", "Hit", "Death", "Leak" })
                serialized.FindProperty(name.ToLowerInvariant()).objectReferenceValue = clips.Single(c => c.name == name);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            BindBodyOcclusion(enemy);
            FlameModelBuilder.BindEnemyOcclusion(enemy);
        }

        public static void BindBodyOcclusion(GameObject enemy)
        {
            var view = enemy.GetComponentInChildren<EnemyModelView>(true); if (view == null) return;
            var material = AssetDatabase.LoadAssetAtPath<Material>(ArtReferenceBuilder.MaterialFolder + "MAT_Art21_ShadeOcclusion.mat");
            foreach (var mesh in view.Model.GetComponentsInChildren<MeshFilter>(true))
            {
                var cueName = "Body occlusion cue " + mesh.name;
                if (mesh.name.StartsWith("Body occlusion cue", StringComparison.Ordinal) || mesh.transform.Find(cueName) != null) continue;
                ShadeOcclusionView.Create(mesh.transform, material).name = cueName;
            }
        }

        [MenuItem("VEILWARD/ART.5/Bind saved Battle")]
        public static void BindScene()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Game/Scenes/Battle.unity");
            var controller = UnityEngine.Object.FindFirstObjectByType<BattleSceneController>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("bossPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(BossPath).transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
    }
}
