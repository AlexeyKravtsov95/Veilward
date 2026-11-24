using System;
using System.Collections.Generic;
using UnityEngine;
using Veilward.Application;
using Veilward.Domain;
using Veilward.Infrastructure;
using Application = UnityEngine.Application;

namespace Veilward.Presentation
{
    public enum BattleInputOwner { None, Placement, Sell, Inspector, ElementChoice, Ability }

    [DefaultExecutionOrder(-100)]
    public sealed class BattleSceneController : MonoBehaviour
    {
        private const float StepSeconds = 1f / SimulationTiming.TicksPerSecond;
        private const int MaxStepsPerFrame = 8;

        [SerializeField] private BattleScenarioConfig scenario;
        [SerializeField] private Transform enemyPrefab;
        [SerializeField] private Transform bossPrefab;
        [SerializeField] private Transform towerPrefab;
        [SerializeField] private UnityEngine.UI.Text hudText;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Material attackBeamMaterial;
        [SerializeField] private Material reactionEffectMaterial;
        [SerializeField] private Material flameProjectileMaterial;
        [SerializeField] private Material flameProjectileCoreMaterial;
        private FlameProjectilePool _flameProjectiles;
        public FlameProjectilePool FlameProjectiles => _flameProjectiles;
        private BasicProjectilePool _basicProjectiles;
        public BasicProjectilePool BasicProjectiles => _basicProjectiles;
        private HybridProjectilePool _hybridProjectiles;
        public HybridProjectilePool HybridProjectiles => _hybridProjectiles;
        [SerializeField] private BattlePauseController pauseController;
        [SerializeField] private BattlePlacementController placementController;
        [SerializeField] private BattleCheckpointController checkpointController;
        [SerializeField] private EnemyInspectorController enemyInspector;
        [SerializeField] private ElementChoiceController elementChoice;
        [SerializeField] private AbilityController abilityController;
        [SerializeField] private BossHudController bossHud;
        private readonly AbilityEffectView[] _abilityPool = new AbilityEffectView[AbilityEffectView.CastCapacity + AbilityEffectView.HitCapacity];
        private int _nextAbilityCast;
        private int _nextAbilityHit;
        private readonly BossCueView[] _bossCues = new BossCueView[BossCueView.Capacity];
        private long _bossCueOrder;
        [SerializeField] private UnityEngine.UI.Button mainMenuButton;
        [SerializeField] private UnityEngine.UI.Text reactionSummaryText;
        private const int ReactionPoolCapacity = 24;
        private readonly ReactionEffectView[] _reactionPool = new ReactionEffectView[ReactionPoolCapacity];
        private int _nextReactionEffect;
        private Vector2 _reactionSummaryOrigin;
        private Canvas _summaryCanvas;
        public ReactionPresentationState Presentation { get; } = new ReactionPresentationState();

        private readonly List<Rect> _healthRects = new List<Rect>(64);
        private const int MaxEnemyTails = 16;
        private readonly List<EnemyModelView> _enemyTails = new List<EnemyModelView>(MaxEnemyTails);
        private readonly Dictionary<EnemyId, EnemyView> _enemyViews = new Dictionary<EnemyId, EnemyView>();
        private readonly Dictionary<TowerId, int> _towerViewLevels = new Dictionary<TowerId, int>();
        private readonly Dictionary<TowerId, Transform> _towerViews = new Dictionary<TowerId, Transform>();
        private readonly List<Rect> _statusRects = new List<Rect>();
        private readonly List<EnemyId> _staleEnemyIds = new List<EnemyId>();
        private readonly List<TowerId> _staleTowerIds = new List<TowerId>();
        private BattleSimulation _simulation;
        private float _accumulator;
        private bool _skipNextDelta;

        public BattleSimulation Simulation => _simulation;
        public bool IsPaused => pauseController != null && pauseController.IsPaused;

        public BattleInputOwner ActiveModalOwner
        {
            get
            {
                if (_simulation != null && _simulation.State.ElementProgression.PendingChoiceId != "none") return BattleInputOwner.ElementChoice;
                if (abilityController != null && abilityController.HasInputOwnership) return BattleInputOwner.Ability;
                if (placementController != null && placementController.IsSellConfirmationVisible) return BattleInputOwner.Sell;
                if (enemyInspector != null && enemyInspector.SelectedEnemyId.HasValue) return BattleInputOwner.Inspector;
                if (placementController != null && placementController.HasSelection) return BattleInputOwner.Placement;
                return BattleInputOwner.None;
            }
        }

