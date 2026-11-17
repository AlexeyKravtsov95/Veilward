namespace Veilward.Domain
{
    public sealed class AbilityState
    {
        internal AbilityState(AbilityDefinition definition)
        {
            RulesId = definition == null ? "ability-disabled-v1" : "rift-seal-v1";
            AbilityId = definition == null ? default : definition.Id;
        }

        public string RulesId { get; }
        public AbilityDefinitionId AbilityId { get; }
        public int CooldownRemainingTicks { get; private set; }
        public long NextCastSequence { get; private set; } = 1;

        internal void AcceptCast(int cooldownTicks)
        {
            CooldownRemainingTicks = cooldownTicks;
            NextCastSequence++;
        }

        internal void AdvanceCombatStep()
        {
            if (CooldownRemainingTicks > 0) CooldownRemainingTicks--;
        }
    }
}
