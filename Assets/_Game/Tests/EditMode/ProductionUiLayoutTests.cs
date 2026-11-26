using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Sprites;
using Veilward.Presentation;
using Object = UnityEngine.Object;

namespace Veilward.Tests.EditMode
{
    public sealed class ProductionUiLayoutTests
    {
        [TestCase("MainMenu")]
        [TestCase("Battle")]
        public void UiRework101_AllProductionGlyphsHaveDistinctAuthoredSprites(string scene)
        {
            var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            try
            {
                EditorSceneManager.OpenScene("Assets/_Game/Scenes/" + scene + ".unity");
                var glyphs = Object.FindObjectsByType<VeilwardUiGlyph>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                Assert.That(glyphs, Is.Not.Empty);
                foreach (var glyph in glyphs)
                {
                    using var serialized = new SerializedObject(glyph);
                    var artwork = serialized.FindProperty("artwork");
                    Assert.That(artwork, Is.Not.Null, $"{glyph.name} needs an authored sprite mapping.");
                    Assert.That(artwork.arraySize, Is.EqualTo(Enum.GetValues(typeof(VeilwardGlyph)).Length));
                    var sprites = Enumerable.Range(0, artwork.arraySize)
                        .Select(index => artwork.GetArrayElementAtIndex(index).objectReferenceValue as Sprite).ToArray();
                    Assert.That(sprites, Is.All.Not.Null);
                    Assert.That(sprites.Distinct().Count(), Is.EqualTo(sprites.Length));
                    Assert.That(sprites.Select(sprite => sprite.texture).Distinct().Count(), Is.EqualTo(1));
                    Assert.That(glyph.raycastTarget, Is.False);
                    var original = glyph.Glyph;
                    AssertGlyph(glyph, sprites[(int)original]);
                    glyph.Glyph = VeilwardGlyph.Lock;
                    AssertGlyph(glyph, sprites[(int)VeilwardGlyph.Lock]);
                    glyph.Glyph = original;
                    AssertGlyph(glyph, sprites[(int)original]);
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(previous))
                    EditorSceneManager.OpenScene(previous);
            }
        }