        public bool CanAcceptInput(BattleInputOwner owner = BattleInputOwner.None)
        {
            if (IsPaused) return false;
            var activeOwner = ActiveModalOwner;
            // Read-only инспектор может забрать обычный выбор построенной башни.
            // Sell/обязательный выбор стихии не передают ему владение вводом.
            var canInspectSelectedTower = owner == BattleInputOwner.Inspector &&
                activeOwner == BattleInputOwner.Placement && placementController.SelectedTowerId.HasValue;
            return activeOwner == BattleInputOwner.None || activeOwner == owner || canInspectSelectedTower;
        }

        private void Start()
        {
            if (scenario == null)
            {
                throw new InvalidOperationException("Battle scenario reference is required.");
            }

            if (enemyPrefab == null)
            {
                throw new InvalidOperationException("Enemy prefab reference is required.");
            }

            if (towerPrefab == null)
            {
                throw new InvalidOperationException("Tower prefab reference is required.");
            }

            if (hudText == null)
            {
                throw new InvalidOperationException("HUD text reference is required.");
            }

            if (worldCamera == null)
            {
                throw new InvalidOperationException("Battle camera reference is required.");
            }

            if (attackBeamMaterial == null)
            {
                throw new InvalidOperationException("Attack beam material reference is required.");
            }

            var session = BattleSessionContext.Consume();
            WaveCheckpointEnvelope checkpoint = null;
            if (session?.Checkpoint != null &&
                !WaveCheckpointCompatibility.TryNormalize(session.Checkpoint, scenario.ScenarioContentId, out checkpoint, out var error))
                throw new InvalidOperationException("Battle session checkpoint is incompatible.");
            _simulation = checkpoint == null ? scenario.CreateSessionSimulation(session?.ScenarioContentId ?? scenario.ScenarioContentId) : scenario.CreateSimulationForCheckpoint(checkpoint);
            if (_flameProjectiles == null)
                _flameProjectiles = new FlameProjectilePool(transform,
                    flameProjectileMaterial != null ? flameProjectileMaterial : attackBeamMaterial,
                    flameProjectileCoreMaterial != null ? flameProjectileCoreMaterial : attackBeamMaterial);
            else _flameProjectiles.Clear();
            if (_basicProjectiles == null)
                _basicProjectiles = new BasicProjectilePool(transform,
                    reactionEffectMaterial != null ? reactionEffectMaterial : attackBeamMaterial);
            else _basicProjectiles.Clear();
            if (_hybridProjectiles == null)
                _hybridProjectiles = new HybridProjectilePool(transform,
                    reactionEffectMaterial != null ? reactionEffectMaterial : attackBeamMaterial);
            else _hybridProjectiles.Clear();
            if (reactionSummaryText != null)
            {
                _reactionSummaryOrigin = reactionSummaryText.rectTransform.anchoredPosition;
                _summaryCanvas = reactionSummaryText.GetComponentInParent<Canvas>();
            }
            for (var i = 0; i < _reactionPool.Length; i++)
            {
                if (_reactionPool[i] != null) { _reactionPool[i].Clear(); continue; }
                var effect = new GameObject("Reaction effect " + i).AddComponent<ReactionEffectView>();
                effect.transform.SetParent(transform, false);
                effect.Initialize(reactionEffectMaterial != null ? reactionEffectMaterial : attackBeamMaterial);
                _reactionPool[i] = effect;
            }
            var sealMaterial = reactionEffectMaterial != null ? reactionEffectMaterial : attackBeamMaterial;
            for (var i = 0; i < _abilityPool.Length; i++)
            {
                if (_abilityPool[i] != null) { _abilityPool[i].Clear(); continue; }
                var effect = new GameObject("Ability effect " + i).AddComponent<AbilityEffectView>();
                effect.transform.SetParent(transform, false);
                effect.Initialize(sealMaterial, i < AbilityEffectView.CastCapacity);
                _abilityPool[i] = effect;
            }
            for (var i = 0; i < _bossCues.Length; i++)
            {
                if (_bossCues[i] != null) { _bossCues[i].Clear(); continue; }
                var cue = new GameObject("Warden cue " + i).AddComponent<BossCueView>();
                cue.transform.SetParent(transform, false);
                cue.Initialize(sealMaterial);
                _bossCues[i] = cue;
            }
            _nextAbilityCast = _nextAbilityHit = 0;
            _bossCueOrder = 0;
            if (checkpointController != null) checkpointController.Restored += ResetPresentation;
            _accumulator = 0f;
            _skipNextDelta = true;
            if (session != null && checkpointController == null)
            {
                throw new InvalidOperationException("Battle checkpoint controller is required for a menu session.");
            }

            if (checkpointController != null)
            {
                checkpointController.Configure(
                    session?.Store,
                    _simulation,
                    checkpoint?.scenarioContentId ?? session?.ScenarioContentId ?? scenario.ScenarioContentId,
                    scenario.CreateSimulationForCheckpoint);
                if (session != null)
                {
                    if (session.Checkpoint != null)
                    {
                        checkpointController.RestoreCheckpoint(checkpoint);
                        if (placementController != null)
                        {
                            placementController.CancelSelection();
                        }
                    }
                    else
                    {
                        checkpointController.SaveCurrentState();
                    }
                }
            }

            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(ReturnToMenu);
            // Синхронный Cast может завершить волну в кадре без fixed Step.
            if (checkpointController != null) checkpointController.ObserveCommandCompletion();
            SynchronizeViews();
            UpdateHud();
        }

