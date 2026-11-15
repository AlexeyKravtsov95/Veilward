using System;

namespace Veilward.Domain
{
    public sealed class TowerDefinition
    {
        public TowerDefinition(
            TowerDefinitionId id,
            int price,
            float range,
            float fireRate,
            int damage,
            StatusEffectDefinition statusEffect = null,
            int armorPenetration = 0,
            ReactionPayload intrinsicReaction = null)
        {
            if (string.IsNullOrWhiteSpace(id.Value))
            {
                throw new ArgumentException("Tower definition ID cannot be empty.", nameof(id));
            }

            if (price < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(price), "Tower price cannot be negative.");
            }

            if (damage <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage), "Damage must be positive.");
            }

            if (armorPenetration < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(armorPenetration), "Armor penetration cannot be negative.");
            }

            if (statusEffect != null && intrinsicReaction != null)
            {
                throw new ArgumentException(
                    "A tower cannot have both a status effect and an intrinsic reaction.", nameof(intrinsicReaction));
            }

            ValidatePositiveFinite(range, nameof(range), "Range");
            ValidatePositiveFinite(fireRate, nameof(fireRate), "Fire rate");
            Id = id;
            Price = price;
            Range = range;
            FireRate = fireRate;
            Damage = damage;
            StatusEffect = statusEffect;
            IntrinsicReaction = intrinsicReaction;
            ArmorPenetration = armorPenetration;

            // Decimal сохраняет midpoint сериализованных FireRate (например, 30 / 0.8 = 37.5).
            var approximateTicks = (double)SimulationTiming.TicksPerSecond / fireRate;
            if (approximateTicks >= int.MaxValue)
            {
                AttackIntervalTicks = int.MaxValue;
            }
            else if (approximateTicks <= 1d)
            {
                AttackIntervalTicks = 1;
            }
            else
            {
                var exactTicks = (decimal)SimulationTiming.TicksPerSecond / (decimal)fireRate;
                var roundedTicks = Math.Round(exactTicks, MidpointRounding.AwayFromZero);
                AttackIntervalTicks = (int)Math.Max(1m, roundedTicks);
            }
        }

        public TowerDefinitionId Id { get; }
        public int Price { get; }
        public float Range { get; }
        public float FireRate { get; }
        public int Damage { get; }
        public StatusEffectDefinition StatusEffect { get; }
        public ReactionPayload IntrinsicReaction { get; }
        public int ArmorPenetration { get; }
        public int AttackIntervalTicks { get; }

        private static void ValidatePositiveFinite(float value, string parameterName, string displayName)
        {
            if (value <= 0f || float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    $"{displayName} must be finite and positive.");
            }
        }
    }
}
