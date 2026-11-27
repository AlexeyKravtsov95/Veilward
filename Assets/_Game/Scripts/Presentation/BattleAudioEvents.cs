using System.Collections.Generic;
using UnityEngine;
using Veilward.Domain;

namespace Veilward.Presentation
{
    internal sealed class BattleAudioEvents
    {
        private readonly VeilwardAudio _audio;
        private readonly Camera _camera;
        private EventCursor _direct;
        private EventCursor _reactions;
        private EventCursor _casts;
        private BattlePhase _phase;

        internal BattleAudioEvents(VeilwardAudio audio, Camera camera, BattlePhase phase)
        {
            _audio = audio;
            _camera = camera;
            _phase = phase;
        }

        internal void Reset(BattlePhase phase)
        {
            _direct = _reactions = _casts = default;
            _phase = phase;
            _audio.ClearRun();
        }

        internal void PhaseChanged(BattlePhase phase)
        {
            if (_phase == phase) return;
            _phase = phase;
            switch (phase)
            {
                case BattlePhase.Combat: _audio.Play(AudioCue.WaveStart); break;
                case BattlePhase.WaveComplete: _audio.Play(AudioCue.WaveEnd); break;
                case BattlePhase.Victory: _audio.Play(AudioCue.Victory); break;
                case BattlePhase.Defeat: _audio.Play(AudioCue.Defeat); break;
            }
        }

        internal void Attack(AttackEvent attack, IReadOnlyList<DamageResolvedEvent> damages,
            string definition, Vector3 source, float flight)
        {
            if (!TryGetShot(definition, out var shot)) return;
            for (var i = 0; i < damages.Count; i++)
            {
                var hit = damages[i];
                if (hit.Origin != DamageOrigin.Direct || hit.Tick != attack.Tick ||
                    hit.SourceTowerId != attack.AttackerTowerId || hit.TargetEnemyId != attack.TargetEnemyId ||
                    hit.HitIndex != 0 || hit.AppliedHpLoss <= 0) continue;
                if (!_direct.Accept(hit.Tick, hit.SequenceInTick)) return;
                var alternate = ((hit.Tick ^ hit.SourceTowerId.Value ^ hit.TargetEnemyId.Value) & 1) != 0;
                _audio.Play(shot, Pan(source), variation: alternate);
                var gain = definition == "steam" || definition == "storm" ? .56f : .85f;
                _audio.Play(shot + 1, Pan(hit.TargetPosition), gain, flight, alternate);
                return;
            }
        }

        internal void Reaction(ReactionEvent reaction, IReadOnlyList<DamageResolvedEvent> damages)
        {
            if (!_reactions.Accept(reaction.Tick, reaction.SequenceInTick)) return;
            var storm = reaction.Kind == ReactionKind.Storm;
            if (storm
                ? reaction.CauseFirstKind != StatusKind.Wet || reaction.CauseSecondKind != StatusKind.Gust
                : reaction.Kind != ReactionKind.Steam || reaction.CauseFirstKind != StatusKind.Burn ||
                    reaction.CauseSecondKind != StatusKind.Wet) return;
            var gain = reaction.TriggerKind == ReactionTriggerKind.Intrinsic ? .72f : 1f;
            var heardHits = 0;
            for (var i = 0; i < damages.Count; i++)
            {
                var hit = damages[i];
                if (hit.Origin != DamageOrigin.Reaction || hit.Tick != reaction.Tick ||
                    hit.SourceTowerId != reaction.SourceTowerId || hit.ReactionKind != reaction.Kind ||
                    hit.ReactionSequenceInTick != reaction.SequenceInTick || hit.AppliedHpLoss <= 0) continue;
                if (!storm && hit.HitIndex == 0 && hit.TargetEnemyId == reaction.PrimaryEnemyId)
                {
                    _audio.Play(AudioCue.SteamBurst, Pan(hit.TargetPosition), gain);
                    return;
                }
                if (!storm || (hit.HitIndex != 1 && hit.HitIndex != 2) ||
                    hit.TargetEnemyId == reaction.PrimaryEnemyId) continue;
                var bit = 1 << hit.HitIndex;
                if ((heardHits & bit) != 0) continue;
                heardHits |= bit;
                _audio.Play(hit.HitIndex == 1 ? AudioCue.StormChain : AudioCue.StormHop,
                    Pan(hit.TargetPosition), gain * (hit.HitIndex == 1 ? 1f : .64f),
                    hit.HitIndex == 1 ? 0f : .085f);
            }
        }

        internal void Cast(AbilityCastEvent cast, IReadOnlyList<DamageResolvedEvent> damages)
        {
            if (!_casts.Accept(cast.Tick, cast.SequenceInTick)) return;
            _audio.Play(AudioCue.SealCast, Pan(cast.Center));
            for (var i = 0; i < damages.Count; i++)
            {
                var hit = damages[i];
                if (hit.Origin != DamageOrigin.Ability || hit.Tick != cast.Tick ||
                    hit.SourceAbilityId != cast.AbilityId || hit.AbilityCastSequence != cast.CastSequence ||
                    hit.HitIndex < 0 || hit.HitIndex >= cast.HitCount || hit.AppliedHpLoss <= 0) continue;
                // Один accent на cast. Первый валидный захваченный endpoint оставляет место аккорду.
                _audio.Play(AudioCue.SealHit, Pan(hit.TargetPosition), .78f, .08f);
                break;
            }
        }

        internal void Boss(BossCueKind kind, EnemyId enemy, PathPosition position)
        {
            switch (kind)
            {
                case BossCueKind.Spawn: _audio.Play(AudioCue.BossSpawn, Pan(position), boss: enemy); break;
                case BossCueKind.Threat: _audio.Play(AudioCue.BossThreat, Pan(position), boss: enemy); break;
                case BossCueKind.Death:
                case BossCueKind.Leak:
                    _audio.ClearBoss(enemy);
                    _audio.Play(kind == BossCueKind.Death ? AudioCue.BossDeath : AudioCue.BossLeak,
                        Pan(position), boss: enemy);
                    break;
            }
        }

        private float Pan(PathPosition position) => Pan(new Vector3(position.X, .6f, position.Y));

        private float Pan(Vector3 position) =>
            Mathf.Clamp(_camera.WorldToViewportPoint(position).x * 2f - 1f, -1f, 1f) * .43f;

        private static bool TryGetShot(string definition, out AudioCue shot)
        {
            switch (definition)
            {
                case "flame": shot = AudioCue.FlameShot; return true;
                case "tide": shot = AudioCue.TideShot; return true;
                case "gale": shot = AudioCue.GaleShot; return true;
                case "stone": shot = AudioCue.StoneShot; return true;
                case "steam": shot = AudioCue.SteamShot; return true;
                case "storm": shot = AudioCue.StormShot; return true;
                default: shot = default; return false;
            }
        }

        private struct EventCursor
        {
            private long _tick;
            private int _sequence;
            private bool _initialized;

            internal bool Accept(long tick, int sequence)
            {
                if (_initialized && (tick < _tick || tick == _tick && sequence <= _sequence)) return false;
                _tick = tick;
                _sequence = sequence;
                _initialized = true;
                return true;
            }
        }
    }
}