        public void ReturnToMenu() => UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");

        public PlaceTowerResult PlaceTower(GridCoordinate coordinate)
        {
            if (_simulation == null) throw new InvalidOperationException("Battle simulation is not initialized.");
            if (_simulation.State.ElementProgression.RulesId == "wave-unlocks-v1")
                return PlaceTowerResult.Failed(PlaceTowerFailureReason.LegacyTowerUnavailable);
            return PlaceTower(_simulation.TowerDefinition.Id, coordinate);
        }

        public PlaceTowerResult PlaceTower(TowerDefinitionId definitionId, GridCoordinate coordinate) =>
            PlaceTowerForOwner(definitionId, coordinate, BattleInputOwner.None);

        internal PlaceTowerResult PlaceTowerForOwner(TowerDefinitionId definitionId, GridCoordinate coordinate, BattleInputOwner owner)
        {
            if (_simulation == null)
            {
                throw new InvalidOperationException("Battle simulation is not initialized.");
            }

            if (!CanAcceptInput(owner))
            {
                return PlaceTowerResult.Failed(PlaceTowerFailureReason.InvalidPhase);
            }

            var result = _simulation.PlaceTower(new PlaceTowerCommand(definitionId, coordinate));
            if (result.Success && checkpointController != null)
            {
                checkpointController.SaveCurrentState();
            }

            return result;
        }

        public TowerOperationResult UpgradeTower(TowerId towerId) => UpgradeTowerForOwner(towerId, BattleInputOwner.None);

        internal TowerOperationResult UpgradeTowerForOwner(TowerId towerId, BattleInputOwner owner)
        {
            if (_simulation == null)
            {
                throw new InvalidOperationException("Battle simulation is not initialized.");
            }

            if (!CanAcceptInput(owner))
            {
                return TowerOperationResult.Failed(TowerOperationFailureReason.InvalidPhase);
            }

            var result = _simulation.UpgradeTower(towerId);
            if (result.Success && checkpointController != null)
            {
                checkpointController.SaveCurrentState();
            }

            return result;
        }

        public TowerOperationResult SellTower(TowerId towerId) => SellTowerForOwner(towerId, BattleInputOwner.None);

        internal TowerOperationResult SellTowerForOwner(TowerId towerId, BattleInputOwner owner)
        {
            if (_simulation == null)
            {
                throw new InvalidOperationException("Battle simulation is not initialized.");
            }

            if (!CanAcceptInput(owner))
            {
                return TowerOperationResult.Failed(TowerOperationFailureReason.InvalidPhase);
            }

            var result = _simulation.SellTower(towerId);
            if (result.Success)
            {
                if (checkpointController != null)
                {
                    checkpointController.SaveCurrentState();
                }

                SynchronizeTowerViews();
            }

            return result;
        }

        public ElementChoiceResult ChooseElement(ChooseElementCommand command)
        {
            if (_simulation == null) throw new InvalidOperationException("Battle simulation is not initialized.");
            if (!CanAcceptInput(BattleInputOwner.ElementChoice)) return new ElementChoiceResult(ElementChoiceFailureReason.InvalidPhase);
            var result = _simulation.ChooseElement(command);
            if (result.Success && checkpointController != null) checkpointController.SaveCurrentState();
            return result;
        }

        public Vector3 GetCellCenter(GridCoordinate coordinate)
        {
            var position = _simulation.Grid.Map.GetCellCenter(coordinate);
            return new Vector3(position.X, 0.04f, position.Y);
        }

