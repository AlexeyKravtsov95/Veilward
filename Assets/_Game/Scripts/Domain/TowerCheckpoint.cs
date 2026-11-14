using System;

namespace Veilward.Domain
{
    [Serializable]
    public sealed class TowerCheckpoint
    {
        public int towerId;
        public string towerTypeId;
        public int x;
        public int y;
        public int level;
        public int investedGold;
        public long nextAttackTick;

        internal static TowerCheckpoint FromState(TowerState tower)
        {
            return new TowerCheckpoint
            {
                towerId = tower.Id.Value,
                towerTypeId = tower.Definition.Id.Value,
                x = tower.Coordinate.X,
                y = tower.Coordinate.Y,
                level = tower.Level,
                investedGold = tower.InvestedGold,
                nextAttackTick = tower.NextAttackTick
            };
        }
    }
}
