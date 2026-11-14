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
        private readonly int _initialBaseHealth;
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
                wave,
                new[] { towerDefinition ?? throw new ArgumentNullException(nameof(towerDefinition)) },
                baseHealth,
                startingGold)
        {
        }

        public BattleSimulation(
            GridMapDefinition map,
            PathDefinition path,
            WaveDefinition wave,
            IReadOnlyList<TowerDefinition> towerLevels,
            int baseHealth,
            int startingGold)
            : this(
                map,
                path,
                new[] { wave ?? throw new ArgumentNullException(nameof(wave)) },
                towerLevels,
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
                new[] { towerDefinition ?? throw new ArgumentNullException(nameof(towerDefinition)) },
                baseHealth,
                startingGold,
                waveCompletionBonus)
        {
        }

        public BattleSimulation(
            GridMapDefinition map,
            PathDefinition path,
            IReadOnlyList<WaveDefinition> waves,
            IReadOnlyList<TowerDefinition> towerLevels,
            int baseHealth,
            int startingGold,
            int waveCompletionBonus)
            : this(
                map,
                path,
                waves,
                towerLevels,
                baseHealth,
                startingGold,
                waveCompletionBonus,
                legacySingleWave: false)
        {
        }

        public BattleSimulation(
            GridMapDefinition map,
            PathDefinition path,
            WaveDefinition wave,
            TowerCatalog towerCatalog,
            int baseHealth,
            int startingGold)
            : this(
                map,
                path,
                new[] { wave ?? throw new ArgumentNullException(nameof(wave)) },
                towerCatalog,
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
            TowerCatalog towerCatalog,
            int baseHealth,
            int startingGold,
            int waveCompletionBonus)
            : this(
                map,
                path,
                waves,
                towerCatalog,
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
            IReadOnlyList<TowerDefinition> towerLevels,
            int baseHealth,
            int startingGold,
            int waveCompletionBonus,
            bool legacySingleWave)
            : this(
                map,
                path,
                waves,
                CreateCompatibilityCatalog(towerLevels),
                baseHealth,
                startingGold,
                waveCompletionBonus,
                legacySingleWave)
        {
        }

        private BattleSimulation(
            GridMapDefinition map,
            PathDefinition path,
            IReadOnlyList<WaveDefinition> waves,
            TowerCatalog towerCatalog,
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
            TowerCatalog = towerCatalog ?? throw new ArgumentNullException(nameof(towerCatalog));
            TowerLevels = towerCatalog.Archetypes[0].Levels;
            _waves = new WaveDefinition[waves.Count];
            for (var index = 0; index < waves.Count; index++)
            {
                _waves[index] = waves[index] ?? throw new ArgumentException(
                    "Waves cannot contain null entries.",
                    nameof(waves));
            }

            _waveCompletionBonus = waveCompletionBonus;
            _legacySingleWave = legacySingleWave;
            _initialBaseHealth = baseHealth;
            Grid = new BattleGrid(map, path);
            State = BattleState.CreateInitial(baseHealth, startingGold, _waves.Length);
            AttackEvents = _attackEvents.AsReadOnly();
        }

        public BattleState State { get; private set; }
        public PathDefinition Path => _path;
        public BattleGrid Grid { get; private set; }
        public TowerCatalog TowerCatalog { get; }
        public IReadOnlyList<TowerDefinition> TowerLevels { get; }
        public TowerDefinition TowerDefinition => TowerLevels[0];
        public IReadOnlyList<AttackEvent> AttackEvents { get; }
        private WaveDefinition CurrentWave => _waves[State.WaveIndex];

        public BattleCheckpoint ExportCheckpoint()
        {
            switch (State.Phase)
            {
                case BattlePhase.Preparation:
                case BattlePhase.WaveComplete:
                case BattlePhase.Victory:
                case BattlePhase.Defeat:
                    return BattleCheckpoint.FromState(State, _nextEnemyId, _nextTowerId);
                default:
                    throw new InvalidOperationException("Only stable battle phases can be exported.");
            }
        }

        public bool TryValidateCheckpoint(BattleCheckpoint checkpoint, out string error)
        {
            return TryBuildRestoredState(checkpoint, out _, out _, out error);
        }

        public void RestoreCheckpoint(BattleCheckpoint checkpoint)
        {
            if (!TryBuildRestoredState(checkpoint, out var restoredState, out var restoredGrid, out var error))
            {
                throw new ArgumentException(error, nameof(checkpoint));
            }

            State = restoredState;
            Grid = restoredGrid;
            _nextEnemyId = checkpoint.nextEnemyId;
            _nextTowerId = checkpoint.nextTowerId;
            _nextSpawnIndex = 0;
            _waveStartTick = checkpoint.tick;
            _attackEvents.Clear();
        }

        private bool TryBuildRestoredState(
            BattleCheckpoint checkpoint,
            out BattleState restoredState,
            out BattleGrid restoredGrid,
            out string error)
        {
            restoredState = null;
            restoredGrid = null;
            error = null;
            if (checkpoint == null)
            {
                error = "Checkpoint is required.";
                return false;
            }

            if (checkpoint.tick < 0 || checkpoint.baseHealth < 0 ||
                checkpoint.baseHealth > _initialBaseHealth || checkpoint.gold < 0 || checkpoint.kills < 0)
            {
                error = "Checkpoint scalar values are invalid.";
                return false;
            }

            if (checkpoint.waveIndex < 0 || checkpoint.waveIndex >= _waves.Length)
            {
                error = "Checkpoint wave index is invalid.";
                return false;
            }

            if (checkpoint.nextEnemyId <= 0 || checkpoint.nextEnemyId >= int.MaxValue ||
                checkpoint.nextTowerId <= 0 || checkpoint.nextTowerId >= int.MaxValue)
            {
                error = "Checkpoint next IDs must be positive and below the exhaustion sentinel.";
                return false;
            }

            switch (checkpoint.phase)
            {
                case BattlePhase.Preparation:
                case BattlePhase.WaveComplete:
                case BattlePhase.Victory:
                    if (checkpoint.baseHealth == 0)
                    {
                        error = "A live checkpoint must have positive base health.";
                        return false;
                    }

                    break;
                case BattlePhase.Defeat:
                    if (checkpoint.baseHealth != 0)
                    {
                        error = "A defeated checkpoint must have zero base health.";
                        return false;
                    }

                    break;
                default:
                    error = "Checkpoint phase is not restorable.";
                    return false;
            }

            if (checkpoint.phase == BattlePhase.Victory && checkpoint.waveIndex != _waves.Length - 1)
            {
                error = "Victory is only valid on the final wave.";
                return false;
            }

            if (!_legacySingleWave && checkpoint.phase == BattlePhase.WaveComplete &&
                checkpoint.waveIndex == _waves.Length - 1)
            {
                error = "The final non-legacy wave must end in Victory.";
                return false;
            }

            if (checkpoint.towers == null)
            {
                error = "Checkpoint towers are required.";
                return false;
            }

            var towers = new List<TowerState>(checkpoint.towers.Length);
            var towerIds = new HashSet<int>();
            var coordinates = new HashSet<GridCoordinate>();
            var grid = new BattleGrid(Grid.Map, _path);
            var maximumTowerId = 0;
            for (var index = 0; index < checkpoint.towers.Length; index++)
            {
                var towerCheckpoint = checkpoint.towers[index];
                if (towerCheckpoint == null || towerCheckpoint.towerId <= 0 ||
                    towerCheckpoint.investedGold < 0 || towerCheckpoint.nextAttackTick < 0)
                {
                    error = "Checkpoint tower values are invalid.";
                    return false;
                }

                if (!towerIds.Add(towerCheckpoint.towerId))
                {
                    error = "Checkpoint tower IDs must be unique.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(towerCheckpoint.towerTypeId) ||
                    !TowerCatalog.TryGet(
                        new TowerDefinitionId(towerCheckpoint.towerTypeId),
                        out var archetype))
                {
                    error = "Checkpoint tower type is missing or unknown.";
                    return false;
                }

                var levels = archetype.Levels;
                if (towerCheckpoint.level <= 0 || towerCheckpoint.level > levels.Count)
                {
                    error = "Checkpoint tower level is invalid.";
                    return false;
                }

                long investedGold = 0;
                for (var levelIndex = 0; levelIndex < towerCheckpoint.level; levelIndex++)
                {
                    investedGold += levels[levelIndex].Price;
                }

                if (towerCheckpoint.investedGold != investedGold)
                {
                    error = "Checkpoint tower investment does not match configured prices.";
                    return false;
                }

                var coordinate = new GridCoordinate(towerCheckpoint.x, towerCheckpoint.y);
                if (!coordinates.Add(coordinate))
                {
                    error = "Checkpoint tower cells must be unique.";
                    return false;
                }

                if (!Grid.Map.Contains(coordinate) || grid.GetState(coordinate) != GridCellState.Available)
                {
                    error = "Checkpoint tower cell is not buildable.";
                    return false;
                }

                var tower = TowerState.Restore(towerCheckpoint, levels[towerCheckpoint.level - 1]);
                if (!grid.TryOccupy(coordinate))
                {
                    error = "Checkpoint tower could not occupy its cell.";
                    return false;
                }

                towers.Add(tower);
                maximumTowerId = Math.Max(maximumTowerId, towerCheckpoint.towerId);
            }

            if (checkpoint.nextTowerId <= maximumTowerId)
            {
                error = "Checkpoint next tower ID would reuse an existing ID.";
                return false;
            }

            try
            {
                restoredState = new BattleState(
                    checkpoint.phase,
                    checkpoint.tick,
                    checkpoint.waveIndex,
                    checkpoint.baseHealth,
                    checkpoint.gold,
                    Array.Empty<EnemyState>(),
                    towers,
                    _waves.Length,
                    checkpoint.kills);
            }
            catch (ArgumentException exception)
            {
                error = exception.Message;
                return false;
            }

            restoredGrid = grid;
            return true;
        }

        public PlaceTowerResult PlaceTower(PlaceTowerCommand command)
        {
            if (State.Phase != BattlePhase.Preparation)
            {
                return PlaceTowerResult.Failed(PlaceTowerFailureReason.InvalidPhase);
            }

            if (!TowerCatalog.TryGet(command.DefinitionId, out var archetype))
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

            var definition = archetype.Levels[0];
            if (State.Gold < definition.Price)
            {
                return PlaceTowerResult.Failed(PlaceTowerFailureReason.InsufficientGold);
            }

            if (_nextTowerId >= int.MaxValue - 1)
            {
                return PlaceTowerResult.Failed(PlaceTowerFailureReason.IdentifierExhausted);
            }

            var tower = new TowerState(new TowerId(_nextTowerId), definition, command.Coordinate);
            if (!Grid.TryOccupy(command.Coordinate))
            {
                return PlaceTowerResult.Failed(PlaceTowerFailureReason.Occupied);
            }

            _nextTowerId++;
            State.Debit(definition.Price);
            State.AddTower(tower);
            return PlaceTowerResult.Succeeded(tower);
        }

        public TowerOperationResult UpgradeTower(TowerId towerId)
        {
            if (State.Phase != BattlePhase.Preparation)
            {
                return TowerOperationResult.Failed(TowerOperationFailureReason.InvalidPhase);
            }

            var towerIndex = FindTowerIndex(towerId);
            if (towerIndex < 0)
            {
                return TowerOperationResult.Failed(TowerOperationFailureReason.TowerNotFound);
            }

            var tower = State.Towers[towerIndex];
            var levels = TowerCatalog.Get(tower.Definition.Id).Levels;
            if (tower.Level >= levels.Count)
            {
                return TowerOperationResult.Failed(TowerOperationFailureReason.MaxLevel);
            }

            var nextLevel = levels[tower.Level];
            if (nextLevel.Price > int.MaxValue - tower.InvestedGold)
            {
                return TowerOperationResult.Failed(TowerOperationFailureReason.InvestmentOverflow);
            }

            if (State.Gold < nextLevel.Price)
            {
                return TowerOperationResult.Failed(TowerOperationFailureReason.InsufficientGold);
            }

            State.Debit(nextLevel.Price);
            tower.UpgradeTo(nextLevel);
            return TowerOperationResult.Succeeded(tower);
        }

        public TowerOperationResult SellTower(TowerId towerId)
        {
            if (State.Phase != BattlePhase.Preparation)
            {
                return TowerOperationResult.Failed(TowerOperationFailureReason.InvalidPhase);
            }

            var towerIndex = FindTowerIndex(towerId);
            if (towerIndex < 0)
            {
                return TowerOperationResult.Failed(TowerOperationFailureReason.TowerNotFound);
            }

            var tower = State.Towers[towerIndex];
            if (Grid.GetState(tower.Coordinate) != GridCellState.Occupied)
            {
                return TowerOperationResult.Failed(TowerOperationFailureReason.InvalidGridState);
            }

            var refund = (int)((long)tower.InvestedGold * 70 / 100);
            State.RemoveTowerAt(towerIndex);
            Grid.TryRelease(tower.Coordinate);
            State.Credit(refund);
            return TowerOperationResult.Succeeded(tower, refund);
        }

        private int FindTowerIndex(TowerId towerId)
        {
            var towers = State.Towers;
            for (var index = 0; index < towers.Count; index++)
            {
                if (towers[index].Id == towerId)
                {
                    return index;
                }
            }

            return -1;
        }

        public WaveTransitionResult StartWave()
        {
            if (State.Phase != BattlePhase.Preparation)
            {
                return WaveTransitionResult.RejectedInvalidPhase;
            }

            if (CurrentWave.Count > int.MaxValue - 1 - _nextEnemyId)
            {
                return WaveTransitionResult.RejectedIdExhausted;
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

        private static TowerCatalog CreateCompatibilityCatalog(IReadOnlyList<TowerDefinition> towerLevels)
        {
            return new TowerCatalog(new[]
            {
                TowerArchetypeDefinition.CreateCompatibility(towerLevels)
            });
        }

        private long NextSpawnTick()
        {
            return _waveStartTick + (long)CurrentWave.FirstSpawnTick +
                   (long)_nextSpawnIndex * CurrentWave.SpawnIntervalTicks;
        }
    }
}
