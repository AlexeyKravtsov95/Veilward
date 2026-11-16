using System;
using UnityEngine;
using UnityEngine.Rendering;
using Veilward.Domain;

namespace Veilward.Presentation
{
    // Изолированный art harness: saved combat без checkpoint/UI и без изменений Battle.
    public sealed class ArtReferenceShowcase : MonoBehaviour
    {
        public const int EnemyCapacity = 12;
        public const int StrikeCapacity = 8;
        public const float StrikeLifetime = .22f;
        [SerializeField] private BattleScenarioConfig scenario;
        [SerializeField] private Transform towerPrefab;
        [SerializeField] private Transform enemyPrefab;
        [SerializeField] private Material strikeMaterial;
        [SerializeField] private Material strikeCoreMaterial;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Transform environmentRoot;
        [SerializeField] private Vector2Int towerCell = new Vector2Int(-2, -1);
        private readonly Transform[] _enemies = new Transform[EnemyCapacity];
        private readonly EnemyHealthBarView[] _health = new EnemyHealthBarView[EnemyCapacity];
        private readonly Strike[] _strikes = new Strike[StrikeCapacity];
        private Transform _tower;
        private float _accumulator;
        private float _terminalTime;
        private bool _skipNextDelta = true;
        private int _nextStrike;
        private bool _applicationPaused;
        private bool _focused = true;
        public BattleSimulation Simulation { get; private set; }
        public bool IsPaused { get; private set; }
        public int ObservedAttacks { get; private set; }
        public Transform TowerView => _tower;
        public Camera WorldCamera => worldCamera;
        public Transform EnvironmentRoot => environmentRoot;
        public int ActiveStrikeCount
        {
            get { var n = 0; foreach (var s in _strikes) if (s != null && s.Remaining > 0) n++; return n; }
        }
        public Transform FirstEnemyView
        {
            get { foreach (var e in _enemies) if (e != null && e.gameObject.activeSelf) return e; return null; }
        }

        private void Start()
        {
            if (scenario == null || towerPrefab == null || enemyPrefab == null || worldCamera == null ||
                strikeMaterial == null || strikeCoreMaterial == null)
                throw new InvalidOperationException("ArtReference serialized references are required.");
            _tower = Instantiate(towerPrefab, transform);
            _tower.name = "FLAME - Ember Lantern";
            for (var i = 0; i < EnemyCapacity; i++)
            {
                _enemies[i] = Instantiate(enemyPrefab, transform);
                _enemies[i].name = "Shade Pool " + i;
                _health[i] = _enemies[i].GetComponent<EnemyHealthBarView>();
                if (_health[i] == null) throw new InvalidOperationException("Shade health view is required.");
                _enemies[i].gameObject.SetActive(false);
            }
            for (var i = 0; i < StrikeCapacity; i++)
                _strikes[i] = new Strike(transform, strikeMaterial, strikeCoreMaterial, i);
            RestartShowcase();
        }

