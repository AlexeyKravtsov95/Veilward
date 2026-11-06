namespace Veilward.Domain
{
    public enum PlaceTowerFailureReason
    {
        None = 0,
        InvalidPhase = 1,
        UnknownTowerDefinition = 2,
        OutsideMap = 3,
        Blocked = 4,
        Occupied = 5,
        InsufficientGold = 6
    }
}
