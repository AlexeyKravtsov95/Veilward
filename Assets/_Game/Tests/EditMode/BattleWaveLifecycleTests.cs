using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Veilward.Domain;

namespace Veilward.Tests.EditMode
{
    public sealed class BattleWaveLifecycleTests
    {
        private static readonly int[] EightWaveCounts = { 5, 6, 7, 8, 9, 10, 11, 12 };

        [Test]
        public void EightWaves_CompleteInOrderAndFinishWithVictory()
        {
            var simulation = CreateSimulation(EightWaveCounts, baseHealth: 100, startingGold: 0);
            var expectedHealth = 100;

            Assert.That(simulation.State.WaveCount, Is.EqualTo(8));
            for (var waveIndex = 0; waveIndex < EightWaveCounts.Length; waveIndex++)
            {
                Assert.That(simulation.State.WaveIndex, Is.EqualTo(waveIndex));
                Assert.That(simulation.State.Phase, Is.EqualTo(BattlePhase.Preparation));
                Assert.That(simulation.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
                CompleteCurrentWave(simulation);
                expectedHealth -= EightWaveCounts[waveIndex];

                Assert.That(simulation.State.BaseHealth, Is.EqualTo(expectedHealth));
                Assert.That(simulation.State.Gold, Is.EqualTo((waveIndex + 1) * 20));
                Assert.That(simulation.State.Phase, Is.EqualTo(
                    waveIndex == 7 ? BattlePhase.Victory : BattlePhase.WaveComplete));
                if (waveIndex < 7)
                {
                    Assert.That(
                        simulation.ContinueToNextWave(),
                        Is.EqualTo(WaveTransitionResult.Continued));
                }
            }

            Assert.That(simulation.State.Kills, Is.Zero);
            Assert.That(simulation.State.WaveIndex, Is.EqualTo(7));
        }

        [Test]
        public void SecondWave_UsesSpawnTicksRelativeToWaveStart()
        {
            var simulation = CreateSimulation(new[] { 1, 2 }, speed: 15f, spawnInterval: 2);
            simulation.StartWave();
            Step(simulation, 2);
            Assert.That(simulation.State.Phase, Is.EqualTo(BattlePhase.WaveComplete));
            Assert.That(simulation.State.Tick, Is.EqualTo(2));
            simulation.ContinueToNextWave();

            simulation.StartWave();
            Assert.That(simulation.State.Tick, Is.EqualTo(2));
            Assert.That(simulation.State.Enemies.Select(enemy => enemy.Id.Value), Is.EqualTo(new[] { 2 }));

            simulation.Step();
            Assert.That(simulation.State.Tick, Is.EqualTo(3));
            Assert.That(simulation.State.Enemies.Select(enemy => enemy.Id.Value), Is.EqualTo(new[] { 2 }));

            simulation.Step();
            Assert.That(simulation.State.Tick, Is.EqualTo(4));
            Assert.That(simulation.State.Enemies.Select(enemy => enemy.Id.Value), Is.EqualTo(new[] { 3 }));
        }

        [Test]
        public void WaveCommands_AreIdempotentAndRejectedCommandsDoNotMutate()
        {
            var simulation = CreateSimulation(new[] { 1, 2 }, startingGold: 0);

            simulation.StartWave();
            var combatSnapshot = Snapshot(simulation);
            Assert.That(simulation.StartWave(), Is.EqualTo(WaveTransitionResult.RejectedInvalidPhase));
            Assert.That(simulation.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.RejectedInvalidPhase));
            Assert.That(Snapshot(simulation), Is.EqualTo(combatSnapshot));

            CompleteCurrentWave(simulation);
            Assert.That(simulation.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.Continued));
            Assert.That(simulation.State.WaveIndex, Is.EqualTo(1));
            var preparationSnapshot = Snapshot(simulation);
            Assert.That(
                simulation.ContinueToNextWave(),
                Is.EqualTo(WaveTransitionResult.RejectedInvalidPhase));
            Assert.That(Snapshot(simulation), Is.EqualTo(preparationSnapshot));

            simulation.StartWave();
            CompleteCurrentWave(simulation);
            Assert.That(simulation.State.Phase, Is.EqualTo(BattlePhase.Victory));
            var victorySnapshot = Snapshot(simulation);
            Assert.That(
                simulation.ContinueToNextWave(),
                Is.EqualTo(WaveTransitionResult.RejectedInvalidPhase));
            Assert.That(Snapshot(simulation), Is.EqualTo(victorySnapshot));
        }

