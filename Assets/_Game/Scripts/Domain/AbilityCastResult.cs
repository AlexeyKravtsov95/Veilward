namespace Veilward.Domain
{
    public enum AbilityCastFailureReason
    {
        None,
        InvalidPhase,
        AbilityDisabled,
        UnknownAbility,
        StaleCommand,
        CooldownActive,
        InvalidTarget,
        NoTargets,
        IdentifierExhausted
    }

    public readonly struct AbilityCastResult
    {
        private AbilityCastResult(bool succeeded, AbilityCastFailureReason reason,
            long? sequence, int hitCount, long totalAppliedHpLoss)
        {
            Succeeded = succeeded;
            FailureReason = reason;
            CastSequence = sequence;
            HitCount = hitCount;
            TotalAppliedHpLoss = totalAppliedHpLoss;
        }

        public bool Succeeded { get; }
        public AbilityCastFailureReason FailureReason { get; }
        public long? CastSequence { get; }
        public int HitCount { get; }
        public long TotalAppliedHpLoss { get; }
        internal static AbilityCastResult Rejected(AbilityCastFailureReason reason) => new AbilityCastResult(false, reason, null, 0, 0);
        internal static AbilityCastResult Allowed() => new AbilityCastResult(true, AbilityCastFailureReason.None, null, 0, 0);
        internal static AbilityCastResult Cast(long sequence, int count, long loss) => new AbilityCastResult(true, AbilityCastFailureReason.None, sequence, count, loss);
    }
}
