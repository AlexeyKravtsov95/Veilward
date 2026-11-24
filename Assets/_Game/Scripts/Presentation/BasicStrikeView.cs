using UnityEngine;
using UnityEngine.Rendering;

namespace Veilward.Presentation
{
    // Четыре prewarmed части на slot: формы и траектории различаются, damage callbacks нет.
    public sealed class BasicStrikeView
    {
        public const int RendererCount = 4;
        private readonly LineRenderer[] _parts = new LineRenderer[RendererCount];
        private readonly MaterialPropertyBlock _color = new MaterialPropertyBlock();
        private Vector3 _direction, _side;
        private float _age;
        public BasicShotKind Kind { get; private set; }
        public Vector3 Source { get; private set; }
        public Vector3 Target { get; private set; }
        public Vector3 HeadPosition { get; private set; }
        public float Remaining { get; private set; }
        public float FlightDuration => Kind == BasicShotKind.Tide ? .20f : Kind == BasicShotKind.Gale ? .14f : .24f;
        public float Lifetime => FlightDuration + (Kind == BasicShotKind.Gale ? .14f : .16f);
        public bool IsFlying => Remaining > 0 && _age + .000001f < FlightDuration;
        public bool ImpactVisible => _parts[2].enabled;

        public BasicStrikeView(Transform parent, Material material, int index)
        {
            var root = new GameObject("Basic Projectile Pool " + index).transform;
            root.SetParent(parent, false);
            for (var i = 0; i < RendererCount; i++)
            {
                var go = new GameObject(i < 2 ? "Basic flight " + i : "Basic impact " + i);
                go.transform.SetParent(root, false);
                var line = go.AddComponent<LineRenderer>();
                line.sharedMaterial = material; line.useWorldSpace = true;
                line.numCapVertices = 0; line.numCornerVertices = 0;
                line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
                _parts[i] = line;
            }
            Clear();
        }

        public void Play(BasicShotKind kind, Vector3 source, Vector3 target)
        {
            Clear(); Kind = kind; Source = source; Target = target;
            _direction = (target - source).normalized;
            if (_direction.sqrMagnitude < .01f) _direction = Vector3.forward;
            _side = Vector3.Cross(_direction, Vector3.up).normalized;
            if (_side.sqrMagnitude < .01f) _side = Vector3.right;
            var tint = kind == BasicShotKind.Tide ? new Color(.25f, .82f, 1) :
                kind == BasicShotKind.Gale ? new Color(.66f, 1, .78f) : new Color(.85f, .68f, 1);
            _color.SetColor("_BaseColor", tint); _color.SetColor("_Color", tint);
            foreach (var line in _parts) line.SetPropertyBlock(_color);
            _age = 0; Remaining = Lifetime; Draw();
        }

        public void Advance(float seconds)
        {
            if (Remaining <= 0 || seconds <= 0) return;
            _age += seconds; Remaining = Mathf.Max(0, Lifetime - _age);
            if (_age + .000001f >= Lifetime) Clear(); else Draw();
        }

