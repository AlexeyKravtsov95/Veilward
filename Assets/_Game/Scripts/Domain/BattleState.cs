using System;
using System.Collections.Generic;

namespace Veilward.Domain
{
    public sealed class BattleState
    {
        private readonly List<EnemyState> _enemies;

        public BattleState(
            BattlePhase phase,
            long tick,
            int waveIndex,
            int baseHealth,
            int gold,
            IEnumerable<EnemyState> enemies)
        {
            if (!Enum.IsDefined(typeof(BattlePhase), phase))
            {
                throw new ArgumentOutOfRangeException(nameof(phase));
            }

            if (tick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tick), "Tick cannot be negative.");
            }

            if (waveIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(waveIndex), "Wave index cannot be negative.");
            }

            if (baseHealth < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(baseHealth), "Base health cannot be negative.");
            }

            if (gold < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(gold), "Gold cannot be negative.");
            }

            if (enemies == null)
            {
                throw new ArgumentNullException(nameof(enemies));
            }

            var enemyCopy = new List<EnemyState>(enemies);
            var enemyIds = new HashSet<EnemyId>();
            foreach (var enemy in enemyCopy)
            {
                if (enemy == null)
                {
                    throw new ArgumentException("Enemies cannot contain null entries.", nameof(enemies));
                }

                if (!enemyIds.Add(enemy.Id))
                {
                    throw new ArgumentException("Enemy IDs must be unique.", nameof(enemies));
                }
            }

            Phase = phase;
            Tick = tick;
            WaveIndex = waveIndex;
            BaseHealth = baseHealth;
            Gold = gold;
            _enemies = enemyCopy;
            Enemies = _enemies.AsReadOnly();
        }

        public BattlePhase Phase { get; private set; }
        public long Tick { get; private set; }
        public int WaveIndex { get; }
        public int BaseHealth { get; private set; }
        public int Gold { get; }
        public IReadOnlyList<EnemyState> Enemies { get; }

        internal void SetPhase(BattlePhase phase)
        {
            if (!Enum.IsDefined(typeof(BattlePhase), phase))
            {
                throw new ArgumentOutOfRangeException(nameof(phase));
            }

            Phase = phase;
        }

        internal void AdvanceTick()
        {
            Tick++;
        }

        internal void AddEnemy(EnemyState enemy)
        {
            _enemies.Add(enemy ?? throw new ArgumentNullException(nameof(enemy)));
        }

        internal void RemoveEnemyAt(int index)
        {
            _enemies.RemoveAt(index);
        }

        internal void ApplyLeak(int damage)
        {
            if (damage <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage), "Leak damage must be positive.");
            }

            BaseHealth = Math.Max(0, BaseHealth - damage);
        }

        public static BattleState CreateInitial(int baseHealth, int startingGold)
        {
            if (baseHealth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(baseHealth), "Initial base health must be positive.");
            }

            return new BattleState(BattlePhase.Preparation, 0, 0, baseHealth, startingGold, Array.Empty<EnemyState>());
        }
    }
}
