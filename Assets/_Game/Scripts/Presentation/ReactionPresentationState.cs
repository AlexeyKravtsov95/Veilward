using System;
using System.Collections.Generic;
using Veilward.Domain;

namespace Veilward.Presentation
{
    public readonly struct ReactionExplanation
    {
        public ReactionExplanation(ReactionEvent reaction, int hitCount, long appliedHpLoss)
        { Reaction = reaction; HitCount = hitCount; AppliedHpLoss = appliedHpLoss; }
        public ReactionEvent Reaction { get; }
        public int HitCount { get; }
        public long AppliedHpLoss { get; }
        public string Cause => Reaction.TriggerKind == ReactionTriggerKind.Intrinsic
            ? (Reaction.Kind == ReactionKind.Steam ? "STEAM: встроенная" : "STORM: встроенная")
            : Reaction.Kind == ReactionKind.Steam ? "Пламя + Прилив → Пар" : "Прилив + Ветер → Шторм";
        public string MarkerLabel => Reaction.Kind == ReactionKind.Steam
            ? (Reaction.TriggerKind == ReactionTriggerKind.Intrinsic ? "Steam burst" : "Flame + Tide: Steam")
            : (Reaction.TriggerKind == ReactionTriggerKind.Intrinsic ? "Storm chain" : "Tide + Gale: Storm");
        public string Summary => $"{(Reaction.Kind == ReactionKind.Steam ? "Пар" : "Шторм")}: {HitCount} целей, {AppliedHpLoss} HP";
    }

    public sealed class EnemyReactionMemory
    {
        public ReactionExplanation? Steam { get; internal set; }
        public ReactionExplanation? Storm { get; internal set; }
        public ReactionExplanation? Cue { get; internal set; }
        public double CueUntil { get; internal set; }
    }

    // Только transient presentation: не владеет HP, cooldown или наградами.
    public sealed class ReactionPresentationState
    {
        public const double CueSeconds = .5;
        private readonly Dictionary<EnemyId, EnemyReactionMemory> _memories = new Dictionary<EnemyId, EnemyReactionMemory>();
        private readonly List<EnemyId> _stale = new List<EnemyId>();
        public double ActiveTime { get; private set; }
        public string LastSummary { get; private set; } = string.Empty;
        public EnemyReactionMemory GetMemory(EnemyId id) => _memories.TryGetValue(id, out var memory) ? memory : null;
        public void Advance(double seconds) { if (seconds > 0) ActiveTime += seconds; }

        public static bool Matches(ReactionEvent reaction, DamageResolvedEvent hit) =>
            hit.Origin == DamageOrigin.Reaction && hit.Tick == reaction.Tick &&
            hit.ReactionSequenceInTick == reaction.SequenceInTick && hit.ReactionKind == reaction.Kind &&
            hit.SourceTowerId == reaction.SourceTowerId;

        public void Consume(ReactionEvent reaction, IReadOnlyList<DamageResolvedEvent> hits)
        {
            var count = 0; long hpLoss = 0;
            for (var i = 0; i < hits.Count; i++)
                if (Matches(reaction, hits[i])) { count++; hpLoss += hits[i].AppliedHpLoss; }
            var explanation = new ReactionExplanation(reaction, count, hpLoss);
            LastSummary = explanation.Summary;
            Remember(reaction.PrimaryEnemyId, explanation, true);
            for (var i = 0; i < hits.Count; i++)
                if (Matches(reaction, hits[i]) && hits[i].TargetEnemyId != reaction.PrimaryEnemyId)
                    Remember(hits[i].TargetEnemyId, explanation, false);
        }

        private void Remember(EnemyId id, ReactionExplanation explanation, bool cue)
        {
            if (!_memories.TryGetValue(id, out var memory))
            { memory = new EnemyReactionMemory(); _memories.Add(id, memory); }
            if (explanation.Reaction.Kind == ReactionKind.Steam) memory.Steam = explanation;
            else memory.Storm = explanation;
            // В один tick Steam сохраняет приоритет независимо от порядка consumer.
            if (cue && (!memory.Cue.HasValue || memory.Cue.Value.Reaction.Tick != explanation.Reaction.Tick ||
                        explanation.Reaction.Kind == ReactionKind.Steam))
            { memory.Cue = explanation; memory.CueUntil = ActiveTime + CueSeconds; }
        }

        public void Prune(IReadOnlyList<EnemyState> enemies)
        {
            _stale.Clear();
            foreach (var pair in _memories)
            {
                var found = false;
                for (var i = 0; i < enemies.Count; i++)
                    if (enemies[i].IsAlive && enemies[i].Id == pair.Key) { found = true; break; }
                if (!found) _stale.Add(pair.Key);
            }
            for (var i = 0; i < _stale.Count; i++) _memories.Remove(_stale[i]);
        }

        public void Reset() { _memories.Clear(); _stale.Clear(); ActiveTime = 0; LastSummary = string.Empty; }
        public static long Cooldown(EnemyState enemy, ReactionKind kind, long tick, ReactionCatalog catalog)
        {
            if (!catalog.TryGet(kind, out var definition)) return 0;
            var last = kind == ReactionKind.Steam ? enemy.LastSteamTick : enemy.LastStormTick;
            if (!last.HasValue) return 0;
            return Math.Max(0, definition.CooldownTicks - Math.Max(0, tick - last.Value));
        }
    }
}
