using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using Veilward.Domain;
using Veilward.Presentation;

namespace Veilward.Tests.EditMode
{
    public sealed class BattleAbilityTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly PathPosition Center = new PathPosition(0, 0);

        // Explicit synthetic setup; production constructors/factories have no ability opt-in.
        private static BattleSimulation Create(int hp = 1000, int armor = 0, int count = 8,
            int interval = 10000, int waveCount = 1, int baseHealth = 20, int gold = 150,
            bool enabled = true, bool native = false, float speed = 0.001f, bool reactionTower = false, AbilityDefinition definition = null)
        {
            var map = new GridMapDefinition(12, 8, new GridCoordinate(-6, -4), 1f);
            var path = new PathDefinition(new[] { new GridCoordinate(0, 0), new GridCoordinate(5, 0) });
            var tower = new TowerDefinition(new TowerDefinitionId("fixture"), 0, 10, 1, 1,
                reactionTower ? new StatusEffectDefinition(StatusKind.Wet, 10, 1000, 0) : null);
            var catalog = new TowerCatalog(new[] { TowerArchetypeDefinition.CreateCompatibility(new[] { tower }) });
            if (native) catalog = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWave.asset").CreateSimulation().TowerCatalog;
            var enemy = new EnemyDefinition(new EnemyDefinitionId("fixture"), hp, speed, 20, 5, armor);
            var waves = Enumerable.Range(0, waveCount).Select(_ => new WaveDefinition(enemy, count, 0, interval)).ToArray();
            return new BattleSimulation(map, path, waves, catalog, baseHealth, gold, 20,
                legacySingleWave: false, reactionCatalog: new ReactionCatalog(new[] {
                    new ReactionDefinition(new ReactionPayload(ReactionKind.Steam, 100, 1.5f, 4), 60, 0),
                    new ReactionDefinition(new ReactionPayload(ReactionKind.Storm, 100, 1.5f, 2), 60, 1) }),
                elementUnlockRules: native ? BattleElementUnlockTests.Rules() : null,
                abilityDefinition: enabled ? definition ?? AbilityDefinition.RiftSeal : null);
        }

        private static CastAbilityCommand Command(BattleSimulation s, PathPosition? center = null,
            AbilityDefinitionId? id = null, int? wave = null, long? tick = null, long? sequence = null) =>
            new CastAbilityCommand(id ?? AbilityDefinitionId.RiftSeal, center ?? Center,
                wave ?? s.State.WaveIndex, tick ?? s.State.Tick, sequence ?? s.AbilityState.NextCastSequence);

        private static EnemyState Add(BattleSimulation s, int id, float distance, int hp = 1000, int armor = 0)
        {
            var enemy = new EnemyState(new EnemyId(id),
                new EnemyDefinition(new EnemyDefinitionId("fixture"), hp, 0.001f, 20, 5, armor), hp, distance);
            s.State.AddEnemy(enemy);
            return enemy;
        }

        private static void SetSequence(BattleSimulation s, long sequence) =>
            typeof(AbilityState).GetField("<NextCastSequence>k__BackingField", Private).SetValue(s.AbilityState, sequence);

        // Snapshot all public state/event properties recursively plus orchestration counters.
        // Unlike event.ToString(), this detects changes to every optional attribution/status field.
        private static string Snapshot(BattleSimulation s)
        {
            var result = new StringBuilder(BattleElementUnlockTests.Snapshot(s));
            foreach (var value in new object[] { s.State, s.AbilityState, s.AttackEvents, s.ReactionEvents, s.DamageEvents, s.AbilityEvents })
                Append(result, value);
            foreach (var field in new[] { "_nextEnemyId", "_nextTowerId", "_nextSpawnIndex", "_waveStartTick", "_sequenceInTick" })
                Append(result, typeof(BattleSimulation).GetField(field, Private).GetValue(s));
            return result.ToString();
        }