        [Test]
        public void LegacySingleWave_ContinueHasNoNextWaveAndDoesNotMutate()
        {
            var simulation = CreateLegacySimulation();
            simulation.StartWave();
            simulation.Step();
            Assert.That(simulation.State.Phase, Is.EqualTo(BattlePhase.WaveComplete));
            var snapshot = Snapshot(simulation);

            Assert.That(
                simulation.ContinueToNextWave(),
                Is.EqualTo(WaveTransitionResult.RejectedNoNextWave));
            Assert.That(Snapshot(simulation), Is.EqualTo(snapshot));
        }

        [Test]
        public void BetweenWaves_PreservesMatchStateAndAllowsPlacement()
        {
            var simulation = CreateSimulation(
                new[] { 1, 1 },
                speed: 15f,
                baseHealth: 20,
                damage: 25);
            Assert.That(Place(simulation, new GridCoordinate(0, 2)).Success, Is.True);
            simulation.StartWave();
            CompleteCurrentWave(simulation);

            Assert.That(simulation.State.Gold, Is.EqualTo(110));
            Assert.That(simulation.State.BaseHealth, Is.EqualTo(19));
            Assert.That(simulation.State.Tick, Is.EqualTo(2));
            Assert.That(simulation.State.Towers[0].NextAttackTick, Is.EqualTo(16));
            var firstTowerId = simulation.State.Towers[0].Id;

            simulation.ContinueToNextWave();
            var secondTower = Place(simulation, new GridCoordinate(-2, 0));

            Assert.That(secondTower.Success, Is.True, secondTower.FailureReason.ToString());
            Assert.That(secondTower.Tower.Id, Is.EqualTo(new TowerId(2)));
            Assert.That(simulation.State.Towers[0].Id, Is.EqualTo(firstTowerId));
            Assert.That(simulation.State.Towers[0].NextAttackTick, Is.EqualTo(16));
            Assert.That(simulation.State.Gold, Is.EqualTo(50));
            Assert.That(simulation.State.BaseHealth, Is.EqualTo(19));
            Assert.That(simulation.State.Tick, Is.EqualTo(2));
            Assert.That(simulation.State.Phase, Is.EqualTo(BattlePhase.Preparation));
        }

