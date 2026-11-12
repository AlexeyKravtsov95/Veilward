using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Veilward.Domain;
using Veilward.Presentation;

namespace Veilward.Tests.EditMode
{
    public sealed class BattleCheckpointTests
    {
        private static readonly GridCoordinate TowerCell = new GridCoordinate(-4, 0);
        private readonly List<UnityEngine.Object> _objects = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (var index = _objects.Count - 1; index >= 0; index--)
            {
                if (_objects[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_objects[index]);
                }
            }

            _objects.Clear();
        }

        [Test]
        public void Build_RoundTrip_RestoresConsistentPreparation()
        {
            var simulation = CreateSimulation();
            Assert.That(simulation.PlaceTower(new PlaceTowerCommand(simulation.TowerDefinition.Id, TowerCell)).Success, Is.True);
            var checkpoint = simulation.ExportCheckpoint();

            var restored = CreateSimulation();
            restored.RestoreCheckpoint(checkpoint);

            AssertStatesEqual(checkpoint, restored.ExportCheckpoint());
            Assert.That(restored.Grid.GetState(TowerCell), Is.EqualTo(GridCellState.Occupied));
            Assert.That(restored.State.Towers[0].Definition.Damage,
                Is.EqualTo(simulation.TowerDefinition.Damage));
            Assert.That(restored.State.Enemies, Is.Empty);
        }

        [Test]
        public void EconomyAndRejectedCommands_SaveOnlySuccessfulMutations()
        {
            var simulation = CreateSimulation();
            simulation.PlaceTower(new PlaceTowerCommand(simulation.TowerDefinition.Id, TowerCell));
            simulation.UpgradeTower(new TowerId(1));
            var upgraded = simulation.ExportCheckpoint();
            var restoredUpgrade = CreateSimulation();
            restoredUpgrade.RestoreCheckpoint(upgraded);
            Assert.That(restoredUpgrade.State.Towers[0].Level, Is.EqualTo(2));
            Assert.That(restoredUpgrade.State.Towers[0].InvestedGold, Is.EqualTo(110));
            Assert.That(restoredUpgrade.State.Gold, Is.EqualTo(390));

            var beforeRejected = restoredUpgrade.ExportCheckpoint();
            Assert.That(restoredUpgrade.PlaceTower(new PlaceTowerCommand(
                restoredUpgrade.TowerDefinition.Id, TowerCell)).Success, Is.False);
            Assert.That(restoredUpgrade.SellTower(new TowerId(999)).Success, Is.False);
            AssertStatesEqual(beforeRejected, restoredUpgrade.ExportCheckpoint());

            Assert.That(restoredUpgrade.SellTower(new TowerId(1)).Success, Is.True);
            var sold = restoredUpgrade.ExportCheckpoint();
            var restoredSale = CreateSimulation();
            restoredSale.RestoreCheckpoint(sold);
            Assert.That(restoredSale.State.Towers, Is.Empty);
            Assert.That(restoredSale.Grid.GetState(TowerCell), Is.EqualTo(GridCellState.Available));
            Assert.That(restoredSale.State.Gold, Is.EqualTo(467));
        }

