namespace Veilward.Domain
{
    public readonly struct AbilityCastEvent
    {
        public AbilityCastEvent(long tick, int sequenceInTick, AbilityDefinitionId abilityId,
            long castSequence, PathPosition center, float radius, int hitCount)
        {
            Tick = tick;
            SequenceInTick = sequenceInTick;
            AbilityId = abilityId;
            CastSequence = castSequence;
            Center = center;
            Radius = radius;
            HitCount = hitCount;
        }

        public long Tick { get; }
        public int SequenceInTick { get; }
        public AbilityDefinitionId AbilityId { get; }
        public long CastSequence { get; }
        public PathPosition Center { get; }
        public float Radius { get; }
        public int HitCount { get; }
    }
}
