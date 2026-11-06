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
        public int Health { get; private set; }
        public bool IsAlive => Health > 0;
        public float DistanceAlongPath { get; private set; }

        internal void Advance(float distanceAlongPath)
        {
            if (distanceAlongPath < DistanceAlongPath || float.IsNaN(distanceAlongPath) || float.IsInfinity(distanceAlongPath))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(distanceAlongPath),
                    "New path distance must be finite and not move the enemy backwards.");
            }

            DistanceAlongPath = distanceAlongPath;
        }

        internal bool ApplyDamage(int damage)
        {
            if (damage <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage), "Damage must be positive.");
            }

            if (!IsAlive)
            {
                return false;
            }

            Health = Math.Max(0, Health - damage);
            return Health == 0;
        }

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
