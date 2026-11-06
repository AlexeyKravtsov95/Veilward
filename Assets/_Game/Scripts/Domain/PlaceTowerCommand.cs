namespace Veilward.Domain
{
    public readonly struct PlaceTowerCommand
    {
        public PlaceTowerCommand(TowerDefinitionId definitionId, GridCoordinate coordinate)
        {
            DefinitionId = definitionId;
            Coordinate = coordinate;
        }

        public TowerDefinitionId DefinitionId { get; }
        public GridCoordinate Coordinate { get; }
    }
}
