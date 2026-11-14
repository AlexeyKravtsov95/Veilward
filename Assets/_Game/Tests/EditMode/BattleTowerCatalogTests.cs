using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Veilward.Domain;
using Veilward.Infrastructure;
using Veilward.Presentation;
using Object = UnityEngine.Object;

namespace Veilward.Tests.EditMode
{
    public sealed class BattleTowerCatalogTests
    {
        private static readonly GridCoordinate[] TowerCells =
        {
            new GridCoordinate(-4, 0),
            new GridCoordinate(-3, 0),
            new GridCoordinate(-2, 0),
            new GridCoordinate(-1, 0)
        };

        private readonly List<Object> _objects = new List<Object>();
        private string _checkpointRoot;

        [TearDown]
        public void TearDown()
        {
            for (var index = _objects.Count - 1; index >= 0; index--)
            {
                if (_objects[index] != null)
                {
                    Object.DestroyImmediate(_objects[index]);
                }
            }

            _objects.Clear();
            if (!string.IsNullOrEmpty(_checkpointRoot) && Directory.Exists(_checkpointRoot))
            {
                Directory.Delete(_checkpointRoot, true);
            }
        }

        [Test]
        public void RegBattle090_SavedCatalog_ContainsExactDefinitionsAndLegacyCompatibility()
        {
            var config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>(
                "Assets/_Game/Configs/TestWave.asset");
            Assert.That(config, Is.Not.Null);
            var simulation = config.CreateSimulation();

            Assert.That(simulation.TowerCatalog.Archetypes.Count, Is.EqualTo(5));
            foreach (var id in new[] { "flame", "tide", "gale", "stone" })
            {
                var archetype = simulation.TowerCatalog.Get(new TowerDefinitionId(id));
                Assert.That(archetype.Id.Value, Is.EqualTo(id));
                Assert.That(archetype.ShowInCatalog, Is.True, id);
                Assert.That(archetype.Levels.Count, Is.EqualTo(3), id);
                var expected = ExpectedLevels(id);
                for (var level = 0; level < expected.Length; level++)
                {
                    AssertLevel(archetype.Levels[level], expected[level], $"{id} L{level + 1}");
                }
            }

            var legacy = simulation.TowerCatalog.Get(new TowerDefinitionId("test-tower"));
            Assert.That(legacy.ShowInCatalog, Is.False);
            Assert.That(legacy.Levels.Count, Is.EqualTo(3));
            Assert.That(legacy.Levels[0].StatusEffect, Is.Null);
            Assert.That(legacy.Levels[0].Price, Is.EqualTo(60));
            Assert.That(legacy.Levels[1].Price, Is.EqualTo(50));
            Assert.That(legacy.Levels[2].Price, Is.EqualTo(80));
            Assert.That(legacy.Levels[0].Damage, Is.EqualTo(25));
            Assert.That(legacy.Levels[1].Damage, Is.EqualTo(35));
            Assert.That(legacy.Levels[2].Damage, Is.EqualTo(50));

            simulation.StartWave();
            Assert.That(simulation.State.Enemies[0].Definition.Armor, Is.Zero);
        }

