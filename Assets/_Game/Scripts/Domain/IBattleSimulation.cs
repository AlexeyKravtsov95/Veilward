namespace Veilward.Domain
{
    public interface IBattleSimulation
    {
        BattleState State { get; }

        PlaceTowerResult PlaceTower(PlaceTowerCommand command);

        void Step();
    }
}
