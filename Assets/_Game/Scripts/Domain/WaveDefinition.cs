using System;
using System.Collections.Generic;
namespace Veilward.Domain
{
    public sealed class WaveDefinition
    {
        // Ограничение памяти расписания до выделения массива, не gameplay balance.
        public const int MaxSpawnCount = 65536;
        public WaveDefinition(EnemyDefinition enemy, int count, int firstSpawnTick, int spawnIntervalTicks)
            : this(new[] { new WaveSpawnGroup(enemy, count, firstSpawnTick, spawnIntervalTicks) }) { }

        public WaveDefinition(IReadOnlyList<WaveSpawnGroup> groups)
        {
            if (groups == null) throw new ArgumentNullException(nameof(groups));
            if (groups.Count == 0 || groups.Count > MaxSpawnCount) throw new ArgumentException("Invalid group budget.", nameof(groups));
            var copy = new WaveSpawnGroup[groups.Count];
            long total = 0;
            for (var g = 0; g < groups.Count; g++)
            {
                copy[g] = groups[g] ?? throw new ArgumentException("Null spawn group.", nameof(groups));
                total += copy[g].Count;
                if (total > MaxSpawnCount) throw new ArgumentException("Spawn schedule exceeds bounded capacity.", nameof(groups));
            }
            var schedule = new WaveSpawn[(int)total];
            var cursor = 0;
            for (var g = 0; g < copy.Length; g++)
                for (var s = 0; s < copy[g].Count; s++)
                    schedule[cursor++] = new WaveSpawn(copy[g].Enemy,
                        (long)copy[g].FirstSpawnTick + (long)s * copy[g].SpawnIntervalTicks, g, s);
            Array.Sort(schedule, (a, b) => {
                var order = a.RelativeTick.CompareTo(b.RelativeTick);
                if (order == 0) order = a.GroupIndex.CompareTo(b.GroupIndex);
                return order == 0 ? a.SpawnIndex.CompareTo(b.SpawnIndex) : order;
            });
            Groups = Array.AsReadOnly(copy); Schedule = Array.AsReadOnly(schedule);
        }
        public IReadOnlyList<WaveSpawnGroup> Groups { get; }
        public IReadOnlyList<WaveSpawn> Schedule { get; }
        public int Count => Schedule.Count;
        public long LastSpawnTick => Schedule[Count - 1].RelativeTick;
        // Совместимые single-group accessors; runtime читает Schedule.
        public EnemyDefinition Enemy => Groups[0].Enemy;
        public int FirstSpawnTick => Groups[0].FirstSpawnTick;
        public int SpawnIntervalTicks => Groups[0].SpawnIntervalTicks;
    }
}
