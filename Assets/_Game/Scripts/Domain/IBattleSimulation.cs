namespace Veilward.Domain
{
    public interface IBattleSimulation
    {
        BattleState State { get; }

        void Step();
    }
}