        [Test]
        public void RegBattle090_InvalidDefinitions_AreRejectedByDomainAndSavedConfigContract()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new StatusEffectDefinition(
                (StatusKind)999, 1, 60, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StatusEffectDefinition(
                StatusKind.Wet, 0, 60, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StatusEffectDefinition(
                StatusKind.Wet, 101, 60, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StatusEffectDefinition(
                StatusKind.Wet, 20, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StatusEffectDefinition(
                StatusKind.Burn, 3, 90, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StatusEffectDefinition(
                StatusKind.Wet, 20, 60, 15));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TowerDefinition(
                new TowerDefinitionId("invalid"), 1, 1f, 1f, 1, armorPenetration: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnemyDefinition(
                new EnemyDefinitionId("invalid"), 1, 1f, 1, 0, armor: -1));

            Assert.DoesNotThrow(() => new StatusEffectDefinition(StatusKind.Wet, 1, 60, 0));
            Assert.DoesNotThrow(() => new StatusEffectDefinition(StatusKind.Wet, 100, 60, 0));

            var config = BattleScenarioConfig.CreateDefault();
            _objects.Add(config);
            var serialized = new SerializedObject(config);
            var tide = serialized.FindProperty("towerArchetypes").GetArrayElementAtIndex(1);
            tide.FindPropertyRelative("levels").GetArrayElementAtIndex(0)
                .FindPropertyRelative("statusEffect.power").intValue = 101;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.Throws<ArgumentOutOfRangeException>(() => config.CreateSimulation());
        }

        [Test]
        public void RegBattle090_Catalog_RejectsDuplicateNullAndIncompleteArchetypes()
        {
            var valid = CreateArchetype("valid");
            Assert.Throws<ArgumentNullException>(() => new TowerCatalog(null));
            Assert.Throws<ArgumentException>(() => new TowerCatalog(
                Array.Empty<TowerArchetypeDefinition>()));
            Assert.Throws<ArgumentException>(() => new TowerCatalog(
                new TowerArchetypeDefinition[] { valid, null }));
            Assert.Throws<ArgumentException>(() => new TowerCatalog(new[]
            {
                valid, CreateArchetype("valid")
            }));
            Assert.Throws<ArgumentNullException>(() => new TowerArchetypeDefinition(
                new TowerDefinitionId("invalid"), true, null));
            Assert.Throws<ArgumentException>(() => new TowerArchetypeDefinition(
                new TowerDefinitionId("invalid"), true, CreateLevels("invalid", 2)));
            Assert.Throws<ArgumentException>(() => new TowerArchetypeDefinition(
                new TowerDefinitionId("invalid"), true, new[]
                {
                    CreateLevel("invalid", StatusKind.Burn), null, CreateLevel("invalid", StatusKind.Burn)
                }));
            Assert.Throws<ArgumentException>(() => new TowerArchetypeDefinition(
                new TowerDefinitionId("invalid"), true, new[]
                {
                    CreateLevel("invalid", StatusKind.Burn),
                    CreateLevel("other", StatusKind.Burn),
                    CreateLevel("invalid", StatusKind.Burn)
                }));
            var elementalWithoutStatus = new TowerDefinition(
                new TowerDefinitionId("flame"), 10, 3f, 1f, 10);
            Assert.Throws<ArgumentException>(() => new TowerArchetypeDefinition(
                new TowerDefinitionId("flame"), true, new[]
                {
                    elementalWithoutStatus, elementalWithoutStatus, elementalWithoutStatus
                }));
        }

        [Test]
        public void RegBattle091_PlaceTower_EachElementalType_CreatesCorrectLevelAndDebitsPrice()
        {
            var simulation = CreateSimulation(1000);
            var ids = new[] { "flame", "tide", "gale", "stone" };
            for (var index = 0; index < ids.Length; index++)
            {
                var expectedGold = simulation.State.Gold - ExpectedLevels(ids[index])[0].Price;
                var result = simulation.PlaceTower(new PlaceTowerCommand(
                    new TowerDefinitionId(ids[index]), TowerCells[index]));

                Assert.That(result.Success, Is.True, ids[index]);
                Assert.That(result.Tower.Definition.Id.Value, Is.EqualTo(ids[index]));
                Assert.That(result.Tower.Level, Is.EqualTo(1));
                Assert.That(result.Tower.Definition.StatusEffect, Is.Not.Null);
                Assert.That(result.Tower.InvestedGold, Is.EqualTo(ExpectedLevels(ids[index])[0].Price));
                Assert.That(simulation.State.Gold, Is.EqualTo(expectedGold));
                Assert.That(simulation.Grid.GetState(TowerCells[index]), Is.EqualTo(GridCellState.Occupied));
            }
        }

        [Test]
        public void RegBattle092_UpgradeTower_PreservesTypeIdentityCellAndCooldown()
        {
            foreach (var id in new[] { "flame", "tide", "gale", "stone" })
            {
                var simulation = CreateSimulation(1000);
                var placed = simulation.PlaceTower(new PlaceTowerCommand(
                    new TowerDefinitionId(id), TowerCells[0]));
                var checkpoint = simulation.ExportCheckpoint();
                checkpoint.towers[0].nextAttackTick = 123;
                simulation.RestoreCheckpoint(checkpoint);
                var beforeGold = simulation.State.Gold;

                var result = simulation.UpgradeTower(placed.Tower.Id);

                Assert.That(result.Success, Is.True, id);
                Assert.That(result.Tower.Id, Is.EqualTo(placed.Tower.Id), id);
                Assert.That(result.Tower.Coordinate, Is.EqualTo(TowerCells[0]), id);
                Assert.That(result.Tower.NextAttackTick, Is.EqualTo(123), id);
                Assert.That(result.Tower.Definition.Id.Value, Is.EqualTo(id), id);
                Assert.That(result.Tower.Level, Is.EqualTo(2), id);
                AssertLevel(result.Tower.Definition, ExpectedLevels(id)[1], $"{id} upgraded");
                Assert.That(result.Tower.InvestedGold,
                    Is.EqualTo(ExpectedLevels(id)[0].Price + ExpectedLevels(id)[1].Price));
                Assert.That(simulation.State.Gold,
                    Is.EqualTo(beforeGold - ExpectedLevels(id)[1].Price));
            }
        }

        [Test]
        public void RegBattle093_SellTower_UsesSelectedTypeInvestmentAndPaysOnlyOnce()
        {
            foreach (var id in new[] { "flame", "tide", "gale", "stone" })
            {
                var simulation = CreateSimulation(1000);
                var placed = simulation.PlaceTower(new PlaceTowerCommand(
                    new TowerDefinitionId(id), TowerCells[0]));
                simulation.UpgradeTower(placed.Tower.Id);
                var expectedInvestment = ExpectedLevels(id)[0].Price + ExpectedLevels(id)[1].Price;
                var expectedRefund = expectedInvestment * 70 / 100;
                var beforeGold = simulation.State.Gold;

                var sale = simulation.SellTower(placed.Tower.Id);

                Assert.That(sale.Success, Is.True, id);
                Assert.That(sale.Refund, Is.EqualTo(expectedRefund), id);
                Assert.That(simulation.State.Gold, Is.EqualTo(beforeGold + expectedRefund), id);
                Assert.That(simulation.Grid.GetState(TowerCells[0]), Is.EqualTo(GridCellState.Available));
                var afterSale = JsonUtility.ToJson(simulation.ExportCheckpoint());
                Assert.That(simulation.SellTower(placed.Tower.Id).Success, Is.False);
                Assert.That(JsonUtility.ToJson(simulation.ExportCheckpoint()), Is.EqualTo(afterSale));
            }
        }

        [Test]
        public void RegBattle094_RejectedCommands_DoNotPartiallyMutateState()
        {
            var simulation = CreateSimulation(70);
            AssertRejectedPlacement(simulation, new PlaceTowerCommand(
                new TowerDefinitionId("unknown"), TowerCells[0]),
                PlaceTowerFailureReason.UnknownTowerDefinition);
            AssertRejectedPlacement(simulation, new PlaceTowerCommand(
                new TowerDefinitionId("stone"), TowerCells[0]),
                PlaceTowerFailureReason.InsufficientGold);
            AssertRejectedPlacement(simulation, new PlaceTowerCommand(
                new TowerDefinitionId("flame"), new GridCoordinate(-4, -2)),
                PlaceTowerFailureReason.Blocked);

            var placed = simulation.PlaceTower(new PlaceTowerCommand(
                new TowerDefinitionId("tide"), TowerCells[0]));
            Assert.That(placed.Success, Is.True);
            AssertRejectedPlacement(simulation, new PlaceTowerCommand(
                new TowerDefinitionId("flame"), TowerCells[0]),
                PlaceTowerFailureReason.Occupied);

            var before = JsonUtility.ToJson(simulation.ExportCheckpoint());
            Assert.That(simulation.UpgradeTower(new TowerId(999)).Success, Is.False);
            Assert.That(JsonUtility.ToJson(simulation.ExportCheckpoint()), Is.EqualTo(before));

            Assert.That(simulation.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
            var gold = simulation.State.Gold;
            var towers = simulation.State.Towers.Count;
            var cell = simulation.Grid.GetState(TowerCells[0]);
            Assert.That(simulation.PlaceTower(new PlaceTowerCommand(
                new TowerDefinitionId("flame"), TowerCells[1])).FailureReason,
                Is.EqualTo(PlaceTowerFailureReason.InvalidPhase));
            Assert.That(simulation.UpgradeTower(placed.Tower.Id).FailureReason,
                Is.EqualTo(TowerOperationFailureReason.InvalidPhase));
            Assert.That(simulation.SellTower(placed.Tower.Id).FailureReason,
                Is.EqualTo(TowerOperationFailureReason.InvalidPhase));
            Assert.That(simulation.State.Gold, Is.EqualTo(gold));
            Assert.That(simulation.State.Towers.Count, Is.EqualTo(towers));
            Assert.That(simulation.Grid.GetState(TowerCells[0]), Is.EqualTo(cell));
            Assert.That(simulation.AttackEvents, Is.Empty);
        }

        [Test]
        public void RegBattle095_Schema2RoundTrip_RestoresEveryTypeWithoutChangingIdentity()
        {
            foreach (var id in new[] { "flame", "tide", "gale", "stone", "test-tower" })
            {
                var simulation = CreateSimulation(1000);
                var placed = simulation.PlaceTower(new PlaceTowerCommand(
                    new TowerDefinitionId(id), TowerCells[0]));
                simulation.UpgradeTower(placed.Tower.Id);
                var checkpoint = simulation.ExportCheckpoint();
                checkpoint.towers[0].nextAttackTick = 77;
                var json = JsonUtility.ToJson(new WaveCheckpointEnvelope
                {
                    schemaVersion = 2,
                    scenarioContentId = "test-scenario-v1",
                    state = checkpoint
                });
                var deserialized = JsonUtility.FromJson<WaveCheckpointEnvelope>(json);

                var restored = CreateSimulation(1000);
                restored.RestoreCheckpoint(deserialized.state);

                var tower = restored.State.Towers[0];
                Assert.That(tower.Definition.Id.Value, Is.EqualTo(id), id);
                Assert.That(tower.Level, Is.EqualTo(2), id);
                Assert.That(tower.InvestedGold, Is.EqualTo(checkpoint.towers[0].investedGold), id);
                Assert.That(tower.NextAttackTick, Is.EqualTo(77), id);
                Assert.That(tower.Id, Is.EqualTo(placed.Tower.Id), id);
                Assert.That(tower.Coordinate, Is.EqualTo(TowerCells[0]), id);
                Assert.That(restored.Grid.GetState(TowerCells[0]), Is.EqualTo(GridCellState.Occupied));
                Assert.That(restored.ExportCheckpoint().nextTowerId, Is.EqualTo(checkpoint.nextTowerId));
                Assert.That(restored.ExportCheckpoint().nextEnemyId, Is.EqualTo(checkpoint.nextEnemyId));
                Assert.That(deserialized.state.towers[0].towerTypeId, Is.EqualTo(id));
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RegBattle096_Schema1_MigratesExplicitlyToLegacyWithoutRewritingSource(bool withTowers)
        {
            var simulation = CreateSimulation(500);
            if (withTowers)
            {
                simulation.PlaceTower(new PlaceTowerCommand(
                    new TowerDefinitionId("test-tower"), TowerCells[0]));
                simulation.UpgradeTower(new TowerId(1));
                simulation.PlaceTower(new PlaceTowerCommand(
                    new TowerDefinitionId("test-tower"), TowerCells[1]));
            }

            var legacyState = simulation.ExportCheckpoint();
            var store = CreateCheckpointStore();
            var sourceJson = WriteLegacyEnvelope(store.PrimaryPath, legacyState, 1);

            Assert.That(store.TryLoad("test-scenario-v1", simulation.TryValidateCheckpoint, out var load),
                Is.True, load.Error);
            Assert.That(load.Envelope.schemaVersion, Is.EqualTo(2));
            Assert.That(load.Envelope.state.towers.Length, Is.EqualTo(legacyState.towers.Length));
            for (var index = 0; index < load.Envelope.state.towers.Length; index++)
            {
                Assert.That(load.Envelope.state.towers[index].towerTypeId, Is.EqualTo("test-tower"));
                Assert.That(load.Envelope.state.towers[index].level, Is.EqualTo(legacyState.towers[index].level));
                Assert.That(load.Envelope.state.towers[index].investedGold,
                    Is.EqualTo(legacyState.towers[index].investedGold));
            }

            Assert.That(File.ReadAllText(store.PrimaryPath), Is.EqualTo(sourceJson));
            var restored = CreateSimulation(500);
            restored.RestoreCheckpoint(load.Envelope.state);
            Assert.That(JsonUtility.ToJson(restored.ExportCheckpoint()),
                Is.EqualTo(JsonUtility.ToJson(load.Envelope.state)));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("unknown")]
        public void RegBattle097_InvalidPrimaryType_FallsBackToValidatedBackup(string towerTypeId)
        {
            var simulation = CreateSimulation(500);
            var store = CreateCheckpointStore();
            var valid = CreateEnvelope(simulation.ExportCheckpoint(), 2);
            Assert.That(store.TrySave(valid, simulation.TryValidateCheckpoint, out var saveError), Is.True, saveError);
            simulation.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId("test-tower"), TowerCells[0]));
            var second = CreateEnvelope(simulation.ExportCheckpoint(), 2);
            Assert.That(store.TrySave(second, simulation.TryValidateCheckpoint, out saveError), Is.True, saveError);

            second.state.towers[0].towerTypeId = towerTypeId;
            File.WriteAllText(store.PrimaryPath, JsonUtility.ToJson(second, true));

            Assert.That(store.TryLoad("test-scenario-v1", simulation.TryValidateCheckpoint, out var load),
                Is.True, load.Error);
            Assert.That(load.Status, Is.EqualTo(WaveCheckpointLoadStatus.LoadedBackup));
            Assert.That(load.Envelope.state.towers, Is.Empty);
            var before = JsonUtility.ToJson(simulation.ExportCheckpoint());
            Assert.Throws<ArgumentException>(() => simulation.RestoreCheckpoint(second.state));
            Assert.That(JsonUtility.ToJson(simulation.ExportCheckpoint()), Is.EqualTo(before));
        }

        [TestCase(3, "test-scenario-v1")]
        [TestCase(2, "other-scenario-v1")]
        public void RegBattle097_UnknownSchemaOrContent_IsRejected(int schemaVersion, string contentId)
        {
            var simulation = CreateSimulation(500);
            var store = CreateCheckpointStore();
            var envelope = CreateEnvelope(simulation.ExportCheckpoint(), schemaVersion);
            envelope.scenarioContentId = contentId;
            File.WriteAllText(store.PrimaryPath, JsonUtility.ToJson(envelope, true));

            Assert.That(store.TryLoad("test-scenario-v1", simulation.TryValidateCheckpoint, out var load), Is.False);
            Assert.That(load.Status, Is.EqualTo(WaveCheckpointLoadStatus.Incompatible));
            Assert.That(load.Envelope, Is.Null);
        }

        [Test]
        public void RegBattle098_InterruptedCombatAfterMigration_RestoresPreparationWithoutRewardsOrIdReuse()
        {
            var simulation = CreateSimulation(500);
            simulation.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId("test-tower"), TowerCells[0]));
            var beforeAttempt = simulation.ExportCheckpoint();
            var store = CreateCheckpointStore();
            WriteLegacyEnvelope(store.PrimaryPath, beforeAttempt, 1);
            Assert.That(store.TryLoad("test-scenario-v1", simulation.TryValidateCheckpoint, out var load),
                Is.True, load.Error);

            var restored = CreateSimulation(500);
            restored.RestoreCheckpoint(load.Envelope.state);
            restored.StartWave();
            for (var index = 0; index < 10; index++)
            {
                restored.Step();
            }

            var combatGold = restored.State.Gold;
            var combatKills = restored.State.Kills;
            restored.RestoreCheckpoint(load.Envelope.state);

            Assert.That(restored.State.Phase, Is.EqualTo(BattlePhase.Preparation));
            Assert.That(restored.State.Gold, Is.EqualTo(beforeAttempt.gold));
            Assert.That(restored.State.Kills, Is.EqualTo(beforeAttempt.kills));
            Assert.That(restored.State.Gold, Is.LessThanOrEqualTo(combatGold));
            Assert.That(restored.State.Kills, Is.LessThanOrEqualTo(combatKills));
            var rebuilt = restored.PlaceTower(new PlaceTowerCommand(
                new TowerDefinitionId("flame"), TowerCells[1]));
            Assert.That(rebuilt.Success, Is.True);
            Assert.That(rebuilt.Tower.Id.Value, Is.EqualTo(2));
        }

        private BattleSimulation CreateSimulation(int startingGold)
        {
            var config = BattleScenarioConfig.CreateDefault();
            _objects.Add(config);
            var serialized = new SerializedObject(config);
            serialized.FindProperty("startingGold").intValue = startingGold;
            serialized.FindProperty("enemyReward").intValue = 5;
            serialized.FindProperty("waveCompletionBonus").intValue = 20;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return config.CreateSimulation();
        }

        private WaveCheckpointStore CreateCheckpointStore()
        {
            _checkpointRoot = Path.GetFullPath(Path.Combine(
                "TestResults", "TowerCatalogTests", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(_checkpointRoot);
            return new WaveCheckpointStore(_checkpointRoot);
        }

        private static WaveCheckpointEnvelope CreateEnvelope(BattleCheckpoint state, int schemaVersion)
        {
            return new WaveCheckpointEnvelope
            {
                schemaVersion = schemaVersion,
                scenarioContentId = "test-scenario-v1",
                state = state
            };
        }

        private static string WriteLegacyEnvelope(string path, BattleCheckpoint state, int schemaVersion)
        {
            var legacy = new LegacyEnvelope
            {
                schemaVersion = schemaVersion,
                scenarioContentId = "test-scenario-v1",
                state = new LegacyState
                {
                    phase = state.phase,
                    waveIndex = state.waveIndex,
                    tick = state.tick,
                    baseHealth = state.baseHealth,
                    gold = state.gold,
                    kills = state.kills,
                    nextEnemyId = state.nextEnemyId,
                    nextTowerId = state.nextTowerId,
                    towers = new LegacyTower[state.towers.Length]
                }
            };
            for (var index = 0; index < state.towers.Length; index++)
            {
                var tower = state.towers[index];
                legacy.state.towers[index] = new LegacyTower
                {
                    towerId = tower.towerId,
                    x = tower.x,
                    y = tower.y,
                    level = tower.level,
                    investedGold = tower.investedGold,
                    nextAttackTick = tower.nextAttackTick
                };
            }

            var json = JsonUtility.ToJson(legacy, true);
            File.WriteAllText(path, json);
            return json;
        }

        private static void AssertRejectedPlacement(
            BattleSimulation simulation,
            PlaceTowerCommand command,
            PlaceTowerFailureReason expectedReason)
        {
            var before = JsonUtility.ToJson(simulation.ExportCheckpoint());
            var result = simulation.PlaceTower(command);
            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(expectedReason));
            Assert.That(JsonUtility.ToJson(simulation.ExportCheckpoint()), Is.EqualTo(before));
        }

        private static TowerArchetypeDefinition CreateArchetype(string id)
        {
            return new TowerArchetypeDefinition(
                new TowerDefinitionId(id), true, CreateLevels(id, 3));
        }

        private static TowerDefinition[] CreateLevels(string id, int count)
        {
            var levels = new TowerDefinition[count];
            for (var index = 0; index < count; index++)
            {
                levels[index] = CreateLevel(id, StatusKind.Burn);
            }

            return levels;
        }

        private static TowerDefinition CreateLevel(string id, StatusKind kind)
        {
            return new TowerDefinition(
                new TowerDefinitionId(id), 10, 3f, 1f, 10,
                new StatusEffectDefinition(kind, 3, 90, kind == StatusKind.Burn ? 15 : 0));
        }

        private static ExpectedLevel[] ExpectedLevels(string id)
        {
            switch (id)
            {
                case "flame":
                    return new[]
                    {
                        new ExpectedLevel(70, 12, 3f, 1f, 30, StatusKind.Burn, 3, 90, 15, 0),
                        new ExpectedLevel(105, 16, 3f, 1.1f, 27, StatusKind.Burn, 4, 105, 15, 0),
                        new ExpectedLevel(140, 22, 3.2f, 1.2f, 25, StatusKind.Burn, 6, 120, 15, 0)
                    };
                case "tide":
                    return new[]
                    {
                        new ExpectedLevel(60, 10, 3.5f, 0.8f, 38, StatusKind.Wet, 20, 60, 0, 0),
                        new ExpectedLevel(90, 14, 3.5f, 0.9f, 33, StatusKind.Wet, 25, 75, 0, 0),
                        new ExpectedLevel(120, 18, 3.5f, 1f, 30, StatusKind.Wet, 30, 90, 0, 0)
                    };
                case "gale":
                    return new[]
                    {
                        new ExpectedLevel(80, 6, 3f, 3f, 10, StatusKind.Gust, 20, 45, 0, 0),
                        new ExpectedLevel(120, 8, 3.2f, 3.5f, 9, StatusKind.Gust, 30, 60, 0, 0),
                        new ExpectedLevel(160, 10, 3.5f, 4f, 8, StatusKind.Gust, 40, 75, 0, 0)
                    };
                case "stone":
                    return new[]
                    {
                        new ExpectedLevel(90, 40, 2.5f, 0.5f, 60, StatusKind.Crack, 2, 90, 0, 4),
                        new ExpectedLevel(135, 55, 2.75f, 0.55f, 55, StatusKind.Crack, 3, 105, 0, 6),
                        new ExpectedLevel(180, 75, 3f, 0.6f, 50, StatusKind.Crack, 4, 120, 0, 8)
                    };
                default:
                    throw new ArgumentOutOfRangeException(nameof(id));
            }
        }

        private static void AssertLevel(TowerDefinition actual, ExpectedLevel expected, string label)
        {
            Assert.That(actual.Price, Is.EqualTo(expected.Price), label);
            Assert.That(actual.Damage, Is.EqualTo(expected.Damage), label);
            Assert.That(actual.Range, Is.EqualTo(expected.Range).Within(0.0001f), label);
            Assert.That(actual.FireRate, Is.EqualTo(expected.FireRate).Within(0.0001f), label);
            Assert.That(actual.AttackIntervalTicks, Is.EqualTo(expected.IntervalTicks), label);
            Assert.That(actual.ArmorPenetration, Is.EqualTo(expected.ArmorPenetration), label);
            Assert.That(actual.StatusEffect, Is.Not.Null, label);
            Assert.That(actual.StatusEffect.Kind, Is.EqualTo(expected.Kind), label);
            Assert.That(actual.StatusEffect.Power, Is.EqualTo(expected.Power), label);
            Assert.That(actual.StatusEffect.DurationTicks, Is.EqualTo(expected.DurationTicks), label);
            Assert.That(actual.StatusEffect.DamageIntervalTicks, Is.EqualTo(expected.DamageIntervalTicks), label);
        }

        private sealed class ExpectedLevel
        {
            public ExpectedLevel(
                int price,
                int damage,
                float range,
                float fireRate,
                int intervalTicks,
                StatusKind kind,
                int power,
                int durationTicks,
                int damageIntervalTicks,
                int armorPenetration)
            {
                Price = price;
                Damage = damage;
                Range = range;
                FireRate = fireRate;
                IntervalTicks = intervalTicks;
                Kind = kind;
                Power = power;
                DurationTicks = durationTicks;
                DamageIntervalTicks = damageIntervalTicks;
                ArmorPenetration = armorPenetration;
            }

            public int Price { get; }
            public int Damage { get; }
            public float Range { get; }
            public float FireRate { get; }
            public int IntervalTicks { get; }
            public StatusKind Kind { get; }
            public int Power { get; }
            public int DurationTicks { get; }
            public int DamageIntervalTicks { get; }
            public int ArmorPenetration { get; }
        }

        [Serializable]
        private sealed class LegacyEnvelope
        {
            public int schemaVersion;
            public string scenarioContentId;
            public LegacyState state;
        }

        [Serializable]
        private sealed class LegacyState
        {
            public BattlePhase phase;
            public int waveIndex;
            public long tick;
            public int baseHealth;
            public int gold;
            public int kills;
            public int nextEnemyId;
            public int nextTowerId;
            public LegacyTower[] towers;
        }

        [Serializable]
        private sealed class LegacyTower
        {
            public int towerId;
            public int x;
            public int y;
            public int level;
            public int investedGold;
            public long nextAttackTick;
        }
    }
}
