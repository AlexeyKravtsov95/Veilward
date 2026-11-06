using System;
using System.Collections.Generic;
using UnityEngine;
using Veilward.Domain;

namespace Veilward.Presentation
{
    public sealed class BattleSceneController : MonoBehaviour
    {
        private const float StepSeconds = 1f / SimulationTiming.TicksPerSecond;
        private const int MaxStepsPerFrame = 8;

        [SerializeField] private BattleScenarioConfig scenario;
        [SerializeField] private Transform enemyPrefab;
        [SerializeField] private Transform towerPrefab;
        [SerializeField] private UnityEngine.UI.Text hudText;

        private readonly Dictionary<EnemyId, EnemyView> _enemyViews = new Dictionary<EnemyId, EnemyView>();
        private readonly Dictionary<TowerId, Transform> _towerViews = new Dictionary<TowerId, Transform>();
        private readonly List<EnemyId> _staleEnemyIds = new List<EnemyId>();
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

        public Vector3 GetCellCenter(GridCoordinate coordinate)
        {
            var position = _simulation.Grid.Map.GetCellCenter(coordinate);
            return new Vector3(position.X, 0.04f, position.Y);
        }

        public void StartWave()
        {
            if (_simulation == null)
            {
                throw new InvalidOperationException("Battle simulation is not initialized.");
            }

            _simulation.StartWave();
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
                Destroy(_enemyViews[enemyId].Transform.gameObject);
                _enemyViews.Remove(enemyId);
            }
        }

        private void UpdateHud()
        {
            var state = _simulation.State;
            hudText.text = $"Base Health: {state.BaseHealth}\n" +
                           $"Gold: {state.Gold}\n" +
                           $"Towers: {state.Towers.Count}\n" +
                           $"Active Enemies: {state.Enemies.Count}\n" +
                           $"Tick: {state.Tick}\n" +
                           $"Phase: {state.Phase}";
        }

        private sealed class EnemyView
        {
            public EnemyView(Transform transform)
            {
                Transform = transform;
            }

            public Transform Transform { get; }
            public long SeenTick { get; set; }
        }
    }
}
