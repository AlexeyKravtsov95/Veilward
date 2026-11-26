using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Veilward.Presentation;
using Object = UnityEngine.Object;

namespace Veilward.Editor
{
    public static class InkBattleArtBuilder
    {
        public const string Sprites = "Assets/_Game/Art/Sprites/InkAndLanterns/";
        public const string Prefabs = "Assets/_Game/Prefabs/InkAndLanterns/";
        private const string Materials = "Assets/_Game/Art/Materials/InkAndLanterns/";
        private const string BattlePath = "Assets/_Game/Scenes/Battle.unity";

        [MenuItem("VEILWARD/Ink and Lanterns/Import and bind Battle")]
        public static void ImportAndBindBattle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Остановите Play Mode перед импортом.");

            Directory.CreateDirectory(Prefabs);
            Directory.CreateDirectory(Materials);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var png in Directory.GetFiles(Sprites, "*.png"))
                ConfigureTexture(png.Replace('\\', '/'));

            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Game/Art/Shaders/InkBattleSprite.shader");
            var tower = Material("Shrine", shader);
            tower.renderQueue = (int)RenderQueue.AlphaTest + 5;
            tower.SetFloat("_Cutoff", .40f);
            tower.SetFloat("_DepthWrite", 1f);
            tower.SetFloat("_StencilPass", (float)StencilOp.Replace);
            tower.SetFloat("_StencilWriteMask", 64f);
            EditorUtility.SetDirty(tower);

            var body = Material("Spirit", shader);
            var ghost = Material("SpiritOccluded", shader);
            ghost.renderQueue = (int)RenderQueue.Transparent + 10;
            ghost.SetFloat("_DepthTest", (float)CompareFunction.Greater);
            ghost.SetFloat("_StencilCompare", (float)CompareFunction.Equal);
            ghost.SetFloat("_Ghost", 1f);
            ghost.SetColor("_Tint", new Color(.48f, .82f, .88f, .46f));
            EditorUtility.SetDirty(ghost);

            BuildFlame(1, tower, .96f, .62f);
            BuildFlame(2, tower, 1.06f, .71f);
            BuildFlame(3, tower, 1.16f, .78f);
            BuildEnemy("Shade", EnemyModelBuilder.ShadePath, body, ghost, 1.19f, .08f, false);
            BuildEnemy("Warden", EnemyModelBuilder.BossPath, body, ghost, 1.83f, .07f, true);
            AssetDatabase.SaveAssets();
            foreach (var material in new[] { tower, body, ghost })
                EnemyModelBuilder.NormalizeSerializedWhitespace(AssetDatabase.GetAssetPath(material));

            var scene = EditorSceneManager.OpenScene(BattlePath);
            var controller = Object.FindFirstObjectByType<BattleSceneController>();
            var binding = new SerializedObject(controller);
            BindController(binding);
            binding.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        internal static void BindController(SerializedObject binding)
        {
            binding.FindProperty("inkShadePrefab").objectReferenceValue = Prefab("Shade").transform;
            binding.FindProperty("inkWardenPrefab").objectReferenceValue = Prefab("Warden").transform;
            var levels = binding.FindProperty("inkFlameLevels");
            levels.arraySize = 3;
            for (var i = 0; i < levels.arraySize; i++)
                levels.GetArrayElementAtIndex(i).objectReferenceValue = Prefab("Flame" + (i + 1)).GetComponent<InkFlameView>();
        }

        private static GameObject Prefab(string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + name + ".prefab")
            ?? throw new InvalidOperationException("Не найден prefab «Чернила и фонари»: " + name);

        private static void ConfigureTexture(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var name = Path.GetFileNameWithoutExtension(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 256f;
            importer.isReadable = false;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.mipMapsPreserveCoverage = name.StartsWith("Flame", StringComparison.Ordinal);
            importer.alphaTestReferenceValue = .40f;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 512;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(name.EndsWith("Fox", StringComparison.Ordinal) ? .68f : .5f, .03f);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);

            var mobile = importer.GetPlatformTextureSettings("iPhone");
            mobile.overridden = true;
            mobile.maxTextureSize = 512;
            mobile.format = TextureImporterFormat.ASTC_6x6;
            importer.SetPlatformTextureSettings(mobile);
            importer.SaveAndReimport();
        }

