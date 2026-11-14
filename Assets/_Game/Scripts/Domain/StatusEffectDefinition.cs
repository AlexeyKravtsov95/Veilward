using System;

namespace Veilward.Domain
{
    public sealed class StatusEffectDefinition
    {
        public StatusEffectDefinition(
            StatusKind kind,
            int power,
            int durationTicks,
            int damageIntervalTicks)
        {
            if (!Enum.IsDefined(typeof(StatusKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            if (power <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(power), "Status power must be positive.");
            }

            if (kind == StatusKind.Wet && power > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(power), "Wet power must be in the range 1..100.");
            }

            if (durationTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(durationTicks), "Status duration must be positive.");
            }

            if (kind == StatusKind.Burn && damageIntervalTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(damageIntervalTicks), "Burn requires a positive damage interval.");
            }

            if (kind != StatusKind.Burn && damageIntervalTicks != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(damageIntervalTicks), "Only Burn can have a damage interval.");
            }

            Kind = kind;
            Power = power;
            DurationTicks = durationTicks;
            DamageIntervalTicks = damageIntervalTicks;
        }

        public StatusKind Kind { get; }
        public int Power { get; }
        public int DurationTicks { get; }
        public int DamageIntervalTicks { get; }
    }
}
