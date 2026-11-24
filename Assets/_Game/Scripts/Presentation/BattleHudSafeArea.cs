using UnityEngine;

namespace Veilward.Presentation
{
    [DefaultExecutionOrder(-100)]
    public sealed class BattleHudSafeArea : MonoBehaviour
    {
        [SerializeField] private RectTransform[] safeRoots;
        public Rect AppliedArea { get; private set; }
        public Vector2 AppliedScreenSize { get; private set; }

        private void OnEnable() => Refresh();

        private void Update() => Refresh();

        private void Refresh()
        {
            var size = new Vector2(Screen.width, Screen.height);
            var area = Screen.safeArea;
            if (area == AppliedArea && size == AppliedScreenSize) return;
            ApplySafeArea(area, size, 1);
        }

        private void ApplySafeArea(Rect area, Vector2 size, float scale)
        {
            if (size.x <= 0 || size.y <= 0 || area.width <= 0 || area.height <= 0 || scale <= 0 || safeRoots == null) return;
            AppliedArea = area;
            AppliedScreenSize = size;
            var min = new Vector2(area.xMin / size.x, area.yMin / size.y);
            var max = new Vector2(area.xMax / size.x, area.yMax / size.y);
            foreach (var root in safeRoots)
            {
                root.anchorMin = min;
                root.anchorMax = max;
                root.offsetMin = root.offsetMax = Vector2.zero;
            }
        }
    }
}
