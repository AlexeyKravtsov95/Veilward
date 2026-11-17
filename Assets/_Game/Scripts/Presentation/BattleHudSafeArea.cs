using UnityEngine;

namespace Veilward.Presentation
{
    public sealed class BattleHudSafeArea : MonoBehaviour
    {
        [SerializeField] private RectTransform[] edgeControls;
        private Vector2[] _origins;
        private Canvas _canvas;
        private Rect _lastArea;
        private Vector2 _lastSize;
        private float _lastScale;
        private void Start() => Initialize();
        private void Initialize()
        {
            if (_origins != null) return;
            _canvas = GetComponentInParent<Canvas>();
            _origins = new Vector2[edgeControls.Length];
            for (var i = 0; i < edgeControls.Length; i++) _origins[i] = edgeControls[i].anchoredPosition;
        }
        private void Update()
        {
            Initialize();
            var size = new Vector2(Screen.width, Screen.height);
            var scale = _canvas != null ? _canvas.scaleFactor : 1;
            if (Screen.safeArea == _lastArea && size == _lastSize && scale == _lastScale) return;
            ApplySafeArea(Screen.safeArea, size, scale);
            _lastArea = Screen.safeArea; _lastSize = size; _lastScale = scale;
        }
        private void ApplySafeArea(Rect area, Vector2 size, float scale)
        {
            Initialize();
            if (size.x <= 0 || size.y <= 0 || area.width <= 0 || area.height <= 0 || scale <= 0) return;
            for (var i = 0; i < edgeControls.Length; i++)
            {
                var rect = edgeControls[i]; var anchor = rect.anchorMin;
                var x = anchor.x <= 0 ? area.xMin : anchor.x >= 1 ? -(size.x - area.xMax) : area.center.x - size.x * .5f;
                var y = anchor.y <= 0 ? area.yMin : anchor.y >= 1 ? -(size.y - area.yMax) : area.center.y - size.y * .5f;
                rect.anchoredPosition = _origins[i] + new Vector2(x, y) / scale;
            }
        }
    }
}
