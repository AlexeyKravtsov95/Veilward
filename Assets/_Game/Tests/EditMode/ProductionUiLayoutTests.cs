using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Veilward.Presentation;

namespace Veilward.Tests.EditMode
{
    public sealed class ProductionUiLayoutTests
    {
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
                var sliders = scaler.GetComponentsInChildren<Slider>(true);
                Assert.That(sliders.Length, Is.EqualTo(3));
                foreach (var slider in sliders)
                {
                    var size = ((RectTransform)slider.transform).sizeDelta * scale / dpr;
                    Assert.That(size.x, Is.GreaterThanOrEqualTo(44), slider.name);
                    Assert.That(size.y, Is.GreaterThanOrEqualTo(44), slider.name);
                    Assert.That(slider.targetGraphic.raycastTarget, Is.True, slider.name);
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

        private static readonly System.Collections.Generic.IEqualityComparer<Vector2> Vector2Comparer =
            new UnityEngine.TestTools.Utils.Vector2EqualityComparer(0.0001f);
    }
}
