using System;

namespace Veilward.Domain
{
    public sealed class ReactionDefinition
    {
        public ReactionDefinition(ReactionPayload payload, int cooldownTicks, int priority)
        {
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
            if (cooldownTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cooldownTicks), "Reaction cooldown must be positive.");
            }

            CooldownTicks = cooldownTicks;
            Priority = priority;
        }

        public ReactionPayload Payload { get; }
        public int CooldownTicks { get; }
        public int Priority { get; }
    }
}
