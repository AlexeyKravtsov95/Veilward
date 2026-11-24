using UnityEngine;
using Veilward.Domain;

namespace Veilward.Presentation
{
    public enum BossCueKind { None, Spawn, Threat, Death, Leak }

    public sealed class BossCueView : MonoBehaviour
    {
        internal const int Capacity = 4;
        internal const float ThreatDistance = 2.2f;
        private MaterialPropertyBlock _color;
        private LineRenderer _line;
        private Vector3 _forward;
        private float _lifetime;

        public BossCueKind Kind { get; private set; }
        public EnemyId EnemyId { get; private set; }
        public PathPosition Position { get; private set; }
        public float RemainingSeconds { get; private set; }
        internal long Order { get; private set; }

        internal void Initialize(Material material)
        {
            _color = new MaterialPropertyBlock();
            _line = HybridEffectGeometry.CreateLine(transform, material, "Warden cue stroke");
            Clear();
        }

        internal void Play(BossCueKind kind, EnemyId enemyId, PathPosition position, Vector3 forward, long order)
        {
            Clear();
            Kind = kind;
            EnemyId = enemyId;
            Position = position;
            Order = order;
            _forward = forward.sqrMagnitude > 0 ? forward.normalized : Vector3.forward;
            RemainingSeconds = _lifetime = kind == BossCueKind.Spawn ? .62f : kind == BossCueKind.Threat ? .56f : .46f;
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

        internal void Clear()
        {
            Kind = BossCueKind.None;
            EnemyId = default;
            Position = default;
            Order = 0;
            _forward = default;
            RemainingSeconds = _lifetime = 0;
            _color.Clear();
            HybridEffectGeometry.Clear(_line);
            gameObject.SetActive(false);
        }

        private void Draw()
        {
            var progress = 1 - RemainingSeconds / _lifetime;
            var fade = 1 - progress * .90f;
            var ground = new Vector3(Position.X, .25f, Position.Y);
            if (Kind == BossCueKind.Threat || Kind == BossCueKind.Leak)
            {
                var leak = Kind == BossCueKind.Leak;
                var tint = leak ? new Color(1f, .40f, .32f) : new Color(.98f, .62f, .26f);
                var right = Vector3.Cross(Vector3.up, _forward);
                var point = ground + _forward * (leak ? -.13f + progress * .40f : .66f + progress * .40f);
                HybridEffectGeometry.Style(_line, 3, .102f * fade, tint, _color);
                _line.endWidth = _line.startWidth;
                _line.SetPosition(0, point - _forward * .39f - right * .45f);
                _line.SetPosition(1, point);
                _line.SetPosition(2, point - _forward * .39f + right * .45f);
                return;
            }

            var spawn = Kind == BossCueKind.Spawn;
            var radius = spawn ? .48f + .41f * progress : .56f + .46f * progress;
            var violet = spawn ? new Color(.76f, .68f, 1f) : new Color(.91f, .82f, 1f);
            HybridEffectGeometry.Style(_line, 13, .098f * fade, violet, _color);
            for (var i = 0; i < _line.positionCount; i++)
            {
                var angle = i * (Mathf.PI / 7) + (spawn ? progress : -progress) * .65f;
                var edge = radius * (spawn || i % 2 == 0 ? 1 : .68f);
                var lift = spawn ? .03f + progress * .12f : .10f + (i % 2) * (.12f + progress * .09f);
                _line.SetPosition(i, ground + new Vector3(Mathf.Cos(angle) * edge, lift, Mathf.Sin(angle) * edge));
            }
        }
    }
}
