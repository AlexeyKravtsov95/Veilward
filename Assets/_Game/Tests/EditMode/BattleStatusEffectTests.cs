using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Veilward.Domain;

namespace Veilward.Tests.EditMode
{
    public sealed class BattleStatusEffectTests
    {
        [Test]
        public void RegBattle100_Burn_AppliesWithoutImmediateDamageAndTicksOnExactSchedule()
        {
            var simulation = CreateSimulation(new[] { Flame() });
            Place(simulation, "flame", new GridCoordinate(0, 2));
            simulation.StartWave();

            StepTo(simulation, 1);
            var enemy = simulation.State.Enemies.Single();
            Assert.That(enemy.Health, Is.EqualTo(988));
            Assert.That(enemy.Burn.Power, Is.EqualTo(3));
            Assert.That(enemy.Burn.RemainingTicks, Is.EqualTo(90));
            Assert.That(enemy.Burn.NextDamageTick, Is.EqualTo(16));
            Assert.That(enemy.Burn.SourceTowerId, Is.EqualTo(new TowerId(1)));

            StepTo(simulation, 15);
            Assert.That(enemy.Health, Is.EqualTo(988), "Burn must not deal immediate or early damage.");
            StepTo(simulation, 16);
            Assert.That(enemy.Health, Is.EqualTo(985));
            Assert.That(enemy.Burn.NextDamageTick, Is.EqualTo(31));
            StepTo(simulation, 30);
            Assert.That(enemy.Health, Is.EqualTo(985));
            StepTo(simulation, 31);
            Assert.That(enemy.Health, Is.EqualTo(970),
                "Tick 31 contains the scheduled DoT followed by the second direct Flame hit.");
        }

        [Test]
        public void RegBattle101_ReappliedBurn_ExtendsDurationWithoutStackingOrDelayingDueTick()
        {
            var simulation = CreateSimulation(new[] { Flame() });
            Place(simulation, "flame", new GridCoordinate(0, 2));
            simulation.StartWave();
            StepTo(simulation, 16);
            var enemy = simulation.State.Enemies.Single();
            Assert.That(enemy.Health, Is.EqualTo(985));

            StepTo(simulation, 31);

            Assert.That(enemy.Statuses.Count(status => status.Kind == StatusKind.Burn), Is.EqualTo(1));
            Assert.That(enemy.Burn.RemainingTicks, Is.EqualTo(90));
            Assert.That(enemy.Burn.NextDamageTick, Is.EqualTo(46));
            Assert.That(enemy.Health, Is.EqualTo(970));
            StepTo(simulation, 45);
            Assert.That(enemy.Health, Is.EqualTo(970));
            StepTo(simulation, 46);
            Assert.That(enemy.Health, Is.EqualTo(967));
        }

        [Test]
        public void RegBattle102_Wet_UsesExactMultiplierAndExpiresBeforeMovement()
        {
            var tide = Tower(
                "tide",
                damage: 10,
                fireRate: 0.01f,
                status: Status(StatusKind.Wet, power: 20, duration: 60));
            var simulation = CreateSimulation(new[] { tide }, speed: 3f, pathLength: 20);
            Place(simulation, "tide", new GridCoordinate(0, 2));
            simulation.StartWave();

            StepTo(simulation, 1);
            var enemy = simulation.State.Enemies.Single();
            Assert.That(enemy.DistanceAlongPath, Is.EqualTo(0.1f).Within(0.0001f),
                "Wet is applied after Movement and cannot slow its application tick.");
            StepTo(simulation, 2);
            Assert.That(enemy.DistanceAlongPath, Is.EqualTo(0.18f).Within(0.0001f));
            StepTo(simulation, 60);
            Assert.That(enemy.DistanceAlongPath, Is.EqualTo(4.82f).Within(0.0001f));
            Assert.That(enemy.Wet.RemainingTicks, Is.EqualTo(1));
            StepTo(simulation, 61);
            Assert.That(enemy.Wet, Is.Null);
            Assert.That(enemy.DistanceAlongPath, Is.EqualTo(4.92f).Within(0.0001f));
        }