        private static Material Material(string name, Shader shader)
        {
            var path = Materials + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Sprite LoadSprite(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(Sprites + name + ".png")
            ?? throw new InvalidOperationException("Не найден игровой спрайт: " + name);

        private static void BuildFlame(int level, Material material, float shrineHeight, float foxHeight)
        {
            var name = "Flame" + level;
            var root = new GameObject(name);
            try
            {
                var billboard = Child(root.transform, "Billboard", Vector3.up * .062f);
                var shrine = Draw(billboard, "Shrine", LoadSprite(name + "Shrine"), material);
                var shrineScale = shrineHeight / shrine.sprite.bounds.size.y;
                shrine.transform.localScale = Vector3.one * shrineScale;

                // Плоскость лиса чуть ближе к камере, чтобы лапы лежали поверх конька.
                var perch = Child(billboard, "Fox pose", new Vector3(0f, shrineHeight * .87f, -.012f));
                var fox = Draw(perch, "Fox", LoadSprite(name + "Fox"), material);
                fox.transform.localScale = Vector3.one * (foxHeight / fox.sprite.bounds.size.y);
                var muzzle = Child(perch, "Muzzle", new Vector3(fox.sprite.bounds.max.x * fox.transform.localScale.x - .012f,
                    foxHeight * .75f, -.04f));

                var view = new SerializedObject(root.AddComponent<InkFlameView>());
                view.FindProperty("billboard").objectReferenceValue = billboard;
                view.FindProperty("fox").objectReferenceValue = perch;
                view.FindProperty("muzzle").objectReferenceValue = muzzle;
                view.ApplyModifiedPropertiesWithoutUndo();
                Save(root, name);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void BuildEnemy(string name, string legacyPath, Material material, Material ghost,
            float height, float hover, bool warden)
        {
            // Существующий root сохраняет HP и статусы вместе со всеми ссылками.
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(legacyPath));
            root.name = name;
            try
            {
                var model = root.GetComponentInChildren<EnemyModelView>();
                Object.DestroyImmediate(model.Model.gameObject);
                var billboard = Child(model.transform, "Billboard", Vector3.up * hover);
                var ink = billboard.gameObject.AddComponent<InkEnemyView>();
                var first = LoadSprite(name + "Stride0");
                var body = Draw(billboard, "Paper pose", first, material);
                var scale = height / first.bounds.size.y;
                billboard.localScale = Vector3.one * scale;
                var cue = Draw(body.transform, "Occlusion", first, ghost);

                var inkData = new SerializedObject(ink);
                inkData.FindProperty("body").objectReferenceValue = body;
                inkData.FindProperty("occluded").objectReferenceValue = cue;
                inkData.FindProperty("warden").boolValue = warden;
                var stride = inkData.FindProperty("stride");
                stride.arraySize = 4;
                stride.GetArrayElementAtIndex(0).objectReferenceValue = first;
                stride.GetArrayElementAtIndex(1).objectReferenceValue = LoadSprite(name + "Stride1");
                stride.GetArrayElementAtIndex(2).objectReferenceValue = first;
                stride.GetArrayElementAtIndex(3).objectReferenceValue = LoadSprite(name + "Stride2");
                inkData.ApplyModifiedPropertiesWithoutUndo();

                var view = new SerializedObject(model);
                view.FindProperty("model").objectReferenceValue = billboard;
                view.FindProperty("ink").objectReferenceValue = ink;
                foreach (var clip in new[] { "idle", "move", "hit", "death", "leak" })
                    view.FindProperty(clip).objectReferenceValue = null;
                view.ApplyModifiedPropertiesWithoutUndo();
                Save(root, name);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static Transform Child(Transform parent, string name, Vector3 position)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = position;
            return child;
        }

        private static SpriteRenderer Draw(Transform parent, string name, Sprite sprite, Material material)
        {
            var renderer = Child(parent, name, Vector3.zero).gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            renderer.sharedMaterial = material;
            return renderer;
        }

        private static void Save(GameObject root, string name)
        {
            var path = Prefabs + name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            EnemyModelBuilder.NormalizeSerializedWhitespace(path);
        }
    }
}