        [Test]
        public void Ids_RemainUniqueAcrossAllWaves()
        {
            var simulation = CreateSimulation(
                new[] { 2, 2, 2 },
                speed: 15f,
                startingGold: 1000,
                damage: 1);
            var enemyIds = new List<int>();
            var towerIds = new List<int>();

            for (var wave = 0; wave < 3; wave++)
            {
                var tower = Place(simulation, new GridCoordinate(-3 + wave, 3));
                Assert.That(tower.Success, Is.True, tower.FailureReason.ToString());
                towerIds.Add(tower.Tower.Id.Value);
                simulation.StartWave();
                CaptureEnemyIds(simulation, enemyIds);
                while (simulation.State.Phase == BattlePhase.Combat)
                {
                    simulation.Step();
                    CaptureEnemyIds(simulation, enemyIds);
                    simulation.ClearAttackEvents();
                }

                if (wave < 2)
                {
                    simulation.ContinueToNextWave();
                }
            }

            Assert.That(enemyIds.Distinct().OrderBy(id => id), Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6 }));
            Assert.That(towerIds, Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(simulation.State.Phase, Is.EqualTo(BattlePhase.Victory));
        }

        [Test]
        public void KillReward_IsGrantedExactlyOnce()
        {
            var simulation = CreateSimulation(new[] { 1 }, speed: 0.03f, reward: 5);
            Place(simulation, new GridCoordinate(0, 2));
            Place(simulation, new GridCoordinate(1, 2));
            simulation.StartWave();

            simulation.Step();

            Assert.That(simulation.State.Kills, Is.EqualTo(1));
            Assert.That(simulation.State.Gold, Is.EqualTo(55));
            Assert.That(simulation.AttackEvents.Count, Is.EqualTo(1));
            Assert.That(simulation.AttackEvents[0].WasLethal, Is.True);
            simulation.ClearAttackEvents();
            var snapshot = Snapshot(simulation);
            Step(simulation, 30);
            Assert.That(Snapshot(simulation), Is.EqualTo(snapshot));
        }

        [Test]
        public void LeakAndWaveBonuses_FollowEconomyInvariants()
        {
            var simulation = CreateSimulation(new[] { 1 }, reward: 5, startingGold: 10);
            simulation.StartWave();

            simulation.Step();

            Assert.That(simulation.State.Phase, Is.EqualTo(BattlePhase.Victory));
            Assert.That(simulation.State.BaseHealth, Is.EqualTo(19));
            Assert.That(simulation.State.Kills, Is.Zero);
            Assert.That(simulation.State.Gold, Is.EqualTo(30));
            var snapshot = Snapshot(simulation);
            simulation.StartWave();
            simulation.ContinueToNextWave();
            Step(simulation, 30);
            Assert.That(Snapshot(simulation), Is.EqualTo(snapshot));
        }

        [Test]
        public void DefeatAndVictory_AreTerminalAndDefeatPaysNoBonus()
        {
            var defeat = CreateSimulation(new[] { 1, 1 }, baseHealth: 1, startingGold: 10);
            defeat.StartWave();
            defeat.Step();
            var defeatSnapshot = Snapshot(defeat);

            Assert.That(defeat.State.Phase, Is.EqualTo(BattlePhase.Defeat));
            Assert.That(defeat.State.Gold, Is.EqualTo(10));
            Assert.That(defeat.State.Enemies, Is.Empty);
            AssertFrozen(defeat, defeatSnapshot);

            var victory = CreateSimulation(new[] { 1 }, startingGold: 10);
            victory.StartWave();
            victory.Step();
            var victorySnapshot = Snapshot(victory);

            Assert.That(victory.State.Phase, Is.EqualTo(BattlePhase.Victory));
            Assert.That(victory.State.Gold, Is.EqualTo(30));
            AssertFrozen(victory, victorySnapshot);
        }

        [Test]
        public void RejectedCommands_PreserveCompleteState()
        {
            var simulation = CreateSimulation(new[] { 1, 1 }, startingGold: 0);
            simulation.StartWave();
            CompleteCurrentWave(simulation);
            var snapshot = Snapshot(simulation);

            Assert.That(simulation.StartWave(), Is.EqualTo(WaveTransitionResult.RejectedInvalidPhase));
            simulation.Step();
            Assert.That(Snapshot(simulation), Is.EqualTo(snapshot));

            simulation.ContinueToNextWave();
            snapshot = Snapshot(simulation);
            var insufficient = Place(simulation, new GridCoordinate(-3, 0));
            Assert.That(insufficient.Success, Is.False);
            Assert.That(insufficient.FailureReason, Is.EqualTo(PlaceTowerFailureReason.InsufficientGold));
            var blocked = Place(simulation, new GridCoordinate(0, 0));
            Assert.That(blocked.Success, Is.False);
            Assert.That(blocked.FailureReason, Is.EqualTo(PlaceTowerFailureReason.Blocked));
            Assert.That(Snapshot(simulation), Is.EqualTo(snapshot));
        }

        [Test]
        public void Rewards_CannotOverflowGold()
        {
            var simulation = CreateSimulation(
                new[] { 1 },
                speed: 0.03f,
                reward: 20,
                startingGold: int.MaxValue,
                towerPrice: 0);
            Place(simulation, new GridCoordinate(0, 2));
            simulation.StartWave();

            simulation.Step();

            Assert.That(simulation.State.Gold, Is.EqualTo(int.MaxValue));
            Assert.That(simulation.State.Gold, Is.GreaterThanOrEqualTo(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateSimulation(
                new[] { 1 },
                waveCompletionBonus: -1));
        }

        [Test]
        public void IdenticalFullMatches_ProduceIdenticalSnapshotsAndEvents()
        {
            var first = CreateFullMatchTimeline();
            var second = CreateFullMatchTimeline();

            Assert.That(second, Is.EqualTo(first));
            Assert.That(first.Count, Is.GreaterThan(8));
            Assert.That(first.Last(), Does.Contain("Phase=Victory"));
            Assert.That(first.Last(), Does.Contain("Kills=68"));
        }

        private static BattleSimulation CreateSimulation(
            IReadOnlyList<int> counts,
            float speed = 300f,
            int reward = 5,
            int waveCompletionBonus = 20,
            int baseHealth = 20,
            int startingGold = 150,
            int damage = 100,
            int towerPrice = 60,
            int spawnInterval = 1,
            float fireRate = 2f)
        {
            var waves = counts.Select(count => new WaveDefinition(
                CreateEnemy(speed, reward),
                count,
                firstSpawnTick: 0,
                spawnIntervalTicks: spawnInterval)).ToArray();
            var map = new GridMapDefinition(12, 8, new GridCoordinate(-6, -4), 1f);
            var path = new PathDefinition(new[]
            {
                new GridCoordinate(0, 0),
                new GridCoordinate(1, 0)
            });
            var tower = new TowerDefinition(
                new TowerDefinitionId("test-tower"),
                towerPrice,
                range: 3f,
                fireRate,
                damage);
            return new BattleSimulation(
                map,
                path,
                waves,
                tower,
                baseHealth,
                startingGold,
                waveCompletionBonus);
        }

        private static BattleSimulation CreateLegacySimulation()
        {
            var path = new PathDefinition(new[]
            {
                new GridCoordinate(0, 0),
                new GridCoordinate(1, 0)
            });
            return new BattleSimulation(
                path,
                new WaveDefinition(CreateEnemy(300f, reward: 0), 1, 0, 1),
                baseHealth: 20);
        }

        private static EnemyDefinition CreateEnemy(float speed, int reward)
        {
            return new EnemyDefinition(
                new EnemyDefinitionId("shade"),
                maxHealth: 100,
                speed,
                leakDamage: 1,
                reward);
        }

        private static PlaceTowerResult Place(BattleSimulation simulation, GridCoordinate coordinate)
        {
            return simulation.PlaceTower(new PlaceTowerCommand(simulation.TowerDefinition.Id, coordinate));
        }

        private static void CompleteCurrentWave(BattleSimulation simulation)
        {
            var guard = 0;
            while (simulation.State.Phase == BattlePhase.Combat && guard++ < 10000)
            {
                simulation.Step();
                simulation.ClearAttackEvents();
            }

            Assert.That(guard, Is.LessThan(10000), "Wave did not complete within the test guard.");
        }

        private static IReadOnlyList<string> CreateFullMatchTimeline()
        {
            var simulation = CreateSimulation(
                EightWaveCounts,
                speed: 15f,
                startingGold: 1000,
                spawnInterval: 2,
                fireRate: 30f);
            Place(simulation, new GridCoordinate(0, 2));
            Place(simulation, new GridCoordinate(1, 2));
            var timeline = new List<string> { Snapshot(simulation) };

            for (var wave = 0; wave < EightWaveCounts.Length; wave++)
            {
                simulation.StartWave();
                timeline.Add(Snapshot(simulation));
                while (simulation.State.Phase == BattlePhase.Combat)
                {
                    simulation.Step();
                    timeline.Add(Snapshot(simulation));
                    simulation.ClearAttackEvents();
                }

                if (wave < EightWaveCounts.Length - 1)
                {
                    simulation.ContinueToNextWave();
                    timeline.Add(Snapshot(simulation));
                }
            }

            return timeline;
        }

        private static void CaptureEnemyIds(BattleSimulation simulation, ICollection<int> ids)
        {
            foreach (var enemy in simulation.State.Enemies)
            {
                if (!ids.Contains(enemy.Id.Value))
                {
                    ids.Add(enemy.Id.Value);
                }
            }
        }

        private static void AssertFrozen(BattleSimulation simulation, string snapshot)
        {
            Assert.That(simulation.StartWave(), Is.EqualTo(WaveTransitionResult.RejectedInvalidPhase));
            Assert.That(
                simulation.ContinueToNextWave(),
                Is.EqualTo(WaveTransitionResult.RejectedInvalidPhase));
            Step(simulation, 30);
            Assert.That(Snapshot(simulation), Is.EqualTo(snapshot));
        }

        private static void Step(BattleSimulation simulation, int count)
        {
            for (var index = 0; index < count; index++)
            {
                simulation.Step();
                simulation.ClearAttackEvents();
            }
        }

        private static string Snapshot(BattleSimulation simulation)
        {
            var state = simulation.State;
            var text = new StringBuilder();
            text.Append("Wave=").Append(state.WaveIndex).Append('/').Append(state.WaveCount)
                .Append("|Phase=").Append(state.Phase)
                .Append("|Tick=").Append(state.Tick)
                .Append("|HP=").Append(state.BaseHealth)
                .Append("|Gold=").Append(state.Gold)
                .Append("|Kills=").Append(state.Kills);

            foreach (var enemy in state.Enemies)
            {
                text.Append("|E:").Append(enemy.Id.Value).Append(',')
                    .Append(enemy.Health).Append(',')
                    .Append(enemy.DistanceAlongPath.ToString("R", CultureInfo.InvariantCulture));
            }

            foreach (var tower in state.Towers)
            {
                text.Append("|T:").Append(tower.Id.Value).Append(',')
                    .Append(tower.Coordinate.X).Append(',')
                    .Append(tower.Coordinate.Y).Append(',')
                    .Append(tower.NextAttackTick);
            }

            foreach (var attack in simulation.AttackEvents)
            {
                text.Append("|A:").Append(attack.AttackerTowerId.Value).Append(',')
                    .Append(attack.TargetEnemyId.Value).Append(',')
                    .Append(attack.TargetPosition.X.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(attack.TargetPosition.Y.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(attack.Tick).Append(',')
                    .Append(attack.Damage).Append(',')
                    .Append(attack.WasLethal);
            }

            return text.ToString();
        }
    }
}
