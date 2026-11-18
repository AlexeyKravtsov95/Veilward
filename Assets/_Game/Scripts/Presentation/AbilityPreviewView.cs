using UnityEngine;
using Veilward.Domain;

namespace Veilward.Presentation
{
    [RequireComponent(typeof(LineRenderer))]
    public sealed class AbilityPreviewView : MonoBehaviour
    {
        private LineRenderer _line;
        private MaterialPropertyBlock _color;
        public void Show(PathPosition center, float radius, bool valid)
        {
            if (_line == null) _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = true; _line.loop = true; _line.positionCount = 64;
            var color = valid ? new Color(1, .7f, .2f) : Color.red;
            if (_color == null) _color = new MaterialPropertyBlock();
            _color.SetColor("_BaseColor", color); _line.SetPropertyBlock(_color);
            _line.startColor = _line.endColor = color;
            for (var i = 0; i < 64; i++)
            {
                var angle = i * Mathf.PI * 2 / 64;
                _line.SetPosition(i, new Vector3(center.X + radius * Mathf.Cos(angle), .16f, center.Y + radius * Mathf.Sin(angle)));
            }
            gameObject.SetActive(true);
        }
        public void Clear() => gameObject.SetActive(false);
    }
}
