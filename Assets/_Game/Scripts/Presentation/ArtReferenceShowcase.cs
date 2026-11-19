using System;
using UnityEngine;
using System.Collections.Generic;
using Veilward.Domain;

namespace Veilward.Presentation
{
    // Изолированный art harness: saved combat без checkpoint/UI и без изменений Battle.
    public sealed class ArtReferenceShowcase : MonoBehaviour
    {
        public const int EnemyCapacity = 12;
        public const int StrikeCapacity = 8;
        public const float StrikeLifetime = FlameStrikeView.Lifetime;
        [SerializeField] private BattleScenarioConfig scenario;
        [SerializeField] private Transform towerPrefab;
        [SerializeField] private Transform enemyPrefab;
        [SerializeField] private Material strikeMaterial;
        [SerializeField] private Material strikeCoreMaterial;
        [SerializeField] private Material occlusionMaterial;
        [SerializeField] private Material towerOcclusionMask;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Transform environmentRoot;
        [SerializeField] private Vector2Int towerCell = new Vector2Int(-2, -1);
        private readonly Transform[] _enemies = new Transform[EnemyCapacity];
        private readonly EnemyHealthBarView[] _health = new EnemyHealthBarView[EnemyCapacity];
        private readonly FlameStrikeView[] _strikes = new FlameStrikeView[StrikeCapacity];
        private readonly List<Rect> _healthRects = new List<Rect>(EnemyCapacity);
        private const int SnapshotCapacity = FlameStrikeView.FlightTicks + 1;
        private readonly EnemySnapshot[] _snapshots = new EnemySnapshot[SnapshotCapacity * EnemyCapacity];
        private readonly int[] _snapshotCounts = new int[SnapshotCapacity];
        private readonly long[] _snapshotTicks = new long[SnapshotCapacity];
        private readonly EnemyId[] _viewEnemyIds = new EnemyId[EnemyCapacity];
        private int _snapshotWrite;
        private Material _healthFillOverlay, _healthBackgroundOverlay;
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
        public AttackEvent LastAttack { get; private set; }
        public long PresentedTick => _snapshotTicks[ReadSnapshotIndex];
        private int ReadSnapshotIndex => (_snapshotWrite + 1) % SnapshotCapacity;
        public FlameStrikeView GetStrike(int index) => _strikes[index];
        public Transform FindEnemyView(EnemyId id)
        {
            for (var i = 0; i < EnemyCapacity; i++)
                if (_viewEnemyIds[i] == id && _enemies[i].gameObject.activeSelf) return _enemies[i];
            return null;
        }
        public int? DisplayedHealth(EnemyId id)
        {
            var index = ReadSnapshotIndex;
            for (var i = 0; i < _snapshotCounts[index]; i++)
            {
                var enemy = _snapshots[index * EnemyCapacity + i];
                if (enemy.Id == id) return enemy.Health;
            }
            return null;
        }
        public Transform TowerView => _tower;
        public Camera WorldCamera => worldCamera;
        public Transform EnvironmentRoot => environmentRoot;
        public int ActiveStrikeCount
        {
            get { var n = 0; foreach (var s in _strikes) if (s != null && s.Remaining > 0) n++; return n; }
        }
        public Transform FirstEnemyView
        {
            get { var index = ReadSnapshotIndex; return _snapshotCounts[index] == 0 ? null : FindEnemyView(_snapshots[index * EnemyCapacity].Id); }
        }

        private void Start()
        {
            if (scenario == null || towerPrefab == null || enemyPrefab == null || worldCamera == null ||
                strikeMaterial == null || strikeCoreMaterial == null)
                throw new InvalidOperationException("ArtReference serialized references are required.");
            _healthFillOverlay = HealthOverlay("HealthFill");
            _healthBackgroundOverlay = HealthOverlay("HealthBackground");
            _tower = Instantiate(towerPrefab, transform);
            _tower.name = "FLAME - Ember Lantern";
            if (towerOcclusionMask != null) ShadeOcclusionView.Create(_tower, towerOcclusionMask);
            for (var i = 0; i < EnemyCapacity; i++)
            {
                _enemies[i] = Instantiate(enemyPrefab, transform);
                _enemies[i].name = "Shade Pool " + i;
                _health[i] = _enemies[i].GetComponent<EnemyHealthBarView>();
                if (_health[i] == null) throw new InvalidOperationException("Shade health view is required.");
                ConfigureHealthView(_health[i]);
                _health[i].Face(worldCamera);
                if (occlusionMaterial != null) ShadeOcclusionView.Create(_enemies[i], occlusionMaterial);
                _enemies[i].gameObject.SetActive(false);
            }
            for (var i = 0; i < StrikeCapacity; i++)
                _strikes[i] = new FlameStrikeView(transform, strikeMaterial, strikeCoreMaterial, i);
            RestartShowcase();
        }

