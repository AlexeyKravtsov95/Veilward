using UnityEngine;
using UnityEngine.Rendering;
using Veilward.Domain;

namespace Veilward.Presentation
{
    public enum HybridShotKind { None, Steam, Storm }

    public sealed class HybridProjectilePool
    {
        public const int Capacity = 8;
        private readonly HybridStrikeView[] _views = new HybridStrikeView[Capacity];
        private long _sequence;

        public HybridProjectilePool(Transform parent, Material material)
        {
            for (var i = 0; i < Capacity; i++)
                _views[i] = new HybridStrikeView(parent, material, i);
        }

        public HybridStrikeView GetView(int index) => _views[index];

        public static HybridShotKind KindFor(TowerDefinitionId definition)
        {
            switch (definition.Value)
            {
                case "steam": return HybridShotKind.Steam;
                case "storm": return HybridShotKind.Storm;
                default: return HybridShotKind.None;
            }
        }

        public void Play(HybridShotKind kind, AttackEvent attack, int level, Vector3 source)
        {
            if (kind == HybridShotKind.None) return;
            // Сначала свободный slot, затем самый ранний запуск среди активных.
            var slot = 0;
            for (var i = 0; i < Capacity; i++)
            {
                if (_views[i].Remaining <= 0) { slot = i; break; }
                if (_views[i].Order < _views[slot].Order) slot = i;
            }
            _views[slot].Play(kind, attack, level, source, ++_sequence);
        }

        public void Advance(float delta)
        {
            foreach (var view in _views) view.Advance(delta);
        }

        public void Clear()
        {
            _sequence = 0;
            foreach (var view in _views) view.Clear();
        }
    }

    public sealed class HybridStrikeView
    {
        public const int RendererCount = 4;
        private readonly LineRenderer[] _lines = new LineRenderer[RendererCount];
        private readonly MaterialPropertyBlock _color = new MaterialPropertyBlock();
        private Vector3 _forward;
        private Vector3 _side;
        private float _age;

        public HybridShotKind Kind { get; private set; }
        public AttackEvent Event { get; private set; }
        public int Level { get; private set; }
        public Vector3 Source { get; private set; }
        public Vector3 Target { get; private set; }
        public Vector3 HeadPosition { get; private set; }
        public float Remaining { get; private set; }
        public float FlightDuration => Kind == HybridShotKind.Steam ? .22f : .14f;
        private float ImpactDuration => Kind == HybridShotKind.Steam ? .18f : .16f;
        public bool IsFlying => Remaining > 0 && _age < FlightDuration;
        internal long Order { get; private set; }

        internal HybridStrikeView(Transform parent, Material material, int index)
        {
            var root = new GameObject("Hybrid Projectile Pool " + index).transform;
            root.SetParent(parent, false);
            for (var i = 0; i < RendererCount; i++)
                _lines[i] = HybridEffectGeometry.CreateLine(root, material, "Hybrid stroke " + i);
        }

        internal void Play(HybridShotKind kind, AttackEvent attack, int level, Vector3 source, long order)
        {
            Clear();
            Kind = kind;
            Event = attack;
            Level = level;
            Source = source;
            Target = new Vector3(attack.TargetPosition.X, .60f, attack.TargetPosition.Y);
            _forward = (Target - Source).normalized;
            _side = HybridEffectGeometry.Side(_forward);
            Order = order;
            Remaining = FlightDuration + ImpactDuration;
            Draw();
        }

        internal void Advance(float delta)
        {
            if (Remaining <= 0 || delta <= 0) return;
            _age += delta;
            Remaining = FlightDuration + ImpactDuration - _age;
            if (Remaining <= 0) Clear();
            else Draw();
        }

        private void Draw()
        {
            var travel = Mathf.Clamp01(_age / FlightDuration);
            var impact = Mathf.Clamp01((_age - FlightDuration) / ImpactDuration);
            HeadPosition = Vector3.Lerp(Source, Target, travel);
            if (Kind == HybridShotKind.Steam) DrawSteam(travel, impact);
            else DrawStorm(travel, impact);
        }

        private void DrawSteam(float travel, float impact)
        {
            HeadPosition += Vector3.up * (.12f * Mathf.Sin(Mathf.PI * travel));
            var fade = 1 - impact;
            for (var i = 0; i < 3; i++)
            {
                var spread = i - 1;
                var center = HeadPosition - _forward * (.07f * i * fade)
                    + _side * (spread * (.12f + .25f * impact))
                    + Vector3.up * (.08f * i + .20f * impact);
                var tint = i == 1 ? new Color(.96f, .99f, 1f) : new Color(.52f, .77f, .81f);
                HybridEffectGeometry.Style(_lines[i], 12, (.052f - i * .006f) * fade, tint, _color);
                HybridEffectGeometry.Curl(_lines[i], center, _side, Vector3.up, .14f + .09f * travel + .10f * impact,
                    i * 2.2f + _age * 8, 4.1f);
            }

            if (IsFlying && travel < .4f)
            {
                HybridEffectGeometry.Style(_lines[3], 10, .048f * (1 - travel / .4f), new Color(.75f, .94f, .95f), _color);
                HybridEffectGeometry.Curl(_lines[3], Source, _side, Vector3.up, .14f + .24f * travel, -.7f, 4.9f);
            }
            else HybridEffectGeometry.Clear(_lines[3]);
        }

