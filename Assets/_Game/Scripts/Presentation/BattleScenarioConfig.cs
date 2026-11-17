using System;
using System.Collections.Generic;
using UnityEngine;
using Veilward.Domain;
using Veilward.Infrastructure;

namespace Veilward.Presentation
{
    [CreateAssetMenu(fileName = "TestWave", menuName = "Veilward/Battle Scenario")]
    public sealed class BattleScenarioConfig : ScriptableObject
    {
        [SerializeField] private string scenarioContentId = "legacy-single-wave-v1";
        [SerializeField, Min(1)] private int baseHealth = 20;
        [SerializeField] private string enemyId = "shade";
        [SerializeField, Min(1)] private int maxHealth = 100;
        [SerializeField, Min(0)] private int enemyArmor;
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

        [Header("Elemental Tower Catalog")]
        [SerializeField] private TowerArchetypeSettings[] towerArchetypes = CreateDefaultTowerArchetypes();

        [Header("Global Reactions (shared scenario)")]
        [SerializeField] private ReactionSettings[] reactions =
        {
            new ReactionSettings { kind = ReactionKind.Steam, cooldownTicks = 60, damagePercent = 100,
                range = 1.25f, targetLimit = 4, priority = 0 },
            new ReactionSettings { kind = ReactionKind.Storm, cooldownTicks = 60, damagePercent = 75,
                range = 2f, targetLimit = 2, priority = 1 }
        };

        [Header("Wave Sequence")]
        [SerializeField] private BattleScenarioConfig sharedScenario;
        [SerializeField, Min(0)] private int enemyReward;
        [SerializeField, Min(0)] private int waveCompletionBonus;
        [SerializeField] private WaveSettings[] waves;

        [Header("Element Progression (EightWave)")]
        [SerializeField] private ElementProgressionSettings progression;

        [Serializable]
        public sealed class ElementProgressionSettings
        {
            public string rulesId;
            public ElementKind[] startingOptions;
            public int[] choiceWaves;
        }

        private ElementUnlockRules CreateProgressionRules()
        {
            if (scenarioContentId != WaveCheckpointCompatibility.NativeContentId || progression == null ||
                progression.rulesId != "wave-unlocks-v1" || waves == null || waves.Length != 8 ||
                progression.startingOptions == null || progression.startingOptions.Length != 2 ||
                progression.startingOptions[0] != ElementKind.Flame || progression.startingOptions[1] != ElementKind.Gale ||
                progression.choiceWaves == null || progression.choiceWaves.Length != 2 ||
                progression.choiceWaves[0] != 3 || progression.choiceWaves[1] != 6)
                throw new ArgumentException("Native EightWave requires wave-unlocks-v1, Flame/Gale, eight waves and choices after 3/6.");
            return new ElementUnlockRules(progression.startingOptions, progression.choiceWaves);
        }

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

        [Serializable]
        public sealed class TowerArchetypeSettings
        {
            public string id;
            public bool showInCatalog = true;
            public TowerLevelSettings[] levels;
        }

        [Serializable]
        public sealed class TowerLevelSettings
        {
            [Min(0)] public int price;
            [Min(1)] public int damage;
            [Min(0.01f)] public float range;
            [Min(0.01f)] public float fireRate;
            public StatusEffectSettings statusEffect;
            public bool hasIntrinsicReaction;
            public ReactionPayloadSettings intrinsicReaction;
            [Min(0)] public int armorPenetration;
        }

        [Serializable]
        public sealed class StatusEffectSettings
        {
            public StatusKind kind;
            [Min(1)] public int power;
            [Min(1)] public int durationTicks;
            [Min(0)] public int damageIntervalTicks;
        }

        [Serializable]
        public sealed class ReactionPayloadSettings
        {
            public ReactionKind kind;
            public int damagePercent;
            public float range;
            public int targetLimit;
        }

        [Serializable]
        public sealed class ReactionSettings
        {
            public ReactionKind kind;
            public int cooldownTicks;
            public int damagePercent;
            public float range;
            public int targetLimit;
            public int priority;
        }

        public string ScenarioContentId => scenarioContentId;

        public static BattleScenarioConfig CreateDefault()
        {
            return CreateInstance<BattleScenarioConfig>();
        }

