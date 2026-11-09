using System;

namespace Veilward.Domain
{
    public sealed class TowerState
    {
        public TowerState(TowerId id, TowerDefinition definition, GridCoordinate coordinate)
        {
            if (id.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id), "Tower ID must be positive.");
            }

            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Id = id;
            Coordinate = coordinate;
            Level = 1;
            InvestedGold = definition.Price;
        }

        public TowerId Id { get; }
        public TowerDefinition Definition { get; private set; }
        public GridCoordinate Coordinate { get; }
        public int Level { get; private set; }
        public int InvestedGold { get; private set; }
        public long NextAttackTick { get; private set; }

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