        private static void Append(StringBuilder result, object value)
        {
            if (value == null) { result.Append("null;"); return; }
            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || value is string)
            { result.Append(Convert.ToString(value, CultureInfo.InvariantCulture)).Append(';'); return; }
            if (value is IEnumerable values) { foreach (var item in values) Append(result, item); result.Append('|'); return; }
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public).OrderBy(p => p.Name))
            { result.Append(property.Name).Append('='); Append(result, property.GetValue(value)); }
        }

        private static void Reject(BattleSimulation s, CastAbilityCommand command, AbilityCastFailureReason expected)
        {
            var before = Snapshot(s);
            var state = s.State; var grid = s.Grid; var ability = s.AbilityState;
            var check = s.CheckCastAbility(command);
            Assert.That(check.Succeeded, Is.False);
            Assert.That(check.FailureReason, Is.EqualTo(expected));
            Assert.That(Snapshot(s), Is.EqualTo(before), "Check mutated state/events");
            var result = s.CastAbility(command);
            Assert.That(result.FailureReason, Is.EqualTo(expected));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.CastSequence, Is.Null);
            Assert.That(result.HitCount, Is.Zero);
            Assert.That(result.TotalAppliedHpLoss, Is.Zero);
            Assert.That(Snapshot(s), Is.EqualTo(before), "Cast mutated state/events");
            Assert.That(s.State, Is.SameAs(state)); Assert.That(s.Grid, Is.SameAs(grid));
            Assert.That(s.AbilityState, Is.SameAs(ability));
        }

        [TestCase(BattlePhase.Preparation)] [TestCase(BattlePhase.WaveComplete)]
        [TestCase(BattlePhase.Victory)] [TestCase(BattlePhase.Defeat)]
        public void RegBattle191_PhaseHasFirstPriority(BattlePhase phase)
        {
            var s = Create(enabled: false); s.State.SetPhase(phase);
            Reject(s, Command(s, id: default(AbilityDefinitionId), tick: -1), AbilityCastFailureReason.InvalidPhase);
        }

        [Test]
        public void RegBattle191_DisabledBeforeUnknownBeforeStale()
        {
            var disabled = Create(enabled: false); disabled.StartWave();
            Reject(disabled, Command(disabled, id: default(AbilityDefinitionId), tick: -1), AbilityCastFailureReason.AbilityDisabled);
            var s = Create(); s.StartWave();
            Reject(s, Command(s, id: default(AbilityDefinitionId), tick: -1), AbilityCastFailureReason.UnknownAbility);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        [TestCase(4)] [TestCase(5)] [TestCase(6)]
        public void RegBattle191_194_RejectionsPreserveFullStateAndFourNonemptyBuffers(int failure)
        {
            var s = Create(reactionTower: true); s.StartWave();
            s.State.AddTower(new TowerState(new TowerId(1), s.TowerDefinition, new GridCoordinate(0, 1)));
            s.Grid.TryOccupy(new GridCoordinate(0, 1));
            var target = s.State.Enemies[0];
            target.ApplyStatus(new StatusEffectDefinition(StatusKind.Burn, 1, 1000, 100), new TowerId(1), 0);
            s.Step();
            Assert.That(s.CastAbility(Command(s)).Succeeded, Is.True);
            Assert.That(s.AttackEvents, Is.Not.Empty); Assert.That(s.ReactionEvents, Is.Not.Empty);
            Assert.That(s.DamageEvents, Is.Not.Empty); Assert.That(s.AbilityEvents, Is.Not.Empty);
            CastAbilityCommand command;
            AbilityCastFailureReason expected;
            if (failure == 0) { s.State.SetPhase(BattlePhase.Victory); command = Command(s); expected = AbilityCastFailureReason.InvalidPhase; }
            else if (failure == 1) { command = Command(s, id: default(AbilityDefinitionId)); expected = AbilityCastFailureReason.UnknownAbility; }
            else if (failure == 2) { command = Command(s, tick: -1); expected = AbilityCastFailureReason.StaleCommand; }
            else if (failure == 3) { command = Command(s, center: new PathPosition(6, 4)); expected = AbilityCastFailureReason.CooldownActive; }
            else
            {
                for (var i = 0; i < 600; i++) s.Step();
                if (failure == 4) { command = Command(s, center: new PathPosition(6, 0)); expected = AbilityCastFailureReason.InvalidTarget; }
                else if (failure == 5) { command = Command(s, center: new PathPosition(-5, -3)); expected = AbilityCastFailureReason.NoTargets; }
                else { SetSequence(s, long.MaxValue); command = Command(s); expected = AbilityCastFailureReason.IdentifierExhausted; }
            }
            Reject(s, command, expected);
        }

        [Test]
        public void RegBattle191_CheckSuccessIsReadOnlyAndRevalidatesMovedTarget()
        {
            var s = Create(); s.StartWave(); var command = Command(s); var before = Snapshot(s);
            var check = s.CheckCastAbility(command);
            Assert.That(check.Succeeded, Is.True); Assert.That(check.CastSequence, Is.Null);
            Assert.That(Snapshot(s), Is.EqualTo(before));
            s.State.Enemies[0].Advance(4);
            Reject(s, command, AbilityCastFailureReason.NoTargets);
        }

        [TestCase(-6f, -4f, AbilityCastFailureReason.NoTargets)]
        [TestCase(5.999f, 3.999f, AbilityCastFailureReason.NoTargets)]
        [TestCase(-6.001f, 0f, AbilityCastFailureReason.InvalidTarget)]
        [TestCase(0f, -4.001f, AbilityCastFailureReason.InvalidTarget)]
        [TestCase(6f, 0f, AbilityCastFailureReason.InvalidTarget)]
        [TestCase(0f, 4f, AbilityCastFailureReason.InvalidTarget)]
        [TestCase(float.MaxValue, float.MaxValue, AbilityCastFailureReason.InvalidTarget)]
        public void RegBattle192_HalfOpenCenterBounds(float x, float y, AbilityCastFailureReason reason)
        {
            var s = Create(); s.StartWave(); Reject(s, Command(s, new PathPosition(x, y)), reason);
        }

        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        public void RegBattle192_NonfiniteCenterCannotEnterCommand(float value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PathPosition(value, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PathPosition(0, value));
        }

        [TestCase(0f)] [TestCase(1f)]
        public void RegBattle192_RoadAndOccupiedCentersAreValid(float y)
        {
            var s = Create(); s.Grid.TryOccupy(new GridCoordinate(0, 1)); s.StartWave();
            Assert.That(s.CastAbility(Command(s, new PathPosition(0, y))).Succeeded, Is.True);
        }

        [Test]
        public void RegBattle192_InclusiveRadiusAliveOnlyCapturedInEnemyIdOrder()
        {
            var s = Create(); s.StartWave();
            var dead = Add(s, 7, 0); dead.ApplyDamage(1000);
            Add(s, 6, 1.5001f); Add(s, 5, 1.5f); Add(s, 3, 0.5f);
            var result = s.CastAbility(Command(s));
            Assert.That(result.HitCount, Is.EqualTo(3)); Assert.That(result.TotalAppliedHpLoss, Is.EqualTo(360));
            Assert.That(s.DamageEvents.Select(e => e.TargetEnemyId.Value), Is.EqualTo(new[] { 1, 3, 5 }));
            Assert.That(s.DamageEvents.Select(e => e.TargetPosition.X), Is.EqualTo(new[] { 0f, 0.5f, 1.5f }));
            Assert.That(s.State.Enemies.Single(e => e.Id.Value == 6).Health, Is.EqualTo(1000));
        }

        [TestCase(10, 0, 110)] [TestCase(10, 4, 114)] [TestCase(10, 100, 120)]
        [TestCase(500, 0, 1)] [TestCase(int.MaxValue, 0, 1)]
        public void RegBattle193_ArmorCrackMinOneWithoutGustOrStatusReactionMutation(int armor, int crack, int loss)
        {
            var s = Create(armor: armor); s.StartWave(); var enemy = s.State.Enemies[0];
            foreach (var kind in new[] { StatusKind.Burn, StatusKind.Wet, StatusKind.Gust })
                enemy.ApplyStatus(new StatusEffectDefinition(kind, kind == StatusKind.Wet ? 10 : 50, 100, kind == StatusKind.Burn ? 30 : 0), new TowerId(1), 0);
            if (crack > 0) enemy.ApplyStatus(new StatusEffectDefinition(StatusKind.Crack, crack, 100, 0), new TowerId(1), 0);
            enemy.ReserveReaction(ReactionKind.Steam, 0); enemy.ReserveReaction(ReactionKind.Storm, 0);
            var statuses = new StringBuilder(); Append(statuses, enemy.Statuses);
            var result = s.CastAbility(Command(s));
            Assert.That(result.TotalAppliedHpLoss, Is.EqualTo(loss)); Assert.That(enemy.Health, Is.EqualTo(1000 - loss));
            var after = new StringBuilder(); Append(after, enemy.Statuses); Assert.That(after.ToString(), Is.EqualTo(statuses.ToString()));
            Assert.That(enemy.LastSteamTick, Is.Zero); Assert.That(enemy.LastStormTick, Is.Zero);
            Assert.That(s.ReactionEvents, Is.Empty); Assert.That(s.AttackEvents, Is.Empty);
        }

        [Test]
        public void RegBattle193_MultiTargetOverkillOneRewardSaturation()
        {
            var s = Create(hp: 50, armor: 10, count: 1, waveCount: 2, gold: int.MaxValue - 2); s.StartWave(); Add(s, 2, 1, hp: 80);
            var command = Command(s); var result = s.CastAbility(command);
            Assert.That(result.TotalAppliedHpLoss, Is.EqualTo(130)); Assert.That(result.HitCount, Is.EqualTo(2));
            Assert.That(s.State.Gold, Is.EqualTo(int.MaxValue)); Assert.That(s.State.Kills, Is.EqualTo(2));
            Assert.That(s.State.Enemies, Is.Empty); Assert.That(s.DamageEvents[0].ResolvedDamage, Is.EqualTo(110));
            Assert.That(s.DamageEvents[0].AppliedHpLoss, Is.EqualTo(50)); Assert.That(s.DamageEvents[0].HealthAfter, Is.Zero);
            Assert.That(s.DamageEvents.All(e => e.WasLethal), Is.True);
            Reject(s, command, AbilityCastFailureReason.InvalidPhase); s.Step();
            Assert.That(s.State.Kills, Is.EqualTo(2)); Assert.That(s.State.Gold, Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void RegBattle193_TotalAppliedHpLossUsesLongWithoutOverflow()
        {
            var s = Create(hp: int.MaxValue, count: 1, waveCount: 2,
                definition: new AbilityDefinition(AbilityDefinitionId.RiftSeal, int.MaxValue, 1.5f, 600));
            s.StartWave(); Add(s, 2, 1, hp: int.MaxValue);
            var result = s.CastAbility(Command(s));
            Assert.That(result.TotalAppliedHpLoss, Is.EqualTo(4294967294L));
            Assert.That(s.State.Kills, Is.EqualTo(2)); Assert.That(s.State.Gold, Is.EqualTo(180));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void RegBattle194_StaleWaveTickSequenceAndDoubleCast(int component)
        {
            var s = Create(); s.StartWave(); var command = Command(s);
            Assert.That(s.CastAbility(command).Succeeded, Is.True);
            Reject(s, command, AbilityCastFailureReason.StaleCommand);
            for (var i = 0; i < 600; i++) s.Step();
            Reject(s, command, AbilityCastFailureReason.StaleCommand);
            var stale = Command(s, wave: component == 0 ? 1 : (int?)null,
                tick: component == 1 ? s.State.Tick - 1 : (long?)null, sequence: component == 2 ? 1 : (long?)null);
            Reject(s, stale, AbilityCastFailureReason.StaleCommand);
            Assert.That(s.CastAbility(Command(s)).CastSequence, Is.EqualTo(2));
        }

        [Test]
        public void RegBattle194_LastSequenceBecomesExhaustedSentinelWithoutOverflow()
        {
            var s = Create(); s.StartWave(); SetSequence(s, long.MaxValue - 1);
            Assert.That(s.CastAbility(Command(s)).CastSequence, Is.EqualTo(long.MaxValue - 1));
            Assert.That(s.AbilityState.NextCastSequence, Is.EqualTo(long.MaxValue));
            for (var i = 0; i < 600; i++) s.Step();
            Reject(s, Command(s), AbilityCastFailureReason.IdentifierExhausted);
            Reject(s, Command(s, new PathPosition(-5, -3)), AbilityCastFailureReason.NoTargets);
        }

        [Test]
        public void RegBattle195_Exactly600CombatStepsAndFreshCommandSucceeds()
        {
            var s = Create(); Assert.That(s.AbilityState.CooldownRemainingTicks, Is.Zero);
            Assert.That(s.AbilityState.NextCastSequence, Is.EqualTo(1));
            s.Step(); Assert.That(s.State.Tick, Is.Zero); s.StartWave();
            Assert.That(s.CastAbility(Command(s)).Succeeded, Is.True); Assert.That(s.State.Tick, Is.Zero);
            Assert.That(s.AbilityState.CooldownRemainingTicks, Is.EqualTo(600));
            s.Step(); Assert.That(s.AbilityState.CooldownRemainingTicks, Is.EqualTo(599));
            for (var i = 1; i < 599; i++) s.Step();
            Assert.That(s.AbilityState.CooldownRemainingTicks, Is.EqualTo(1));
            Reject(s, Command(s), AbilityCastFailureReason.CooldownActive);
            s.Step(); Assert.That(s.State.Tick, Is.EqualTo(600)); Assert.That(s.AbilityState.CooldownRemainingTicks, Is.Zero);
            Assert.That(s.CastAbility(Command(s)).Succeeded, Is.True);
        }

        [Test]
        public void RegBattle195_LastCombatStepCountsAndCooldownCarriesAcrossNextStart()
        {
            var s = Create(hp: 120, count: 2, interval: 1, waveCount: 2); s.StartWave();
            s.CastAbility(Command(s));
            Assert.That(s.State.Phase, Is.EqualTo(BattlePhase.Combat));
            s.State.AddTower(new TowerState(new TowerId(1), new TowerDefinition(new TowerDefinitionId("killer"), 0, 10, 30, 1000), new GridCoordinate(0, 1)));
            s.Step(); Assert.That(s.State.Phase, Is.EqualTo(BattlePhase.WaveComplete));
            Assert.That(s.AbilityState.CooldownRemainingTicks, Is.EqualTo(599));
            var before = Snapshot(s); for (var i = 0; i < 100; i++) s.Step(); Assert.That(Snapshot(s), Is.EqualTo(before));
            s.ContinueToNextWave(); Assert.That(s.AbilityState.CooldownRemainingTicks, Is.EqualTo(599));
            s.Step(); s.StartWave(); Assert.That(s.AbilityState.CooldownRemainingTicks, Is.EqualTo(599));
            s.Step(); Assert.That(s.AbilityState.CooldownRemainingTicks, Is.EqualTo(598));
        }

        [TestCase(false)] [TestCase(true)]
        public void RegBattle196_CastBeforeLethalMovementSavesAndAfterStepRejects(bool stepFirst)
        {
            var s = Create(hp: 100, count: 1, baseHealth: 1, speed: 30); s.StartWave();
            var enemy = s.State.Enemies[0]; enemy.Advance(4.9f);
            enemy.ApplyStatus(new StatusEffectDefinition(StatusKind.Burn, 100, 2, 1), new TowerId(1), 0);
            if (stepFirst)
            {
                // No due Burn on this branch: a completed leak cannot be undone.
                s = Create(hp: 100, count: 1, baseHealth: 1, speed: 30); s.StartWave(); s.State.Enemies[0].Advance(4.9f);
                s.Step(); Assert.That(s.State.Phase, Is.EqualTo(BattlePhase.Defeat));
                Assert.That(s.State.Gold, Is.EqualTo(150)); Assert.That(s.State.Kills, Is.Zero);
                Reject(s, Command(s, new PathPosition(4.9f, 0)), AbilityCastFailureReason.InvalidPhase);
            }
            else
            {
                var tick = s.State.Tick;
                Assert.That(s.CastAbility(Command(s, new PathPosition(4.9f, 0))).Succeeded, Is.True);
                Assert.That(s.State.Phase, Is.EqualTo(BattlePhase.Victory)); Assert.That(s.State.Tick, Is.EqualTo(tick));
                Assert.That(s.State.BaseHealth, Is.EqualTo(1)); Assert.That(s.State.Gold, Is.EqualTo(175)); Assert.That(s.State.Kills, Is.EqualTo(1));
            }
            var before = Snapshot(s); s.Step(); Assert.That(Snapshot(s), Is.EqualTo(before));
        }

        [TestCase(3, "after-wave-3")] [TestCase(6, "after-wave-6")]
        public void RegBattle196_LastCastCompletesImmediatelyAndAwardsPendingOnce(int wave, string pending)
        {
            var s = Create(hp: 100, count: 1, waveCount: 8, baseHealth: 200, native: true);
            BattleElementUnlockTests.Choose(s, "start-resonance", "flame");
            while (s.State.WaveIndex + 1 < wave)
            {
                s.StartWave(); s.State.Enemies[0].Advance(5); s.Step();
                if (s.State.ElementProgression.PendingChoiceId == "after-wave-3")
                    BattleElementUnlockTests.Choose(s, "after-wave-3", "tide");
                s.ContinueToNextWave();
            }
            s.StartWave(); var tick = s.State.Tick; var gold = s.State.Gold;
            var command = Command(s); Assert.That(s.CastAbility(command).Succeeded, Is.True);
            Assert.That(s.State.Tick, Is.EqualTo(tick)); Assert.That(s.State.Phase, Is.EqualTo(BattlePhase.WaveComplete));
            Assert.That(s.State.ElementProgression.PendingChoiceId, Is.EqualTo(pending));
            Assert.That(s.State.Gold, Is.EqualTo(gold + 25)); Assert.That(s.State.Kills, Is.EqualTo(1));
            Reject(s, command, AbilityCastFailureReason.InvalidPhase);
            Assert.That(s.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.RejectedPendingElementChoice));
        }

        [Test]
        public void RegBattle196_FutureSpawnPreventsCompletionAndDefeatHasPriority()
        {
            var s = Create(hp: 100, count: 2, interval: 2); s.StartWave(); s.CastAbility(Command(s));
            Assert.That(s.State.Phase, Is.EqualTo(BattlePhase.Combat)); Assert.That(s.State.Gold, Is.EqualTo(155));
            s.Step(); Assert.That(s.State.Enemies, Is.Empty); s.Step(); Assert.That(s.State.Enemies.Count, Is.EqualTo(1));
            var defeat = Create(count: 1, baseHealth: 1, speed: 30); defeat.StartWave();
            defeat.State.Enemies[0].Advance(5); var burn = Add(defeat, 2, 0, hp: 1);
            burn.ApplyStatus(new StatusEffectDefinition(StatusKind.Burn, 1, 1, 1), new TowerId(1), 0);
            defeat.Step(); Assert.That(defeat.State.Phase, Is.EqualTo(BattlePhase.Defeat));
            Assert.That(defeat.State.Kills, Is.EqualTo(1)); Assert.That(defeat.State.Gold, Is.EqualTo(155));
        }

        [Test]
        public void RegBattle197_AttributionCapturedLethalPositionAndNoFakeTowerAttack()
        {
            var s = Create(hp: 50, count: 1); s.StartWave(); var target = s.State.Enemies[0]; target.Advance(1.5f);
            var result = s.CastAbility(Command(s)); var cast = s.AbilityEvents.Single(); var damage = s.DamageEvents.Single();
            Assert.That(result.CastSequence, Is.EqualTo(1)); Assert.That(result.TotalAppliedHpLoss, Is.EqualTo(50));
            Assert.That(cast.Tick, Is.Zero); Assert.That(cast.SequenceInTick, Is.Zero); Assert.That(cast.CastSequence, Is.EqualTo(1));
            Assert.That(cast.AbilityId, Is.EqualTo(AbilityDefinitionId.RiftSeal)); Assert.That(cast.HitCount, Is.EqualTo(1));
            Assert.That(cast.Radius, Is.EqualTo(1.5f)); Assert.That(cast.Center, Is.EqualTo(Center));
            Assert.That(damage.SequenceInTick, Is.EqualTo(1)); Assert.That(damage.Origin, Is.EqualTo(DamageOrigin.Ability));
            Assert.That(damage.SourceAbilityId, Is.EqualTo(AbilityDefinitionId.RiftSeal)); Assert.That(damage.AbilityCastSequence, Is.EqualTo(1));
            Assert.That(damage.SourceTowerId, Is.EqualTo(default(TowerId))); Assert.That(damage.TargetEnemyId, Is.EqualTo(target.Id));
            Assert.That(damage.FromPosition, Is.EqualTo(Center)); Assert.That(damage.TargetPosition, Is.EqualTo(new PathPosition(1.5f, 0)));
            Assert.That(damage.ConfiguredBaseDamage, Is.EqualTo(120)); Assert.That(damage.RawDamage, Is.EqualTo(120));
            Assert.That(damage.ResolvedDamage, Is.EqualTo(120)); Assert.That(damage.AppliedHpLoss, Is.EqualTo(50));
            Assert.That(damage.ReactionKind, Is.Null); Assert.That(damage.ReactionSequenceInTick, Is.Null); Assert.That(damage.HitIndex, Is.Zero);
            Assert.That(s.AttackEvents, Is.Empty); Assert.That(s.ReactionEvents, Is.Empty); Assert.That(s.State.Enemies, Is.Empty);
        }

        [Test]
        public void RegBattle197_ClearDoesNotResetSequenceAndStepStartsNextTick()
        {
            var s = Create(); s.StartWave(); s.CastAbility(Command(s));
            // Keep the command boundary at one tick; cooldown zero only via the test fixture.
            typeof(AbilityState).GetField("<CooldownRemainingTicks>k__BackingField", Private).SetValue(s.AbilityState, 0);
            s.ClearAbilityEvents(); s.ClearDamageEvents(); s.ClearReactionEvents(); s.ClearAttackEvents();
            Assert.That(s.CastAbility(Command(s)).Succeeded, Is.True);
            Assert.That(s.AbilityEvents[0].SequenceInTick, Is.EqualTo(2)); Assert.That(s.DamageEvents[0].SequenceInTick, Is.EqualTo(3));
            s.State.AddTower(new TowerState(new TowerId(1), s.TowerDefinition, new GridCoordinate(0, 1)));
            s.Step(); Assert.That(s.DamageEvents.Last().Tick, Is.EqualTo(1)); Assert.That(s.DamageEvents.Last().SequenceInTick, Is.Zero);
            Assert.That(s.DamageEvents.Last().SourceAbilityId, Is.Null); Assert.That(s.DamageEvents.Last().AbilityCastSequence, Is.Null);
        }

        [Test]
        public void RegBattle197_AbilityReactionDamageShareSequenceInCurrentTick()
        {
            var s = Create(reactionTower: true); s.StartWave();
            s.State.Enemies[0].ApplyStatus(new StatusEffectDefinition(StatusKind.Burn, 1, 1000, 100), new TowerId(1), 0);
            s.State.AddTower(new TowerState(new TowerId(1), s.TowerDefinition, new GridCoordinate(0, 1)));
            s.Step();
            Assert.That(s.DamageEvents.Select(e => e.SequenceInTick), Is.EqualTo(new[] { 0, 2 }));
            Assert.That(s.ReactionEvents.Single().SequenceInTick, Is.EqualTo(1));
            Assert.That(s.DamageEvents.All(e => e.SourceAbilityId == null && e.AbilityCastSequence == null), Is.True);
            Assert.That(s.CastAbility(Command(s)).Succeeded, Is.True);
            Assert.That(s.AbilityEvents.Single().SequenceInTick, Is.EqualTo(3));
            Assert.That(s.DamageEvents.Last().SequenceInTick, Is.EqualTo(4));
            Assert.That(s.DamageEvents.Last().Tick, Is.EqualTo(1));
        }

        [Test]
        public void RegBattle191_EnabledRestoreExplicitlyUnsupportedAndAtomicUntilPersistenceTask()
        {
            var disabled = Create(enabled: false); var checkpoint = disabled.ExportCheckpoint();
            var s = Create(); s.StartWave(); s.CastAbility(Command(s)); var before = Snapshot(s);
            Assert.That(s.TryValidateCheckpoint(checkpoint, out var error), Is.False);
            Assert.That(error, Does.Contain("ability"));
            Assert.Throws<ArgumentException>(() => s.RestoreCheckpoint(checkpoint));
            Assert.That(Snapshot(s), Is.EqualTo(before));
        }

        [Test]
        public void RegBattle191_ProductionDisabledAndEnabledExportUnsupported()
        {
            var production = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWave.asset").CreateSimulation();
            Assert.That(production.AbilityState.RulesId, Is.EqualTo("ability-disabled-v1"));
            Assert.That(production.AbilityState.CooldownRemainingTicks, Is.Zero); Assert.That(production.AbilityState.NextCastSequence, Is.EqualTo(1));
            Assert.DoesNotThrow(() => production.ExportCheckpoint());
            var enabled = Create(); Assert.Throws<NotSupportedException>(() => enabled.ExportCheckpoint());
        }
    }
}
