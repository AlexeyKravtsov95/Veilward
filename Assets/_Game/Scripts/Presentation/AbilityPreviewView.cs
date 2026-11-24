using UnityEngine;
using Veilward.Domain;

namespace Veilward.Presentation
{
    [RequireComponent(typeof(LineRenderer))]
    public sealed class AbilityPreviewView : MonoBehaviour
    {
        private LineRenderer _line;
        private LineRenderer _mark;
        private MaterialPropertyBlock _color;

        internal void Initialize()
        {
            if (_line != null) return;
            _color = new MaterialPropertyBlock();
            _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _mark = HybridEffectGeometry.CreateLine(transform, _line.sharedMaterial, "Preview mark");
            Clear();
        }

        public void Show(PathPosition center, float radius, bool valid)
        {
            Initialize();
            if (float.IsNaN(center.X) || float.IsInfinity(center.X) || float.IsNaN(center.Y) || float.IsInfinity(center.Y))
            {
                Clear();
                return;
            }

            var tint = valid ? AbilitySealGeometry.Amber : Color.red;
            var ground = new Vector3(center.X, .20f, center.Y);
            AbilitySealGeometry.Boundary(_line, ground, radius, .052f, tint, _color);
            AbilitySealGeometry.Mark(_mark, ground, radius * .27f, .048f, tint, _color, valid);
            gameObject.SetActive(true);
        }

        public void Clear()
        {
            if (_line != null) HybridEffectGeometry.Clear(_line);
            if (_mark != null) HybridEffectGeometry.Clear(_mark);
            _color?.Clear();
            gameObject.SetActive(false);
        }
    }
}