        internal void SetDeviceProbeOverride(bool active)
        {
            if (active)
            {
                foreach (var enemy in _enemies) enemy.gameObject.SetActive(false);
                foreach (var strike in _strikes) strike.Clear();
            }
            else SynchronizeViews();
        }

        internal Transform CreateDeviceProbeEnemy(Transform parent)
        {
            var view = Instantiate(enemyPrefab, parent);
            ConfigureHealthView(view.GetComponent<EnemyHealthBarView>());
            if (occlusionMaterial != null) ShadeOcclusionView.Create(view, occlusionMaterial);
            return view;
        }

        internal FlameStrikeView CreateDeviceProbeStrike(Transform parent) =>
            new FlameStrikeView(parent, strikeMaterial, strikeCoreMaterial, 0);

        private Material HealthOverlay(string part)
        {
            var source = enemyPrefab.Find("Health Bar/" + part).GetComponent<Renderer>().sharedMaterial;
            // Только эталон: две shared copies, прежние цвета; HP рисуются после короткого VFX.
            var material = new Material(source) { shader = strikeMaterial.shader, renderQueue = 3030 };
            material.SetColor("_BaseColor", source.GetColor("_BaseColor"));
            return material;
        }

        public void ConfigureHealthView(EnemyHealthBarView health)
        {
            foreach (var renderer in health.GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.name == "HealthFill")
                { renderer.sharedMaterial = _healthFillOverlay; renderer.sortingOrder = 101; }
                else if (renderer.name == "HealthBackground")
                { renderer.sharedMaterial = _healthBackgroundOverlay; renderer.sortingOrder = 100; }
            }
        }

        private void OnDestroy()
        {
            if (_healthFillOverlay != null) Destroy(_healthFillOverlay);
            if (_healthBackgroundOverlay != null) Destroy(_healthBackgroundOverlay);
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
            LastAttack = default;
            Simulation.StartWave();
            ResetSnapshots();
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
            _accumulator += delta;
            const float step = 1f / SimulationTiming.TicksPerSecond;
            var guard = 0;
            while (_accumulator >= step && guard++ < 8)
            {
                AdvanceOneTick();
                _accumulator -= step;
            }
            SynchronizeViews();
            if (Simulation.State.Phase != BattlePhase.Combat)
            {
                _terminalTime += delta;
                if (_terminalTime >= 2f) RestartShowcase();
            }
        }