        public BattleSimulation CreateSimulation()
        {
            if (scenarioContentId == WaveCheckpointCompatibility.LegacySingleWaveContentId &&
                sharedScenario == null)
                return CreateSimulationCore(null, historical: false);
            return CreateSimulationCore(null, historical: false, CreateProgressionRules());
        }

        public BattleSimulation CreateSimulationForCheckpoint(WaveCheckpointEnvelope normalized)
        {
            var requestedId = ScenarioContentId == WaveCheckpointCompatibility.LegacyEightWaveContentId
                ? WaveCheckpointCompatibility.NativeContentId : ScenarioContentId;
            if (normalized == null || normalized.schemaVersion != WaveCheckpointStore.CurrentSchemaVersion ||
                !WaveCheckpointCompatibility.TryNormalize(normalized, requestedId, out _, out var error))
                throw new ArgumentException("Checkpoint is incompatible with this scenario.", nameof(normalized));
            return normalized.scenarioContentId == WaveCheckpointCompatibility.NativeContentId
                ? CreateSimulationCore(null, historical: false, CreateProgressionRules())
                : CreateSimulationCore(null, historical: false);
        }

        // Явный исторический test path; production New Game никогда его не вызывает.
        internal BattleSimulation CreateHistoricalAllOpenSimulation(ReactionCatalog reactions = null) =>
            CreateSimulationCore(reactions, historical: ReferenceEquals(reactions, ReactionCatalog.Empty));

        internal BattleSimulation CreateSimulation(ReactionCatalog reactionCatalog)
        {
            if (reactionCatalog == null)
            {
                throw new ArgumentNullException(nameof(reactionCatalog));
            }

            return CreateSimulationCore(reactionCatalog,
                historical: ReferenceEquals(reactionCatalog, ReactionCatalog.Empty));
        }

        internal BattleSimulation CreateSimulation(ElementUnlockRules rules)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (waves == null || waves.Length != 8 || rules.StartingOptions.Count != 2 ||
                rules.StartingOptions[0] != ElementKind.Flame || rules.StartingOptions[1] != ElementKind.Gale ||
                rules.ChoiceWaves.Count != 2 || rules.ChoiceWaves[0] != 3 || rules.ChoiceWaves[1] != 6)
                throw new ArgumentException("Native MVP requires Flame/Gale, milestones 3/6 and eight waves.", nameof(rules));
            return CreateSimulationCore(null, historical: false, rules);
        }

        private BattleSimulation CreateSimulationCore(ReactionCatalog reactionCatalog, bool historical,
            ElementUnlockRules rules = null)
        {
            var shared = sharedScenario != null ? sharedScenario : this;
            // Даже historical override сначала проверяет production data, без fallback на Empty.
            var productionReactions = CreateReactionCatalog(shared);
            reactionCatalog = reactionCatalog ?? productionReactions;
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
                enemyReward,
                shared.enemyArmor);
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