        [Test]
        public void InterruptedWave_RestoresPreparationBeforeAttempt()
        {
            var simulation = CreateSimulation(waveCount: 2, enemiesPerWave: 2, maxHealth: 20);
            simulation.PlaceTower(new PlaceTowerCommand(simulation.TowerDefinition.Id, TowerCell));
            var beforeAttempt = simulation.ExportCheckpoint();

            simulation.StartWave();
            for (var index = 0; index < 46; index++)
            {
                simulation.Step();
            }

            Assert.That(simulation.State.Phase, Is.EqualTo(BattlePhase.Combat));
            Assert.That(simulation.State.Kills, Is.EqualTo(1));
            Assert.That(simulation.State.Gold, Is.GreaterThan(beforeAttempt.gold));

            var restored = CreateSimulation(waveCount: 2, enemiesPerWave: 2);
            restored.RestoreCheckpoint(beforeAttempt);
            AssertStatesEqual(beforeAttempt, restored.ExportCheckpoint());
            Assert.That(restored.State.Phase, Is.EqualTo(BattlePhase.Preparation));
            Assert.That(restored.State.Enemies, Is.Empty);
            Assert.That(restored.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
        }

        [Test]
        public void WaveComplete_RepeatedLoads_DoNotDuplicateRewards()
        {
            var simulation = CreateSimulation(waveCount: 2);
            simulation.PlaceTower(new PlaceTowerCommand(simulation.TowerDefinition.Id, TowerCell));
            simulation.StartWave();
            simulation.Step();
            Assert.That(simulation.State.Phase, Is.EqualTo(BattlePhase.WaveComplete));
            var completed = simulation.ExportCheckpoint();

            for (var index = 0; index < 3; index++)
            {
                var restored = CreateSimulation(waveCount: 2);
                restored.RestoreCheckpoint(completed);
                restored.Step();
                AssertStatesEqual(completed, restored.ExportCheckpoint());
                Assert.That(restored.State.Gold, Is.EqualTo(465));
                Assert.That(restored.State.Kills, Is.EqualTo(1));
            }
        }

        [Test]
        public void NextWave_RoundTrip_PreservesRewardAndRequiresExplicitStart()
        {
            var simulation = CreateSimulation(waveCount: 2);
            simulation.PlaceTower(new PlaceTowerCommand(simulation.TowerDefinition.Id, TowerCell));
            simulation.StartWave();
            simulation.Step();
            simulation.ContinueToNextWave();
            var next = simulation.ExportCheckpoint();

            var restored = CreateSimulation(waveCount: 2);
            restored.RestoreCheckpoint(next);
            restored.Step();

            AssertStatesEqual(next, restored.ExportCheckpoint());
            Assert.That(restored.State.WaveIndex, Is.EqualTo(1));
            Assert.That(restored.State.Phase, Is.EqualTo(BattlePhase.Preparation));
            Assert.That(restored.State.Enemies, Is.Empty);
        }

        [TestCase(BattlePhase.Victory)]
        [TestCase(BattlePhase.Defeat)]
        public void TerminalStates_RemainTerminalAfterRestore(BattlePhase phase)
        {
            var simulation = CreateSimulation(
                waveCount: 1,
                baseHealth: phase == BattlePhase.Defeat ? 1 : 20,
                speed: phase == BattlePhase.Defeat ? 300f : 1.5f);
            if (phase == BattlePhase.Victory)
            {
                simulation.PlaceTower(new PlaceTowerCommand(simulation.TowerDefinition.Id, TowerCell));
            }

            simulation.StartWave();
            var guard = 0;
            while (simulation.State.Phase == BattlePhase.Combat && guard++ < 10)
            {
                simulation.Step();
            }

            Assert.That(simulation.State.Phase, Is.EqualTo(phase));
            var terminal = simulation.ExportCheckpoint();

            var restored = CreateSimulation(
                waveCount: 1,
                baseHealth: phase == BattlePhase.Defeat ? 1 : 20,
                speed: phase == BattlePhase.Defeat ? 300f : 1.5f);
            restored.RestoreCheckpoint(terminal);
            restored.Step();

            AssertStatesEqual(terminal, restored.ExportCheckpoint());
            Assert.That(restored.State.Enemies, Is.Empty);
            Assert.That(restored.StartWave(), Is.EqualTo(WaveTransitionResult.RejectedInvalidPhase));
        }

        [Test]
        public void RestoredRun_MatchesContinuousDeterministicTimeline()
        {
            var continuous = LoadSavedEightWave();
            continuous.PlaceTower(new PlaceTowerCommand(continuous.TowerDefinition.Id, TowerCell));
            var checkpoint = continuous.ExportCheckpoint();
            var restored = LoadSavedEightWave();
            restored.RestoreCheckpoint(checkpoint);

            var transitions = 0;
            while (continuous.State.Phase != BattlePhase.Victory &&
                   continuous.State.Phase != BattlePhase.Defeat && transitions < 16)
            {
                Assert.That(continuous.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
                Assert.That(restored.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
                var guard = 0;
                while (continuous.State.Phase == BattlePhase.Combat && guard++ < 20000)
                {
                    continuous.Step();
                    restored.Step();
                    AssertTickEqual(continuous, restored);
                    continuous.ClearAttackEvents();
                    restored.ClearAttackEvents();
                }

                Assert.That(guard, Is.LessThan(20000));
                AssertStatesEqual(continuous.ExportCheckpoint(), restored.ExportCheckpoint());
                if (continuous.State.Phase == BattlePhase.WaveComplete)
                {
                    Assert.That(continuous.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.Continued));
                    Assert.That(restored.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.Continued));
                }

                transitions++;
            }

            Assert.That(continuous.State.Phase, Is.EqualTo(restored.State.Phase));
            Assert.That(transitions, Is.LessThan(16));
        }

        [Test]
        public void SellRestoreRebuild_DoesNotReuseSoldTowerId()
        {
            var simulation = CreateSimulation();
            simulation.PlaceTower(new PlaceTowerCommand(simulation.TowerDefinition.Id, TowerCell));
            simulation.SellTower(new TowerId(1));
            var checkpoint = simulation.ExportCheckpoint();

            var restored = CreateSimulation();
            restored.RestoreCheckpoint(checkpoint);
            var result = restored.PlaceTower(new PlaceTowerCommand(restored.TowerDefinition.Id, TowerCell));

            Assert.That(result.Success, Is.True);
            Assert.That(result.Tower.Id.Value, Is.EqualTo(2));
        }

        [Test]
        public void InvalidTowers_AreRejectedWithoutPublishingPartialState()
        {
            var mutations = new Action<BattleCheckpoint>[]
            {
                checkpoint => checkpoint.towers = new[] { Clone(checkpoint.towers[0]), Clone(checkpoint.towers[0]) },
                checkpoint =>
                {
                    var second = Clone(checkpoint.towers[0]);
                    second.towerId = 2;
                    checkpoint.towers = new[] { Clone(checkpoint.towers[0]), second };
                },
                checkpoint => checkpoint.towers[0].x = 999,
                checkpoint => checkpoint.towers[0].y = -2,
                checkpoint => checkpoint.towers[0].level = 4,
                checkpoint => checkpoint.towers[0].investedGold = 1,
                checkpoint => checkpoint.nextTowerId = 1
            };

            AssertInvalidMutations(mutations);
        }

        [Test]
        public void InvalidScalarsAndPhases_AreRejectedWithoutMutation()
        {
            var mutations = new Action<BattleCheckpoint>[]
            {
                checkpoint => checkpoint.tick = -1,
                checkpoint => checkpoint.baseHealth = -1,
                checkpoint => checkpoint.baseHealth = 21,
                checkpoint => checkpoint.gold = -1,
                checkpoint => checkpoint.kills = -1,
                checkpoint => checkpoint.waveIndex = 2,
                checkpoint => checkpoint.nextEnemyId = 0,
                checkpoint => checkpoint.nextTowerId = 0,
                checkpoint => checkpoint.nextEnemyId = int.MaxValue,
                checkpoint => checkpoint.nextTowerId = int.MaxValue,
                checkpoint => checkpoint.phase = BattlePhase.Combat,
                checkpoint => checkpoint.phase = (BattlePhase)999,
                checkpoint => checkpoint.phase = BattlePhase.Defeat,
                checkpoint => checkpoint.phase = BattlePhase.Victory,
                checkpoint => checkpoint.baseHealth = 0
            };

            AssertInvalidMutations(mutations);
        }

        [Test]
        public void EnemyIdExhaustion_RejectsStartWaveWithoutMutation()
        {
            var simulation = CreateSimulation(enemiesPerWave: 2);
            var checkpoint = simulation.ExportCheckpoint();
            checkpoint.nextEnemyId = int.MaxValue - 1;
            simulation.RestoreCheckpoint(checkpoint);
            var before = simulation.ExportCheckpoint();

            Assert.That(simulation.StartWave(), Is.EqualTo(WaveTransitionResult.RejectedIdExhausted));
            AssertStatesEqual(before, simulation.ExportCheckpoint());
            Assert.That(simulation.State.Enemies, Is.Empty);
        }

        [Test]
        public void LastSafeEnemyId_CompletesRestorableWaveAndRejectsFollowingWave()
        {
            var simulation = CreateSimulation(enemiesPerWave: 1, speed: 1000f);
            var checkpoint = simulation.ExportCheckpoint();
            checkpoint.nextEnemyId = int.MaxValue - 2;
            simulation.RestoreCheckpoint(checkpoint);

            Assert.That(simulation.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
            simulation.Step();
            Assert.That(simulation.State.Phase, Is.EqualTo(BattlePhase.WaveComplete));
            var completed = simulation.ExportCheckpoint();
            Assert.That(simulation.TryValidateCheckpoint(completed, out _), Is.True);
            Assert.That(simulation.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.Continued));
            var before = simulation.ExportCheckpoint();

            Assert.That(simulation.StartWave(), Is.EqualTo(WaveTransitionResult.RejectedIdExhausted));
            AssertStatesEqual(before, simulation.ExportCheckpoint());
        }

        [Test]
        public void TowerIdExhaustion_RejectsPlacementWithoutMutation()
        {
            var simulation = CreateSimulation();
            var checkpoint = simulation.ExportCheckpoint();
            checkpoint.nextTowerId = int.MaxValue - 2;
            simulation.RestoreCheckpoint(checkpoint);
            var command = new PlaceTowerCommand(simulation.TowerDefinition.Id, TowerCell);
            Assert.That(simulation.PlaceTower(command).Success, Is.True);
            var before = simulation.ExportCheckpoint();
            Assert.That(simulation.TryValidateCheckpoint(before, out _), Is.True);

            var result = simulation.PlaceTower(new PlaceTowerCommand(
                simulation.TowerDefinition.Id,
                new GridCoordinate(-3, 0)));

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(PlaceTowerFailureReason.IdentifierExhausted));
            AssertStatesEqual(before, simulation.ExportCheckpoint());
        }