        // Для воспроизводимого Editor evidence; проходит те же public simulation/events, без fake damage.
        public void AdvanceForReview(int ticks)
        {
            if (!IsPaused) throw new InvalidOperationException("Pause showcase before deterministic review stepping.");
            if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(ticks));
            for (var i = 0; i < ticks; i++)
            {
                AdvanceOneTick();
            }
            SynchronizeViews();
        }

        private void AdvanceOneTick()
        {
            foreach (var strike in _strikes) strike.Advance(1f / SimulationTiming.TicksPerSecond);
            if (Simulation.State.Phase == BattlePhase.Combat) Simulation.Step();
            try
            {
                var attacks = Simulation.AttackEvents;
                for (var i = 0; i < attacks.Count; i++)
                {
                    var attack = attacks[i];
                    var end = new Vector3(attack.TargetPosition.X, .72f, attack.TargetPosition.Y);
                    if (_strikes[_nextStrike].Remaining > 0) throw new InvalidOperationException("ArtReference flight pool capacity exceeded.");
                    _strikes[_nextStrike].Play(_tower.position + Vector3.up * .95f, end, attack.TargetEnemyId, attack.WasLethal);
                    LastAttack = attack;
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
            _snapshotWrite = (_snapshotWrite + 1) % SnapshotCapacity;
            CaptureSnapshot(_snapshotWrite);
        }

        private readonly struct EnemySnapshot
        {
            public EnemySnapshot(EnemyId id, int health, int maxHealth, Vector3 position, Quaternion rotation)
            { Id = id; Health = health; MaxHealth = maxHealth; Position = position; Rotation = rotation; }
            public readonly EnemyId Id;
            public readonly int Health, MaxHealth;
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
        }

        private void CaptureSnapshot(int index)
        {
            var enemies = Simulation.State.Enemies;
            if (enemies.Count > EnemyCapacity) throw new InvalidOperationException("ArtReference enemy pool capacity exceeded.");
            _snapshotCounts[index] = enemies.Count; _snapshotTicks[index] = Simulation.State.Tick;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                var p = Simulation.Path.GetPosition(enemy.DistanceAlongPath);
                var ahead = Simulation.Path.GetPosition(enemy.DistanceAlongPath + .05f);
                var direction = new Vector3(ahead.X - p.X, 0, ahead.Y - p.Y);
                var rotation = direction.sqrMagnitude > .00001f ? Quaternion.LookRotation(direction) : Quaternion.identity;
                _snapshots[index * EnemyCapacity + i] = new EnemySnapshot(enemy.Id, enemy.Health,
                    enemy.Definition.MaxHealth, new Vector3(p.X, .14f, p.Y), rotation);
            }
        }

        private void ResetSnapshots()
        {
            _snapshotWrite = 0;
            Array.Clear(_viewEnemyIds, 0, EnemyCapacity);
            for (var i = 0; i < SnapshotCapacity; i++) CaptureSnapshot(i);
            foreach (var enemy in _enemies) enemy.gameObject.SetActive(false);
        }

        private bool SnapshotContains(int index, EnemyId id)
        {
            for (var i = 0; i < _snapshotCounts[index]; i++)
                if (_snapshots[index * EnemyCapacity + i].Id == id) return true;
            return false;
        }

        private void SynchronizeViews()
        {
            var snapshot = ReadSnapshotIndex;
            _healthRects.Clear();
            for (var slot = 0; slot < EnemyCapacity; slot++)
                if (!SnapshotContains(snapshot, _viewEnemyIds[slot]))
                { _viewEnemyIds[slot] = default; _enemies[slot].gameObject.SetActive(false); }
            // Snapshot order — EnemyId order; view slots сохраняют ownership до удаления ID из timeline.
            for (var i = 0; i < _snapshotCounts[snapshot]; i++)
            {
                var enemy = _snapshots[snapshot * EnemyCapacity + i];
                var slot = -1;
                for (var j = 0; j < EnemyCapacity; j++) if (_viewEnemyIds[j] == enemy.Id) { slot = j; break; }
                if (slot < 0)
                    for (var j = 0; j < EnemyCapacity; j++) if (_viewEnemyIds[j].Value == 0) { slot = j; break; }
                if (slot < 0) throw new InvalidOperationException("ArtReference view slots exhausted.");
                _viewEnemyIds[slot] = enemy.Id;
                var view = _enemies[slot]; view.gameObject.SetActive(true);
                view.SetPositionAndRotation(enemy.Position, enemy.Rotation);
                _health[slot].SetNormalizedHealth((float)enemy.Health / enemy.MaxHealth);
                _health[slot].Face(worldCamera); _health[slot].Layout(worldCamera, _healthRects);
            }
        }

        private void OnDisable()
        {
            foreach (var strike in _strikes) if (strike != null) strike.Clear();
            foreach (var enemy in _enemies) if (enemy != null) enemy.gameObject.SetActive(false);
            Array.Clear(_snapshotCounts, 0, SnapshotCapacity); Array.Clear(_viewEnemyIds, 0, EnemyCapacity);
            Array.Clear(_snapshots, 0, _snapshots.Length); Array.Clear(_snapshotTicks, 0, SnapshotCapacity);
            LastAttack = default;
            _accumulator = 0; _skipNextDelta = true;
        }

        private void OnEnable()
        {
            if (Simulation == null) return;
            ResetSnapshots(); SynchronizeViews();
        }
    }
}