        [Test]
        public void RegBattle103_Gust_UsesFloorRoundingOnLaterDirectDamageOnly()
        {
            var galeSimulation = CreateSimulation(new[] { Gale() });
            Place(galeSimulation, "gale", new GridCoordinate(0, 2));
            galeSimulation.StartWave();
            StepTo(galeSimulation, 1);
            Assert.That(galeSimulation.State.Enemies.Single().Health, Is.EqualTo(994),
                "The hit that applies Gust receives no Gust bonus.");

            var directSimulation = CreateSimulation(new[] { Plain("direct", damage: 9) });
            Place(directSimulation, "direct", new GridCoordinate(0, 2));
            directSimulation.StartWave();
            var directTarget = directSimulation.State.Enemies.Single();
            directTarget.ApplyStatus(
                Status(StatusKind.Gust, power: 20, duration: 45), new TowerId(99), currentTick: 0);
            StepTo(directSimulation, 1);
            Assert.That(directTarget.Health, Is.EqualTo(990), "floor(9 * 1.20) must be 10.");

            var burnSimulation = CreateSimulation(new[] { Plain("unused") }, maxHealth: 100);
            burnSimulation.StartWave();
            var burnTarget = burnSimulation.State.Enemies.Single();
            burnTarget.ApplyStatus(
                Status(StatusKind.Gust, power: 50, duration: 30), new TowerId(99), currentTick: 0);
            burnTarget.ApplyStatus(
                Status(StatusKind.Burn, power: 5, duration: 30, interval: 15), new TowerId(98), currentTick: 0);
            StepTo(burnSimulation, 15);
            Assert.That(burnTarget.Health, Is.EqualTo(95), "Gust must not amplify Burn.");
        }

        [Test]
        public void RegBattle103_ExtremeGust_SaturatesDamageWithoutOverflow()
        {
            var simulation = CreateSimulation(
                new[] { Plain("direct", damage: 100) }, maxHealth: int.MaxValue);
            Place(simulation, "direct", new GridCoordinate(0, 2));
            simulation.StartWave();
            var target = simulation.State.Enemies.Single();
            target.ApplyStatus(
                Status(StatusKind.Gust, power: int.MaxValue, duration: 45),
                new TowerId(99),
                currentTick: 0);

            Assert.DoesNotThrow(() => StepTo(simulation, 1));

            Assert.That(target.Health, Is.Zero);
            Assert.That(simulation.State.Kills, Is.EqualTo(1));
        }

        [Test]
        public void RegBattle104_StoneAndCrack_ApplyPenetrationThenReduceDirectAndBurnArmor()
        {
            var flame = Tower(
                "flame",
                damage: 12,
                fireRate: 1f,
                status: Status(StatusKind.Burn, power: 12, duration: 90, interval: 15));
            var simulation = CreateSimulation(new[] { Stone(), flame }, armor: 10);
            Place(simulation, "stone", new GridCoordinate(-1, 2));
            Place(simulation, "flame", new GridCoordinate(1, 2));
            simulation.StartWave();

            StepTo(simulation, 1);
            var enemy = simulation.State.Enemies.Single();
            Assert.That(enemy.Health, Is.EqualTo(962),
                "Stone deals 40 - (10 - 4) = 34; subsequent Flame deals 12 - (10 - 2) = 4.");
            Assert.That(enemy.Crack.Power, Is.EqualTo(2));
            StepTo(simulation, 15);
            Assert.That(enemy.Health, Is.EqualTo(962));
            StepTo(simulation, 16);
            Assert.That(enemy.Health, Is.EqualTo(958), "Burn deals 12 - (10 - 2) = 4.");
        }