            var towerCatalog = CreateTowerCatalog(shared, towerLevels);
            if (historical)
            {
                // Только явный test path Empty выбирает прежние objects из уже validated каталога.
                var previous = new List<TowerArchetypeDefinition>(5);
                for (var index = 0; index < towerCatalog.Archetypes.Count; index++)
                {
                    var archetype = towerCatalog.Archetypes[index];
                    var id = archetype.Id.Value;
                    if (id == shared.towerId || id == "flame" || id == "tide" || id == "gale" || id == "stone")
                    {
                        previous.Add(archetype);
                    }
                }

                towerCatalog = new TowerCatalog(previous);
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
                    towerCatalog,
                    shared.baseHealth,
                    shared.startingGold,
                    reactionCatalog);
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
                towerCatalog,
                shared.baseHealth,
                shared.startingGold,
                waveCompletionBonus,
                reactionCatalog, rules);
        }

        private static TowerCatalog CreateTowerCatalog(
            BattleScenarioConfig shared,
            IReadOnlyList<TowerDefinition> legacyLevels)
        {
            var requiredIds = new[] { "flame", "tide", "gale", "stone", "steam", "storm" };
            var requirements = new[]
            {
                new[] { ElementKind.Flame }, new[] { ElementKind.Tide },
                new[] { ElementKind.Gale }, new[] { ElementKind.Stone },
                new[] { ElementKind.Flame, ElementKind.Tide }, new[] { ElementKind.Tide, ElementKind.Gale }
            };
            if (shared.towerArchetypes == null || shared.towerArchetypes.Length != requiredIds.Length)
            {
                throw new ArgumentException("Exactly six visible tower archetypes are required.", nameof(towerArchetypes));
            }

            var archetypes = new List<TowerArchetypeDefinition>(7)
            {
                new TowerArchetypeDefinition(new TowerDefinitionId(shared.towerId), false, legacyLevels)
            };
            for (var archetypeIndex = 0; archetypeIndex < requiredIds.Length; archetypeIndex++)
            {
                var settings = shared.towerArchetypes[archetypeIndex];
                if (settings == null || settings.id != requiredIds[archetypeIndex] || !settings.showInCatalog)
                {
                    throw new ArgumentException("Tower catalog must use FLAME/TIDE/GALE/STONE/STEAM/STORM order and visibility.",
                        nameof(towerArchetypes));
                }

                var hybrid = archetypeIndex >= 4;
                var levels = new TowerDefinition[settings.levels?.Length ?? 0];
                for (var index = 0; index < levels.Length; index++)
                {
                    var level = settings.levels[index] ?? throw new ArgumentException("Tower level cannot be null.", nameof(towerArchetypes));
                    StatusEffectDefinition statusDefinition = null;
                    ReactionPayload intrinsic = null;
                    if (hybrid)
                    {
                        var requiredKind = settings.id == "steam" ? ReactionKind.Steam : ReactionKind.Storm;
                        if (!level.hasIntrinsicReaction || level.intrinsicReaction == null ||
                            level.intrinsicReaction.kind != requiredKind || level.armorPenetration != 0 ||
                            !IsEmptyStatus(level.statusEffect))
                        {
                            throw new ArgumentException($"Hybrid {settings.id} requires only its matching intrinsic payload.", nameof(towerArchetypes));
                        }
                        var payload = level.intrinsicReaction;
                        intrinsic = new ReactionPayload(payload.kind, payload.damagePercent, payload.range, payload.targetLimit);
                    }
                    else
                    {
                        var status = level.statusEffect;
                        if (level.hasIntrinsicReaction || !IsEmptyPayload(level.intrinsicReaction) ||
                            status == null || (int)status.kind != archetypeIndex)
                        {
                            throw new ArgumentException($"Basic {settings.id} requires its matching status and no intrinsic payload.", nameof(towerArchetypes));
                        }
                        statusDefinition = new StatusEffectDefinition(status.kind, status.power, status.durationTicks, status.damageIntervalTicks);
                    }
                    levels[index] = new TowerDefinition(new TowerDefinitionId(settings.id), level.price,
                        level.range, level.fireRate, level.damage, statusDefinition, level.armorPenetration, intrinsic);
                }
                var id = new TowerDefinitionId(settings.id);
                archetypes.Add(new TowerArchetypeDefinition(id, true, levels,
                    requirements[archetypeIndex]));
            }
            return new TowerCatalog(archetypes);
        }

        // Unity inline serialization представляет null class как объект с нулевыми полями.
        private static bool IsEmptyStatus(StatusEffectSettings status) => status == null ||
            (status.kind == default && status.power == 0 && status.durationTicks == 0 && status.damageIntervalTicks == 0);

        private static bool IsEmptyPayload(ReactionPayloadSettings payload) => payload == null ||
            (payload.kind == default && payload.damagePercent == 0 && payload.range == 0f && payload.targetLimit == 0);

        private static ReactionCatalog CreateReactionCatalog(BattleScenarioConfig shared)
        {
            if (shared.reactions == null || shared.reactions.Length != 2)
                throw new ArgumentException("Both production reaction definitions are required.", nameof(reactions));
            var definitions = new ReactionDefinition[2];
            for (var index = 0; index < definitions.Length; index++)
            {
                var settings = shared.reactions[index];
                if (settings == null || (int)settings.kind != index || settings.priority != index)
                    throw new ArgumentException("Production reactions require Steam/Storm with priorities 0/1.", nameof(reactions));
                definitions[index] = new ReactionDefinition(new ReactionPayload(settings.kind, settings.damagePercent,
                    settings.range, settings.targetLimit), settings.cooldownTicks, settings.priority);
            }
            return new ReactionCatalog(definitions);
        }

        private static TowerArchetypeSettings[] CreateDefaultTowerArchetypes()
        {
            return new[]
            {
                CreateArchetype(
                    "flame",
                    CreateLevel(70, 12, 3f, 1f, StatusKind.Burn, 3, 90, 15),
                    CreateLevel(105, 16, 3f, 1.1f, StatusKind.Burn, 4, 105, 15),
                    CreateLevel(140, 22, 3.2f, 1.2f, StatusKind.Burn, 6, 120, 15)),
                CreateArchetype(
                    "tide",
                    CreateLevel(60, 10, 3.5f, 0.8f, StatusKind.Wet, 20, 60, 0),
                    CreateLevel(90, 14, 3.5f, 0.9f, StatusKind.Wet, 25, 75, 0),
                    CreateLevel(120, 18, 3.5f, 1f, StatusKind.Wet, 30, 90, 0)),
                CreateArchetype(
                    "gale",
                    CreateLevel(80, 6, 3f, 3f, StatusKind.Gust, 20, 45, 0),
                    CreateLevel(120, 8, 3.2f, 3.5f, StatusKind.Gust, 30, 60, 0),
                    CreateLevel(160, 10, 3.5f, 4f, StatusKind.Gust, 40, 75, 0)),
                CreateArchetype(
                    "stone",
                    CreateLevel(90, 40, 2.5f, 0.5f, StatusKind.Crack, 2, 90, 0, 4),
                    CreateLevel(135, 55, 2.75f, 0.55f, StatusKind.Crack, 3, 105, 0, 6),
                    CreateLevel(180, 75, 3f, 0.6f, StatusKind.Crack, 4, 120, 0, 8)),
                CreateArchetype("steam",
                    CreateHybridLevel(110, 18, 3f, .8f, ReactionKind.Steam, 100, 1.25f, 4),
                    CreateHybridLevel(165, 25, 3.2f, .9f, ReactionKind.Steam, 125, 1.5f, 4),
                    CreateHybridLevel(220, 34, 3.4f, 1f, ReactionKind.Steam, 150, 1.75f, 4)),
                CreateArchetype("storm",
                    CreateHybridLevel(110, 9, 3.5f, 1.5f, ReactionKind.Storm, 75, 2f, 2),
                    CreateHybridLevel(165, 13, 3.5f, 1.7f, ReactionKind.Storm, 90, 2.25f, 2),
                    CreateHybridLevel(220, 18, 3.75f, 2f, ReactionKind.Storm, 100, 2.5f, 2))
            };
        }

        private static TowerArchetypeSettings CreateArchetype(
            string id,
            params TowerLevelSettings[] levels)
        {
            return new TowerArchetypeSettings
            {
                id = id,
                showInCatalog = true,
                levels = levels
            };
        }

        private static TowerLevelSettings CreateLevel(
            int price,
            int damage,
            float range,
            float fireRate,
            StatusKind kind,
            int power,
            int durationTicks,
            int damageIntervalTicks,
            int armorPenetration = 0)
        {
            return new TowerLevelSettings
            {
                price = price,
                damage = damage,
                range = range,
                fireRate = fireRate,
                armorPenetration = armorPenetration,
                statusEffect = new StatusEffectSettings
                {
                    kind = kind,
                    power = power,
                    durationTicks = durationTicks,
                    damageIntervalTicks = damageIntervalTicks
                }
            };
        }

        private static TowerLevelSettings CreateHybridLevel(int price, int damage, float range, float fireRate,
            ReactionKind kind, int damagePercent, float reactionRange, int targetLimit)
        {
            return new TowerLevelSettings
            {
                price = price, damage = damage, range = range, fireRate = fireRate,
                hasIntrinsicReaction = true,
                intrinsicReaction = new ReactionPayloadSettings
                { kind = kind, damagePercent = damagePercent, range = reactionRange, targetLimit = targetLimit }
            };
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