        public void RestartShowcase()
        {
            // Аллокации только между циклами, не на кадр/атаку; saved balance не меняется.
            Simulation = scenario.CreateSimulation();
            var cell = new GridCoordinate(towerCell.x, towerCell.y);
            var result = Simulation.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId("flame"), cell));
            if (!result.Success) throw new InvalidOperationException("ArtReference FLAME placement failed.");
            var center = Simulation.Grid.Map.GetCellCenter(cell);
            _tower.position = new Vector3(center.X, .16f, center.Y);
            ObservedAttacks = 0; _nextStrike = 0; _accumulator = 0; _terminalTime = 0; _skipNextDelta = true;
            foreach (var strike in _strikes) strike.Clear();
            Simulation.StartWave();
            SynchronizeViews();
        }

        public void SetPaused(bool paused)
        {
            if (!paused && (_applicationPaused || !_focused)) return;
            IsPaused = paused;
            _accumulator = 0;
            _skipNextDelta = true;
        }

        private void OnApplicationPause(bool paused)
        {
            _applicationPaused = paused;
            if (paused) SetPaused(true);
        }

        private void OnApplicationFocus(bool focused)
        {
            _focused = focused;
            if (!focused) SetPaused(true);
        }

        private void Update() => AdvanceFrame(Time.deltaTime);

        private void AdvanceFrame(float deltaSeconds)
        {
            if (Simulation == null || IsPaused) return;
            if (_skipNextDelta)
            {
                _skipNextDelta = false;
                _accumulator = 0;
                return;
            }
            var delta = Mathf.Min(deltaSeconds, .25f);
            foreach (var s in _strikes) s.Advance(delta);
            if (Simulation.State.Phase != BattlePhase.Combat)
            {
                _terminalTime += delta;
                if (_terminalTime >= 2f) RestartShowcase();
                return;
            }
            _accumulator += delta;
            const float step = 1f / SimulationTiming.TicksPerSecond;
            var guard = 0;
            while (_accumulator >= step && guard++ < 8)
            {
                AdvanceOneTick();
                _accumulator -= step;
            }
            SynchronizeViews();
        }

        // Для воспроизводимого Editor evidence; проходит те же public simulation/events, без fake damage.
        public void AdvanceForReview(int ticks)
        {
            if (!IsPaused) throw new InvalidOperationException("Pause showcase before deterministic review stepping.");
            if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(ticks));
            for (var i = 0; i < ticks; i++)
            {
                foreach (var s in _strikes) s.Advance(1f / SimulationTiming.TicksPerSecond);
                AdvanceOneTick();
            }
            SynchronizeViews();
        }

        private void AdvanceOneTick()
        {
            Simulation.Step();
            try
            {
                var attacks = Simulation.AttackEvents;
                for (var i = 0; i < attacks.Count; i++)
                {
                    var attack = attacks[i];
                    var end = new Vector3(attack.TargetPosition.X, .72f, attack.TargetPosition.Y);
                    _strikes[_nextStrike].Play(_tower.position + Vector3.up * .95f, end);
                    _nextStrike = (_nextStrike + 1) % StrikeCapacity;
                    ObservedAttacks++;
                }
            }
            finally
            {
                Simulation.ClearAttackEvents();
                Simulation.ClearReactionEvents();
                Simulation.ClearDamageEvents();
            }
        }

        private void SynchronizeViews()
        {
            var enemies = Simulation.State.Enemies;
            if (enemies.Count > EnemyCapacity) throw new InvalidOperationException("ArtReference enemy pool capacity exceeded.");
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                var p = Simulation.Path.GetPosition(enemy.DistanceAlongPath);
                var ahead = Simulation.Path.GetPosition(enemy.DistanceAlongPath + .05f);
                var direction = new Vector3(ahead.X - p.X, 0, ahead.Y - p.Y);
                var view = _enemies[i];
                view.gameObject.SetActive(true);
                view.position = new Vector3(p.X, .14f, p.Y);
                if (direction.sqrMagnitude > .00001f) view.rotation = Quaternion.LookRotation(direction);
                _health[i].SetNormalizedHealth((float)enemy.Health / enemy.Definition.MaxHealth);
                _health[i].Face(worldCamera);
            }
            for (var i = enemies.Count; i < EnemyCapacity; i++) _enemies[i].gameObject.SetActive(false);
        }

        private sealed class Strike
        {
            private readonly LineRenderer _ribbon;
            private readonly LineRenderer _core;
            private readonly LineRenderer _impact;
            public float Remaining { get; private set; }
            public Strike(Transform parent, Material outer, Material core, int index)
            {
                var root = new GameObject("Ember Strike Pool " + index).transform;
                root.SetParent(parent, false);
                _ribbon = Line(root, "Flame Stroke", outer, 7, .13f, .045f);
                _core = Line(root, "Hot Core", core, 7, .045f, .012f);
                _impact = Line(root, "Impact Petals", outer, 13, .045f, .012f);
                Clear();
            }
            private static LineRenderer Line(Transform root, string name, Material mat, int count, float a, float b)
            {
                var go = new GameObject(name); go.transform.SetParent(root, false);
                var line = go.AddComponent<LineRenderer>();
                line.sharedMaterial = mat; line.useWorldSpace = true; line.positionCount = count;
                line.startWidth = a; line.endWidth = b; line.numCapVertices = 2;
                line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
                return line;
            }
            public void Play(Vector3 start, Vector3 end)
            {
                for (var i = 0; i < 7; i++)
                {
                    var t = i / 6f;
                    var p = Vector3.Lerp(start, end, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * .28f);
                    _ribbon.SetPosition(i, p); _core.SetPosition(i, p + Vector3.up * .008f);
                }
                for (var i = 0; i < 13; i++)
                {
                    var a = i * Mathf.PI / 6f; var r = i % 2 == 0 ? .22f : .12f;
                    _impact.SetPosition(i, end + new Vector3(Mathf.Cos(a) * r, .01f, Mathf.Sin(a) * r));
                }
                Remaining = StrikeLifetime;
                _ribbon.enabled = _core.enabled = _impact.enabled = true;
            }
            public void Advance(float dt) { if (Remaining <= 0) return; Remaining -= dt; if (Remaining <= 0) Clear(); }
            public void Clear() { Remaining = 0; _ribbon.enabled = _core.enabled = _impact.enabled = false; }
        }
    }
}