        [Test]
        public void RegBattle105_Reapplication_PreservesStrongestPowerAndUsesMaximumDuration()
        {
            var enemy = Enemy(maxHealth: 100);
            enemy.ApplyStatus(
                Status(StatusKind.Burn, power: 5, duration: 80, interval: 15), new TowerId(1), currentTick: 0);

            enemy.ApplyStatus(
                Status(StatusKind.Burn, power: 3, duration: 90, interval: 30), new TowerId(2), currentTick: 5);
            Assert.That(enemy.Burn.Power, Is.EqualTo(5));
            Assert.That(enemy.Burn.DamageIntervalTicks, Is.EqualTo(15));
            Assert.That(enemy.Burn.RemainingTicks, Is.EqualTo(90));
            Assert.That(enemy.Burn.NextDamageTick, Is.EqualTo(15));
            Assert.That(enemy.Burn.SourceTowerId, Is.EqualTo(new TowerId(1)));

            enemy.ApplyStatus(
                Status(StatusKind.Burn, power: 7, duration: 10, interval: 20), new TowerId(3), currentTick: 10);
            Assert.That(enemy.Burn.Power, Is.EqualTo(7));
            Assert.That(enemy.Burn.DamageIntervalTicks, Is.EqualTo(20));
            Assert.That(enemy.Burn.RemainingTicks, Is.EqualTo(90));
            Assert.That(enemy.Burn.NextDamageTick, Is.EqualTo(15));
            Assert.That(enemy.Burn.SourceTowerId, Is.EqualTo(new TowerId(3)));

            enemy.ApplyStatus(
                Status(StatusKind.Burn, power: 7, duration: 5, interval: 20), new TowerId(4), currentTick: 11);
            Assert.That(enemy.Burn.SourceTowerId, Is.EqualTo(new TowerId(4)),
                "Equal power updates the event source without replacing the due tick.");
            Assert.That(enemy.Burn.NextDamageTick, Is.EqualTo(15));
            Assert.That(enemy.Statuses.Count, Is.EqualTo(1));
        }

        [Test]
        public void RegBattle106_LastActiveTick_DealsBurnBeforeExpiryAndExpiredModifiersSkipAttacks()
        {
            var burnSimulation = CreateSimulation(new[] { Plain("unused") }, maxHealth: 100);
            burnSimulation.StartWave();
            var burnTarget = burnSimulation.State.Enemies.Single();
            burnTarget.ApplyStatus(
                Status(StatusKind.Burn, power: 5, duration: 15, interval: 15), new TowerId(1), currentTick: 0);
            StepTo(burnSimulation, 15);
            Assert.That(burnTarget.Health, Is.EqualTo(95));
            Assert.That(burnTarget.Burn, Is.Null);

            var gustSimulation = CreateSimulation(new[] { Plain("direct", damage: 10) }, maxHealth: 100);
            Place(gustSimulation, "direct", new GridCoordinate(0, 2));
            gustSimulation.StartWave();
            var gustTarget = gustSimulation.State.Enemies.Single();
            gustTarget.ApplyStatus(
                Status(StatusKind.Gust, power: 50, duration: 1), new TowerId(1), currentTick: 0);
            StepTo(gustSimulation, 1);
            Assert.That(gustTarget.Health, Is.EqualTo(90));
            Assert.That(gustTarget.Gust, Is.Null);

            var crackSimulation = CreateSimulation(
                new[] { Plain("direct", damage: 20) }, maxHealth: 100, armor: 10);
            Place(crackSimulation, "direct", new GridCoordinate(0, 2));
            crackSimulation.StartWave();
            var crackTarget = crackSimulation.State.Enemies.Single();
            crackTarget.ApplyStatus(
                Status(StatusKind.Crack, power: 5, duration: 1), new TowerId(1), currentTick: 0);
            StepTo(crackSimulation, 1);
            Assert.That(crackTarget.Health, Is.EqualTo(90));
            Assert.That(crackTarget.Crack, Is.Null);
        }

