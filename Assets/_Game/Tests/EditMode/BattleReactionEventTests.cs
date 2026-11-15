using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Veilward.Domain;
using Veilward.Presentation;
using static Veilward.Tests.EditMode.BattleReactionTests;

namespace Veilward.Tests.EditMode
{
    public sealed class BattleReactionEventTests
    {
        [Test]
        public void RegBattle143_DirectThenReactionUsesSharedSequenceCorrelationAndCapturedPositions()
        {
            var s = Create(new[] { Tower("a", StatusKind.Wet) }, hp: 15);
            Place(s, "a", 0); s.StartWave(); var p = s.State.Enemies[0]; p.Advance(5); Status(p, StatusKind.Burn);
            s.Step(); var d = Damages(s); var r = Reactions(s)[0];
            Assert.That(d.Count, Is.EqualTo(2)); Assert.That(d[0].SequenceInTick, Is.Zero);
            Assert.That(d[0].Origin, Is.EqualTo(DamageOrigin.Direct)); Assert.That(d[0].RawDamage, Is.EqualTo(10));
            Assert.That(d[0].ResolvedDamage, Is.EqualTo(10)); Assert.That(d[0].AppliedHpLoss, Is.EqualTo(10));
            Assert.That(d[0].HealthAfter, Is.EqualTo(5)); Assert.That(d[0].WasLethal, Is.False);
            Assert.That(d[0].FromPosition.X, Is.EqualTo(.5f)); Assert.That(d[0].FromPosition.Y, Is.EqualTo(2.5f));
            Assert.That(d[0].TargetPosition.X, Is.EqualTo(5)); Assert.That(d[0].ReactionSequenceInTick, Is.Null);
            Assert.That(r.SequenceInTick, Is.EqualTo(1)); Assert.That(r.PrimaryPosition.X, Is.EqualTo(5));
            Assert.That(r.CauseFirstKind, Is.EqualTo(StatusKind.Burn)); Assert.That(r.CauseSecondKind, Is.EqualTo(StatusKind.Wet));
            Assert.That(r.RadiusOrHopRange, Is.EqualTo(1.25f)); Assert.That(r.TargetLimit, Is.EqualTo(4));
            Assert.That(d[1].SequenceInTick, Is.EqualTo(2)); Assert.That(d[1].ReactionSequenceInTick, Is.EqualTo(1));
            Assert.That(d[1].ReactionKind, Is.EqualTo(ReactionKind.Steam)); Assert.That(d[1].HitIndex, Is.Zero);
            Assert.That(d[1].AppliedHpLoss, Is.EqualTo(5)); Assert.That(d[1].HealthAfter, Is.Zero); Assert.That(d[1].WasLethal, Is.True);
            Assert.That(d[1].FromPosition.X, Is.EqualTo(5)); Assert.That(d[1].TargetPosition.X, Is.EqualTo(5));
            Assert.That(s.State.Enemies, Is.Empty); Assert.That(s.AttackEvents[0].WasLethal, Is.False);
            Assert.That(s.AttackEvents[0].Damage, Is.EqualTo(10));
        }

