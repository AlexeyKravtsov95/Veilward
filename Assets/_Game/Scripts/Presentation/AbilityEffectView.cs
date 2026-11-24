using UnityEngine;
using Veilward.Domain;

namespace Veilward.Presentation
{
    public sealed class AbilityEffectView : MonoBehaviour
    {
        internal const int CastCapacity = 4;
        internal const int HitCapacity = 28;
        private const float CastLifetime = .50f;
        private const float HitLifetime = .34f;
        private readonly MaterialPropertyBlock _color = new MaterialPropertyBlock();
        private LineRenderer _outline;
        private LineRenderer _mark;
        private float _lifetime;

        public float RemainingSeconds { get; private set; }
        public AbilityCastEvent Cast { get; private set; }
        public DamageResolvedEvent? Hit { get; private set; }
        public PathPosition CapturedPosition => Hit?.TargetPosition ?? Cast.Center;
        public long CastSequence => Cast.CastSequence;
        public int HitIndex => Hit?.HitIndex ?? -1;

        internal void Initialize(Material material, bool castSlot)
        {
            if (castSlot) _outline = HybridEffectGeometry.CreateLine(transform, material, "Seal boundary");
            _mark = HybridEffectGeometry.CreateLine(transform, material, castSlot ? "Seal mark" : "Seal hit spark");
            Clear();
        }

        internal void Play(AbilityCastEvent cast, DamageResolvedEvent? hit = null)
        {
            Clear();
            Cast = cast;
            Hit = hit;
            RemainingSeconds = _lifetime = hit.HasValue ? HitLifetime : CastLifetime;
            Draw();
            gameObject.SetActive(true);
        }

        internal void Advance(float seconds)
        {
            if (RemainingSeconds <= 0 || seconds <= 0) return;
            RemainingSeconds -= seconds;
            if (RemainingSeconds <= 0) Clear();
            else Draw();
        }

        private void Draw()
        {
            var progress = 1 - RemainingSeconds / _lifetime;
            var fade = 1 - progress * .90f;
            var point = CapturedPosition;
            if (Hit.HasValue)
            {
                var center = new Vector3(point.X, .36f + .28f * progress, point.Y);
                var size = .26f + .15f * progress;
                var color = Hit.Value.WasLethal ? new Color(1f, .77f, .43f) : new Color(1f, .91f, .64f);
                HybridEffectGeometry.Style(_mark, 5, .083f * fade, color, _color);
                _mark.SetPosition(0, center + new Vector3(-size, .19f, 0f));
                _mark.SetPosition(1, center + new Vector3(-.08f, .02f, 0f));
                _mark.SetPosition(2, center + new Vector3(0f, .45f, 0f));
                _mark.SetPosition(3, center + new Vector3(.08f, .02f, 0f));
                _mark.SetPosition(4, center + new Vector3(size, .19f, 0f));
                return;
            }

            var ground = new Vector3(point.X, .24f, point.Y);
            AbilitySealGeometry.Boundary(_outline, ground, Cast.Radius, .079f * fade, AbilitySealGeometry.Amber, _color);
            AbilitySealGeometry.Mark(_mark, ground, Cast.Radius * (.29f + .34f * progress), .082f * fade,
                new Color(1f, .87f, .54f), _color, true);
        }

        internal void Clear()
        {
            RemainingSeconds = _lifetime = 0;
            Cast = default;
            Hit = null;
            _color.Clear();
            if (_outline != null) HybridEffectGeometry.Clear(_outline);
            if (_mark != null) HybridEffectGeometry.Clear(_mark);
            gameObject.SetActive(false);
        }
    }

    internal static class AbilitySealGeometry
    {
        internal static readonly Color Amber = new Color(1f, .69f, .28f);

        internal static void Boundary(LineRenderer line, Vector3 center, float radius, float width,
            Color tint, MaterialPropertyBlock color)
        {
            const int sides = 32;
            HybridEffectGeometry.Style(line, sides, width, tint, color);
            line.endWidth = width;
            line.loop = true;
            for (var i = 0; i < sides; i++)
            {
                var angle = i * (2 * Mathf.PI / sides);
                line.SetPosition(i, center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius);
            }
        }

        internal static void Mark(LineRenderer line, Vector3 center, float size, float width,
            Color tint, MaterialPropertyBlock color, bool valid)
        {
            HybridEffectGeometry.Style(line, valid ? 4 : 5, width, tint, color);
            line.endWidth = width;
            line.loop = valid;
            if (valid)
            {
                line.SetPosition(0, center + Vector3.forward * size);
                line.SetPosition(1, center + Vector3.right * size);
                line.SetPosition(2, center + Vector3.back * size);
                line.SetPosition(3, center + Vector3.left * size);
                return;
            }

            var diagonal = new Vector3(size, 0, size);
            var opposite = new Vector3(-size, 0, size);
            line.SetPosition(0, center + diagonal);
            line.SetPosition(1, center - diagonal);
            line.SetPosition(2, center);
            line.SetPosition(3, center + opposite);
            line.SetPosition(4, center - opposite);
        }
    }
}
