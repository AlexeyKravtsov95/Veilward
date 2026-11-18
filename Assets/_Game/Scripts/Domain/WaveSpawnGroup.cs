using System;
namespace Veilward.Domain
{
    public sealed class WaveSpawnGroup
    {
        public WaveSpawnGroup(EnemyDefinition enemy, int count, int firstSpawnTick, int spawnIntervalTicks)
        {
            Enemy = enemy ?? throw new ArgumentNullException(nameof(enemy));
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (firstSpawnTick < 0) throw new ArgumentOutOfRangeException(nameof(firstSpawnTick));
            if (spawnIntervalTicks <= 0) throw new ArgumentOutOfRangeException(nameof(spawnIntervalTicks));
            Count = count; FirstSpawnTick = firstSpawnTick; SpawnIntervalTicks = spawnIntervalTicks;
        }
        public EnemyDefinition Enemy { get; }
        public int Count { get; }
        public int FirstSpawnTick { get; }
        public int SpawnIntervalTicks { get; }
        public long LastSpawnTick => (long)FirstSpawnTick + (long)(Count - 1) * SpawnIntervalTicks;
    }

    public readonly struct WaveSpawn
    {
        internal WaveSpawn(EnemyDefinition enemy, long relativeTick, int groupIndex, int spawnIndex)
        { Enemy = enemy; RelativeTick = relativeTick; GroupIndex = groupIndex; SpawnIndex = spawnIndex; }
        public EnemyDefinition Enemy { get; }
        public long RelativeTick { get; }
        public int GroupIndex { get; }
        public int SpawnIndex { get; }
    }
}