        [Test]
        public void RegBattle107_LethalDirectHit_DoesNotApplyStatusOrBecomeLaterTarget()
        {
            var simulation = CreateSimulation(new[]
            {
                Tower("flame", damage: 100, fireRate: 30f,
                    status: Status(StatusKind.Burn, power: 3, duration: 90, interval: 15)),
                Plain("direct", damage: 10)
            }, maxHealth: 100);
            Place(simulation, "flame", new GridCoordinate(-1, 2));
            Place(simulation, "direct", new GridCoordinate(1, 2));
            simulation.StartWave();
            var target = simulation.State.Enemies.Single();

            StepTo(simulation, 1);

            Assert.That(target.IsAlive, Is.False);
            Assert.That(target.Burn, Is.Null);
            Assert.That(simulation.AttackEvents.Count, Is.EqualTo(1));
            Assert.That(simulation.State.Kills, Is.EqualTo(1));
            Assert.That(simulation.State.Enemies, Is.Empty);
        }

        [Test]
        public void RegBattle108_DotKillBeforeMovement_DoesNotLeakAndPaysExactlyOnce()
        {
            var simulation = CreateSimulation(
                new[] { Plain("unused") }, maxHealth: 5, speed: 30f, reward: 7,
                startingGold: 0, pathLength: 15);
            simulation.StartWave();
            var target = simulation.State.Enemies.Single();
            target.ApplyStatus(
                Status(StatusKind.Burn, power: 5, duration: 15, interval: 15), new TowerId(1), currentTick: 0);
            StepTo(simulation, 14);
            Assert.That(target.DistanceAlongPath, Is.EqualTo(14f).Within(0.0001f));

            StepTo(simulation, 15);
            var gold = simulation.State.Gold;
            var kills = simulation.State.Kills;
            Step(simulation, 5);

            Assert.That(simulation.State.BaseHealth, Is.EqualTo(20));
            Assert.That(kills, Is.EqualTo(1));
            Assert.That(gold, Is.EqualTo(7));
            Assert.That(simulation.State.Gold, Is.EqualTo(7));
            Assert.That(simulation.State.Kills, Is.EqualTo(1));
            Assert.That(simulation.State.Phase, Is.EqualTo(BattlePhase.WaveComplete));
        }

        [Test]
        public void RegBattle109_OverkillAndRepeatedDeathProcessing_DoNotDuplicateReward()
        {
            var simulation = CreateSimulation(new[]
            {
                Tower("flame", damage: 100, fireRate: 30f,
                    status: Status(StatusKind.Burn, power: 3, duration: 90, interval: 15)),
                Plain("direct", damage: 100)
            }, maxHealth: 50, reward: 5, startingGold: 0);
            Place(simulation, "flame", new GridCoordinate(-1, 2));
            Place(simulation, "direct", new GridCoordinate(1, 2));
            simulation.StartWave();

            StepTo(simulation, 1);
            Step(simulation, 5);

            Assert.That(simulation.State.Kills, Is.EqualTo(1));
            Assert.That(simulation.State.Gold, Is.EqualTo(5));
            Assert.That(simulation.AttackEvents.Count, Is.EqualTo(1));
        }

        [Test]
        public void RegBattle109_DotReward_SaturatesGoldWithoutOverflowOrDuplication()
        {
            var simulation = CreateSimulation(
                new[] { Plain("unused") }, maxHealth: 5, reward: 5,
                startingGold: int.MaxValue - 2);
            simulation.StartWave();
            var target = simulation.State.Enemies.Single();
            target.ApplyStatus(
                Status(StatusKind.Burn, power: 5, duration: 15, interval: 15), new TowerId(1), currentTick: 0);

            StepTo(simulation, 15);
            Step(simulation, 5);

            Assert.That(simulation.State.Gold, Is.EqualTo(int.MaxValue));
            Assert.That(simulation.State.Kills, Is.EqualTo(1));
        }