        public WaveTransitionResult StartWave()
        {
            if (_simulation == null)
            {
                throw new InvalidOperationException("Battle simulation is not initialized.");
            }

            var eligibility = _simulation.CheckStartWave();
            if (eligibility != WaveTransitionResult.Started) return eligibility;
            if (!CanAcceptInput()) return WaveTransitionResult.RejectedInvalidPhase;
            if (checkpointController != null && !checkpointController.SaveForStartWave())
            {
                return WaveTransitionResult.RejectedInvalidPhase;
            }

            var result = _simulation.StartWave();
            if (checkpointController != null)
            {
                checkpointController.ObservePhaseTransition();
            }

            ProcessAttackEvents();
            return result;
        }

        public WaveTransitionResult ContinueToNextWave()
        {
            if (_simulation == null)
            {
                throw new InvalidOperationException("Battle simulation is not initialized.");
            }

            var eligibility = _simulation.CheckContinueToNextWave();
            if (eligibility != WaveTransitionResult.Continued) return eligibility;
            if (!CanAcceptInput()) return WaveTransitionResult.RejectedInvalidPhase;
            if (checkpointController != null && !checkpointController.SaveForNextWave())
                return WaveTransitionResult.RejectedInvalidPhase;

            var result = _simulation.ContinueToNextWave();
            if (result == WaveTransitionResult.Continued && checkpointController != null)
            {
                checkpointController.SaveCurrentState();
            }

            return result;
        }

        internal AbilityCastResult DispatchAbility(AbilityController caller, CastAbilityCommand command)
        {
            if (caller == null || caller != abilityController || !CanAcceptInput(BattleInputOwner.Ability))
                throw new InvalidOperationException("Only the wired AbilityController may dispatch a guarded intent.");
            if (bossHud != null) bossHud.Refresh();
            var result = _simulation.CastAbility(command);
            if (result.Succeeded)
            {
                try { ProcessAttackEvents(); }
                finally { if (checkpointController != null) checkpointController.ObserveCommandCompletion(); }
            }
            return result;
        }

        private void Update()
        {
            if (_simulation == null)
            {
                return;
            }

            if (IsPaused)
            {
                _accumulator = 0f;
                _skipNextDelta = true;
                return;
            }

            if (_skipNextDelta)
            {
                _skipNextDelta = false;
                _accumulator = 0f;
                return;
            }

            var activeDelta = Mathf.Min(Time.deltaTime, 0.25f);
            Presentation.Advance(activeDelta);
            _flameProjectiles?.Advance(activeDelta);
            _basicProjectiles?.Advance(activeDelta);
            _hybridProjectiles?.Advance(activeDelta);
            foreach (var view in _enemyViews.Values) if (view.Model != null) view.Model.Advance(activeDelta);
            for (var i = _enemyTails.Count - 1; i >= 0; i--)
                if (_enemyTails[i] == null || _enemyTails[i].Advance(activeDelta))
                { if (_enemyTails[i] != null) RemoveEnemyVisual(_enemyTails[i].gameObject); _enemyTails.RemoveAt(i); }
            for (var i = 0; i < _reactionPool.Length; i++)
                if (_reactionPool[i] != null) _reactionPool[i].Advance(activeDelta);
            for (var i = 0; i < _abilityPool.Length; i++) if (_abilityPool[i] != null) _abilityPool[i].Advance(activeDelta);
            foreach (var cue in _bossCues) if (cue != null) cue.Advance(activeDelta);
            _accumulator += activeDelta;
            var steps = 0;
            while (_accumulator >= StepSeconds && steps < MaxStepsPerFrame)
            {
                if (bossHud != null) bossHud.Refresh();
                _simulation.Step();
                ProcessAttackEvents();
                if (checkpointController != null)
                {
                    checkpointController.ObservePhaseTransition();
                }

                _accumulator -= StepSeconds;
                steps++;
            }
        }

        private void LateUpdate()
        {
            if (_simulation == null)
            {
                return;
            }

            // UI callbacks публикуют intent; все накопленные Steps этого кадра уже выполнены.
            if (abilityController != null) { abilityController.Refresh(); abilityController.DispatchIntent(); }
            if (checkpointController != null) checkpointController.ObserveCommandCompletion();
            SynchronizeViews();
            UpdateHud();
            if (reactionSummaryText != null && _summaryCanvas != null && _summaryCanvas.scaleFactor > 0)
            {
                var area = Screen.safeArea;
                reactionSummaryText.rectTransform.anchoredPosition = _reactionSummaryOrigin + new Vector2(
                    area.center.x - Screen.width * .5f, -Mathf.Max(0, Screen.height - area.yMax)) / _summaryCanvas.scaleFactor;
                if (bossHud != null) bossHud.Refresh();
                if (bossHud != null && bossHud.IsVisible)
                {
                    var bossRect = bossHud.PanelRect;
                    reactionSummaryText.rectTransform.anchoredPosition = bossRect.anchoredPosition + Vector2.down * (bossRect.rect.height + 8);
                    reactionSummaryText.rectTransform.sizeDelta = new Vector2(bossRect.rect.width, reactionSummaryText.rectTransform.sizeDelta.y);
                }
                else reactionSummaryText.rectTransform.sizeDelta = new Vector2(680, reactionSummaryText.rectTransform.sizeDelta.y);
            }
        }

