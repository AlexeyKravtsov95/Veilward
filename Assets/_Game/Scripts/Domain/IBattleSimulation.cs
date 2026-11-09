namespace Veilward.Domain
{
    public interface IBattleSimulation
    {
        BattleState State { get; }

        PlaceTowerResult PlaceTower(PlaceTowerCommand command);

        TowerOperationResult UpgradeTower(TowerId towerId);

        TowerOperationResult SellTower(TowerId towerId);

        WaveTransitionResult StartWave();

        WaveTransitionResult ContinueToNextWave();

        void Step();
    }
}