        [Test]
        public void RegBattle110_BurnWithAbsentSource_StillKillsAndPaysExactlyOnce()
        {
            var simulation = CreateSimulation(
                new[] { Plain("unused") }, maxHealth: 5, reward: 9, startingGold: 0);
            simulation.StartWave();
            var target = simulation.State.Enemies.Single();
            target.ApplyStatus(
                Status(StatusKind.Burn, power: 5, duration: 15, interval: 15),
                new TowerId(12345), currentTick: 0);

            StepTo(simulation, 15);
            Step(simulation, 5);

            Assert.That(simulation.State.Kills, Is.EqualTo(1));
            Assert.That(simulation.State.Gold, Is.EqualTo(9));
            Assert.That(simulation.State.Enemies, Is.Empty);
        }

        [Test]
        public void RegBattle111_SameTickStatusEffects_AreVisibleOnlyToLaterTowerIds()
        {
            var direct = Plain("direct", damage: 20);
            var stoneFirst = CreateSimulation(new[] { Stone(), direct }, armor: 10);
            Place(stoneFirst, "stone", new GridCoordinate(-1, 2));
            Place(stoneFirst, "direct", new GridCoordinate(1, 2));
            stoneFirst.StartWave();
            StepTo(stoneFirst, 1);

            var stoneLast = CreateSimulation(new[] { Stone(), direct }, armor: 10);
            Place(stoneLast, "direct", new GridCoordinate(-1, 2));
            Place(stoneLast, "stone", new GridCoordinate(1, 2));
            stoneLast.StartWave();
            StepTo(stoneLast, 1);

            Assert.That(stoneFirst.State.Enemies.Single().Health, Is.EqualTo(954));
            Assert.That(stoneLast.State.Enemies.Single().Health, Is.EqualTo(956));
            Assert.That(stoneFirst.AttackEvents.Select(attack => attack.AttackerTowerId.Value),
                Is.EqualTo(new[] { 1, 2 }));
            Assert.That(stoneLast.AttackEvents.Select(attack => attack.AttackerTowerId.Value),
                Is.EqualTo(new[] { 1, 2 }));

            var gale = Gale();
            var gustDirect = Plain("direct", damage: 10);
            var gustFirst = CreateSimulation(new[] { gale, gustDirect });
            Place(gustFirst, "gale", new GridCoordinate(-1, 2));
            Place(gustFirst, "direct", new GridCoordinate(1, 2));
            gustFirst.StartWave();
            StepTo(gustFirst, 1);

            var gustLast = CreateSimulation(new[] { gale, gustDirect });
            Place(gustLast, "direct", new GridCoordinate(-1, 2));
            Place(gustLast, "gale", new GridCoordinate(1, 2));
            gustLast.StartWave();
            StepTo(gustLast, 1);

            Assert.That(gustFirst.State.Enemies.Single().Health, Is.EqualTo(982));
            Assert.That(gustLast.State.Enemies.Single().Health, Is.EqualTo(984));
        }

