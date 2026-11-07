using System;
using System.Collections.Generic;

namespace Veilward.Domain
{
    public sealed class BattleSimulation : IBattleSimulation
    {
        private readonly PathDefinition _path;
        private readonly WaveDefinition[] _waves;
        private readonly int _waveCompletionBonus;
        private readonly bool _legacySingleWave;
        private readonly List<AttackEvent> _attackEvents = new List<AttackEvent>();
        private int _nextEnemyId = 1;
        private int _nextTowerId = 1;
        private int _nextSpawnIndex;
        private long _waveStartTick;

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
            : this(
                map,
                path,
                new[] { wave ?? throw new ArgumentNullException(nameof(wave)) },
                towerDefinition,
                baseHealth,
                startingGold,
                waveCompletionBonus: 0,
                legacySingleWave: true)
        {
        }

        public BattleSimulation(
            GridMapDefinition map,
            PathDefinition path,
            IReadOnlyList<WaveDefinition> waves,
            TowerDefinition towerDefinition,
            int baseHealth,
            int startingGold,
            int waveCompletionBonus)
            : this(
                map,
                path,
                waves,
                towerDefinition,
                baseHealth,
                startingGold,
                waveCompletionBonus,
                legacySingleWave: false)
        {
        }

        private BattleSimulation(
            GridMapDefinition map,
            PathDefinition path,
            IReadOnlyList<WaveDefinition> waves,
            TowerDefinition towerDefinition,
            int baseHealth,
            int startingGold,
            int waveCompletionBonus,
            bool legacySingleWave)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (waves == null)
            {
                throw new ArgumentNullException(nameof(waves));
            }

            if (waves.Count == 0)
            {
                throw new ArgumentException("At least one wave is required.", nameof(waves));
            }

            if (waveCompletionBonus < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(waveCompletionBonus),
                    "Wave completion bonus cannot be negative.");
            }

            _path = path ?? throw new ArgumentNullException(nameof(path));
            TowerDefinition = towerDefinition ?? throw new ArgumentNullException(nameof(towerDefinition));
            _waves = new WaveDefinition[waves.Count];
            for (var index = 0; index < waves.Count; index++)
            {
                _waves[index] = waves[index] ?? throw new ArgumentException(
                    "Waves cannot contain null entries.",
                    nameof(waves));
            }

            _waveCompletionBonus = waveCompletionBonus;
            _legacySingleWave = legacySingleWave;
            Grid = new BattleGrid(map, path);
            State = BattleState.CreateInitial(baseHealth, startingGold, _waves.Length);
            AttackEvents = _attackEvents.AsReadOnly();
        }

        public BattleState State { get; }
        public PathDefinition Path => _path;
        public BattleGrid Grid { get; }
        public TowerDefinition TowerDefinition { get; }
        public IReadOnlyList<AttackEvent> AttackEvents { get; }
        private WaveDefinition CurrentWave => _waves[State.WaveIndex];

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

        public WaveTransitionResult StartWave()
        {
            if (State.Phase != BattlePhase.Preparation)
            {
                return WaveTransitionResult.RejectedInvalidPhase;
            }

            _waveStartTick = State.Tick;
            _nextSpawnIndex = 0;
            State.SetPhase(BattlePhase.Combat);
            SpawnDueEnemies();
            return WaveTransitionResult.Started;
        }

        public WaveTransitionResult ContinueToNextWave()
        {
            if (State.Phase != BattlePhase.WaveComplete)
            {
                return WaveTransitionResult.RejectedInvalidPhase;
            }

            if (State.WaveIndex + 1 >= State.WaveCount)
            {
                return WaveTransitionResult.RejectedNoNextWave;
            }

            State.AdvanceWave();
            State.SetPhase(BattlePhase.Preparation);
            return WaveTransitionResult.Continued;
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
            else if (_nextSpawnIndex == CurrentWave.Count && State.Enemies.Count == 0)
            {
                State.Credit(_waveCompletionBonus);
                State.SetPhase(
                    !_legacySingleWave && State.WaveIndex == State.WaveCount - 1
                        ? BattlePhase.Victory
                        : BattlePhase.WaveComplete);
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
            while (_nextSpawnIndex < CurrentWave.Count && NextSpawnTick() <= State.Tick)
            {
                State.AddEnemy(EnemyState.CreateInitial(new EnemyId(_nextEnemyId), CurrentWave.Enemy));
                _nextEnemyId++;
                _nextSpawnIndex++;
            }
        }

        private long NextSpawnTick()
        {
            return _waveStartTick + (long)CurrentWave.FirstSpawnTick +
                   (long)_nextSpawnIndex * CurrentWave.SpawnIntervalTicks;
        }
    }
}
