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
