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

        public static BattleScenarioConfig CreateDefault()
        {
            return CreateInstance<BattleScenarioConfig>();
        }

        public BattleSimulation CreateSimulation()
        {
            var gridPoints = new GridCoordinate[waypoints.Length];
            for (var index = 0; index < waypoints.Length; index++)
            {
                gridPoints[index] = new GridCoordinate(waypoints[index].x, waypoints[index].y);
            }

            var map = new GridMapDefinition(
                gridWidth,
                gridHeight,
                new GridCoordinate(gridOrigin.x, gridOrigin.y),
                cellSize);
            var path = new PathDefinition(gridPoints);
            var enemy = new EnemyDefinition(
                new EnemyDefinitionId(enemyId),
                maxHealth,
                speed,
                leakDamage,
                reward: 0);
            var wave = new WaveDefinition(enemy, enemyCount, firstSpawnTick, spawnIntervalTicks);
            var tower = new TowerDefinition(
                new TowerDefinitionId(towerId),
                towerPrice,
                towerRange,
                towerFireRate,
                towerDamage);
            return new BattleSimulation(map, path, wave, tower, baseHealth, startingGold);
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
        }
    }
}
