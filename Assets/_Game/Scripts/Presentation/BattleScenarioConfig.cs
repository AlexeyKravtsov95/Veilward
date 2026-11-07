using System;
using UnityEngine;
using Veilward.Domain;

namespace Veilward.Presentation
{
    [CreateAssetMenu(fileName = "TestWave", menuName = "Veilward/Battle Scenario")]
    public sealed class BattleScenarioConfig : ScriptableObject
    {
        [SerializeField, Min(1)] private int baseHealth = 20;
        [SerializeField] private string enemyId = "shade";
        [SerializeField, Min(1)] private int maxHealth = 100;
        [SerializeField, Min(0.01f)] private float speed = 1.5f;
        [SerializeField, Min(1)] private int leakDamage = 1;
        [SerializeField, Min(1)] private int enemyCount = 5;
        [SerializeField, Min(0)] private int firstSpawnTick;
        [SerializeField, Min(1)] private int spawnIntervalTicks = 30;
        [SerializeField] private Vector2Int[] waypoints =
        {
            new Vector2Int(-5, -2),
            new Vector2Int(0, -2),
            new Vector2Int(0, 2),
            new Vector2Int(5, 2)
        };

        [Header("Build Grid")]
        [SerializeField, Min(1)] private int gridWidth = 12;
        [SerializeField, Min(1)] private int gridHeight = 8;
        [SerializeField] private Vector2Int gridOrigin = new Vector2Int(-6, -4);
        [SerializeField, Min(0.01f)] private float cellSize = 1f;

        [Header("Test Tower")]
        [SerializeField, Min(0)] private int startingGold = 150;
        [SerializeField] private string towerId = "test-tower";
        [SerializeField, Min(0)] private int towerPrice = 60;
        [SerializeField, Min(0.01f)] private float towerRange = 2.5f;
        [SerializeField, Min(0.01f)] private float towerFireRate = 1f;
        [SerializeField, Min(1)] private int towerDamage = 10;

        [Header("Wave Sequence")]
        [SerializeField] private BattleScenarioConfig sharedScenario;
        [SerializeField, Min(0)] private int enemyReward;
        [SerializeField, Min(0)] private int waveCompletionBonus;
        [SerializeField] private WaveSettings[] waves;

        [Serializable]
        public sealed class WaveSettings
        {
            [Min(1)] public int enemyCount = 5;
            [Min(0)] public int firstSpawnTick;
            [Min(1)] public int spawnIntervalTicks = 30;
        }

        public static BattleScenarioConfig CreateDefault()
        {
            return CreateInstance<BattleScenarioConfig>();
        }

        public BattleSimulation CreateSimulation()
        {
            var shared = sharedScenario != null ? sharedScenario : this;
            var gridPoints = new GridCoordinate[shared.waypoints.Length];
            for (var index = 0; index < shared.waypoints.Length; index++)
            {
                gridPoints[index] = new GridCoordinate(
                    shared.waypoints[index].x,
                    shared.waypoints[index].y);
            }

            var map = new GridMapDefinition(
                shared.gridWidth,
                shared.gridHeight,
                new GridCoordinate(shared.gridOrigin.x, shared.gridOrigin.y),
                shared.cellSize);
            var path = new PathDefinition(gridPoints);
            var enemy = new EnemyDefinition(
                new EnemyDefinitionId(shared.enemyId),
                shared.maxHealth,
                shared.speed,
                shared.leakDamage,
                enemyReward);
            var tower = new TowerDefinition(
                new TowerDefinitionId(shared.towerId),
                shared.towerPrice,
                shared.towerRange,
                shared.towerFireRate,
                shared.towerDamage);

            if (waves == null || waves.Length == 0)
            {
                var legacyWave = new WaveDefinition(
                    enemy,
                    shared.enemyCount,
                    shared.firstSpawnTick,
                    shared.spawnIntervalTicks);
                return new BattleSimulation(map, path, legacyWave, tower, shared.baseHealth, shared.startingGold);
            }

            var sequence = new WaveDefinition[waves.Length];
            for (var index = 0; index < waves.Length; index++)
            {
                var settings = waves[index];
                sequence[index] = new WaveDefinition(
                    enemy,
                    settings.enemyCount,
                    settings.firstSpawnTick,
                    settings.spawnIntervalTicks);
            }

            return new BattleSimulation(
                map,
                path,
                sequence,
                tower,
                shared.baseHealth,
                shared.startingGold,
                waveCompletionBonus);
        }

        private void OnValidate()
        {
            baseHealth = Mathf.Max(1, baseHealth);
            maxHealth = Mathf.Max(1, maxHealth);
            speed = Mathf.Max(0.01f, speed);
            leakDamage = Mathf.Max(1, leakDamage);
            enemyCount = Mathf.Max(1, enemyCount);
            firstSpawnTick = Mathf.Max(0, firstSpawnTick);
            spawnIntervalTicks = Mathf.Max(1, spawnIntervalTicks);
            gridWidth = Mathf.Max(1, gridWidth);
            gridHeight = Mathf.Max(1, gridHeight);
            cellSize = Mathf.Max(0.01f, cellSize);
            startingGold = Mathf.Max(0, startingGold);
            towerPrice = Mathf.Max(0, towerPrice);
            towerRange = Mathf.Max(0.01f, towerRange);
            towerFireRate = Mathf.Max(0.01f, towerFireRate);
            towerDamage = Mathf.Max(1, towerDamage);
            enemyReward = Mathf.Max(0, enemyReward);
            waveCompletionBonus = Mathf.Max(0, waveCompletionBonus);
            if (waves == null)
            {
                return;
            }

            foreach (var wave in waves)
            {
                if (wave == null)
                {
                    continue;
                }

                wave.enemyCount = Mathf.Max(1, wave.enemyCount);
                wave.firstSpawnTick = Mathf.Max(0, wave.firstSpawnTick);
                wave.spawnIntervalTicks = Mathf.Max(1, wave.spawnIntervalTicks);
            }
        }
    }
}
