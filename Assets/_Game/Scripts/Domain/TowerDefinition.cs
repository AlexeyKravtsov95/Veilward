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
            int damage)
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

            ValidatePositiveFinite(range, nameof(range), "Range");
            ValidatePositiveFinite(fireRate, nameof(fireRate), "Fire rate");
            Id = id;
            Price = price;
            Range = range;
            FireRate = fireRate;
            Damage = damage;

            // FireRate is attacks/second. Rounding picks the nearest tick; one tick is the minimum interval.
            var roundedTicks = Math.Round(
                (double)SimulationTiming.TicksPerSecond / fireRate,
                MidpointRounding.AwayFromZero);
            AttackIntervalTicks = (int)Math.Min(int.MaxValue, Math.Max(1d, roundedTicks));
        }

        public TowerDefinitionId Id { get; }
        public int Price { get; }
        public float Range { get; }
        public float FireRate { get; }
        public int Damage { get; }
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
