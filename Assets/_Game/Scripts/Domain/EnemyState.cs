using System;

namespace Veilward.Domain
{
    public sealed class EnemyState
    {
        public EnemyState(EnemyId id, EnemyDefinition definition, int health, float distanceAlongPath)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));

            if (id.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id), "Enemy ID must be positive.");
            }

            if (health < 0 || health > definition.MaxHealth)
            {
                throw new ArgumentOutOfRangeException(nameof(health), "Health must be between zero and maximum health.");
            }

            if (distanceAlongPath < 0f || float.IsNaN(distanceAlongPath) || float.IsInfinity(distanceAlongPath))
            {
                throw new ArgumentOutOfRangeException(nameof(distanceAlongPath), "Path distance must be finite and non-negative.");
            }

            Id = id;
            Health = health;
            DistanceAlongPath = distanceAlongPath;
        }

        public EnemyId Id { get; }
        public EnemyDefinition Definition { get; }
        public int Health { get; }
        public float DistanceAlongPath { get; }

        public static EnemyState CreateInitial(EnemyId id, EnemyDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            return new EnemyState(id, definition, definition.MaxHealth, 0f);
        }
    }
}
