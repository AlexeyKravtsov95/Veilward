using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Veilward.Presentation;

namespace Veilward.Editor
{
    public static class ArtReferenceBuilder
    {
        public const string ScenePath = "Assets/_Game/Scenes/ArtReference.unity";
        public const string ModelFolder = "Assets/_Game/Art/Models/ART1/";
        public const string PrefabFolder = "Assets/_Game/Prefabs/ART1/";
        public const string MaterialFolder = "Assets/_Game/Art/Materials/ART1/";
        public static readonly string[] ModelNames = { "Tower_FlameLantern", "Enemy_Shade", "Environment_FloatingIsland",
            "Environment_PathTile", "Environment_BuildPad", "Prop_Waystone", "Prop_MossRock", "Prop_RitualShrub" };

        [MenuItem("VEILWARD/ART.1/Build ArtReference")]
        public static void Build()
        {
            if (UnityEngine.Application.isPlaying) throw new InvalidOperationException("Stop Play Mode before rebuilding art assets.");
            Directory.CreateDirectory(PrefabFolder); Directory.CreateDirectory(MaterialFolder);
            AssetDatabase.Refresh();
            var texturePath = "Assets/_Game/Art/Textures/ART1/Art1Palette_BaseColor_128.png";
            var ti = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            ti.textureType = TextureImporterType.Default; ti.sRGBTexture = true; ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Point; ti.wrapMode = TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            var ios = ti.GetPlatformTextureSettings("iPhone"); ios.overridden = true;
            ios.maxTextureSize = 128; ios.format = TextureImporterFormat.ASTC_4x4;
            ti.SetPlatformTextureSettings(ios); ti.SaveAndReimport();
            var palette = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            var surface = Material("MAT_Art1_Surface", "Universal Render Pipeline/Lit", Color.white, palette);
            surface.SetFloat("_Smoothness", .12f); surface.SetFloat("_Metallic", 0);
            var glow = Material("MAT_Art1_Glow", "Universal Render Pipeline/Unlit", Color.white, palette);
            var strike = Material("MAT_Art1_EmberStroke", "Veilward/FlameImpulse", new Color(1f,.22f,.035f));
            var occlusion = OcclusionMaterial();
            var core = Material("MAT_Art1_EmberCore", "Veilward/FlameImpulse", new Color(1f,.87f,.42f));
            foreach (var name in ModelNames)
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(ModelFolder + name + ".fbx");
                importer.globalScale = 1f; importer.useFileScale = true; importer.bakeAxisConversion = true;
                importer.isReadable = false; importer.importAnimation = false; importer.importCameras = false;
                importer.importLights = false; importer.addCollider = false;
                importer.importNormals = ModelImporterNormals.Import; importer.importTangents = ModelImporterTangents.None;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Art1Surface"), surface);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Art1Glow"), glow);
                importer.SaveAndReimport();
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + name + ".fbx");
                var root = new GameObject("P_" + name);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
                model.transform.SetParent(root.transform, false);
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                {
                    var mats = renderer.sharedMaterials;
                    for (var i = 0; i < mats.Length; i++)
                        mats[i] = mats[i] != null && mats[i].name.Contains("Glow") ? glow : surface;
                    renderer.sharedMaterials = mats;
                }
                if (name == "Enemy_Shade") AddHealth(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "P_" + name + ".prefab");
                UnityEngine.Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Battle Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            // Читаем сохранённую камеру, не дублируем её pose/projection константами.
            var battle = EditorSceneManager.OpenScene("Assets/_Game/Scenes/Battle.unity", OpenSceneMode.Additive);
            var original = battle.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>()).Single();
            camera.CopyFrom(original);
            camera.transform.SetPositionAndRotation(original.transform.position, original.transform.rotation);
            EditorSceneManager.CloseScene(battle, true);
            SceneManager.SetActiveScene(scene);
            camera.backgroundColor = new Color(.105f, .18f, .23f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            var sun = new GameObject("Directional Light", typeof(Light)).GetComponent<Light>();
            sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(50,-30,0);
            sun.intensity = 1.2f; sun.color = new Color(1f,.94f,.83f); sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun; RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.64f,.70f,.74f); RenderSettings.fog = false;
            var environment = new GameObject("Sanctuary Fragment").transform;
            Place("Environment_FloatingIsland", Vector3.zero, environment);
            var scenario = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/TestWave.asset");
            var simulation = scenario.CreateSimulation();
            // Pavers follow the saved route. Coincident corner tiles emitted once.
            var placed = new System.Collections.Generic.HashSet<Vector2Int>();
            for (float d = 0; d <= simulation.Path.TotalLength; d += .5f)
            {
                var p = simulation.Path.GetPosition(d);
                var key = new Vector2Int(Mathf.RoundToInt(p.X), Mathf.RoundToInt(p.Y));
                if (placed.Add(key)) Place("Environment_PathTile", new Vector3(key.x,0,key.y), environment);
            }
            foreach (var p in new[] { new Vector3(-2,0,-1), new Vector3(2,0,-1),
                         new Vector3(-3,0,1), new Vector3(2,0,3) })
                Place("Environment_BuildPad", p, environment);
            foreach (var p in new[] { new Vector3(-5.5f,0,-2.8f),new Vector3(5.25f,0,2.8f),
                         new Vector3(-4.8f,0,2.8f),new Vector3(3.3f,0,-2.9f) })
                Place("Prop_Waystone", p, environment);
            var rocks = new[] { new Vector3(-4.8f,0,1.8f),new Vector3(-4.3f,0,2.1f),new Vector3(3.7f,0,-3),
                new Vector3(4.2f,0,-2.6f),new Vector3(4.8f,0,3.2f),new Vector3(-2.7f,0,-3.4f) };
            for (var i = 0; i < rocks.Length; i++)
            {
                var rock = Place("Prop_MossRock",rocks[i],environment); rock.localScale *= .7f + (i%3)*.15f;
                Place("Prop_RitualShrub",rocks[i]+new Vector3(.22f,0,-.08f),environment);
            }
            AddArt11Clusters(environment);
            var showcase = new GameObject("ArtReference Showcase").AddComponent<ArtReferenceShowcase>();
            var so = new SerializedObject(showcase);
            so.FindProperty("scenario").objectReferenceValue = scenario;
            so.FindProperty("towerPrefab").objectReferenceValue = Prefab("Tower_FlameLantern").transform;
            so.FindProperty("enemyPrefab").objectReferenceValue = Prefab("Enemy_Shade").transform;
            so.FindProperty("strikeMaterial").objectReferenceValue = strike;
            so.FindProperty("strikeCoreMaterial").objectReferenceValue = core;
            so.FindProperty("occlusionMaterial").objectReferenceValue = occlusion;
            so.FindProperty("towerOcclusionMask").objectReferenceValue = Material("MAT_Art21_TowerMask", "Veilward/TowerOcclusionMask", Color.white);
            so.FindProperty("worldCamera").objectReferenceValue = camera;
            so.FindProperty("environmentRoot").objectReferenceValue = environment;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, ScenePath); AssetDatabase.SaveAssets();
            Debug.Log("ART.1 ArtReference built; Battle/MainMenu/configs/build settings unchanged.");
        }

        private static Material OcclusionMaterial()
        {
            var palette = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Textures/ART1/Art1Palette_BaseColor_128.png");
            return Material("MAT_Art21_ShadeOcclusion", "Veilward/ShadeOcclusion", Color.white, palette);
        }

        [MenuItem("VEILWARD/ART.2a.1/Apply Presentation")]
        public static void ApplyArt21()
        {
            if (UnityEngine.Application.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var showcase = UnityEngine.Object.FindFirstObjectByType<ArtReferenceShowcase>();
            var so = new SerializedObject(showcase);
            so.FindProperty("occlusionMaterial").objectReferenceValue = OcclusionMaterial();
            so.FindProperty("towerOcclusionMask").objectReferenceValue = Material("MAT_Art21_TowerMask", "Veilward/TowerOcclusionMask", Color.white);
            so.ApplyModifiedPropertiesWithoutUndo();
            Material("MAT_Art1_EmberStroke", "Veilward/FlameImpulse", new Color(1f,.22f,.035f));
            Material("MAT_Art1_EmberCore", "Veilward/FlameImpulse", new Color(1f,.87f,.42f));
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }

        // Фиксированные кластеры эталона: kit instances, без runtime генерации/новой карты.
        private static void AddArt11Clusters(Transform environment)
        {
            var group=new GameObject("ART.1.1 Sanctuary Clusters").transform;group.SetParent(environment,false);
            foreach(var p in new[]{new Vector3(-3.9f,0,.7f),new Vector3(2.9f,0,-.6f),
                new Vector3(1.4f,0,3.8f),new Vector3(-2.9f,0,-.25f)})
            {
                var rock=Place("Prop_MossRock",p,group);rock.localScale=Vector3.one*.85f;
                var shrub=Place("Prop_RitualShrub",p+new Vector3(-.2f,0,.16f),group);shrub.localScale=Vector3.one*.8f;
            }
            // Низкие парные знаки у входа/выхода повторяют язык дорожки и свободных sockets.
            foreach(var p in new[]{new Vector3(-5.5f,0,-1.2f),new Vector3(5.25f,0,1.2f)})
            {var marker=Place("Prop_Waystone",p,group);marker.localScale=Vector3.one*.75f;}
        }

        [MenuItem("VEILWARD/ART.1/Apply ART.1.1 Environment Clusters")]
        public static void RefineEnvironment()
        {
            if(UnityEngine.Application.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
            if(SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Open ArtReference only.");
            var environment=GameObject.Find("Sanctuary Fragment").transform;
            var previous=environment.Find("ART.1.1 Sanctuary Clusters");
            if(previous!=null)UnityEngine.Object.DestroyImmediate(previous.gameObject);
            AddArt11Clusters(environment);EditorSceneManager.MarkSceneDirty(environment.gameObject.scene);
            EditorSceneManager.SaveScene(environment.gameObject.scene);
        }

        public static GameObject Prefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "P_" + name + ".prefab");
        private static Transform Place(string name, Vector3 p, Transform parent)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(Prefab(name));
            go.transform.SetParent(parent, false); go.transform.position = p;
            return go.transform;
        }
        private static Material Material(string name, string shader, Color color, Texture2D texture = null)
        {
            var path = MaterialFolder + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = Shader.Find(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
            EditorUtility.SetDirty(mat); return mat;
        }
        private static void AddHealth(GameObject enemy)
        {
            var health = enemy.AddComponent<EnemyHealthBarView>();
            var root = new GameObject("Health Bar").transform;
            root.SetParent(enemy.transform, false); root.localPosition = new Vector3(0,1.53f,0);
            Transform fill = null;
            foreach (var name in new[] { "HealthBackground", "HealthFill" })
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = name; UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
                quad.transform.SetParent(root, false); quad.transform.localScale = new Vector3(.9f,.1f,1);
                quad.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/" + name + ".mat");
                if (name == "HealthFill") { fill = quad.transform; fill.localPosition = new Vector3(0,0,-.01f); }
            }
            var so = new SerializedObject(health);
            so.FindProperty("barRoot").objectReferenceValue = root;
            so.FindProperty("fill").objectReferenceValue = fill;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
