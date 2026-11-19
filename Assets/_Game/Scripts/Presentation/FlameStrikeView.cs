using UnityEngine;
using UnityEngine.Rendering;

namespace Veilward.Presentation
{
    // Presentation-only: impact начинается сразу по AttackEvent, без projectile/damage clock.
    public sealed class FlameStrikeView
    {
        public const float Lifetime = .22f;
        public const float TrailLifetime = .065f;
        private readonly LineRenderer[] _parts = new LineRenderer[12];
        private Vector3 _start, _end, _side;
        private float _age;
        public float Remaining { get; private set; }
        public Vector3 Source => _start;
        public Vector3 Target => _end;
        public bool TrailVisible => _parts[0].enabled;

        public FlameStrikeView(Transform parent, Material flame, Material hot, int index)
        {
            var root = new GameObject("Flame Impulse Pool " + index).transform;
            root.SetParent(parent, false);
            for (var i = 0; i < _parts.Length; i++)
            {
                var go = new GameObject(i < 3 ? "Torn streak" : i < 5 ? "Source flash" : i < 8 ? "Impact tongue" : "Ember");
                go.transform.SetParent(root, false);
                var line = go.AddComponent<LineRenderer>();
                line.sharedMaterial = i == 4 || i == 7 ? hot : flame;
                line.useWorldSpace = true; line.positionCount = 3;
                line.numCapVertices = 0; line.numCornerVertices = 0;
                line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
                _parts[i] = line;
            }
            Clear();
        }

        public void Play(Vector3 source, Vector3 target)
        {
            _start = source; _end = target;
            var direction = target - source;
            _side = Vector3.Cross(direction.normalized, Vector3.up).normalized;
            if (_side.sqrMagnitude < .01f) _side = Vector3.right;
            _age = 0; Remaining = Lifetime;
            Draw();
        }

        public void Advance(float seconds)
        {
            if (Remaining <= 0) return;
            _age += Mathf.Max(0, seconds); Remaining = Mathf.Max(0, Lifetime - _age);
            if (Remaining <= 0) Clear(); else Draw();
        }

        private void Draw()
        {
            var fade = Mathf.Clamp01(Remaining / Lifetime);
            // Три раздельных рваных языка, а не непрерывная дуга между башней и целью.
            for (var i = 0; i < 3; i++)
            {
                var visible = _age < TrailLifetime;
                _parts[i].enabled = visible;
                if (!visible) continue;
                var t = .12f + i * .30f;
                var a = Vector3.Lerp(_start, _end, t);
                var b = Vector3.Lerp(_start, _end, t + .15f);
                var c = Vector3.Lerp(_start, _end, t + .23f);
                Set(i, a, b + _side * (i % 2 == 0 ? .075f : -.07f) + Vector3.up * .09f, c,
                    (.14f - i * .025f) * (1f - _age / TrailLifetime));
            }
            for (var i = 3; i < 5; i++)
            {
                var f = Mathf.Clamp01(1f - _age / .09f);
                var p = _start + _side * ((i - 3) * .06f - .03f);
                Set(i, p - Vector3.up * .06f, p + Vector3.up * .10f, p + Vector3.up * .24f, (i == 3 ? .21f : .065f) * f);
                _parts[i].enabled = f > 0;
            }
            for (var i = 5; i < 8; i++)
            {
                var n = i - 6;
                var p = _end + _side * n * .095f;
                Set(i, p - Vector3.up * .08f, p + _side * n * .035f + Vector3.up * (.09f + _age * .5f),
                    p - _side * n * .04f + Vector3.up * (.26f + _age * .4f), (i == 7 ? .065f : .16f) * fade);
                _parts[i].enabled = true;
            }
            for (var i = 8; i < 12; i++)
            {
                var n = i - 9.5f;
                var p = _end + _side * n * (.05f + _age * .5f) + Vector3.up * (.06f + _age * (1.1f + (i % 2) * .7f));
                Set(i, p, p + Vector3.up * .025f, p + Vector3.up * .055f, .033f * fade);
                _parts[i].enabled = true;
            }
        }

        private void Set(int i, Vector3 a, Vector3 b, Vector3 c, float width)
        {
            var line = _parts[i];
            line.SetPosition(0, a); line.SetPosition(1, b); line.SetPosition(2, c);
            line.startWidth = width; line.endWidth = width * .06f;
        }

        public void Clear()
        {
            Remaining = 0; _age = Lifetime;
            foreach (var line in _parts) line.enabled = false;
        }
    }
}