        [Test]
        public void CombatCheckpointExport_IsRejected()
        {
            var simulation = CreateSimulation(waveCount: 2, enemiesPerWave: 2);
            simulation.StartWave();

            Assert.Throws<InvalidOperationException>(() => simulation.ExportCheckpoint());
        }

        private void AssertInvalidMutations(IEnumerable<Action<BattleCheckpoint>> mutations)
        {
            foreach (var mutation in mutations)
            {
                var simulation = CreateSimulation();
                simulation.PlaceTower(new PlaceTowerCommand(simulation.TowerDefinition.Id, TowerCell));
                var before = simulation.ExportCheckpoint();
                var invalid = Clone(before);
                mutation(invalid);

                Assert.That(simulation.TryValidateCheckpoint(invalid, out _), Is.False);
                Assert.Throws<ArgumentException>(() => simulation.RestoreCheckpoint(invalid));
                AssertStatesEqual(before, simulation.ExportCheckpoint());
            }
        }

        private BattleSimulation CreateSimulation(
            int waveCount = 2,
            int enemiesPerWave = 1,
            int baseHealth = 20,
            float speed = 1.5f,
            int maxHealth = 10)
        {
            var config = BattleScenarioConfig.CreateDefault();
            _objects.Add(config);
            var serialized = new SerializedObject(config);
            serialized.FindProperty("startingGold").intValue = 500;
            serialized.FindProperty("enemyReward").intValue = 5;
            serialized.FindProperty("waveCompletionBonus").intValue = 20;
            serialized.FindProperty("baseHealth").intValue = baseHealth;
            serialized.FindProperty("speed").floatValue = speed;
            serialized.FindProperty("maxHealth").intValue = maxHealth;
            var waves = serialized.FindProperty("waves");
            waves.arraySize = waveCount;
            for (var index = 0; index < waveCount; index++)
            {
                var wave = waves.GetArrayElementAtIndex(index);
                wave.FindPropertyRelative("enemyCount").intValue = enemiesPerWave;
                wave.FindPropertyRelative("firstSpawnTick").intValue = 0;
                wave.FindPropertyRelative("spawnIntervalTicks").intValue = 30;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return config.CreateSimulation();
        }

        private static BattleSimulation LoadSavedEightWave()
        {
            var config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>(
                "Assets/_Game/Configs/EightWave.asset");
            Assert.That(config, Is.Not.Null);
            return config.CreateSimulation();
        }

        private static void AssertTickEqual(BattleSimulation expected, BattleSimulation actual)
        {
            var expectedState = expected.State;
            var actualState = actual.State;
            Assert.That(actualState.Phase, Is.EqualTo(expectedState.Phase));
            Assert.That(actualState.Tick, Is.EqualTo(expectedState.Tick));
            Assert.That(actualState.WaveIndex, Is.EqualTo(expectedState.WaveIndex));
            Assert.That(actualState.BaseHealth, Is.EqualTo(expectedState.BaseHealth));
            Assert.That(actualState.Gold, Is.EqualTo(expectedState.Gold));
            Assert.That(actualState.Kills, Is.EqualTo(expectedState.Kills));
            Assert.That(actualState.Enemies.Count, Is.EqualTo(expectedState.Enemies.Count));
            for (var index = 0; index < expectedState.Enemies.Count; index++)
            {
                Assert.That(actualState.Enemies[index].Id, Is.EqualTo(expectedState.Enemies[index].Id));
                Assert.That(actualState.Enemies[index].Health, Is.EqualTo(expectedState.Enemies[index].Health));
                Assert.That(actualState.Enemies[index].DistanceAlongPath,
                    Is.EqualTo(expectedState.Enemies[index].DistanceAlongPath));
            }

            Assert.That(actual.AttackEvents.Count, Is.EqualTo(expected.AttackEvents.Count));
            for (var index = 0; index < expected.AttackEvents.Count; index++)
            {
                Assert.That(actual.AttackEvents[index], Is.EqualTo(expected.AttackEvents[index]));
            }
        }

        private static void AssertStatesEqual(BattleCheckpoint expected, BattleCheckpoint actual)
        {
            Assert.That(actual.phase, Is.EqualTo(expected.phase));
            Assert.That(actual.waveIndex, Is.EqualTo(expected.waveIndex));
            Assert.That(actual.tick, Is.EqualTo(expected.tick));
            Assert.That(actual.baseHealth, Is.EqualTo(expected.baseHealth));
            Assert.That(actual.gold, Is.EqualTo(expected.gold));
            Assert.That(actual.kills, Is.EqualTo(expected.kills));
            Assert.That(actual.nextEnemyId, Is.EqualTo(expected.nextEnemyId));
            Assert.That(actual.nextTowerId, Is.EqualTo(expected.nextTowerId));
            Assert.That(actual.towers.Length, Is.EqualTo(expected.towers.Length));
            for (var index = 0; index < expected.towers.Length; index++)
            {
                Assert.That(actual.towers[index].towerId, Is.EqualTo(expected.towers[index].towerId));
                Assert.That(actual.towers[index].x, Is.EqualTo(expected.towers[index].x));
                Assert.That(actual.towers[index].y, Is.EqualTo(expected.towers[index].y));
                Assert.That(actual.towers[index].level, Is.EqualTo(expected.towers[index].level));
                Assert.That(actual.towers[index].investedGold, Is.EqualTo(expected.towers[index].investedGold));
                Assert.That(actual.towers[index].nextAttackTick, Is.EqualTo(expected.towers[index].nextAttackTick));
            }
        }

        private static BattleCheckpoint Clone(BattleCheckpoint source)
        {
            var clone = new BattleCheckpoint
            {
                phase = source.phase,
                waveIndex = source.waveIndex,
                tick = source.tick,
                baseHealth = source.baseHealth,
                gold = source.gold,
                kills = source.kills,
                nextEnemyId = source.nextEnemyId,
                nextTowerId = source.nextTowerId,
                towers = new TowerCheckpoint[source.towers.Length]
            };
            for (var index = 0; index < source.towers.Length; index++)
            {
                clone.towers[index] = Clone(source.towers[index]);
            }

            return clone;
        }

        private static TowerCheckpoint Clone(TowerCheckpoint source)
        {
            return new TowerCheckpoint
            {
                towerId = source.towerId,
                x = source.x,
                y = source.y,
                level = source.level,
                investedGold = source.investedGold,
                nextAttackTick = source.nextAttackTick
            };
        }
    }
}
