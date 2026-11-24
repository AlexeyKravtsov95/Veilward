using System;
using System.Collections.Generic;
using UnityEngine;
using Veilward.Domain;

namespace Veilward.Presentation
{
    public sealed class ReactionEffectView : MonoBehaviour
    {
        public const int RendererCount = 5;
        private const int SteamHitCapacity = 4;
        private const int StormHitCapacity = 2;
        private const float Lifetime = .5f;
        private const float HopDelay = .07f;
        private const float HopReveal = .045f;
        private const float HopDuration = .21f;
        private readonly DamageResolvedEvent[] _hits = new DamageResolvedEvent[SteamHitCapacity];
        private readonly LineRenderer[] _lines = new LineRenderer[RendererCount];
        private MaterialPropertyBlock _color;
        public ReactionEvent Event { get; private set; }
        public float RemainingSeconds { get; private set; }
        public int HitCount { get; private set; }

        public DamageResolvedEvent GetHit(int index)
        {
            if (index < 0 || index >= HitCount) throw new ArgumentOutOfRangeException(nameof(index));
            return _hits[index];
        }

        public void Initialize(Material material)
        {
            _color = new MaterialPropertyBlock();
            _lines[0] = HybridEffectGeometry.CreateLine(transform, material, "Reaction origin");
            for (var i = 1; i < RendererCount; i++)
                _lines[i] = HybridEffectGeometry.CreateLine(transform, material, "Reaction stroke " + i);
            Clear();
        }

        public void Play(ReactionEvent reaction, IReadOnlyList<DamageResolvedEvent> damages)
        {
            Clear();
            Event = reaction;
            var steam = reaction.Kind == ReactionKind.Steam;
            var first = steam ? 0 : 1;
            var limit = Mathf.Clamp(reaction.TargetLimit, 0, steam ? SteamHitCapacity : StormHitCapacity);
            // Вставляем только фактические hits своего события, сохраняя авторитетный HitIndex.
            for (var i = 0; i < damages.Count; i++)
            {
                var hit = damages[i];
                if (!ReactionPresentationState.Matches(reaction, hit) ||
                    hit.HitIndex < first || hit.HitIndex >= first + limit ||
                    (hit.TargetEnemyId == reaction.PrimaryEnemyId) != (hit.HitIndex == 0))
                    continue;
                var at = HitCount;
                while (at > 0 && _hits[at - 1].HitIndex > hit.HitIndex) at--;
                if ((at > 0 && _hits[at - 1].HitIndex == hit.HitIndex) || HitCount == limit) continue;
                for (var j = HitCount; j > at; j--) _hits[j] = _hits[j - 1];
                _hits[at] = hit;
                HitCount++;
            }

            RemainingSeconds = Lifetime;
            gameObject.SetActive(true);
            Draw();
        }

        public void Advance(float seconds)
        {
            if (RemainingSeconds <= 0 || seconds <= 0) return;
            RemainingSeconds -= seconds;
            if (RemainingSeconds <= 0) Clear();
            else Draw();
        }

        private void Draw()
        {
            var age = Lifetime - RemainingSeconds;
            var primary = Position(Event.PrimaryPosition, .34f);
            var intrinsic = Event.TriggerKind == ReactionTriggerKind.Intrinsic;
            if (Event.Kind == ReactionKind.Steam) DrawSteam(primary, age / Lifetime, intrinsic);
            else DrawStorm(primary, age, intrinsic);
        }

        private void DrawSteam(Vector3 primary, float progress, bool intrinsic)
        {
            var tint = intrinsic ? new Color(.91f, .95f, .89f) : new Color(1f, .82f, .56f);
            var fade = 1 - progress;
            var range = Event.RadiusOrHopRange;
            var radius = range * (.25f + .75f * progress);
            HybridEffectGeometry.Style(_lines[0], 32, .061f * fade, tint, _color);
            for (var i = 0; i < _lines[0].positionCount; i++)
            {
                var angle = i * (Mathf.PI * 2 / 31);
                var lobe = .80f + .20f * Mathf.Cos(angle * 5 - progress * 4);
                var p = primary + new Vector3(Mathf.Cos(angle), .25f * fade * Mathf.Sin(angle * 5), Mathf.Sin(angle)) * (radius * lobe);
                _lines[0].SetPosition(i, p);
            }

            for (var i = 0; i < HitCount; i++)
            {
                var hit = _hits[i];
                if (hit.AppliedHpLoss <= 0) continue;
                // Пар огибает ноги у captured точки и остаётся виден по краям модели.
                var center = Position(hit.TargetPosition, .43f + .12f * progress);
                var size = .35f + .14f * progress;
                HybridEffectGeometry.Style(_lines[i + 1], 13, .073f * fade, tint, _color);
                HybridEffectGeometry.Curl(_lines[i + 1], center, Vector3.right, Vector3.forward,
                    size, progress * 7 + hit.HitIndex * 1.8f, 4.9f);
            }
        }

        private void DrawStorm(Vector3 primary, float age, bool intrinsic)
        {
            var tint = intrinsic ? new Color(.50f, .91f, 1f) : new Color(.68f, .75f, 1f);
            var originFade = 1 - age / .20f;
            if (originFade > 0)
            {
                HybridEffectGeometry.Style(_lines[0], 6, .064f * originFade, tint, _color);
                var origin = primary + Vector3.up * .26f;
                HybridEffectGeometry.Bolt(_lines[0], origin - Vector3.right * .18f,
                    origin + Vector3.right * .21f + Vector3.up * .18f, age * 55, .15f);
            }
            else HybridEffectGeometry.Clear(_lines[0]);

            for (var i = 0; i < HitCount; i++)
            {
                var hit = _hits[i];
                var elapsed = age - (hit.HitIndex - 1) * HopDelay;
                var line = _lines[1 + i];
                var flash = _lines[3 + i];
                if (elapsed < 0 || elapsed >= HopDuration)
                {
                    HybridEffectGeometry.Clear(line);
                    HybridEffectGeometry.Clear(flash);
                    continue;
                }

                var from = Position(hit.FromPosition, .57f);
                var target = Position(hit.TargetPosition, .57f);
                var t = Mathf.Clamp01(.28f + .72f * elapsed / HopReveal);
                var fade = 1 - elapsed / HopDuration;
                var amplitude = Mathf.Min(.19f, Event.RadiusOrHopRange * .17f) * fade;
                HybridEffectGeometry.Style(line, 9, .074f * fade, tint, _color);
                HybridEffectGeometry.Bolt(line, from, Vector3.Lerp(from, target, t), age * 90 + hit.HitIndex * 5, amplitude);
                if (t < 1 || hit.AppliedHpLoss <= 0) continue;
                HybridEffectGeometry.Style(flash, 5, .073f * fade, new Color(.87f, .96f, 1f), _color);
                var spread = .38f + .13f * (1 - fade);
                var diagonal = new Vector3(.82f, 0, -.57f) * spread;
                HybridEffectGeometry.Bolt(flash, target + Vector3.up * .24f - diagonal,
                    target + Vector3.up * .10f + diagonal, age * 60, .14f);
            }
        }

        private static Vector3 Position(PathPosition point, float height) => new Vector3(point.X, height, point.Y);

        public void Clear()
        {
            RemainingSeconds = 0;
            HitCount = 0;
            Event = default;
            Array.Clear(_hits, 0, _hits.Length);
            _color.Clear();
            foreach (var line in _lines) HybridEffectGeometry.Clear(line);
            gameObject.SetActive(false);
        }
    }
}
