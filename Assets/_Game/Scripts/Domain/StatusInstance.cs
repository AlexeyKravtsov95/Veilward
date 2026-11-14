using System;

namespace Veilward.Domain
{
    public sealed class StatusInstance
    {
        internal StatusInstance(
            StatusEffectDefinition definition,
            TowerId sourceTowerId,
            long currentTick)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (sourceTowerId.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceTowerId));
            }

            if (currentTick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(currentTick));
            }

            Kind = definition.Kind;
            Power = definition.Power;
            RemainingTicks = definition.DurationTicks;
            DamageIntervalTicks = definition.DamageIntervalTicks;
            SourceTowerId = sourceTowerId;
            NextDamageTick = Kind == StatusKind.Burn
                ? currentTick + DamageIntervalTicks
                : 0;
        }

        public StatusKind Kind { get; }
        public int Power { get; private set; }
        public int RemainingTicks { get; private set; }
        public long NextDamageTick { get; private set; }
        public TowerId SourceTowerId { get; private set; }
        public int DamageIntervalTicks { get; private set; }

        internal void Reapply(StatusEffectDefinition incoming, TowerId sourceTowerId)
        {
            if (incoming == null)
            {
                throw new ArgumentNullException(nameof(incoming));
            }

            if (incoming.Kind != Kind)
            {
                throw new ArgumentException("Status kind cannot change during reapplication.", nameof(incoming));
            }

            RemainingTicks = Math.Max(RemainingTicks, incoming.DurationTicks);
            if (incoming.Power >= Power)
            {
                Power = incoming.Power;
                DamageIntervalTicks = incoming.DamageIntervalTicks;
                SourceTowerId = sourceTowerId;
            }
        }

        internal void ScheduleNextDamage(long currentTick)
        {
            NextDamageTick = currentTick + DamageIntervalTicks;
        }

        internal bool AdvanceTimer()
        {
            RemainingTicks--;
            return RemainingTicks == 0;
        }
    }
}
