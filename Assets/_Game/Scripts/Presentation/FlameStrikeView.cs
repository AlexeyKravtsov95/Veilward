using UnityEngine;
using UnityEngine.Rendering;
using Veilward.Domain;

namespace Veilward.Presentation
{
    // Полёт только визуальный: authoritative damage уже произошёл в исходном AttackEvent.
    public sealed class FlameStrikeView
    {
        public const int FlightTicks = 6;
        public const float FlightDuration = FlightTicks / 30f;
        public const float Lifetime = (FlightTicks + 5) / 30f;
        private readonly LineRenderer[] _parts = new LineRenderer[13];
        private Vector3 _start, _end, _direction, _side;
        private float _age;
        public float Remaining { get; private set; }
        public Vector3 Source => _start;
        public Vector3 Target => _end;
        public Vector3 HeadPosition { get; private set; }
        public Vector3 TailEnd { get; private set; }
        public EnemyId TargetEnemyId { get; private set; }
        public bool WasLethal { get; private set; }
        public bool IsFlying => Remaining > 0 && _age + .000001f < FlightDuration;
        public bool ImpactVisible => _parts[6].enabled;
        public bool TrailVisible => _parts[3].enabled;

        public FlameStrikeView(Transform parent, Material flame, Material hot, int index)
        {
            var root = new GameObject("Flame Projectile Pool " + index).transform;
            root.SetParent(parent, false);
            var shape = new AnimationCurve(new Keyframe(0, .05f), new Keyframe(.3f, 1), new Keyframe(.65f, .9f), new Keyframe(1, .02f));
            for (var i = 0; i < _parts.Length; i++)
            {
                var name = i < 3 ? "Fire head layer" : i < 5 ? "Short moving tail" : i == 5 ? "Source flash" : i < 9 ? "Arrival impact" : "Ember";
                var go = new GameObject(name); go.transform.SetParent(root, false);
                var line = go.AddComponent<LineRenderer>();
                line.sharedMaterial = i == 2 || i == 5 || i == 8 ? hot : flame;
                line.sortingOrder = i == 2 || i == 5 || i == 8 ? 2 : 0;
                line.useWorldSpace = true; line.positionCount = 3;
                line.numCapVertices = 0; line.numCornerVertices = 0;
                line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
                if (i < 3) line.widthCurve = shape;
                _parts[i] = line;
            }
            Clear();
        }

        public void Play(Vector3 source, Vector3 target) => Play(source, target, default, false);

        public void Play(Vector3 source, Vector3 target, EnemyId targetId, bool wasLethal)
        {
            _start = source; _end = target; TargetEnemyId = targetId; WasLethal = wasLethal;
            _direction = (target - source).normalized;
            if (_direction.sqrMagnitude < .01f) _direction = Vector3.forward;
            _side = Vector3.Cross(_direction, Vector3.up).normalized;
            if (_side.sqrMagnitude < .01f) _side = Vector3.right;
            _age = 0; Remaining = Lifetime; Draw();
        }

        public void Advance(float seconds)
        {
            if (Remaining <= 0) return;
            _age += Mathf.Max(0, seconds);
            Remaining = Mathf.Max(0, Lifetime - _age);
            if (_age + .000001f >= Lifetime) Clear(); else Draw();
        }

        private void Draw()
        {
            var progress = Mathf.Clamp01(_age / FlightDuration);
            HeadPosition = Vector3.Lerp(_start, _end, progress) + Vector3.up * (Mathf.Sin(progress * Mathf.PI) * .10f);
            if (!IsFlying) HeadPosition = _end;
            var length = Mathf.Min(.48f, Vector3.Distance(_start, HeadPosition));
            TailEnd = HeadPosition - _direction * length;
            for (var i = 0; i < 3; i++)
            {
                _parts[i].enabled = IsFlying;
                var radius = i == 2 ? .065f : .14f;
                var center = HeadPosition + _side * (i == 1 ? .025f : 0) + Vector3.up * (i == 1 ? .025f : 0);
                Set(i, center - _direction * radius, center, center + _direction * radius,
                    i == 2 ? .115f : i == 1 ? .24f : .32f, false);
            }
            for (var i = 3; i < 5; i++)
            {
                var tail = length * (i == 3 ? 1 : .65f);
                Set(i, HeadPosition - _direction * .045f,
                    HeadPosition - _direction * (tail * .5f) + _side * (Mathf.Sin(_age * 45 + i) * .045f),
                    HeadPosition - _direction * tail, i == 3 ? .14f : .065f);
                _parts[i].enabled = IsFlying && length > .02f;
            }
            var flash = Mathf.Clamp01(1 - _age / (2 / 30f));
            Set(5, _start - Vector3.up * .07f, _start, _start + Vector3.up * .13f, .17f * flash);
            _parts[5].enabled = flash > 0;
            var hitAge = Mathf.Max(0, _age - FlightDuration);
            var fade = Mathf.Clamp01(1 - hitAge / (Lifetime - FlightDuration));
            for (var i = 6; i < 9; i++)
            {
                var n = i - 7;
                var p = _end + _side * n * .09f;
                Set(i, p - Vector3.up * .07f, p + Vector3.up * (.07f + hitAge * .4f),
                    p - _side * n * .04f + Vector3.up * (.24f + hitAge * .4f), (i == 8 ? .065f : .16f) * fade);
                _parts[i].enabled = !IsFlying;
            }
            for (var i = 9; i < 13; i++)
            {
                var n = i - 10.5f;
                var center = IsFlying ? HeadPosition - _direction * (length * .65f) : _end;
                var t = IsFlying ? _age : hitAge;
                var p = center + _side * n * (.035f + t * .3f) + Vector3.up * (.03f + t * (.4f + (i % 2) * .4f));
                Set(i, p, p + Vector3.up * .02f, p + Vector3.up * .045f, .026f * (IsFlying ? 1 : fade));
                _parts[i].enabled = true;
            }
        }

        private void Set(int i, Vector3 a, Vector3 b, Vector3 c, float width, bool tapered = true)
        {
            var line = _parts[i]; line.SetPosition(0, a); line.SetPosition(1, b); line.SetPosition(2, c);
            if (tapered) { line.startWidth = width; line.endWidth = width * .04f; }
            else line.widthMultiplier = width;
        }

        public void Clear()
        {
            Remaining = 0; _age = Lifetime; TargetEnemyId = default; WasLethal = false;
            _start = _end = _direction = _side = Vector3.zero;
            HeadPosition = TailEnd = Vector3.zero;
            foreach (var line in _parts) line.enabled = false;
        }
    }
}
