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
            float damage)
        {
            if (string.IsNullOrWhiteSpace(id.Value))
            {
                throw new ArgumentException("Tower definition ID cannot be empty.", nameof(id));
            }

            if (price < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(price), "Tower price cannot be negative.");
            }

            ValidatePositiveFinite(range, nameof(range), "Range");
            ValidatePositiveFinite(fireRate, nameof(fireRate), "Fire rate");
            ValidatePositiveFinite(damage, nameof(damage), "Damage");
            Id = id;
            Price = price;
            Range = range;
            FireRate = fireRate;
            Damage = damage;
        }

        public TowerDefinitionId Id { get; }
        public int Price { get; }
        public float Range { get; }
        public float FireRate { get; }
        public float Damage { get; }

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
