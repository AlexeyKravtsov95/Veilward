using System;
using UnityEngine;
using Veilward.Domain;

namespace Veilward.Presentation
{
    [CreateAssetMenu(fileName = "TestWave", menuName = "Veilward/Battle Scenario")]
    public sealed class BattleScenarioConfig : ScriptableObject
    {
        [SerializeField] private string scenarioContentId = "legacy-single-wave-v1";
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
        [SerializeField] private TowerUpgradeSettings[] towerUpgrades =
        {
            new TowerUpgradeSettings { price = 50, range = 3f, fireRate = 2f, damage = 35 },
            new TowerUpgradeSettings { price = 80, range = 3f, fireRate = 2f, damage = 50 }
        };

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

        [Serializable]
        public sealed class TowerUpgradeSettings
        {
            [Min(0)] public int price = 50;
            [Min(0.01f)] public float range = 3f;
            [Min(0.01f)] public float fireRate = 2f;
            [Min(1)] public int damage = 35;
        }

        public string ScenarioContentId => scenarioContentId;

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
            var upgradeCount = shared.towerUpgrades?.Length ?? 0;
            var towerLevels = new TowerDefinition[upgradeCount + 1];
            towerLevels[0] = new TowerDefinition(
                new TowerDefinitionId(shared.towerId),
                shared.towerPrice,
                shared.towerRange,
                shared.towerFireRate,
                shared.towerDamage);
            for (var index = 0; index < upgradeCount; index++)
            {
                var upgrade = shared.towerUpgrades[index] ?? throw new ArgumentException(
                    "Tower upgrades cannot contain null entries.",
                    nameof(towerUpgrades));
                towerLevels[index + 1] = new TowerDefinition(
                    new TowerDefinitionId(shared.towerId),
                    upgrade.price,
                    upgrade.range,
                    upgrade.fireRate,
                    upgrade.damage);
            }

            if (waves == null || waves.Length == 0)
            {
                var legacyWave = new WaveDefinition(
                    enemy,
                    shared.enemyCount,
                    shared.firstSpawnTick,
                    shared.spawnIntervalTicks);
                return new BattleSimulation(
                    map,
                    path,
                    legacyWave,
                    towerLevels,
                    shared.baseHealth,
                    shared.startingGold);
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
                towerLevels,
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
            if (towerUpgrades != null)
            {
                foreach (var upgrade in towerUpgrades)
                {
                    if (upgrade == null)
                    {
                        continue;
                    }

                    upgrade.price = Mathf.Max(0, upgrade.price);
                    upgrade.range = Mathf.Max(0.01f, upgrade.range);
                    upgrade.fireRate = Mathf.Max(0.01f, upgrade.fireRate);
                    upgrade.damage = Mathf.Max(1, upgrade.damage);
                }
            }

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
