using System;

namespace Veilward.Domain
{
    public sealed class EnemyDefinition
    {
        public EnemyDefinition(
            EnemyDefinitionId id,
            int maxHealth,
            float speed,
            int leakDamage,
            int reward)
        {
            if (string.IsNullOrWhiteSpace(id.Value))
            {
                throw new ArgumentException("Enemy definition ID cannot be empty.", nameof(id));
            }

            if (maxHealth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHealth), "Maximum health must be positive.");
            }

            if (speed <= 0f || float.IsNaN(speed) || float.IsInfinity(speed))
            {
                throw new ArgumentOutOfRangeException(nameof(speed), "Speed must be finite and positive.");
            }

            if (leakDamage <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(leakDamage), "Leak damage must be positive.");
            }

            if (reward < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reward), "Reward cannot be negative.");
            }

            Id = id;
            MaxHealth = maxHealth;
            Speed = speed;
            LeakDamage = leakDamage;
            Reward = reward;
        }

        public EnemyDefinitionId Id { get; }
        public int MaxHealth { get; }
        public float Speed { get; }
        public int LeakDamage { get; }
        public int Reward { get; }
    }
}
