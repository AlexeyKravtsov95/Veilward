using System;

namespace Veilward.Domain
{
    public sealed class ReactionPayload
    {
        public ReactionPayload(ReactionKind kind, int damagePercent, float range, int targetLimit)
        {
            if (!Enum.IsDefined(typeof(ReactionKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            if (damagePercent <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damagePercent), "Damage percent must be positive.");
            }

            if (range <= 0f || float.IsNaN(range) || float.IsInfinity(range))
            {
                throw new ArgumentOutOfRangeException(nameof(range), "Reaction range must be finite and positive.");
            }

            // Steam считает primary в общем лимите; Storm считает только дополнительные цели.
            var maximumTargets = kind == ReactionKind.Steam ? 4 : 2;
            if (targetLimit < 1 || targetLimit > maximumTargets)
            {
                throw new ArgumentOutOfRangeException(nameof(targetLimit),
                    $"{kind} target limit must be in the range 1..{maximumTargets}.");
            }

            Kind = kind;
            DamagePercent = damagePercent;
            Range = range;
            TargetLimit = targetLimit;
        }

        public ReactionKind Kind { get; }
        public int DamagePercent { get; }
        public float Range { get; }
        public int TargetLimit { get; }
    }
}
