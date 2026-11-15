namespace Veilward.Domain
{
    public enum ReactionTriggerKind
    {
        StatusPair,
        Intrinsic
    }

    public readonly struct ReactionEvent
    {
        public ReactionEvent(
            long tick,
            int sequenceInTick,
            ReactionKind kind,
            ReactionTriggerKind triggerKind,
            TowerId sourceTowerId,
            EnemyId primaryEnemyId,
            PathPosition primaryPosition,
            StatusKind causeFirstKind,
            StatusKind causeSecondKind,
            float radiusOrHopRange,
            int targetLimit)
        {
            Tick = tick;
            SequenceInTick = sequenceInTick;
            Kind = kind;
            TriggerKind = triggerKind;
            SourceTowerId = sourceTowerId;
            PrimaryEnemyId = primaryEnemyId;
            PrimaryPosition = primaryPosition;
            CauseFirstKind = causeFirstKind;
            CauseSecondKind = causeSecondKind;
            RadiusOrHopRange = radiusOrHopRange;
            TargetLimit = targetLimit;
        }

        public long Tick { get; }
        public int SequenceInTick { get; }
        public ReactionKind Kind { get; }
        public ReactionTriggerKind TriggerKind { get; }
        public TowerId SourceTowerId { get; }
        public EnemyId PrimaryEnemyId { get; }
        public PathPosition PrimaryPosition { get; }
        // Для Intrinsic это стихии гибрида, а не наложенные на primary статусы.
        public StatusKind CauseFirstKind { get; }
        public StatusKind CauseSecondKind { get; }
        public float RadiusOrHopRange { get; }
        public int TargetLimit { get; }
    }
}
