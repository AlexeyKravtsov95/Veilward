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
        private readonly int _initialGold;
        private readonly ElementUnlockRules _elementUnlockRules;
        private readonly List<AttackEvent> _attackEvents = new List<AttackEvent>();
        private readonly List<ReactionEvent> _reactionEvents = new List<ReactionEvent>(32);
        private readonly List<DamageResolvedEvent> _damageEvents = new List<DamageResolvedEvent>(128);
        private readonly ReactionResolver _reactionResolver = new ReactionResolver();
        private readonly AbilityDefinition _abilityDefinition;
        private readonly List<AbilityTarget> _abilityTargets;
        private readonly List<AbilityCastEvent> _abilityEvents = new List<AbilityCastEvent>();
        private int _sequenceInTick;
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
            int startingGold,
            ReactionCatalog reactionCatalog = null,
            ElementUnlockRules elementUnlockRules = null)
            : this(
                map,
                path,
                new[] { wave ?? throw new ArgumentNullException(nameof(wave)) },
                towerCatalog,
                baseHealth,
                startingGold,
                waveCompletionBonus: 0,
                legacySingleWave: true,
                reactionCatalog, elementUnlockRules)
        {
        }

        public BattleSimulation(
            GridMapDefinition map,
            PathDefinition path,
            IReadOnlyList<WaveDefinition> waves,
            TowerCatalog towerCatalog,
            int baseHealth,
            int startingGold,
            int waveCompletionBonus,
            ReactionCatalog reactionCatalog = null,
            ElementUnlockRules elementUnlockRules = null,
            AbilityDefinition abilityDefinition = null)
            : this(
                map,
                path,
                waves,
                towerCatalog,
                baseHealth,
                startingGold,
                waveCompletionBonus,
                legacySingleWave: false,
                reactionCatalog, elementUnlockRules, abilityDefinition)
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

        internal BattleSimulation(
            GridMapDefinition map,
            PathDefinition path,
            IReadOnlyList<WaveDefinition> waves,
            TowerCatalog towerCatalog,
            int baseHealth,
            int startingGold,
            int waveCompletionBonus,
            bool legacySingleWave,
            ReactionCatalog reactionCatalog = null,
            ElementUnlockRules elementUnlockRules = null,
            AbilityDefinition abilityDefinition = null)
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
            ReactionCatalog = reactionCatalog ?? ReactionCatalog.Empty;
            for (var archetypeIndex = 0; archetypeIndex < towerCatalog.Archetypes.Count; archetypeIndex++)
            {
                var levels = towerCatalog.Archetypes[archetypeIndex].Levels;
                for (var levelIndex = 0; levelIndex < levels.Count; levelIndex++)
                {
                    var intrinsic = levels[levelIndex].IntrinsicReaction;
                    if (intrinsic != null && !ReactionCatalog.TryGet(intrinsic.Kind, out _))
                    {
                        throw new ArgumentException(
                            "Intrinsic reaction requires a matching global definition.", nameof(reactionCatalog));
                    }
                }
            }

            _elementUnlockRules = elementUnlockRules;
            if (elementUnlockRules != null)
            {
                elementUnlockRules.ValidateWaveCount(waves.Count);
                foreach (var archetype in towerCatalog.Archetypes) archetype.ValidateNativeRequirements();
            }
            TowerLevels = towerCatalog.Archetypes[0].Levels;
            _waves = new WaveDefinition[waves.Count];
            for (var index = 0; index < waves.Count; index++)
            {
                _waves[index] = waves[index] ?? throw new ArgumentException(
                    "Waves cannot contain null entries.",
                    nameof(waves));
            }

            Waves = Array.AsReadOnly(_waves);
            _waveCompletionBonus = waveCompletionBonus;
            _legacySingleWave = legacySingleWave;
            _initialBaseHealth = baseHealth;
            _initialGold = startingGold;
            Grid = new BattleGrid(map, path);
            long targetCapacity = 0;
            foreach (var wave in _waves)
            {
                targetCapacity += wave.Count;
                if (targetCapacity > WaveDefinition.MaxSpawnCount)
                    throw new ArgumentException("Mission spawn budget exceeds bounded capacity.", nameof(waves));
            }
            _abilityTargets = new List<AbilityTarget>(abilityDefinition == null ? 0 : (int)targetCapacity);
            _abilityDefinition = abilityDefinition;
            AbilityState = new AbilityState(abilityDefinition);
            AbilityEvents = _abilityEvents.AsReadOnly();
            State = BattleState.CreateInitial(baseHealth, startingGold, _waves.Length);
            State.SetElementProgression(ElementProgressionState.CreateInitial(elementUnlockRules != null));
            AttackEvents = _attackEvents.AsReadOnly();
            ReactionEvents = _reactionEvents.AsReadOnly();
            DamageEvents = _damageEvents.AsReadOnly();
        }

        public BattleState State { get; private set; }
        public PathDefinition Path => _path;
        public IReadOnlyList<WaveDefinition> Waves { get; }
        public long WaveElapsedTicks => State.Phase == BattlePhase.Combat ? State.Tick - _waveStartTick : 0;
        public BattleGrid Grid { get; private set; }
        public TowerCatalog TowerCatalog { get; }
        public IReadOnlyList<TowerDefinition> TowerLevels { get; }
        public TowerDefinition TowerDefinition => TowerLevels[0];
        public IReadOnlyList<AttackEvent> AttackEvents { get; }
        public ReactionCatalog ReactionCatalog { get; }
        public IReadOnlyList<ReactionEvent> ReactionEvents { get; }
        public IReadOnlyList<DamageResolvedEvent> DamageEvents { get; }
        public AbilityDefinition AbilityDefinition => _abilityDefinition;

        public AbilityState AbilityState { get; private set; }
        public IReadOnlyList<AbilityCastEvent> AbilityEvents { get; }
        public void ClearAbilityEvents() => _abilityEvents.Clear();

        public AbilityCastResult CheckCastAbility(CastAbilityCommand command)
        {
            if (State.Phase != BattlePhase.Combat)
                return AbilityCastResult.Rejected(AbilityCastFailureReason.InvalidPhase);
            if (_abilityDefinition == null)
                return AbilityCastResult.Rejected(AbilityCastFailureReason.AbilityDisabled);
            if (command.AbilityId != _abilityDefinition.Id)
                return AbilityCastResult.Rejected(AbilityCastFailureReason.UnknownAbility);
            if (command.ExpectedWaveIndex != State.WaveIndex || command.ExpectedTick != State.Tick ||
                command.ExpectedCastSequence != AbilityState.NextCastSequence)
                return AbilityCastResult.Rejected(AbilityCastFailureReason.StaleCommand);
            if (AbilityState.CooldownRemainingTicks != 0)
                return AbilityCastResult.Rejected(AbilityCastFailureReason.CooldownActive);
            if (!IsAbilityCenterInBounds(command.Center))
                return AbilityCastResult.Rejected(AbilityCastFailureReason.InvalidTarget);

            var hasTarget = false;
            for (var index = 0; index < State.Enemies.Count; index++)
            {
                var enemy = State.Enemies[index];
                if (enemy.IsAlive && IsAbilityTarget(command.Center, _path.GetPosition(enemy.DistanceAlongPath)))
                {
                    hasTarget = true;
                    break;
                }
            }
            if (!hasTarget) return AbilityCastResult.Rejected(AbilityCastFailureReason.NoTargets);
            if (AbilityState.NextCastSequence == long.MaxValue)
                return AbilityCastResult.Rejected(AbilityCastFailureReason.IdentifierExhausted);
            return AbilityCastResult.Allowed();
        }

        public AbilityCastResult CastAbility(CastAbilityCommand command)
        {
            var check = CheckCastAbility(command);
            if (!check.Succeeded) return check;

            // Список фиксируется целиком до первого damage, независимо от порядка State.Enemies.
            _abilityTargets.Clear();
            for (var index = 0; index < State.Enemies.Count; index++)
            {
                var enemy = State.Enemies[index];
                if (!enemy.IsAlive) continue;
                var position = _path.GetPosition(enemy.DistanceAlongPath);
                if (!IsAbilityTarget(command.Center, position)) continue;
                var insertion = _abilityTargets.Count;
                while (insertion > 0 && _abilityTargets[insertion - 1].Enemy.Id.Value > enemy.Id.Value) insertion--;
                _abilityTargets.Insert(insertion, new AbilityTarget(enemy, position));
            }

            var sequence = AbilityState.NextCastSequence;
            AbilityState.AcceptCast(_abilityDefinition.CooldownTicks);
            _abilityEvents.Add(new AbilityCastEvent(State.Tick, _sequenceInTick++, _abilityDefinition.Id,
                sequence, command.Center, _abilityDefinition.Radius, _abilityTargets.Count));
            long totalLoss = 0;
            for (var index = 0; index < _abilityTargets.Count; index++)
            {
                var captured = _abilityTargets[index];
                var enemy = captured.Enemy;
                var resolved = ApplyArmor(_abilityDefinition.Damage, EffectiveArmor(enemy), penetration: 0);
                var healthBefore = enemy.Health;
                var lethal = enemy.ApplyDamage(resolved);
                var applied = healthBefore - enemy.Health;
                totalLoss += applied;
                _damageEvents.Add(new DamageResolvedEvent(State.Tick, _sequenceInTick++, DamageOrigin.Ability,
                    default, enemy.Id, command.Center, captured.Position, _abilityDefinition.Damage,
                    _abilityDefinition.Damage, resolved, applied, enemy.Health, lethal, null, null, index,
                    _abilityDefinition.Id, sequence));
            }
            var count = _abilityTargets.Count;
            _abilityTargets.Clear();
            ResolveDeathsAndCompletion();
            return AbilityCastResult.Cast(sequence, count, totalLoss);
        }

        private bool IsAbilityCenterInBounds(PathPosition center)
        {
            var map = Grid.Map;
            return !float.IsNaN(center.X) && !float.IsInfinity(center.X) &&
                   !float.IsNaN(center.Y) && !float.IsInfinity(center.Y) &&
                   center.X >= map.Origin.X &&
                   center.Y >= map.Origin.Y &&
                   center.X < map.Origin.X + (double)map.Width * map.CellSize &&
                   center.Y < map.Origin.Y + (double)map.Height * map.CellSize;
        }

        private bool IsAbilityTarget(PathPosition center, PathPosition target)
        {
            var dx = (double)target.X - center.X;
            var dy = (double)target.Y - center.Y;
            return dx * dx + dy * dy <= (double)_abilityDefinition.Radius * _abilityDefinition.Radius;
        }

        private readonly struct AbilityTarget
        {
            public AbilityTarget(EnemyState enemy, PathPosition position) { Enemy = enemy; Position = position; }
            public EnemyState Enemy { get; }
            public PathPosition Position { get; }
        }

        private WaveDefinition CurrentWave => _waves[State.WaveIndex];

        public BattleCheckpoint ExportCheckpoint()
        {
            switch (State.Phase)
            {
                case BattlePhase.Preparation:
                case BattlePhase.WaveComplete:
                case BattlePhase.Victory:
                case BattlePhase.Defeat:
                    return BattleCheckpoint.FromState(State, _nextEnemyId, _nextTowerId, AbilityState);
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

            var restoredAbility = new AbilityState(_abilityDefinition, checkpoint.ability);
            State = restoredState;
            AbilityState = restoredAbility;
            Grid = restoredGrid;
            _nextEnemyId = checkpoint.nextEnemyId;
            _nextTowerId = checkpoint.nextTowerId;
            _nextSpawnIndex = 0;
            _waveStartTick = checkpoint.tick;
            _attackEvents.Clear();
            _reactionEvents.Clear();
            _damageEvents.Clear();
            _abilityEvents.Clear();
            _abilityTargets.Clear();
            _sequenceInTick = 0;
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

            if (checkpoint.elementProgression == null ||
                !checkpoint.elementProgression.TryRestore(checkpoint, _elementUnlockRules != null,
                    _initialBaseHealth, _initialGold, out var progression))
            {
                error = "Checkpoint element progression is invalid.";
                return false;
            }

            if (checkpoint.ability == null || !checkpoint.ability.IsValid(_abilityDefinition != null,
                    checkpoint.elementProgression.pendingChoiceId == "start-resonance"))
            {
                error = "Checkpoint ability is invalid or incompatible.";
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

                if (progression.IsNative)
                {
                    if (towerCheckpoint.towerTypeId == "test-tower")
                    {
                        error = "Native checkpoint cannot contain a legacy tower.";
                        return false;
                    }
                    foreach (var element in archetype.RequiredElements)
                        if (!progression.IsUnlocked(element))
                        {
                            error = "Checkpoint tower requirements are locked.";
                            return false;
                        }
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

            towers.Sort((left, right) => left.Id.Value.CompareTo(right.Id.Value));

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
                restoredState.SetElementProgression(progression);
            }
            catch (ArgumentException exception)
            {
                error = exception.Message;
                return false;
            }

            restoredGrid = grid;
            return true;
        }

        public TowerAvailability GetTowerAvailability(PlaceTowerCommand command)
        {
            if (State.Phase != BattlePhase.Preparation)
            {
                return new TowerAvailability(PlaceTowerFailureReason.InvalidPhase);
            }

            if (State.ElementProgression.HasPendingChoice)
                return new TowerAvailability(PlaceTowerFailureReason.PendingElementChoice);

            if (!TowerCatalog.TryGet(command.DefinitionId, out var archetype))
            {
                return new TowerAvailability(PlaceTowerFailureReason.UnknownTowerDefinition);
            }

            if (State.ElementProgression.IsNative)
            {
                if (archetype.Id.Value == "test-tower")
                    return new TowerAvailability(PlaceTowerFailureReason.LegacyTowerUnavailable);
                var missing = MissingElements(archetype);
                if (missing.Count > 0) return new TowerAvailability(PlaceTowerFailureReason.ElementLocked, missing);
            }

            if (!Grid.Map.Contains(command.Coordinate))
            {
                return new TowerAvailability(PlaceTowerFailureReason.OutsideMap);
            }

            var cellState = Grid.GetState(command.Coordinate);
            if (cellState == GridCellState.Blocked)
            {
                return new TowerAvailability(PlaceTowerFailureReason.Blocked);
            }

            if (cellState == GridCellState.Occupied)
            {
                return new TowerAvailability(PlaceTowerFailureReason.Occupied);
            }

            var definition = archetype.Levels[0];
            if (State.Gold < definition.Price)
            {
                return new TowerAvailability(PlaceTowerFailureReason.InsufficientGold);
            }

            if (_nextTowerId >= int.MaxValue - 1)
            {
                return new TowerAvailability(PlaceTowerFailureReason.IdentifierExhausted);
            }

            return new TowerAvailability(PlaceTowerFailureReason.None);
        }

        private IReadOnlyList<ElementKind> MissingElements(TowerArchetypeDefinition archetype)
        {
            List<ElementKind> missing = null;
            foreach (var element in archetype.RequiredElements)
            {
                if (State.ElementProgression.IsUnlocked(element)) continue;
                if (missing == null) missing = new List<ElementKind>();
                missing.Add(element);
            }
            return missing == null ? Array.Empty<ElementKind>() : (IReadOnlyList<ElementKind>)missing;
        }

        public PlaceTowerResult PlaceTower(PlaceTowerCommand command)
        {
            var availability = GetTowerAvailability(command);
            if (!availability.CanPlace) return PlaceTowerResult.Failed(availability.FailureReason);
            var definition = TowerCatalog.Get(command.DefinitionId).Levels[0];
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

            if (State.ElementProgression.HasPendingChoice)
                return TowerOperationResult.Failed(TowerOperationFailureReason.PendingElementChoice);

            var towerIndex = FindTowerIndex(towerId);
            if (towerIndex < 0)
            {
                return TowerOperationResult.Failed(TowerOperationFailureReason.TowerNotFound);
            }

            var tower = State.Towers[towerIndex];
            var archetype = TowerCatalog.Get(tower.Definition.Id);
            if (State.ElementProgression.IsNative &&
                (archetype.Id.Value == "test-tower" || MissingElements(archetype).Count > 0))
                return TowerOperationResult.Failed(TowerOperationFailureReason.ElementLocked);
            var levels = archetype.Levels;
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

            if (State.ElementProgression.HasPendingChoice)
                return TowerOperationResult.Failed(TowerOperationFailureReason.PendingElementChoice);

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

        public WaveTransitionResult CheckStartWave()
        {
            if (State.Phase != BattlePhase.Preparation)
            {
                return WaveTransitionResult.RejectedInvalidPhase;
            }

            if (State.ElementProgression.HasPendingChoice)
                return WaveTransitionResult.RejectedPendingElementChoice;

            if (CurrentWave.Count > int.MaxValue - 1 - _nextEnemyId)
            {
                return WaveTransitionResult.RejectedIdExhausted;
            }

            if (State.Tick >= long.MaxValue - CurrentWave.LastSpawnTick)
                return WaveTransitionResult.RejectedScheduleOverflow;
            return WaveTransitionResult.Started;
        }

        public WaveTransitionResult StartWave()
        {
            var check = CheckStartWave();
            if (check != WaveTransitionResult.Started) return check;
            _waveStartTick = State.Tick;
            _nextSpawnIndex = 0;
            State.SetPhase(BattlePhase.Combat);
            SpawnDueEnemies();
            return WaveTransitionResult.Started;
        }

        public WaveTransitionResult CheckContinueToNextWave()
        {
            if (State.Phase != BattlePhase.WaveComplete)
            {
                return WaveTransitionResult.RejectedInvalidPhase;
            }

            if (State.ElementProgression.HasPendingChoice)
                return WaveTransitionResult.RejectedPendingElementChoice;

            if (State.WaveIndex + 1 >= State.WaveCount)
            {
                return WaveTransitionResult.RejectedNoNextWave;
            }

            return WaveTransitionResult.Continued;
        }

        public WaveTransitionResult ContinueToNextWave()
        {
            var check = CheckContinueToNextWave();
            if (check != WaveTransitionResult.Continued) return check;
            State.AdvanceWave();
            State.SetPhase(BattlePhase.Preparation);
            return WaveTransitionResult.Continued;
        }

        public ElementChoiceResult ChooseElement(ChooseElementCommand command)
        {
            var progression = State.ElementProgression;
            if (State.Phase != BattlePhase.Preparation && State.Phase != BattlePhase.WaveComplete)
                return new ElementChoiceResult(ElementChoiceFailureReason.InvalidPhase);
            if (progression.HasPendingChoice &&
                (progression.PendingChoiceId == "start-resonance"
                    ? State.Phase != BattlePhase.Preparation || State.WaveIndex != 0
                    : State.Phase != BattlePhase.WaveComplete ||
                      progression.PendingChoiceId != "after-wave-" + (State.WaveIndex + 1)))
                return new ElementChoiceResult(ElementChoiceFailureReason.InvalidPhase);
            if (_elementUnlockRules == null)
                return new ElementChoiceResult(ElementChoiceFailureReason.ProgressionDisabled);
            if (!progression.HasPendingChoice)
                return new ElementChoiceResult(ElementChoiceFailureReason.NoPendingChoice);
            if (!string.Equals(command.ChoiceId, progression.PendingChoiceId, StringComparison.Ordinal))
                return new ElementChoiceResult(ElementChoiceFailureReason.ChoiceMismatch);
            ElementKind element;
            switch (command.OptionId)
            {
                case "flame": element = ElementKind.Flame; break;
                case "tide": element = ElementKind.Tide; break;
                case "gale": element = ElementKind.Gale; break;
                case "stone": element = ElementKind.Stone; break;
                default: return new ElementChoiceResult(ElementChoiceFailureReason.InvalidOption);
            }
            var start = progression.PendingChoiceId == "start-resonance";
            if (start)
            {
                var valid = false;
                foreach (var option in _elementUnlockRules.StartingOptions)
                    if (option == element) valid = true;
                if (!valid) return new ElementChoiceResult(ElementChoiceFailureReason.InvalidOption);
            }
            if (progression.IsUnlocked(element))
                return new ElementChoiceResult(ElementChoiceFailureReason.AlreadyUnlocked);
            if (!start && progression.ChoicesEarned <= progression.ChoicesSpent)
                return new ElementChoiceResult(ElementChoiceFailureReason.NoChoiceCredit);
            State.SetElementProgression(progression.Confirm(element, command.OptionId));
            return new ElementChoiceResult(ElementChoiceFailureReason.None);
        }

        public void Step()
        {
            if (State.Phase != BattlePhase.Combat)
            {
                return;
            }

            State.AdvanceTick();
            _sequenceInTick = 0;
            AbilityState.AdvanceCombatStep();
            SpawnDueEnemies();
            ProcessStatusDamageAndExpiry();
            MoveEnemiesAndApplyLeaks();
            ProcessTowerAttacks();
            ResolveDeathsAndCompletion();
        }

        private void ResolveDeathsAndCompletion()
        {
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
                if (_elementUnlockRules != null && _elementUnlockRules.IsChoiceWave(State.WaveIndex + 1))
                    State.SetElementProgression(State.ElementProgression.AwardChoice(State.WaveIndex + 1));
            }
        }

        public void ClearAttackEvents()
        {
            _attackEvents.Clear();
        }

        public void ClearReactionEvents()
        {
            _reactionEvents.Clear();
        }

        public void ClearDamageEvents()
        {
            _damageEvents.Clear();
        }

        private void ProcessStatusDamageAndExpiry()
        {
            // Spawn и удаление сохраняют возрастание EnemyId в этом списке.
            for (var index = 0; index < State.Enemies.Count; index++)
            {
                var enemy = State.Enemies[index];
                var burn = enemy.Burn;
                if (enemy.IsAlive && burn != null && burn.NextDamageTick == State.Tick)
                {
                    var position = _path.GetPosition(enemy.DistanceAlongPath);
                    var resolved = ApplyArmor(burn.Power, EffectiveArmor(enemy), penetration: 0);
                    var healthBefore = enemy.Health;
                    var wasLethal = enemy.ApplyDamage(resolved);
                    _damageEvents.Add(new DamageResolvedEvent(
                        State.Tick, _sequenceInTick++, DamageOrigin.Burn, burn.SourceTowerId, enemy.Id,
                        position, position, burn.Power, burn.Power, resolved,
                        healthBefore - enemy.Health, enemy.Health, wasLethal, null, null, 0));
                    burn.ScheduleNextDamage(State.Tick);
                }

                enemy.AdvanceStatusTimers();
            }
        }

        private void MoveEnemiesAndApplyLeaks()
        {
            var index = 0;
            while (index < State.Enemies.Count)
            {
                var enemy = State.Enemies[index];
                if (!enemy.IsAlive)
                {
                    index++;
                    continue;
                }

                var speed = enemy.Definition.Speed;
                var wet = enemy.Wet;
                if (wet != null)
                {
                    speed *= (100 - wet.Power) / 100f;
                }

                var distance = Math.Min(
                    _path.TotalLength,
                    enemy.DistanceAlongPath + speed / SimulationTiming.TicksPerSecond);
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
                var afterArmor = ApplyArmor(
                    tower.Definition.Damage,
                    EffectiveArmor(target),
                    tower.Definition.ArmorPenetration);
                var directDamage = ApplyGust(afterArmor, target.Gust);
                var healthBefore = target.Health;
                var wasLethal = target.ApplyDamage(directDamage);
                _damageEvents.Add(new DamageResolvedEvent(
                    State.Tick, _sequenceInTick++, DamageOrigin.Direct, tower.Id, target.Id,
                    center, targetPosition, tower.Definition.Damage, tower.Definition.Damage, directDamage,
                    healthBefore - target.Health, target.Health, wasLethal, null, null, 0));
                if (!wasLethal)
                {
                    if (tower.Definition.StatusEffect != null)
                    {
                        target.ApplyStatus(tower.Definition.StatusEffect, tower.Id, State.Tick);
                    }

                    _reactionResolver.TryResolve(State, _path, tower, target, targetPosition,
                        ReactionCatalog, _reactionEvents, _damageEvents, ref _sequenceInTick);
                }

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

        private static int EffectiveArmor(EnemyState enemy)
        {
            var crackPower = enemy.Crack == null ? 0 : enemy.Crack.Power;
            return Math.Max(0, enemy.Definition.Armor - crackPower);
        }

        private static int ApplyArmor(int damage, int armor, int penetration)
        {
            var remainingArmor = Math.Max(0, armor - penetration);
            return Math.Max(1, damage - remainingArmor);
        }

        private static int ApplyGust(int damage, StatusInstance gust)
        {
            if (gust == null)
            {
                return damage;
            }

            return (int)Math.Min(int.MaxValue, ((long)damage * (100L + gust.Power)) / 100);
        }

        private void SpawnDueEnemies()
        {
            while (_nextSpawnIndex < CurrentWave.Count && NextSpawnTick() <= State.Tick)
            {
                State.AddEnemy(EnemyState.CreateInitial(new EnemyId(_nextEnemyId), CurrentWave.Schedule[_nextSpawnIndex].Enemy));
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
            return _waveStartTick + CurrentWave.Schedule[_nextSpawnIndex].RelativeTick;
        }
    }
}
