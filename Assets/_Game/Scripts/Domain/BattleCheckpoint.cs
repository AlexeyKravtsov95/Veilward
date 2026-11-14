using System;

namespace Veilward.Domain
{
    [Serializable]
    public sealed class BattleCheckpoint
    {
        public BattlePhase phase;
        public int waveIndex;
        public long tick;
        public int baseHealth;
        public int gold;
        public int kills;
        public int nextEnemyId;
        public int nextTowerId;
        public TowerCheckpoint[] towers;

        public BattleCheckpoint DeepCopyForMigration()
        {
            var copy = new BattleCheckpoint
            {
                phase = phase,
                waveIndex = waveIndex,
                tick = tick,
                baseHealth = baseHealth,
                gold = gold,
                kills = kills,
                nextEnemyId = nextEnemyId,
                nextTowerId = nextTowerId,
                towers = towers == null ? null : new TowerCheckpoint[towers.Length]
            };
            if (towers == null)
            {
                return copy;
            }

            for (var index = 0; index < towers.Length; index++)
            {
                var tower = towers[index];
                if (tower == null)
                {
                    continue;
                }

                copy.towers[index] = new TowerCheckpoint
                {
                    towerId = tower.towerId,
                    towerTypeId = "test-tower",
                    x = tower.x,
                    y = tower.y,
                    level = tower.level,
                    investedGold = tower.investedGold,
                    nextAttackTick = tower.nextAttackTick
                };
            }

            return copy;
        }

        internal static BattleCheckpoint FromState(BattleState state, int nextEnemyId, int nextTowerId)
        {
            var checkpoint = new BattleCheckpoint
            {
                phase = state.Phase,
                waveIndex = state.WaveIndex,
                tick = state.Tick,
                baseHealth = state.BaseHealth,
                gold = state.Gold,
                kills = state.Kills,
                nextEnemyId = nextEnemyId,
                nextTowerId = nextTowerId,
                towers = new TowerCheckpoint[state.Towers.Count]
            };
            for (var index = 0; index < state.Towers.Count; index++)
            {
                checkpoint.towers[index] = TowerCheckpoint.FromState(state.Towers[index]);
            }

            return checkpoint;
        }
    }
}
