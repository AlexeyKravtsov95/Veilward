using System;
using System.Collections.Generic;

namespace Veilward.Domain
{
    public sealed class BattleSimulation : IBattleSimulation
    {
        private readonly PathDefinition _path;
        private readonly WaveDefinition _wave;
        private readonly List<AttackEvent> _attackEvents = new List<AttackEvent>();
        private int _nextEnemyId = 1;
        private int _nextTowerId = 1;
        private int _nextSpawnIndex;

        public BattleSimulation(PathDefinition path, WaveDefinition wave, int baseHealth)
            : this(
                new GridMapDefinition(1, 1, new GridCoordinate(0, 0), 1f),
                path,
                wave,
                new TowerDefinition(new TowerDefinitionId("legacy"), 0, 1f, 1f, 1),
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
            AttackEvents = _attackEvents.AsReadOnly();
        }

        public BattleState State { get; }
        public PathDefinition Path => _path;
        public BattleGrid Grid { get; }
        public TowerDefinition TowerDefinition { get; }
        public IReadOnlyList<AttackEvent> AttackEvents { get; }

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
            MoveEnemiesAndApplyLeaks();
            ProcessTowerAttacks();
            State.RemoveDefeatedEnemies();

            if (State.BaseHealth == 0)
            {
                State.SetPhase(BattlePhase.Defeat);
            }
            else if (_nextSpawnIndex == _wave.Count && State.Enemies.Count == 0)
            {
                State.SetPhase(BattlePhase.WaveComplete);
            }
        }

        public void ClearAttackEvents()
        {
            _attackEvents.Clear();
        }

        private void MoveEnemiesAndApplyLeaks()
        {
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
        }

        private void ProcessTowerAttacks()
        {
            var towers = State.Towers;
            for (var towerIndex = 0; towerIndex < towers.Count; towerIndex++)
            {
                var tower = towers[towerIndex];
                if (State.Tick < tower.NextAttackTick)
                {
                    continue;
                }

                var center = Grid.Map.GetCellCenter(tower.Coordinate);
                var target = EnemyTargeting.SelectFirst(center, tower.Definition.Range, State.Enemies, _path);
                if (target == null)
                {
                    continue;
                }

                var targetPosition = _path.GetPosition(target.DistanceAlongPath);
                var wasLethal = target.ApplyDamage(tower.Definition.Damage);
                tower.MarkAttacked(State.Tick);
                _attackEvents.Add(new AttackEvent(
                    tower.Id,
                    target.Id,
                    targetPosition,
                    State.Tick,
                    tower.Definition.Damage,
                    wasLethal));
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
