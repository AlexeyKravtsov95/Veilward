namespace Veilward.Domain
{
    public enum DamageOrigin
    {
        Direct,
        Burn,
        Reaction
    }

    public readonly struct DamageResolvedEvent
    {
        public DamageResolvedEvent(
            long tick,
            int sequenceInTick,
            DamageOrigin origin,
            TowerId sourceTowerId,
            EnemyId targetEnemyId,
            PathPosition fromPosition,
            PathPosition targetPosition,
            int configuredBaseDamage,
            int rawDamage,
            int resolvedDamage,
            int appliedHpLoss,
            int healthAfter,
            bool wasLethal,
            ReactionKind? reactionKind,
            int? reactionSequenceInTick,
            int hitIndex)
        {
            Tick = tick;
            SequenceInTick = sequenceInTick;
            Origin = origin;
            SourceTowerId = sourceTowerId;
            TargetEnemyId = targetEnemyId;
            FromPosition = fromPosition;
            TargetPosition = targetPosition;
            ConfiguredBaseDamage = configuredBaseDamage;
            RawDamage = rawDamage;
            ResolvedDamage = resolvedDamage;
            AppliedHpLoss = appliedHpLoss;
            HealthAfter = healthAfter;
            WasLethal = wasLethal;
            ReactionKind = reactionKind;
            ReactionSequenceInTick = reactionSequenceInTick;
            HitIndex = hitIndex;
        }

        public long Tick { get; }
        public int SequenceInTick { get; }
        public DamageOrigin Origin { get; }
        public TowerId SourceTowerId { get; }
        public EnemyId TargetEnemyId { get; }
        public PathPosition FromPosition { get; }
        public PathPosition TargetPosition { get; }
        public int ConfiguredBaseDamage { get; }
        public int RawDamage { get; }
        public int ResolvedDamage { get; }
        public int AppliedHpLoss { get; }
        public int HealthAfter { get; }
        public bool WasLethal { get; }
        public ReactionKind? ReactionKind { get; }
        public int? ReactionSequenceInTick { get; }
        // Steam primary = 0; Storm дополнительные попадания = 1/2.
        public int HitIndex { get; }
    }
}