        [TestCase("MainMenu", 1136, 640, 2, 0, 0)]
        [TestCase("MainMenu", 2556, 1179, 3, 132, 63)]
        [TestCase("MainMenu", 2048, 1536, 2, 0, 40)]
        [TestCase("Battle", 1136, 640, 2, 0, 0)]
        [TestCase("Battle", 2556, 1179, 3, 132, 63)]
        [TestCase("Battle", 2048, 1536, 2, 0, 40)]
        public void Ui104_SavedControlsMeetTouchFloor(string scene, int width, int height, int dpr, int side, int bottom)
        {
            var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            try
            {
                EditorSceneManager.OpenScene("Assets/_Game/Scenes/" + scene + ".unity");
                var scaler = Object.FindFirstObjectByType<CanvasScaler>();
                var safe = scaler.GetComponent<BattleHudSafeArea>();
                Assert.That(safe, Is.Not.Null);
                Assert.That(scaler.screenMatchMode, Is.EqualTo(CanvasScaler.ScreenMatchMode.Expand));
                var scale = Mathf.Min(width / scaler.referenceResolution.x, height / scaler.referenceResolution.y);
                var area = new Rect(side, bottom, width - 2 * side, height - bottom);
                var buttons = scaler.GetComponentsInChildren<Button>(true);
                Assert.That(buttons.Length, Is.GreaterThanOrEqualTo(scene == "Battle" ? 20 : 3));
                foreach (var button in buttons)
                {
                    var size = ((RectTransform)button.transform).sizeDelta * scale / dpr;
                    Assert.That(size.x, Is.GreaterThanOrEqualTo(44), button.name);
                    Assert.That(size.y, Is.GreaterThanOrEqualTo(44), button.name);
                    Assert.That(button.targetGraphic.raycastTarget, Is.True, button.name);
                    Assert.That(button.transform.GetComponentInParent<Canvas>(), Is.EqualTo(scaler.GetComponent<Canvas>()));
                }
                var apply = typeof(BattleHudSafeArea).GetMethod("ApplySafeArea",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                apply.Invoke(safe, new object[] { area, new Vector2(width, height), scale });
                Assert.That(safe.AppliedArea, Is.EqualTo(area));
                var roots = scaler.GetComponentsInChildren<RectTransform>(true).Where(rect => rect.name.EndsWith(" Safe Area")).ToArray();
                Assert.That(roots.Length, Is.GreaterThan(0));
                foreach (var root in roots)
                {
                    Assert.That(root.anchorMin, Is.EqualTo(new Vector2((float)side / width, (float)bottom / height)));
                    Assert.That(root.anchorMax, Is.EqualTo(new Vector2(1f - (float)side / width, 1f)).Using(Vector2Comparer));
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(previous))
                    EditorSceneManager.OpenScene(previous);
            }
        }

        [TestCase("Stats Panel", false, 48)]
        [TestCase("Boss Panel", false, 54)]
        [TestCase("Ability Button", true, 110)]
        [TestCase("Placement Panel", false, 86)]
        public void UiRework109_ReviewSurfacesKeepCompactBattleFootprint(string name, bool width, int limit)
        {
            var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            try
            {
                EditorSceneManager.OpenScene("Assets/_Game/Scenes/Battle.unity");
                var root = (RectTransform)Object.FindFirstObjectByType<CanvasScaler>().transform;
                var rect = Find(root, name).rect;
                Assert.That(width ? rect.width : rect.height, Is.LessThanOrEqualTo(limit));
            }
            finally
            {
                if (!string.IsNullOrEmpty(previous))
                    EditorSceneManager.OpenScene(previous);
            }
        }

        [TestCase(1600, 900, 2, 0, 0)]
        [TestCase(1136, 640, 2, 0, 0)]
        [TestCase(2556, 1179, 3, 132, 63)]
        [TestCase(2048, 1536, 2, 0, 40)]
        public void UiRework106_ActiveLayoutFitsSafeAreaAndSeparatesTargets(int width, int height, int dpr, int side, int bottom)
        {
            var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            try
            {
                var area = new Rect(side, bottom, width - 2 * side, height - bottom);
                var screen = new Vector2(width, height);
                EditorSceneManager.OpenScene("Assets/_Game/Scenes/MainMenu.unity");
                var (menu, menuScale) = SimulateScreen(area, screen);
                VerifyTargets(menu, menuScale / dpr, "Continue Button", "New Game Button", "Status Text", "Retry Load Button");

                EditorSceneManager.OpenScene("Assets/_Game/Scenes/Battle.unity");
                var (battle, battleScale) = SimulateScreen(area, screen);
                var logicalScale = battleScale / dpr;
                var catalog = Find(battle, "Tower Catalog");
                var template = Find(catalog, "Catalog Button Template");
                var slots = Enumerable.Range(0, 6).Select(index =>
                {
                    var slot = Object.Instantiate(template, catalog);
                    slot.name = $"Test Catalog {index}";
                    return slot.name;
                }).ToArray();

                VerifyTargets(battle, logicalScale, "Stats Panel", "Phase Plate", "Start Wave Button", "Pause Button", "Save Warning");
                VerifyTargets(battle, logicalScale, "Stats Panel", "Phase Plate", "Next Wave Button", "Pause Button", "Boss Panel", "Save Warning");
                var phases = new[]
                {
                    new[] { "Build Stats", "Confirm Button", "Cancel Button" }.Concat(slots).ToArray(),
                    new[] { "Tower Stats", "Close Tower Button", "Upgrade Button", "Sell Button" },
                    new[] { "Enemy Inspector Details", "Close Inspector Button", "Ability Button" },
                    new[] { "Ability Status", "Confirm Ability Button", "Cancel Ability Button" },
                    new[] { "Sell Confirmation Label", "Confirm Sell Button", "Cancel Sell Button" },
                    new[] { "Choice Title", "Choice Help", "flame", "tide", "gale", "stone", "Choice Menu Button", "Cancel Element Button", "Confirm Element Button" },
                    new[] { "Result Title", "Result Details", "Result Save Status", "Result Menu Button" },
                    new[] { "Paused Label", "Resume Button", "Main Menu Button", "Pause Save Status" },
                };
                foreach (var phase in phases) VerifyTargets(battle, logicalScale, phase);

                var drawer = Find(battle, "Placement Panel");
                drawer.anchorMin = drawer.anchorMax = drawer.pivot = new Vector2(.5f, 0);
                drawer.anchoredPosition = new Vector2(0, 8);
                VerifyTargets(battle, logicalScale, phases[0].Append("Save Warning").ToArray());

                drawer.anchorMin = drawer.anchorMax = drawer.pivot = new Vector2(.5f, 1);
                drawer.anchoredPosition = new Vector2(0, -78);
                var warning = Find(battle, "Save Warning");
                warning.anchorMin = warning.anchorMax = warning.pivot = Vector2.right;
                warning.anchoredPosition = new Vector2(-10, 8);
                VerifyTargets(battle, logicalScale, phases[0].Concat(new[]
                    { "Stats Panel", "Phase Plate", "Start Wave Button", "Pause Button", "Save Warning" }).ToArray());
                VerifyTargets(battle, logicalScale, "Save Warning Text", "Retry Save Button");
            }
            finally
            {
                if (!string.IsNullOrEmpty(previous))
                    EditorSceneManager.OpenScene(previous);
            }
        }

        private static void AssertGlyph(VeilwardUiGlyph glyph, Sprite sprite)
        {
            Assert.That(glyph.mainTexture, Is.EqualTo(sprite.texture));
            using var mesh = new VertexHelper();
            typeof(VeilwardUiGlyph).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(VertexHelper) }, null)
                .Invoke(glyph, new object[] { mesh });
            var vertex = default(UIVertex);
            mesh.PopulateUIVertex(ref vertex, 0);
            var rectangle = DataUtility.GetOuterUV(sprite);
            Assert.That((Vector2)vertex.uv0, Is.EqualTo(new Vector2(rectangle.x, rectangle.y)).Using(Vector2Comparer));
        }

