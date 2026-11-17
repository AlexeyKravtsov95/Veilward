namespace Veilward.Domain
{
    // Immutable Domain snapshot; wire DTO находится в Infrastructure, без Unity-зависимостей здесь.
    public sealed class AbilityCheckpoint
    {
        public AbilityCheckpoint(string rulesId, string abilityId, int cooldownRemainingTicks, long nextCastSequence)
        {
            RulesId = rulesId;
            AbilityId = abilityId;
            CooldownRemainingTicks = cooldownRemainingTicks;
            NextCastSequence = nextCastSequence;
        }

        public string RulesId { get; }
        public string AbilityId { get; }
        public int CooldownRemainingTicks { get; }
        public long NextCastSequence { get; }
        public AbilityCheckpoint DeepCopy() => new AbilityCheckpoint(RulesId, AbilityId, CooldownRemainingTicks, NextCastSequence);
        public static AbilityCheckpoint Disabled() => new AbilityCheckpoint("ability-disabled-v1", "none", 0, 1);

        internal static AbilityCheckpoint FromState(AbilityState state) => new AbilityCheckpoint(state.RulesId,
            state.RulesId == "ability-disabled-v1" ? "none" : state.AbilityId.Value,
            state.CooldownRemainingTicks, state.NextCastSequence);

        internal bool IsValid(bool enabled, bool initialPending)
        {
            if (!enabled) return RulesId == "ability-disabled-v1" && AbilityId == "none" &&
                CooldownRemainingTicks == 0 && NextCastSequence == 1;
            return RulesId == "rift-seal-v1" && AbilityId == "rift-seal" &&
                CooldownRemainingTicks >= 0 && CooldownRemainingTicks <= 600 && NextCastSequence >= 1 &&
                (CooldownRemainingTicks == 0 || NextCastSequence >= 2) &&
                (!initialPending || CooldownRemainingTicks == 0 && NextCastSequence == 1);
        }
    }
}