        [Test]
        public void RegBattle143_BurnPrecedesDirectWithAbsentSourceAndSequenceRestartsEachTick()
        {
            var s = Create(new[] { Tower("a", null, fireRate: 30) }); Place(s, "a", 0); s.StartWave();
            var p = s.State.Enemies[0]; Status(p, StatusKind.Burn, power: 3, interval: 1);
            s.Step(); var first = Damages(s);
            Assert.That(first.Count, Is.EqualTo(2)); Assert.That(first[0].Origin, Is.EqualTo(DamageOrigin.Burn));
            Assert.That(first[0].SourceTowerId.Value, Is.EqualTo(99)); Assert.That(first[0].ConfiguredBaseDamage, Is.EqualTo(3));
            Assert.That(first[0].RawDamage, Is.EqualTo(3)); Assert.That(first[0].FromPosition.X, Is.Zero);
            Assert.That(first[0].TargetPosition.X, Is.Zero); Assert.That(first[1].SequenceInTick, Is.EqualTo(1));
            s.Step(); Assert.That(Damages(s).Count, Is.EqualTo(4), "No implicit clear between Steps");
            Assert.That(Damages(s)[2].Tick, Is.EqualTo(2)); Assert.That(Damages(s)[2].SequenceInTick, Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void RegBattle143_SceneControllerClearsAllStreamsAfterEachStepIncludingConsumerException(bool throws)
        {
            var s = Create(new[] { Tower("a", StatusKind.Burn, fireRate: 30) }); Place(s, "a", 0); s.StartWave();
            Status(s.State.Enemies[0], StatusKind.Wet);
            var go = new GameObject("Reaction drain test");
            try
            {
                var controller = go.AddComponent<BattleSceneController>();
                typeof(BattleSceneController).GetField("_simulation", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, s);
                var process = typeof(BattleSceneController).GetMethod("ProcessAttackEvents", BindingFlags.Instance | BindingFlags.NonPublic);
                // Отсутствующий Transform в существующем tower view map воспроизводит исключение реального consumer.
                if (throws)
                {
                    var views = (System.Collections.Generic.Dictionary<TowerId, Transform>)typeof(BattleSceneController)
                        .GetField("_towerViews", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
                    views[new TowerId(1)] = null;
                }
                for (var step = 0; step < 3; step++)
                {
                    s.Step(); Assert.That(Damages(s).Count, Is.GreaterThan(0));
                    if (throws) Assert.Throws<TargetInvocationException>(() => process.Invoke(controller, null));
                    else process.Invoke(controller, null);
                    Assert.That(s.AttackEvents, Is.Empty); Assert.That(Reactions(s), Is.Empty); Assert.That(Damages(s), Is.Empty);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void RegBattle145_ExplicitClearsKeepStateAndRejectedRestorePreservesTransientState()
        {
            var s = Create(new[] { Tower("a", StatusKind.Burn) }); Place(s, "a", 0); var cp = s.ExportCheckpoint();
            s.StartWave(); var p = s.State.Enemies[0]; Status(p, StatusKind.Wet); s.Step();
            var state = s.State; var damageView = Damages(s); var reactionView = Reactions(s);
            var invalid = cp.DeepCopyForMigration(); invalid.gold = -1;
            Assert.Throws<ArgumentException>(() => s.RestoreCheckpoint(invalid));
            Assert.That(s.State, Is.SameAs(state)); Assert.That(damageView.Count, Is.EqualTo(2)); Assert.That(reactionView.Count, Is.EqualTo(1));
            Assert.That(Last(p, ReactionKind.Steam), Is.EqualTo(1));
            Clear(s, "ClearReactionEvents"); Assert.That(damageView.Count, Is.EqualTo(2)); Assert.That(reactionView, Is.Empty);
            Clear(s, "ClearDamageEvents"); Assert.That(damageView, Is.Empty); Assert.That(s.State, Is.SameAs(state));
            Assert.That(s.AttackEvents.Count, Is.EqualTo(1));
            s.RestoreCheckpoint(cp); Assert.That(s.State.Enemies, Is.Empty); Assert.That(s.AttackEvents, Is.Empty);
            s.StartWave(); var next = s.State.Enemies[0]; Assert.That(Last(next, ReactionKind.Steam), Is.Null);
            Status(next, StatusKind.Wet); s.Step(); Assert.That(Damages(s)[0].SequenceInTick, Is.Zero);
            Assert.That(Reactions(s).Count, Is.EqualTo(1));
        }

        [Test]
        public void RegBattle145_SuccessfulRestoreClearsAllUnconsumedStreams()
        {
            var s = Create(new[] { Tower("a", StatusKind.Burn) }); Place(s, "a", 0); var cp = s.ExportCheckpoint();
            s.StartWave(); Status(s.State.Enemies[0], StatusKind.Wet); s.Step();
            Assert.That(Reactions(s).Count, Is.EqualTo(1)); Assert.That(Damages(s).Count, Is.EqualTo(2));
            s.RestoreCheckpoint(cp); Assert.That(Reactions(s), Is.Empty); Assert.That(Damages(s), Is.Empty); Assert.That(s.AttackEvents, Is.Empty);
        }

        [TestCase(0, 17)] [TestCase(int.MaxValue - 1, int.MaxValue)]
        public void RegBattle145_ReactionKillsRewardAndWaveBonusExactlyOnce(int gold, int expected)
        {
            var s = Create(new[] { Tower("a", StatusKind.Burn), Tower("b", StatusKind.Wet) }, hp: 25, gold: gold, reward: 7, bonus: 10);
            Place(s, "a", 0); Place(s, "b", 1); s.StartWave(); s.Step();
            Assert.That(s.State.Phase, Is.EqualTo(BattlePhase.Victory)); Assert.That(s.State.Kills, Is.EqualTo(1));
            Assert.That(s.State.Gold, Is.EqualTo(expected)); Assert.That(Damages(s)[2].WasLethal, Is.True);
            for (var i = 0; i < 5; i++) s.Step();
            Clear(s, "ClearReactionEvents"); Clear(s, "ClearDamageEvents"); s.ClearAttackEvents();
            Assert.That(s.StartWave(), Is.EqualTo(WaveTransitionResult.RejectedInvalidPhase));
            Assert.That(s.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.RejectedInvalidPhase));
            Assert.That(s.State.Gold, Is.EqualTo(expected)); Assert.That(s.State.Kills, Is.EqualTo(1));
        }

        private static void Clear(BattleSimulation s, string name)
        { var m = typeof(BattleSimulation).GetMethod(name); Assert.That(m, Is.Not.Null, "Missing clear API"); m.Invoke(s, null); }
    }
}
