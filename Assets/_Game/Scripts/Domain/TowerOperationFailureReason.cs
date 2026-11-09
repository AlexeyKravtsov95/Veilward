namespace Veilward.Domain
{
    public enum TowerOperationFailureReason
    {
        None = 0,
        InvalidPhase = 1,
        TowerNotFound = 2,
        MaxLevel = 3,
        InsufficientGold = 4,
        InvestmentOverflow = 5,
        InvalidGridState = 6,
        ConfirmationRequired = 7
    }
}