        private void DrawStorm(float travel, float impact)
        {
            var blue = EnemyStatusView.StormColor;
            var white = new Color(.82f, .96f, 1);
            var fade = 1 - impact;
            if (IsFlying)
            {
                var tail = travel == 0 ? Source - _forward * .13f
                    : Vector3.MoveTowards(HeadPosition, Source - _forward * .13f, .82f);
                HybridEffectGeometry.Style(_lines[0], 9, .071f, blue, _color);
                HybridEffectGeometry.Bolt(_lines[0], tail, HeadPosition, _age * 95, .19f);
                HybridEffectGeometry.Style(_lines[1], 5, .039f, white, _color);
                HybridEffectGeometry.Bolt(_lines[1], Vector3.Lerp(tail, HeadPosition, .45f),
                    HeadPosition + _side * .22f + Vector3.up * .09f, _age * 91 + 1, .09f);
                HybridEffectGeometry.Style(_lines[2], 5, .026f, blue, _color);
                HybridEffectGeometry.Bolt(_lines[2], Vector3.Lerp(tail, HeadPosition, .30f),
                    HeadPosition - _side * .24f, _age * 99 + 3, .11f);
                HybridEffectGeometry.Clear(_lines[3]);
                return;
            }

            for (var i = 0; i < RendererCount; i++)
            {
                var angle = i * Mathf.PI / 2 + .5f;
                // Искры расходятся вокруг границ силуэта в плоскости поля.
                var direction = Vector3.right * Mathf.Cos(angle) + Vector3.forward * Mathf.Sin(angle);
                var from = Target + direction * (.23f + .17f * impact) + Vector3.up * .06f;
                var to = from + direction * (.36f - .14f * impact) + Vector3.up * (.16f + .11f * impact);
                HybridEffectGeometry.Style(_lines[i], 5, .072f * fade, i % 2 == 0 ? white : blue, _color);
                HybridEffectGeometry.Bolt(_lines[i], from, to, i * 2, .09f);
            }
        }

        internal void Clear()
        {
            Kind = HybridShotKind.None;
            Event = default;
            Level = 0;
            Order = 0;
            Source = Target = HeadPosition = _forward = _side = default;
            _age = Remaining = 0;
            _color.Clear();
            foreach (var line in _lines) HybridEffectGeometry.Clear(line);
        }
    }

    internal static class HybridEffectGeometry
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        internal static LineRenderer CreateLine(Transform parent, Material material, string label)
        {
            var line = new GameObject(label).AddComponent<LineRenderer>();
            line.transform.SetParent(parent, false);
            line.useWorldSpace = true;
            line.sharedMaterial = material;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            Clear(line);
            return line;
        }

        internal static void Style(LineRenderer line, int count, float width, Color tint, MaterialPropertyBlock color)
        {
            line.positionCount = count;
            line.startWidth = width;
            line.endWidth = width * .52f;
            color.SetColor(BaseColor, tint);
            color.SetColor(ColorId, tint);
            line.SetPropertyBlock(color);
            line.enabled = true;
        }

        internal static Vector3 Side(Vector3 direction)
        {
            var side = Vector3.Cross(direction, Vector3.up).normalized;
            return side.sqrMagnitude > 0 ? side : Vector3.right;
        }

        internal static void Bolt(LineRenderer line, Vector3 from, Vector3 to, float phase, float amplitude)
        {
            var side = Side(to - from);
            var last = line.positionCount - 1;
            line.SetPosition(0, from);
            for (var i = 1; i < last; i++)
            {
                var t = i / (float)last;
                var jitter = Mathf.Sin(i * 7.1f + phase) * amplitude * Mathf.Sin(t * Mathf.PI);
                line.SetPosition(i, Vector3.Lerp(from, to, t) + side * jitter + Vector3.up * (.38f * jitter));
            }
            line.SetPosition(last, to);
        }

        internal static void Curl(LineRenderer line, Vector3 center, Vector3 right, Vector3 up, float radius, float angle, float sweep)
        {
            var last = line.positionCount - 1;
            for (var i = 0; i <= last; i++)
            {
                var t = i / (float)last;
                var a = angle + sweep * t;
                var r = radius * Mathf.Lerp(.53f, 1, t);
                line.SetPosition(i, center + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * r);
            }
        }

        internal static void Clear(LineRenderer line)
        {
            line.enabled = false;
            line.positionCount = 0;
            line.startWidth = line.endWidth = 0;
            line.SetPropertyBlock(null);
        }
    }
}
