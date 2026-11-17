using System;

namespace Veilward.Domain
{
    public sealed class AbilityDefinition
    {
        public AbilityDefinition(AbilityDefinitionId id, int damage, float radius, int cooldownTicks)
        {
            if (id != AbilityDefinitionId.RiftSeal) throw new ArgumentException("Unknown ability ID.", nameof(id));
            if (damage <= 0) throw new ArgumentOutOfRangeException(nameof(damage));
            if (radius <= 0 || float.IsNaN(radius) || float.IsInfinity(radius))
                throw new ArgumentOutOfRangeException(nameof(radius));
            if (cooldownTicks <= 0 || cooldownTicks > 600) throw new ArgumentOutOfRangeException(nameof(cooldownTicks));
            Id = id;
            Damage = damage;
            Radius = radius;
            CooldownTicks = cooldownTicks;
        }

        public static AbilityDefinition RiftSeal { get; } = new AbilityDefinition(AbilityDefinitionId.RiftSeal, 120, 1.5f, 600);
        public AbilityDefinitionId Id { get; }
        public int Damage { get; }
        public float Radius { get; }
        public int CooldownTicks { get; }
    }
}
