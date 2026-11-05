using System;

namespace Veilward.Domain
{
    public sealed class WaveDefinition
    {
        public WaveDefinition(
            EnemyDefinition enemy,
            int count,
            int firstSpawnTick,
            int spawnIntervalTicks)
        {
            Enemy = enemy ?? throw new ArgumentNullException(nameof(enemy));
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Enemy count must be positive.");
            }

            if (firstSpawnTick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(firstSpawnTick), "First spawn tick cannot be negative.");
            }

            if (spawnIntervalTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(spawnIntervalTicks), "Spawn interval must be positive.");
            }

            Count = count;
            FirstSpawnTick = firstSpawnTick;
            SpawnIntervalTicks = spawnIntervalTicks;
        }

        public EnemyDefinition Enemy { get; }
        public int Count { get; }
        public int FirstSpawnTick { get; }
        public int SpawnIntervalTicks { get; }
    }
}
