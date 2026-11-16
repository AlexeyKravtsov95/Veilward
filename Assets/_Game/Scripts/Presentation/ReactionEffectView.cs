using System.Collections.Generic;
using UnityEngine;
using Veilward.Domain;

namespace Veilward.Presentation
{
    // Один элемент ограниченного pool. Координаты берутся только из immutable events.
    public sealed class ReactionEffectView : MonoBehaviour
    {
        private readonly DamageResolvedEvent[] _hits = new DamageResolvedEvent[4];
        private readonly LineRenderer[] _impacts = new LineRenderer[4];
        private LineRenderer _shape;
        private ReactionEvent _reaction;
        private MaterialPropertyBlock _colorBlock;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public float RemainingSeconds { get; private set; }
        public int HitCount { get; private set; }

        public void Initialize(Material material)
        {
            _colorBlock = new MaterialPropertyBlock();
            _shape = Line("Reaction shape", material, .035f);
            for (var i = 0; i < _impacts.Length; i++) _impacts[i] = Line("Reaction impact " + i, material, .045f);
            Clear();
        }
        private LineRenderer Line(string label, Material material, float width)
        {
            var line = new GameObject(label).AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false); line.useWorldSpace = true;
            line.sharedMaterial = material; line.startWidth = width; line.endWidth = width;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
            return line;
        }
        public void Play(ReactionEvent reaction, IReadOnlyList<DamageResolvedEvent> damages)
        {
            _reaction = reaction; HitCount = 0;
            // HitIndex — сохранённый hop order, входной stream может содержать другие реакции.
            for (var i = 0; i < damages.Count; i++)
            {
                var hit = damages[i];
                if (!ReactionPresentationState.Matches(reaction, hit) || HitCount == _hits.Length) continue;
                var insert = HitCount;
                while (insert > 0 && _hits[insert - 1].HitIndex > hit.HitIndex) { _hits[insert] = _hits[insert - 1]; insert--; }
                _hits[insert] = hit; HitCount++;
            }
            RemainingSeconds = .5f; gameObject.SetActive(true);
            var steam = reaction.Kind == ReactionKind.Steam;
            _shape.name = steam ? "Steam ring" : "Storm chain";
            _shape.loop = steam;
            var color = steam ? EnemyStatusView.SteamColor : EnemyStatusView.StormColor;
            _colorBlock.SetColor(BaseColor, color);
            _shape.SetPropertyBlock(_colorBlock);
            _shape.startColor = _shape.endColor = color;
            for (var i = 0; i < _impacts.Length; i++)
            {
                _impacts[i].SetPropertyBlock(_colorBlock);
                _impacts[i].startColor = _impacts[i].endColor = color;
                _impacts[i].gameObject.SetActive(i < HitCount);
            }
            Draw();
        }
        public void Advance(float seconds)
        {
            if (RemainingSeconds <= 0 || seconds <= 0) return;
            RemainingSeconds = Mathf.Max(0, RemainingSeconds - seconds);
            if (RemainingSeconds <= 0) Clear(); else Draw();
        }
        private void Draw()
        {
            var progress = 1 - RemainingSeconds / .5f;
            if (_reaction.Kind == ReactionKind.Steam)
            {
                _shape.positionCount = 48;
                var center = Position(_reaction.PrimaryPosition);
                var radius = _reaction.RadiusOrHopRange * (.15f + .85f * progress);
                for (var i = 0; i < 48; i++)
                {
                    var angle = i * Mathf.PI * 2 / 48;
                    _shape.SetPosition(i, center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius);
                }
            }
            else
            {
                _shape.positionCount = 1 + HitCount * 3;
                _shape.SetPosition(0, Position(_reaction.PrimaryPosition));
                for (var i = 0; i < HitCount; i++)
                {
                    var from = Position(_hits[i].FromPosition); var to = Position(_hits[i].TargetPosition);
                    var side = Vector3.Cross(to - from, Vector3.up).normalized * .09f;
                    _shape.SetPosition(1 + i * 3, Vector3.Lerp(from, to, .33f) + side);
                    _shape.SetPosition(2 + i * 3, Vector3.Lerp(from, to, .67f) - side);
                    _shape.SetPosition(3 + i * 3, to);
                }
            }
            for (var i = 0; i < HitCount; i++)
            {
                var line = _impacts[i]; line.gameObject.SetActive(progress < .6f);
                line.positionCount = 4;
                var p = Position(_hits[i].TargetPosition); var r = .08f + .12f * progress;
                line.SetPosition(0, p + new Vector3(-r, 0, -r)); line.SetPosition(1, p + new Vector3(r, 0, r));
                line.SetPosition(2, p); line.SetPosition(3, p + new Vector3(-r, 0, r));
            }
        }
        private static Vector3 Position(PathPosition p) => new Vector3(p.X, .13f, p.Y);
        public void Clear() { RemainingSeconds = 0; gameObject.SetActive(false); }
    }
}
