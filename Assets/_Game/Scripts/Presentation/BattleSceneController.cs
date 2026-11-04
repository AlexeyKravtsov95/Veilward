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
        [SerializeField] private UnityEngine.UI.Text hudText;

        private readonly Dictionary<EnemyId, EnemyView> _enemyViews = new Dictionary<EnemyId, EnemyView>();
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

            if (hudText == null)
            {
                throw new InvalidOperationException("HUD text reference is required.");
            }

            _simulation = scenario.CreateSimulation();
            _simulation.StartWave();
            SynchronizeViews();
            UpdateHud();
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