        private void ProcessAttackEvents()
        {
            try
            {
                var reactions = _simulation.ReactionEvents;
                var damages = _simulation.DamageEvents;
                // До очистки streams: lethal и leak видны даже при нескольких Steps/frame.
                // Rendering-free orchestration fixtures вызывают consumer без Start; production Start валидирует required prefab.
                if (enemyPrefab != null) SynchronizeEnemyViews();
                for (var damageIndex = 0; damageIndex < damages.Count; damageIndex++)
                {
                    var damage = damages[damageIndex];
                    if (damage.AppliedHpLoss > 0 && _enemyViews.TryGetValue(damage.TargetEnemyId, out var target) && target.Model != null)
                        target.Model.Hit();
                }
                var abilities = _simulation.AbilityEvents;
                var ai = 0; var ri = 0;
                while (ai < abilities.Count || ri < reactions.Count)
                {
                    if (ai < abilities.Count && (ri == reactions.Count || abilities[ai].SequenceInTick < reactions[ri].SequenceInTick))
                    {
                        PlayAbilityCast(abilities[ai++], damages);
                        continue;
                    }
                    var i = ri++;
                    Presentation.Consume(reactions[i], damages);
                    var effect = _reactionPool[_nextReactionEffect];
                    if (effect != null) effect.Play(reactions[i], damages);
                    _nextReactionEffect = (_nextReactionEffect + 1) % _reactionPool.Length;
                }
                if (reactionSummaryText != null) reactionSummaryText.text = Presentation.LastSummary;
                var attacks = _simulation.AttackEvents;
                for (var index = 0; index < attacks.Count; index++)
                {
                    var attack = attacks[index];
                    var towers = _simulation.State.Towers;
                    TowerState attacker = null;
                    for (var t = 0; t < towers.Count; t++)
                        if (towers[t].Id == attack.AttackerTowerId) { attacker = towers[t]; break; }
                    var basicKind = attacker != null ? BasicProjectilePool.KindFor(attacker.Definition.Id) : BasicShotKind.None;
                    var hybridKind = attacker != null ? HybridProjectilePool.KindFor(attacker.Definition.Id) : HybridShotKind.None;
                    if (attacker != null && (attacker.Definition.Id.Value == "flame" ||
                        basicKind != BasicShotKind.None || hybridKind != HybridShotKind.None))
                    {
                        var source = GetCellCenter(attacker.Coordinate) + Vector3.up * .85f;
                        if (_towerViews.TryGetValue(attacker.Id, out var towerView) && towerView != null &&
                            towerView.TryGetComponent<FlameModelBinding>(out var binding) && binding.Level == attacker.Level)
                        {
                            var anchor = binding.ShotSource;
                            // Не принимаем повреждённый/чужой anchor далеко от authoritative cell.
                            var limit = 2 * _simulation.Grid.Map.CellSize;
                            if (anchor != null && (anchor.position - GetCellCenter(attacker.Coordinate)).sqrMagnitude <= limit * limit)
                                source = anchor.position;
                        }
                        if (hybridKind != HybridShotKind.None) _hybridProjectiles?.Play(hybridKind, attack, attacker.Level, source);
                        else if (basicKind != BasicShotKind.None) _basicProjectiles?.Play(basicKind, attack, attacker.Level, source);
                        else _flameProjectiles?.Play(attack, attacker.Level, source);
                        continue;
                    }
                    if (!TryGetTowerPosition(attack.AttackerTowerId, out var start))
                    {
                        continue;
                    }

                    var end = new Vector3(attack.TargetPosition.X, 0.6f, attack.TargetPosition.Y);
                    var beam = new GameObject(
                        $"Attack Beam Tower {attack.AttackerTowerId.Value} Enemy {attack.TargetEnemyId.Value}");
                    beam.transform.SetParent(transform, false);
                    beam.AddComponent<AttackBeamView>().Initialize(
                        attackBeamMaterial,
                        start + new Vector3(0f, 0.85f, 0f),
                        end,
                        this);
                }
            }
            finally
            {
                _simulation.ClearAbilityEvents();
                _simulation.ClearAttackEvents();
                // Все consumer используют один Step; очистка обязательна и при исключении.
                _simulation.ClearReactionEvents();
                _simulation.ClearDamageEvents();
            }
        }

