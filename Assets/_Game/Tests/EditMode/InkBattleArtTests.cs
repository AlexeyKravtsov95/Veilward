using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Veilward.Editor;
using Veilward.Presentation;

namespace Veilward.Tests.EditMode
{
    public sealed class InkBattleArtTests
    {
        [Test]
        public void Ar0a01_SavedBattleReferencesThreeDifferentLayeredFlamePrefabs()
        {
            EditorSceneManager.OpenScene("Assets/_Game/Scenes/Battle.unity");
            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            var refs = new SerializedObject(battle).FindProperty("inkFlameLevels");
            Assert.That(refs.arraySize, Is.EqualTo(3));
            for (var i = 0; i < refs.arraySize; i++)
            {
                var view = (InkFlameView)refs.GetArrayElementAtIndex(i).objectReferenceValue;
                Assert.That(AssetDatabase.GetAssetPath(view), Is.EqualTo(InkBattleArtBuilder.Prefabs + $"Flame{i + 1}.prefab"));
                var layers = view.GetComponentsInChildren<SpriteRenderer>();
                Assert.That(layers.Length, Is.EqualTo(2));
                Assert.That(layers.Select(layer => layer.sprite.name),
                    Is.EquivalentTo(new[] { $"Flame{i + 1}Shrine", $"Flame{i + 1}Fox" }));
                Assert.That(layers.Select(layer => layer.sharedMaterial).Distinct().Count(), Is.EqualTo(1));
                Assert.That(layers[0].sharedMaterial.GetFloat("_DepthWrite"), Is.EqualTo(1f));
                Assert.That(layers[0].sharedMaterial.GetFloat("_StencilWriteMask"), Is.EqualTo(64f));
                Assert.That(view.Muzzle.parent, Is.SameAs(layers.Single(layer => layer.name == "Fox").transform.parent));
                Assert.That(view.Muzzle.position.magnitude, Is.LessThan(2f));
            }
        }

        [TestCase("Shade")]
        [TestCase("Warden")]
        public void Ar0a01_SpiritsShareDepthContractAndHaveThreeRegisteredStrideDrawings(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(InkBattleArtBuilder.Prefabs + name + ".prefab");
            Assert.That(prefab.GetComponents<EnemyStatusView>().Length, Is.EqualTo(name == "Shade" ? 1 : 0));
            Assert.That(prefab.GetComponent<EnemyHealthBarView>(), Is.Not.Null);
            var legacy = AssetDatabase.LoadAssetAtPath<GameObject>(name == "Shade"
                ? EnemyModelBuilder.ShadePath : EnemyModelBuilder.BossPath);
            Assert.That(prefab.GetComponentsInChildren<Collider>().Length,
                Is.EqualTo(legacy.GetComponentsInChildren<Collider>().Length));
            Assert.That(prefab.GetComponent<Renderer>().enabled, Is.False);

            var ink = prefab.GetComponentInChildren<InkEnemyView>();
            Assert.That(prefab.GetComponentInChildren<EnemyModelView>().Model, Is.SameAs(ink.transform));
            var layers = ink.GetComponentsInChildren<SpriteRenderer>();
            Assert.That(layers.Length, Is.EqualTo(2));
            Assert.That(layers[0].sprite, Is.SameAs(layers[1].sprite));
            Assert.That(layers[0].sharedMaterial.GetFloat("_DepthWrite"), Is.Zero);
            Assert.That(layers[1].sharedMaterial.GetFloat("_DepthTest"), Is.EqualTo((float)CompareFunction.Greater));
            Assert.That(layers[1].sharedMaterial.GetFloat("_StencilCompare"), Is.EqualTo((float)CompareFunction.Equal));

            var sprites = Enumerable.Range(0, 3)
                .Select(i => AssetDatabase.LoadAssetAtPath<Sprite>(InkBattleArtBuilder.Sprites + $"{name}Stride{i}.png")).ToArray();
            Assert.That(sprites.All(sprite => sprite != null), Is.True);
            Assert.That(sprites.Select(sprite => sprite.rect.size).Distinct().Count(), Is.EqualTo(1));
            Assert.That(sprites.Select(sprite => sprite.pivot).Distinct().Count(), Is.EqualTo(1));
        }

        [Test]
        public void Ar0a01_ImportRetainsAlphaPivotAndMobileSettings()
        {
            var paths = AssetDatabase.FindAssets("t:Sprite", new[] { InkBattleArtBuilder.Sprites.TrimEnd('/') })
                .Select(AssetDatabase.GUIDToAssetPath).ToArray();
            Assert.That(paths.Length, Is.EqualTo(12));
            foreach (var path in paths)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Assert.That(importer.DoesSourceTextureHaveAlpha(), Is.True, path);
                Assert.That(importer.alphaIsTransparency, Is.True, path);
                Assert.That(importer.isReadable, Is.False, path);
                Assert.That(sprite.texture.mipmapCount, Is.GreaterThan(1), path);
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Trilinear), path);
                Assert.That(sprite.pivot.y / sprite.rect.height, Is.EqualTo(.03f).Within(.0001f), path);
                Assert.That(sprite.rect.size.x, Is.LessThanOrEqualTo(512f), path);
                Assert.That(sprite.rect.size.y, Is.LessThanOrEqualTo(512f), path);
                Assert.That(importer.GetPlatformTextureSettings("iPhone").format, Is.EqualTo(TextureImporterFormat.ASTC_6x6), path);
            }
        }
    }
}