        private static (RectTransform, float) SimulateScreen(Rect area, Vector2 screen)
        {
            var scaler = Object.FindFirstObjectByType<CanvasScaler>();
            var scale = Mathf.Min(screen.x / scaler.referenceResolution.x, screen.y / scaler.referenceResolution.y);
            var canvas = scaler.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var root = (RectTransform)canvas.transform;
            root.sizeDelta = screen / scale;
            typeof(BattleHudSafeArea).GetMethod("ApplySafeArea", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(canvas.GetComponent<BattleHudSafeArea>(), new object[] { area, screen, scale });
            return (root, scale);
        }

        private static RectTransform Find(RectTransform root, string name) =>
            root.GetComponentsInChildren<RectTransform>(true).Single(rect => rect.name == name);

        private static void VerifyTargets(RectTransform root, float logicalScale, params string[] names)
        {
            var targets = names.Select(name => Find(root, name)).ToArray();
            foreach (var target in targets)
                for (var current = target; current != root; current = (RectTransform)current.parent)
                    current.gameObject.SetActive(true);

            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            var bounds = new Rect[targets.Length];
            for (var i = 0; i < bounds.Length; i++)
            {
                var target = targets[i];
                Assert.That(target.gameObject.activeInHierarchy, Is.True, target.name);
                target.GetWorldCorners(corners);
                var min = (Vector2)root.InverseTransformPoint(corners[0]);
                var max = (Vector2)root.InverseTransformPoint(corners[2]);
                bounds[i] = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
                var safe = target;
                while (!safe.name.EndsWith(" Safe Area")) safe = (RectTransform)safe.parent;
                safe.GetWorldCorners(corners);
                var safeMin = (Vector2)root.InverseTransformPoint(corners[0]);
                var safeMax = (Vector2)root.InverseTransformPoint(corners[2]);
                Assert.That(min.x, Is.GreaterThanOrEqualTo(safeMin.x - .1f), target.name);
                Assert.That(min.y, Is.GreaterThanOrEqualTo(safeMin.y - .1f), target.name);
                Assert.That(max.x, Is.LessThanOrEqualTo(safeMax.x + .1f), target.name);
                Assert.That(max.y, Is.LessThanOrEqualTo(safeMax.y + .1f), target.name);
                if (target.TryGetComponent<Button>(out _))
                {
                    Assert.That(bounds[i].width * logicalScale, Is.GreaterThanOrEqualTo(44), target.name);
                    Assert.That(bounds[i].height * logicalScale, Is.GreaterThanOrEqualTo(44), target.name);
                }
                for (var j = 0; j < i; j++)
                    Assert.That(bounds[i].Overlaps(bounds[j]), Is.False, $"{target.name} overlaps {targets[j].name}");
            }
        }

        private static readonly System.Collections.Generic.IEqualityComparer<Vector2> Vector2Comparer =
            new UnityEngine.TestTools.Utils.Vector2EqualityComparer(0.0001f);
    }
}