        private void PlayAbilityCast(AbilityCastEvent cast, IReadOnlyList<DamageResolvedEvent> damages)
        {
            _abilityPool[_nextAbilityCast]?.Play(cast);
            _nextAbilityCast = (_nextAbilityCast + 1) % AbilityEffectView.CastCapacity;
            for (var i = 0; i < damages.Count; i++)
            {
                var hit = damages[i];
                if (hit.Origin != DamageOrigin.Ability || hit.Tick != cast.Tick ||
                    hit.SourceAbilityId != cast.AbilityId || hit.AbilityCastSequence != cast.CastSequence ||
                    hit.AppliedHpLoss <= 0)
                    continue;

                _abilityPool[AbilityEffectView.CastCapacity + _nextAbilityHit]?.Play(cast, hit);
                _nextAbilityHit = (_nextAbilityHit + 1) % AbilityEffectView.HitCapacity;
            }
        }

        private bool TryGetTowerPosition(TowerId towerId, out Vector3 position)
        {
            if (_towerViews.TryGetValue(towerId, out var view))
            {
                position = view.position;
                return true;
            }

            var towers = _simulation.State.Towers;
            for (var index = 0; index < towers.Count; index++)
            {
                if (towers[index].Id == towerId)
                {
                    position = GetCellCenter(towers[index].Coordinate);
                    return true;
                }
            }

            position = default;
            return false;
        }

        private void OnDestroy()
        {
            if (mainMenuButton != null) mainMenuButton.onClick.RemoveListener(ReturnToMenu);
            if (checkpointController != null) checkpointController.Restored -= ResetPresentation;
        }

        public void FreezePresentationClock()
        {
            _accumulator = 0; _skipNextDelta = true;
            if (abilityController != null) abilityController.Invalidate();
            if (placementController != null) placementController.SuppressPointerForCurrentFrame();
        }

        public void ResetPresentation()
        {
            _flameProjectiles?.Clear();
            _basicProjectiles?.Clear();
            _hybridProjectiles?.Clear();
            if (bossHud != null) bossHud.ResetView();
            if (abilityController != null) abilityController.ResetTransient();
            _nextAbilityCast = _nextAbilityHit = 0;
            _bossCueOrder = 0;
            for (var i = 0; i < _abilityPool.Length; i++) if (_abilityPool[i] != null) _abilityPool[i].Clear();
            foreach (var cue in _bossCues) if (cue != null) cue.Clear();
            Presentation.Reset(); _accumulator = 0; _skipNextDelta = true; _nextReactionEffect = 0;
            for (var i = 0; i < _reactionPool.Length; i++) if (_reactionPool[i] != null) _reactionPool[i].Clear();
            foreach (var beam in GetComponentsInChildren<AttackBeamView>())
                if (UnityEngine.Application.isPlaying) Destroy(beam.gameObject); else DestroyImmediate(beam.gameObject);
            foreach (var tail in _enemyTails) if (tail != null) RemoveEnemyVisual(tail.gameObject);
            _enemyTails.Clear();
            foreach (var view in _enemyViews.Values) RemoveEnemyVisual(view.Transform.gameObject);
            _enemyViews.Clear();
            foreach (var view in _towerViews.Values)
            {
                view.gameObject.SetActive(false);
                if (UnityEngine.Application.isPlaying) Destroy(view.gameObject); else DestroyImmediate(view.gameObject);
            }
            _towerViews.Clear(); _towerViewLevels.Clear();
            if (enemyInspector != null) enemyInspector.ClearSelection();
            if (placementController != null) placementController.ClearSelection();
            if (elementChoice != null) elementChoice.ClearPreview();
            if (reactionSummaryText != null) reactionSummaryText.text = string.Empty;
            SynchronizeTowerViews();
            SynchronizeEnemyViews(false);
        }

        private void SynchronizeViews()
        {
            SynchronizeTowerViews();
            SynchronizeEnemyViews();
        }

