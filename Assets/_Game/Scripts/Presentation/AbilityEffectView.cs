using UnityEngine;
using Veilward.Domain;

namespace Veilward.Presentation
{
    // Ограниченный pool переиспользует слоты; каждый impact берёт captured position даже после lethal removal.
    public sealed class AbilityEffectView : MonoBehaviour
    {
        private LineRenderer _line;
        private MaterialPropertyBlock _color;
        public float RemainingSeconds { get; private set; }
        public PathPosition CapturedPosition { get; private set; }
        public long CastSequence { get; private set; }
        public int HitIndex { get; private set; }
        public void Initialize(Material material)
        {
            _line = gameObject.AddComponent<LineRenderer>(); _line.sharedMaterial = material;
            _line.useWorldSpace = true; _line.startWidth = _line.endWidth = .045f;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; _line.receiveShadows = false;
            var color = new Color(1, .7f, .2f); _color = new MaterialPropertyBlock();
            _color.SetColor("_BaseColor", color); _line.SetPropertyBlock(_color);
            _line.startColor = _line.endColor = color; Clear();
        }
        public void Play(PathPosition point, float radius, long sequence, int hitIndex)
        {
            CapturedPosition = point; CastSequence = sequence; HitIndex = hitIndex;
            RemainingSeconds = .5f; _line.loop = true; _line.positionCount = 32;
            for (var i = 0; i < 32; i++)
            {
                var a = i * Mathf.PI * 2 / 32;
                _line.SetPosition(i, new Vector3(point.X + Mathf.Cos(a) * radius, .22f, point.Y + Mathf.Sin(a) * radius));
            }
            gameObject.SetActive(true);
        }
        public void Advance(float seconds)
        {
            RemainingSeconds = Mathf.Max(0, RemainingSeconds - seconds); if (RemainingSeconds == 0) Clear();
        }
        public void Clear() { RemainingSeconds = 0; gameObject.SetActive(false); }
    }
}
