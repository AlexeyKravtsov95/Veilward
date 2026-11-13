using System;

namespace Veilward.Domain
{
    public sealed class TowerState
    {
        public TowerState(TowerId id, TowerDefinition definition, GridCoordinate coordinate)
            : this(id, definition, coordinate, 1, definition?.Price ?? 0, 0)
        {
        }

        private TowerState(
            TowerId id,
            TowerDefinition definition,
            GridCoordinate coordinate,
            int level,
            int investedGold,
            long nextAttackTick)
        {
            if (id.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id), "Tower ID must be positive.");
            }

            if (level <= 0 || investedGold < 0 || nextAttackTick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(level), "Restored tower values are invalid.");
            }

            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Id = id;
            Coordinate = coordinate;
            Level = level;
            InvestedGold = investedGold;
            NextAttackTick = nextAttackTick;
        }

        public TowerId Id { get; }
        public TowerDefinition Definition { get; private set; }
        public GridCoordinate Coordinate { get; }
        public int Level { get; private set; }
        public int InvestedGold { get; private set; }
        public long NextAttackTick { get; private set; }

        internal static TowerState Restore(TowerCheckpoint checkpoint, TowerDefinition definition)
        {
            if (checkpoint == null)
            {
                throw new ArgumentNullException(nameof(checkpoint));
            }

            return new TowerState(
                new TowerId(checkpoint.towerId),
                definition,
                new GridCoordinate(checkpoint.x, checkpoint.y),
                checkpoint.level,
                checkpoint.investedGold,
                checkpoint.nextAttackTick);
        }

        internal void UpgradeTo(TowerDefinition nextLevel)
        {
            Definition = nextLevel ?? throw new ArgumentNullException(nameof(nextLevel));
            Level++;
            InvestedGold = checked(InvestedGold + nextLevel.Price);
        }

        internal void MarkAttacked(long tick)
        {
            NextAttackTick = tick + Definition.AttackIntervalTicks;
        }
    }
}
