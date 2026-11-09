using System;
using System.Collections.Generic;
using UnityEngine;
using Veilward.Domain;

namespace Veilward.Presentation
{
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

        private readonly Dictionary<EnemyId, EnemyView> _enemyViews = new Dictionary<EnemyId, EnemyView>();
        private readonly Dictionary<TowerId, Transform> _towerViews = new Dictionary<TowerId, Transform>();
        private readonly List<EnemyId> _staleEnemyIds = new List<EnemyId>();
        private readonly List<TowerId> _staleTowerIds = new List<TowerId>();
        private BattleSimulation _simulation;
        private float _accumulator;

        public BattleSimulation Simulation => _simulation;

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

            _simulation = scenario.CreateSimulation();
            SynchronizeViews();
            UpdateHud();
        }

        public PlaceTowerResult PlaceTower(GridCoordinate coordinate)
        {
            if (_simulation == null)
            {
                throw new InvalidOperationException("Battle simulation is not initialized.");
            }

            return _simulation.PlaceTower(new PlaceTowerCommand(_simulation.TowerDefinition.Id, coordinate));
        }

        public TowerOperationResult UpgradeTower(TowerId towerId)
        {
            if (_simulation == null)
            {
                throw new InvalidOperationException("Battle simulation is not initialized.");
            }

            return _simulation.UpgradeTower(towerId);
        }

        public TowerOperationResult SellTower(TowerId towerId)
        {
            if (_simulation == null)
            {
                throw new InvalidOperationException("Battle simulation is not initialized.");
            }

            var result = _simulation.SellTower(towerId);
            if (result.Success)
            {
                SynchronizeTowerViews();
            }

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

            var result = _simulation.StartWave();
            ProcessAttackEvents();
            return result;
        }

        public WaveTransitionResult ContinueToNextWave()
        {
            if (_simulation == null)
            {
                throw new InvalidOperationException("Battle simulation is not initialized.");
            }

            return _simulation.ContinueToNextWave();
        }

        private void Update()
        {
            if (_simulation == null)
            {
                return;
            }

            _accumulator += Mathf.Min(Time.deltaTime, 0.25f);
            var steps = 0;
            while (_accumulator >= StepSeconds && steps < MaxStepsPerFrame)
            {
                _simulation.Step();
                ProcessAttackEvents();
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
        }

        private void ProcessAttackEvents()
        {
            try
            {
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
                        end);
                }
            }
            finally
            {
                _simulation.ClearAttackEvents();
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
                if (_towerViews.ContainsKey(tower.Id))
                {
                    continue;
                }

                var view = Instantiate(towerPrefab, transform);
                view.name = $"Tower {tower.Id.Value}";
                view.position = GetCellCenter(tower.Coordinate);
                view.localScale = Vector3.one * _simulation.Grid.Map.CellSize;
                _towerViews.Add(tower.Id, view);
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
                if (Application.isPlaying)
                {
                    Destroy(staleView);
                }
                else
                {
                    DestroyImmediate(staleView);
                }

                _towerViews.Remove(towerId);
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
            var state = _simulation.State;
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
                if (Application.isPlaying)
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
                           result;
        }

        private sealed class EnemyView
        {
            public EnemyView(Transform transform)
            {
                Transform = transform;
                HealthBar = transform.GetComponentInChildren<EnemyHealthBarView>(true);
            }

            public Transform Transform { get; }
            public EnemyHealthBarView HealthBar { get; }
            public long SeenTick { get; set; }
        }
    }
}
