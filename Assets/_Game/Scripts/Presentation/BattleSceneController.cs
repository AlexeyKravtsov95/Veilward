using System;
using System.Collections.Generic;
using UnityEngine;
using Veilward.Application;
using Veilward.Domain;
using Veilward.Infrastructure;
using Application = UnityEngine.Application;

namespace Veilward.Presentation
{
    public enum BattleInputOwner { None, Placement, Sell, Inspector, ElementChoice }

    [DefaultExecutionOrder(-100)]
    public sealed class BattleSceneController : MonoBehaviour
    {
        private const float StepSeconds = 1f / SimulationTiming.TicksPerSecond;
        private const int MaxStepsPerFrame = 8;

        [SerializeField] private BattleScenarioConfig scenario;
        [SerializeField] private Transform enemyPrefab;
        [SerializeField] private Transform towerPrefab;
        [SerializeField] private UnityEngine.UI.Text hudText;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Material attackBeamMaterial;
        [SerializeField] private Material reactionEffectMaterial;
        [SerializeField] private BattlePauseController pauseController;
        [SerializeField] private BattlePlacementController placementController;
        [SerializeField] private BattleCheckpointController checkpointController;
        [SerializeField] private EnemyInspectorController enemyInspector;
        [SerializeField] private ElementChoiceController elementChoice;
        [SerializeField] private UnityEngine.UI.Button mainMenuButton;
        [SerializeField] private UnityEngine.UI.Text reactionSummaryText;
        private const int ReactionPoolCapacity = 24;
        private readonly ReactionEffectView[] _reactionPool = new ReactionEffectView[ReactionPoolCapacity];
        private int _nextReactionEffect;
        private Vector2 _reactionSummaryOrigin;
        private Canvas _summaryCanvas;
        public ReactionPresentationState Presentation { get; } = new ReactionPresentationState();

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
                if (placementController != null && placementController.IsSellConfirmationVisible) return BattleInputOwner.Sell;
                if (enemyInspector != null && enemyInspector.SelectedEnemyId.HasValue) return BattleInputOwner.Inspector;
                // Pending обязателен даже до первого Refresh UI.
                if (_simulation != null && _simulation.State.ElementProgression.PendingChoiceId != "none")
                    return BattleInputOwner.ElementChoice;
                if (placementController != null && placementController.HasSelection) return BattleInputOwner.Placement;
                return BattleInputOwner.None;
            }
        }

        public bool CanAcceptInput(BattleInputOwner owner = BattleInputOwner.None) =>
            !IsPaused && (ActiveModalOwner == BattleInputOwner.None || ActiveModalOwner == owner);

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
            if (session != null && session.Checkpoint == null && session.ScenarioContentId != scenario.ScenarioContentId)
                throw new InvalidOperationException("New Game session scenario content ID does not match.");
            _simulation = checkpoint == null ? scenario.CreateSimulation() : scenario.CreateSimulationForCheckpoint(checkpoint);
            if (reactionSummaryText != null)
            {
                _reactionSummaryOrigin = reactionSummaryText.rectTransform.anchoredPosition;
                _summaryCanvas = reactionSummaryText.GetComponentInParent<Canvas>();
            }
            for (var i = 0; i < _reactionPool.Length; i++)
            {
                var effect = new GameObject("Reaction effect " + i).AddComponent<ReactionEffectView>();
                effect.transform.SetParent(transform, false);
                effect.Initialize(reactionEffectMaterial != null ? reactionEffectMaterial : attackBeamMaterial);
                _reactionPool[i] = effect;
            }
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
                    checkpoint?.scenarioContentId ?? scenario.ScenarioContentId,
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
            for (var i = 0; i < _reactionPool.Length; i++)
                if (_reactionPool[i] != null) _reactionPool[i].Advance(activeDelta);
            _accumulator += activeDelta;
            var steps = 0;
            while (_accumulator >= StepSeconds && steps < MaxStepsPerFrame)
            {
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

            SynchronizeViews();
            UpdateHud();
            if (reactionSummaryText != null && _summaryCanvas != null && _summaryCanvas.scaleFactor > 0)
            {
                var area = Screen.safeArea;
                reactionSummaryText.rectTransform.anchoredPosition = _reactionSummaryOrigin + new Vector2(
                    area.center.x - Screen.width * .5f, -Mathf.Max(0, Screen.height - area.yMax)) / _summaryCanvas.scaleFactor;
            }
        }

        private void ProcessAttackEvents()
        {
            try
            {
                var reactions = _simulation.ReactionEvents;
                var damages = _simulation.DamageEvents;
                for (var i = 0; i < reactions.Count; i++)
                {
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
                _simulation.ClearAttackEvents();
                // Все consumer используют один Step; очистка обязательна и при исключении.
                _simulation.ClearReactionEvents();
                _simulation.ClearDamageEvents();
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

        public void FreezePresentationClock() { _accumulator = 0; _skipNextDelta = true; }

        public void ResetPresentation()
        {
            Presentation.Reset(); _accumulator = 0; _skipNextDelta = true; _nextReactionEffect = 0;
            for (var i = 0; i < _reactionPool.Length; i++) if (_reactionPool[i] != null) _reactionPool[i].Clear();
            foreach (var beam in GetComponentsInChildren<AttackBeamView>())
                if (UnityEngine.Application.isPlaying) Destroy(beam.gameObject); else DestroyImmediate(beam.gameObject);
            foreach (var view in _enemyViews.Values)
                if (UnityEngine.Application.isPlaying) Destroy(view.Transform.gameObject); else DestroyImmediate(view.Transform.gameObject);
            _enemyViews.Clear();
            foreach (var view in _towerViews.Values)
                if (UnityEngine.Application.isPlaying) Destroy(view.gameObject); else DestroyImmediate(view.gameObject);
            _towerViews.Clear(); _towerViewLevels.Clear();
            if (enemyInspector != null) enemyInspector.ClearSelection();
            if (placementController != null) placementController.ClearSelection();
            if (elementChoice != null) elementChoice.ClearPreview();
            if (reactionSummaryText != null) reactionSummaryText.text = string.Empty;
            SynchronizeViews();
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

        private void SynchronizeEnemyViews()
        {
            Presentation.Prune(_simulation.State.Enemies);
            var state = _simulation.State;
            _statusRects.Clear();
            for (var index = 0; index < state.Enemies.Count; index++)
            {
                var enemy = state.Enemies[index];
                if (!_enemyViews.TryGetValue(enemy.Id, out var view))
                {
                    view = new EnemyView(Instantiate(enemyPrefab, transform));
                    _enemyViews.Add(enemy.Id, view);
                }

                var position = _simulation.Path.GetPosition(enemy.DistanceAlongPath);
                view.Transform.position = new Vector3(position.X, 0.6f, position.Y);
                view.SeenTick = state.Tick;
                if (view.Status != null)
                    view.Status.Refresh(enemy, state.Tick, _simulation.ReactionCatalog,
                        Presentation.GetMemory(enemy.Id), Presentation.ActiveTime, worldCamera);
                if (view.Status != null) view.Status.Layout(worldCamera, _statusRects);
                if (view.HealthBar != null)
                {
                    view.HealthBar.SetNormalizedHealth((float)enemy.Health / enemy.Definition.MaxHealth);
                    view.HealthBar.Face(worldCamera);
                }
            }

            _staleEnemyIds.Clear();
            foreach (var pair in _enemyViews)
            {
                if (pair.Value.SeenTick != state.Tick)
                {
                    _staleEnemyIds.Add(pair.Key);
                }
            }

            for (var index = 0; index < _staleEnemyIds.Count; index++)
            {
                var enemyId = _staleEnemyIds[index];
                var staleView = _enemyViews[enemyId].Transform.gameObject;
                if (UnityEngine.Application.isPlaying)
                {
                    Destroy(staleView);
                }
                else
                {
                    DestroyImmediate(staleView);
                }

                _enemyViews.Remove(enemyId);
            }
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
                               ? "\nLegacy: прежние правила, все стихии открыты" : string.Empty);
        }

        private sealed class EnemyView
        {
            public EnemyView(Transform transform)
            {
                Transform = transform;
                HealthBar = transform.GetComponentInChildren<EnemyHealthBarView>(true);
                Status = transform.GetComponent<EnemyStatusView>();
            }

            public Transform Transform { get; }
            public EnemyHealthBarView HealthBar { get; }
            public EnemyStatusView Status { get; }
            public long SeenTick { get; set; }
        }
    }
}
