using System;

namespace Veilward.Domain
{
    public sealed class BattleSimulation : IBattleSimulation
    {
        private readonly PathDefinition _path;
        private readonly WaveDefinition _wave;
        private int _nextEnemyId = 1;
        private int _nextTowerId = 1;
        private int _nextSpawnIndex;

        public BattleSimulation(PathDefinition path, WaveDefinition wave, int baseHealth)
            : this(
                new GridMapDefinition(1, 1, new GridCoordinate(0, 0), 1f),
                path,
                wave,
                new TowerDefinition(new TowerDefinitionId("legacy"), 0, 1f, 1f, 1f),
                baseHealth,
                startingGold: 0)
        {
        }

        public BattleSimulation(
            GridMapDefinition map,
            PathDefinition path,
            WaveDefinition wave,
            TowerDefinition towerDefinition,
            int baseHealth,
            int startingGold)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            _path = path ?? throw new ArgumentNullException(nameof(path));
            _wave = wave ?? throw new ArgumentNullException(nameof(wave));
            TowerDefinition = towerDefinition ?? throw new ArgumentNullException(nameof(towerDefinition));
            Grid = new BattleGrid(map, path);
            State = BattleState.CreateInitial(baseHealth, startingGold);
        }

        public BattleState State { get; }
        public PathDefinition Path => _path;
        public BattleGrid Grid { get; }
        public TowerDefinition TowerDefinition { get; }

        public PlaceTowerResult PlaceTower(PlaceTowerCommand command)
        {
            if (State.Phase != BattlePhase.Preparation)
            {
                return PlaceTowerResult.Failed(PlaceTowerFailureReason.InvalidPhase);
            }

            if (command.DefinitionId != TowerDefinition.Id)
            {
                return PlaceTowerResult.Failed(PlaceTowerFailureReason.UnknownTowerDefinition);
            }

            if (!Grid.Map.Contains(command.Coordinate))
            {
                return PlaceTowerResult.Failed(PlaceTowerFailureReason.OutsideMap);
            }

            var cellState = Grid.GetState(command.Coordinate);
            if (cellState == GridCellState.Blocked)
            {
                return PlaceTowerResult.Failed(PlaceTowerFailureReason.Blocked);
            }

            if (cellState == GridCellState.Occupied)
            {
                return PlaceTowerResult.Failed(PlaceTowerFailureReason.Occupied);
            }

            if (State.Gold < TowerDefinition.Price)
            {
                return PlaceTowerResult.Failed(PlaceTowerFailureReason.InsufficientGold);
            }

            var tower = new TowerState(new TowerId(_nextTowerId), TowerDefinition, command.Coordinate);
            if (!Grid.TryOccupy(command.Coordinate))
            {
                return PlaceTowerResult.Failed(PlaceTowerFailureReason.Occupied);
            }

            _nextTowerId++;
            State.Debit(TowerDefinition.Price);
            State.AddTower(tower);
            return PlaceTowerResult.Succeeded(tower);
        }

        public void StartWave()
        {
            if (State.Phase != BattlePhase.Preparation)
            {
                return;
            }

            State.SetPhase(BattlePhase.Combat);
            SpawnDueEnemies();
        }

        public void Step()
        {
            if (State.Phase != BattlePhase.Combat)
            {
                return;
            }

            State.AdvanceTick();
            SpawnDueEnemies();

            var index = 0;
            while (index < State.Enemies.Count)
            {
                var enemy = State.Enemies[index];
                var distance = Math.Min(
                    _path.TotalLength,
                    enemy.DistanceAlongPath + enemy.Definition.Speed / SimulationTiming.TicksPerSecond);
                if (distance >= _path.TotalLength)
                {
                    State.RemoveEnemyAt(index);
                    State.ApplyLeak(enemy.Definition.LeakDamage);
                    continue;
                }

                enemy.Advance(distance);
                index++;
            }

            if (State.BaseHealth == 0)
            {
                State.SetPhase(BattlePhase.Defeat);
            }
            else if (_nextSpawnIndex == _wave.Count && State.Enemies.Count == 0)
            {
                State.SetPhase(BattlePhase.WaveComplete);
            }
        }

        private void SpawnDueEnemies()
        {
            while (_nextSpawnIndex < _wave.Count && NextSpawnTick() <= State.Tick)
            {
                State.AddEnemy(EnemyState.CreateInitial(new EnemyId(_nextEnemyId), _wave.Enemy));
                _nextEnemyId++;
                _nextSpawnIndex++;
            }
        }

        private long NextSpawnTick()
        {
            return (long)_wave.FirstSpawnTick + (long)_nextSpawnIndex * _wave.SpawnIntervalTicks;
        }
    }
}
