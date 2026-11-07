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
        }

        public TowerId Id { get; }
        public TowerDefinition Definition { get; }
        public GridCoordinate Coordinate { get; }
        public long NextAttackTick { get; private set; }

        internal void MarkAttacked(long tick)
        {
            NextAttackTick = tick + Definition.AttackIntervalTicks;
        }
    }
}
