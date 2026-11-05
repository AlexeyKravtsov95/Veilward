using System;

namespace Veilward.Domain
{
    public sealed class TowerState
    {
        public TowerState(TowerId id, TowerDefinition definition, GridCoordinate coordinate)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Id = id;
            Coordinate = coordinate;
        }

        public TowerId Id { get; }
        public TowerDefinition Definition { get; }
        public GridCoordinate Coordinate { get; }
    }
}