        private void SynchronizeTowerViews()
        {
            var towers = _simulation.State.Towers;
            for (var index = 0; index < towers.Count; index++)
            {
                var tower = towers[index];
                if (_towerViews.TryGetValue(tower.Id, out var existing))
                {
                    if (_towerViewLevels[tower.Id] != tower.Level)
                    {
                        TowerDisplay.ApplyVisual(existing, tower.Definition, tower.Level);
                        _towerViewLevels[tower.Id] = tower.Level;
                    }
                    continue;
                }

                var view = Instantiate(towerPrefab, transform);
                view.name = $"Tower {tower.Id.Value}";
                view.position = GetCellCenter(tower.Coordinate);
                view.localScale = Vector3.one * _simulation.Grid.Map.CellSize;
                TowerDisplay.ApplyVisual(view, tower.Definition, tower.Level);
                _towerViews.Add(tower.Id, view);
                _towerViewLevels.Add(tower.Id, tower.Level);
            }

            _staleTowerIds.Clear();
            foreach (var pair in _towerViews)
            {
                if (!ContainsTower(towers, pair.Key))
                {
                    _staleTowerIds.Add(pair.Key);
                }
            }

            for (var index = 0; index < _staleTowerIds.Count; index++)
            {
                var towerId = _staleTowerIds[index];
                var staleView = _towerViews[towerId].gameObject;
                staleView.SetActive(false);
                if (UnityEngine.Application.isPlaying)
                {
                    Destroy(staleView);
                }
                else
                {
                    DestroyImmediate(staleView);
                }

                _towerViews.Remove(towerId);
                _towerViewLevels.Remove(towerId);
            }
        }

        private static bool ContainsTower(IReadOnlyList<TowerState> towers, TowerId towerId)
        {
            for (var index = 0; index < towers.Count; index++)
            {
                if (towers[index].Id == towerId)
                {
                    return true;
                }
            }

            return false;
        }

        private void SynchronizeEnemyViews(bool playBossCues = true)
        {
            Presentation.Prune(_simulation.State.Enemies);
            var state = _simulation.State;
            _statusRects.Clear();
            _healthRects.Clear();
            for (var index = 0; index < state.Enemies.Count; index++)
            {
                var enemy = state.Enemies[index];
                var firstView = !_enemyViews.TryGetValue(enemy.Id, out var view);
                if (firstView)
                {
                    var prefab = SelectEnemyPrefab(enemy.Definition.Id);
                    view = new EnemyView(Instantiate(prefab, transform), enemy.Definition.Id.Value == BossHudController.BossId);
                    if (view.IsBoss)
                    { view.Transform.name = "Страж Разрыва " + enemy.Id.Value; if (prefab == enemyPrefab) view.Transform.localScale *= 1.4f; }
                    _enemyViews.Add(enemy.Id, view);
                }

                var position = _simulation.Path.GetPosition(enemy.DistanceAlongPath);
                view.Transform.position = new Vector3(position.X, 0.6f, position.Y);
                view.SeenTick = state.Tick;
                if (view.Model != null || view.IsBoss)
                {
                    var ahead = _simulation.Path.GetPosition(Mathf.Min(_simulation.Path.TotalLength, enemy.DistanceAlongPath + .01f));
                    var forward = new Vector3(ahead.X - position.X, 0, ahead.Y - position.Y);
                    if (view.Model != null) view.Model.Synchronize(forward, state.Phase == BattlePhase.Combat);
                    if (view.IsBoss)
                    {
                        if (firstView && playBossCues) PlayBossCue(BossCueKind.Spawn, enemy.Id, position, forward);
                        if (!view.BossThreatShown && _simulation.Path.TotalLength - enemy.DistanceAlongPath <= BossCueView.ThreatDistance)
                        {
                            view.BossThreatShown = true;
                            if (playBossCues) PlayBossCue(BossCueKind.Threat, enemy.Id, position, forward);
                        }
                    }
                }
                if (view.Status != null)
                    view.Status.Refresh(enemy, state.Tick, _simulation.ReactionCatalog,
                        Presentation.GetMemory(enemy.Id), Presentation.ActiveTime, worldCamera);
                if (view.Status != null) view.Status.Layout(worldCamera, _statusRects);
                if (view.HealthBar != null)
                {
                    view.HealthBar.SetNormalizedHealth((float)enemy.Health / enemy.Definition.MaxHealth);
                    view.HealthBar.Face(worldCamera);
                    view.HealthBar.Layout(worldCamera, _healthRects);
                }
            }

            _staleEnemyIds.Clear();
            foreach (var pair in _enemyViews)
            {
                if (!HasEnemy(state.Enemies, pair.Key))
                {
                    _staleEnemyIds.Add(pair.Key);
                }
            }

            for (var index = 0; index < _staleEnemyIds.Count; index++)
            {
                var enemyId = _staleEnemyIds[index];
                var stale = _enemyViews[enemyId];
                if (stale.Model != null || stale.IsBoss)
                {
                    var dead = false;
                    var tailPosition = _simulation.Path.GetPosition(_simulation.Path.TotalLength);
                    var damageEvents = _simulation.DamageEvents;
                    for (var i = 0; i < damageEvents.Count; i++)
                        if (damageEvents[i].TargetEnemyId == enemyId && damageEvents[i].WasLethal)
                        { dead = true; tailPosition = damageEvents[i].TargetPosition; }
                    if (stale.IsBoss && playBossCues)
                    {
                        foreach (var cue in _bossCues)
                            if (cue != null && cue.Kind != BossCueKind.None && cue.EnemyId == enemyId) cue.Clear();
                        var end = _simulation.Path.GetPosition(_simulation.Path.TotalLength);
                        var beforeEnd = _simulation.Path.GetPosition(Mathf.Max(0, _simulation.Path.TotalLength - .05f));
                        PlayBossCue(dead ? BossCueKind.Death : BossCueKind.Leak, enemyId, tailPosition,
                            new Vector3(end.X - beforeEnd.X, 0, end.Y - beforeEnd.Y));
                    }
                    if (stale.Model != null)
                    {
                        stale.Transform.position = new Vector3(tailPosition.X, .6f, tailPosition.Y);
                        // Tail содержит только visual child; EnemyId/HP/status/selection сразу исчезают.
                        if (_enemyTails.Count == MaxEnemyTails)
                        { RemoveEnemyVisual(_enemyTails[0].gameObject); _enemyTails.RemoveAt(0); }
                        stale.Model.BeginTail(dead, transform);
                        _enemyTails.Add(stale.Model);
                    }
                }
                RemoveEnemyVisual(stale.Transform.gameObject);

                _enemyViews.Remove(enemyId);
            }
        }