        private void Draw()
        {
            var t = Mathf.Clamp01(_age / FlightDuration);
            var arc = Mathf.Sin(t * Mathf.PI);
            HeadPosition = Vector3.Lerp(Source, Target, t);
            if (Kind == BasicShotKind.Stone) HeadPosition += Vector3.up * (arc * .35f);
            if (Kind == BasicShotKind.Gale) HeadPosition += _side * (arc * .17f);
            if (!IsFlying) HeadPosition = Target;
            var fade = 1 - Mathf.Clamp01((_age - FlightDuration) / (Lifetime - FlightDuration));
            _parts[0].enabled = _parts[1].enabled = IsFlying;
            _parts[2].enabled = _parts[3].enabled = !IsFlying;
            if (Kind == BasicShotKind.Tide)
            {
                // Замкнутая капля с узким носом и круглым основанием, короткий загнутый след.
                var p = HeadPosition;
                Setup(0, 7, .055f);
                Point(0, 0, p + _direction * .23f); Point(0, 1, p + _side * .14f);
                Point(0, 2, p - _direction * .13f + _side * .13f); Point(0, 3, p - _direction * .20f);
                Point(0, 4, p - _direction * .13f - _side * .13f); Point(0, 5, p - _side * .14f);
                Point(0, 6, p + _direction * .23f);
                Setup(1, 3, .035f);
                Point(1, 0, p - _direction * .15f); Point(1, 1, p - _direction * .27f + _side * .07f);
                Point(1, 2, p - _direction * .35f + _side * .04f);
                Arc(2, Target, Vector3.right, Vector3.forward, .10f + (1 - fade) * .30f, 0, Mathf.PI * 2, .05f * fade, 17);
                Setup(3, 3, .045f * fade);
                Point(3, 0, Target - _side * .15f); Point(3, 1, Target + Vector3.up * (.17f + (1 - fade) * .2f));
                Point(3, 2, Target + _side * .15f);
            }
            else if (Kind == BasicShotKind.Gale)
            {
                var spin = _age * 12;
                Arc(0, HeadPosition, _direction, _side, .25f, spin - 1.2f, spin + 1.2f, .065f, 9);
                Arc(1, HeadPosition - _direction * .08f, _direction, _side, .17f, spin - 1.3f, spin + .6f, .03f, 8);
                Arc(2, Target, _direction, _side, .16f + (1 - fade) * .24f, -.9f, .9f, .055f * fade, 9);
                Arc(3, Target, _direction, _side, .12f + (1 - fade) * .24f, 2.1f, 3.9f, .035f * fade, 9);
            }
            else
            {
                var spin = _age * 13;
                var forward = _direction * Mathf.Cos(spin) + _side * Mathf.Sin(spin);
                var side = _side * Mathf.Cos(spin) - _direction * Mathf.Sin(spin);
                Setup(0, 5, .095f);
                Point(0, 0, HeadPosition + forward * .23f); Point(0, 1, HeadPosition + side * .14f);
                Point(0, 2, HeadPosition - forward * .16f); Point(0, 3, HeadPosition - side * .11f);
                Point(0, 4, HeadPosition + forward * .23f);
                Setup(1, 3, .045f);
                Point(1, 0, HeadPosition - side * .10f); Point(1, 1, HeadPosition + forward * .06f);
                Point(1, 2, HeadPosition - forward * .11f + side * .10f);
                for (var part = 2; part < 4; part++)
                {
                    var sign = part == 2 ? -1 : 1;
                    var p = Target + _side * sign * (.06f + (1 - fade) * .25f);
                    Setup(part, 3, .085f * fade);
                    Point(part, 0, p - _direction * .13f); Point(part, 1, p + _side * sign * .07f + Vector3.up * .09f);
                    Point(part, 2, p + _direction * .14f);
                }
            }
        }

        private void Setup(int i, int count, float width)
        { _parts[i].positionCount = count; _parts[i].startWidth = width; _parts[i].endWidth = width; }
        private void Point(int i, int j, Vector3 p) => _parts[i].SetPosition(j, p);
        private void Arc(int i, Vector3 center, Vector3 a, Vector3 b, float radius, float from, float to, float width, int count)
        {
            Setup(i, count, width);
            for (var j = 0; j < count; j++)
            {
                var angle = Mathf.Lerp(from, to, j / (float)(count - 1));
                Point(i, j, center + (a * Mathf.Cos(angle) + b * Mathf.Sin(angle)) * radius);
            }
        }

        public void Clear()
        {
            Remaining = 0; _age = 0; Kind = BasicShotKind.None;
            Source = Target = HeadPosition = _direction = _side = Vector3.zero;
            _color.Clear();
            foreach (var line in _parts)
            {
                line.enabled = false; line.positionCount = 0;
                line.startWidth = line.endWidth = 0; line.SetPropertyBlock(null);
            }
        }
    }
}
