namespace Veilward.Domain
{
    public readonly struct CastAbilityCommand
    {
        public CastAbilityCommand(AbilityDefinitionId abilityId, PathPosition center,
            int expectedWaveIndex, long expectedTick, long expectedCastSequence)
        {
            AbilityId = abilityId;
            Center = center;
            ExpectedWaveIndex = expectedWaveIndex;
            ExpectedTick = expectedTick;
            ExpectedCastSequence = expectedCastSequence;
        }

        public AbilityDefinitionId AbilityId { get; }
        public PathPosition Center { get; }
        public int ExpectedWaveIndex { get; }
        public long ExpectedTick { get; }
        public long ExpectedCastSequence { get; }
    }
}