        [Test]
        public void RegBattle111_RestoredTowers_KeepTowerIdAttackOrder()
        {
            var direct = Plain("direct", damage: 20);
            var simulation = CreateSimulation(new[] { Stone(), direct }, armor: 10);
            Place(simulation, "stone", new GridCoordinate(-1, 2));
            Place(simulation, "direct", new GridCoordinate(1, 2));
            var checkpoint = simulation.ExportCheckpoint();
            Array.Reverse(checkpoint.towers);

            var restored = CreateSimulation(new[] { Stone(), direct }, armor: 10);
            restored.RestoreCheckpoint(checkpoint);
            restored.StartWave();
            StepTo(restored, 1);

            Assert.That(restored.State.Enemies.Single().Health, Is.EqualTo(954));
            Assert.That(
                restored.AttackEvents.Select(attack => attack.AttackerTowerId.Value),
                Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void RegBattle113_CheckpointRollback_DiscardsPartialDotAttemptAndRewards()
        {
            var simulation = CreateSimulation(new[] { Flame() }, maxHealth: 20, reward: 5, startingGold: 0);
            Place(simulation, "flame", new GridCoordinate(0, 2));
            var beforeAttempt = simulation.ExportCheckpoint();

            simulation.StartWave();
            StepTo(simulation, 31);
            Assert.That(simulation.State.Kills, Is.EqualTo(1));
            Assert.That(simulation.State.Gold, Is.EqualTo(5));

            var restored = CreateSimulation(new[] { Flame() }, maxHealth: 20, reward: 5, startingGold: 0);
            restored.RestoreCheckpoint(beforeAttempt);

            Assert.That(restored.State.Phase, Is.EqualTo(BattlePhase.Preparation));
            Assert.That(restored.State.Tick, Is.Zero);
            Assert.That(restored.State.Enemies, Is.Empty);
            Assert.That(restored.State.Kills, Is.Zero);
            Assert.That(restored.State.Gold, Is.Zero);
            Assert.That(restored.ExportCheckpoint().nextEnemyId, Is.EqualTo(beforeAttempt.nextEnemyId));
            restored.StartWave();
            StepTo(restored, 31);
            Assert.That(restored.State.Kills, Is.EqualTo(1));
            Assert.That(restored.State.Gold, Is.EqualTo(5));
        }

        [Test]
        public void RegBattle114_IdenticalMixedStatusRuns_ProduceIdenticalCompleteTimelines()
        {
            var first = RunMixedStatusReplay();
            var second = RunMixedStatusReplay();

            Assert.That(second.Timeline, Is.EqualTo(first.Timeline));
            Assert.That(first.Timeline, Is.Not.Empty);
            Assert.That(first.TerminalPhase, Is.EqualTo(BattlePhase.WaveComplete));
            Assert.That(first.AppliedStatuses, Is.EquivalentTo(new[]
            {
                StatusKind.Burn, StatusKind.Wet, StatusKind.Gust, StatusKind.Crack
            }));
        }

        private static ReplayResult RunMixedStatusReplay()
        {
            var simulation = CreateSimulation(
                new[] { Flame(), Tide(), Gale(), Stone() },
                maxHealth: 1000, armor: 10, count: 2, spawnInterval: 30);
            Place(simulation, "gale", new GridCoordinate(-2, 2));
            Place(simulation, "stone", new GridCoordinate(-1, 2));
            Place(simulation, "flame", new GridCoordinate(0, 2));
            Place(simulation, "tide", new GridCoordinate(1, 2));
            simulation.StartWave();
            var timeline = new List<string>();
            var statuses = new HashSet<StatusKind>();
            var guard = 0;
            while (simulation.State.Phase == BattlePhase.Combat && guard++ < 5000)
            {
                simulation.Step();
                foreach (var enemy in simulation.State.Enemies)
                {
                    foreach (var status in enemy.Statuses)
                    {
                        statuses.Add(status.Kind);
                    }
                }

                timeline.Add(Snapshot(simulation));
                simulation.ClearAttackEvents();
            }

            Assert.That(guard, Is.LessThan(5000), "Mixed-status replay must reach a terminal phase.");
            return new ReplayResult(timeline, simulation.State.Phase, statuses);
        }

        private static string Snapshot(BattleSimulation simulation)
        {
            var state = simulation.State;
            var enemies = string.Join(";", state.Enemies.Select(enemy =>
                $"{enemy.Id.Value}:{enemy.Health}:{enemy.DistanceAlongPath:R}:" +
                string.Join(",", enemy.Statuses.Select(status =>
                    $"{status.Kind}:{status.Power}:{status.RemainingTicks}:{status.NextDamageTick}:{status.SourceTowerId.Value}"))));
            var towers = string.Join(";", state.Towers.Select(tower =>
                $"{tower.Id.Value}:{tower.NextAttackTick}"));
            var attacks = string.Join(";", simulation.AttackEvents.Select(attack =>
                $"{attack.AttackerTowerId.Value}:{attack.TargetEnemyId.Value}:{attack.Tick}:{attack.Damage}:{attack.WasLethal}"));
            return $"{state.Tick}|{state.Phase}|{state.BaseHealth}|{state.Gold}|{state.Kills}|{enemies}|{towers}|{attacks}";
        }

        private static BattleSimulation CreateSimulation(
            IReadOnlyList<TowerDefinition> towerDefinitions,
            int maxHealth = 1000,
            float speed = 0.03f,
            int armor = 0,
            int reward = 5,
            int startingGold = 10000,
            int count = 1,
            int spawnInterval = 30,
            int pathLength = 20)
        {
            var map = new GridMapDefinition(9, 7, new GridCoordinate(-4, -3), 1f);
            var path = new PathDefinition(new[]
            {
                new GridCoordinate(0, 0),
                new GridCoordinate(pathLength, 0)
            });
            var enemy = new EnemyDefinition(
                new EnemyDefinitionId("shade"), maxHealth, speed, leakDamage: 1, reward, armor);
            var wave = new WaveDefinition(enemy, count, firstSpawnTick: 0, spawnInterval);
            var archetypes = towerDefinitions.Select(definition =>
                new TowerArchetypeDefinition(
                    definition.Id,
                    showInCatalog: true,
                    new[] { definition, definition, definition })).ToArray();
            return new BattleSimulation(
                map, path, wave, new TowerCatalog(archetypes), baseHealth: 20, startingGold);
        }

        private static EnemyState Enemy(int maxHealth)
        {
            return EnemyState.CreateInitial(
                new EnemyId(1),
                new EnemyDefinition(
                    new EnemyDefinitionId("shade"), maxHealth, speed: 1f, leakDamage: 1, reward: 0));
        }

        private static TowerDefinition Flame()
        {
            return Tower(
                "flame", damage: 12, fireRate: 1f,
                status: Status(StatusKind.Burn, power: 3, duration: 90, interval: 15));
        }

        private static TowerDefinition Tide()
        {
            return Tower(
                "tide", damage: 10, fireRate: 0.8f,
                status: Status(StatusKind.Wet, power: 20, duration: 60));
        }

        private static TowerDefinition Gale()
        {
            return Tower(
                "gale", damage: 6, fireRate: 3f,
                status: Status(StatusKind.Gust, power: 20, duration: 45));
        }

        private static TowerDefinition Stone()
        {
            return Tower(
                "stone", damage: 40, fireRate: 0.5f,
                status: Status(StatusKind.Crack, power: 2, duration: 90), armorPenetration: 4);
        }

        private static TowerDefinition Plain(string id, int damage = 25)
        {
            return Tower(id, damage, fireRate: 30f);
        }

        private static TowerDefinition Tower(
            string id,
            int damage,
            float fireRate,
            StatusEffectDefinition status = null,
            int armorPenetration = 0)
        {
            return new TowerDefinition(
                new TowerDefinitionId(id), price: 0, range: 10f, fireRate, damage, status, armorPenetration);
        }

        private static StatusEffectDefinition Status(
            StatusKind kind,
            int power,
            int duration,
            int interval = 0)
        {
            return new StatusEffectDefinition(kind, power, duration, interval);
        }

        private static void Place(BattleSimulation simulation, string id, GridCoordinate coordinate)
        {
            var result = simulation.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId(id), coordinate));
            Assert.That(result.Success, Is.True, result.FailureReason.ToString());
        }

        private static void StepTo(BattleSimulation simulation, long targetTick)
        {
            while (simulation.State.Tick < targetTick && simulation.State.Phase == BattlePhase.Combat)
            {
                simulation.Step();
            }

            Assert.That(simulation.State.Tick, Is.EqualTo(targetTick));
        }

        private static void Step(BattleSimulation simulation, int count)
        {
            for (var index = 0; index < count; index++)
            {
                simulation.Step();
            }
        }

        private sealed class ReplayResult
        {
            public ReplayResult(
                List<string> timeline,
                BattlePhase terminalPhase,
                HashSet<StatusKind> appliedStatuses)
            {
                Timeline = timeline;
                TerminalPhase = terminalPhase;
                AppliedStatuses = appliedStatuses;
            }

            public List<string> Timeline { get; }
            public BattlePhase TerminalPhase { get; }
            public HashSet<StatusKind> AppliedStatuses { get; }
        }
    }
}