        private void PlayBossCue(BossCueKind kind, EnemyId enemyId, PathPosition position, Vector3 forward)
        {
            BossCueView slot = null;
            foreach (var cue in _bossCues)
            {
                if (cue == null) continue;
                if (cue.Kind == BossCueKind.None) { slot = cue; break; }
                if (slot == null || cue.Order < slot.Order) slot = cue;
            }
            slot?.Play(kind, enemyId, position, forward, ++_bossCueOrder);
        }

        public Transform SelectEnemyPrefab(EnemyDefinitionId id) =>
            id.Value == BossHudController.BossId && bossPrefab != null ? bossPrefab : enemyPrefab;

        private static void RemoveEnemyVisual(GameObject visual)
        {
            visual.SetActive(false);
            if (UnityEngine.Application.isPlaying) Destroy(visual); else DestroyImmediate(visual);
        }

        private static bool HasEnemy(IReadOnlyList<EnemyState> enemies, EnemyId id)
        {
            for (var i = 0; i < enemies.Count; i++) if (enemies[i].Id == id) return true;
            return false;
        }

        private void UpdateHud()
        {
            var state = _simulation.State;
            var result = state.Phase == BattlePhase.Victory || state.Phase == BattlePhase.Defeat
                ? $"\nResult: {state.Phase}"
                : string.Empty;
            hudText.text = $"Wave: {state.WaveIndex + 1} / {state.WaveCount}\n" +
                           $"Base Health: {state.BaseHealth}\n" +
                           $"Gold: {state.Gold}\n" +
                           $"Towers: {state.Towers.Count}\n" +
                           $"Kills: {state.Kills}\n" +
                           $"Active Enemies: {state.Enemies.Count}\n" +
                           $"Tick: {state.Tick}\n" +
                           $"Phase: {state.Phase}" +
                           result + (_simulation.State.ElementProgression.RulesId == "legacy-all-open-v1"
                               ? "\nLegacy: прежние правила" : string.Empty);
        }

        private sealed class EnemyView
        {
            public EnemyView(Transform transform, bool isBoss)
            {
                Transform = transform;
                IsBoss = isBoss;
                HealthBar = transform.GetComponentInChildren<EnemyHealthBarView>(true);
                Status = transform.GetComponent<EnemyStatusView>();
                Model = transform.GetComponentInChildren<EnemyModelView>(true);
            }

            public Transform Transform { get; }
            public bool IsBoss { get; }
            public bool BossThreatShown { get; set; }
            public EnemyHealthBarView HealthBar { get; }
            public EnemyStatusView Status { get; }
            public EnemyModelView Model { get; }
            public long SeenTick { get; set; }
        }
    }
}
