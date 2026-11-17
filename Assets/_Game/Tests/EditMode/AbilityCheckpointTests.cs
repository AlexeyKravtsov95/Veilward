using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Veilward.Domain;
using Veilward.Infrastructure;

namespace Veilward.Tests.EditMode
{
    public sealed class AbilityCheckpointTests
    {
        internal const string Content = "synthetic-p24-ability-v1";
        internal static BattleSimulation Create(bool enabled = true, int health = 20, bool native = true, int enemyHp = 100)
        {
            var production = ElementProgressionStorageTests.Config().CreateSimulation();
            var enemy = new EnemyDefinition(new EnemyDefinitionId("fixture"), enemyHp, 0.01f, 1, 5);
            var waves = Enumerable.Range(0, 8).Select(_ => new WaveDefinition(enemy, 2, 0, 1)).ToArray();
            return new BattleSimulation(new GridMapDefinition(5, 3, new GridCoordinate(0, 0), 1),
                new PathDefinition(new[] { new GridCoordinate(0, 0), new GridCoordinate(4, 0) }),
                waves, production.TowerCatalog, health, 10000, 20, legacySingleWave: false, reactionCatalog: production.ReactionCatalog,
                elementUnlockRules: native ? BattleElementUnlockTests.Rules() : null, abilityDefinition: enabled ? AbilityDefinition.RiftSeal : null);
        }
        internal static WaveCheckpointEnvelope Envelope(BattleSimulation s, string id = Content) => new WaveCheckpointEnvelope
        { schemaVersion = WaveCheckpointStore.CurrentSchemaVersion, scenarioContentId = id, state = s.ExportCheckpoint() };
        internal static string Snapshot(BattleSimulation s) => WaveCheckpointJson.Serialize(Envelope(s));
        internal static CastAbilityCommand Command(BattleSimulation s) => new CastAbilityCommand(
            AbilityDefinitionId.RiftSeal, new PathPosition(0, 0), s.State.WaveIndex, s.State.Tick, s.AbilityState.NextCastSequence);
        internal static void Choose(BattleSimulation s) => BattleElementUnlockTests.Choose(s,
            s.State.ElementProgression.PendingChoiceId, s.State.WaveIndex == 0 ? "flame" : s.State.WaveIndex == 2 ? "tide" : "gale");

        [TestCase(true)] [TestCase(false)]
        public void RegBattle198_AllSafeBoundariesDeepRoundTrip(bool enabled)
        {
            var s = Create(enabled); RoundTrip(s, enabled); Choose(s); RoundTrip(s, enabled);
            s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId("flame"), new GridCoordinate(0, 1)));
            for (var wave = 1; wave <= 8; wave++)
            {
                s.StartWave();
                for (var n = 0; n < 20000 && s.State.Phase == BattlePhase.Combat; n++) s.Step();
                Assert.That(s.State.Phase, Is.EqualTo(wave == 8 ? BattlePhase.Victory : BattlePhase.WaveComplete));
                RoundTrip(s, enabled);
                if (s.State.ElementProgression.HasPendingChoice) { Choose(s); RoundTrip(s, enabled); }
                if (wave < 8) { s.ContinueToNextWave(); RoundTrip(s, enabled); }
            }
        }
        private static void RoundTrip(BattleSimulation s, bool enabled)
        {
            var expected = Snapshot(s); var cp = s.ExportCheckpoint();
            var copy = cp.DeepCopy(); Assert.That(copy.ability, Is.Not.SameAs(cp.ability));
            var restored = Create(enabled); restored.RestoreCheckpoint(copy);
            Assert.That(Snapshot(restored), Is.EqualTo(expected));
            copy.towers = null; copy.elementProgression.unlockedElementIds = null;
            Assert.That(Snapshot(restored), Is.EqualTo(expected));
        }

        [TestCase(-1, 2)] [TestCase(601, 2)] [TestCase(1, 1)] [TestCase(0, 0)]
        public void RegBattle199_InvalidAbilityRejectsDirtyCombatAtomically(int remaining, long sequence)
        {
            var s = BattleAbilityTests.CheckpointDirtyFixture();
            var cp = Create(native: false).ExportCheckpoint();
            Assert.That(s.TryValidateCheckpoint(cp, out _), Is.True, "control snapshot must be valid before ability corruption");
            var before = BattleAbilityTests.FullSnapshot(s);
            Assert.That(s.AttackEvents, Is.Not.Empty); Assert.That(s.ReactionEvents, Is.Not.Empty); Assert.That(s.DamageEvents, Is.Not.Empty); Assert.That(s.AbilityEvents, Is.Not.Empty);
            cp.ability = new AbilityCheckpoint("rift-seal-v1", "rift-seal", remaining, sequence);
            Assert.That(s.TryValidateCheckpoint(cp, out _), Is.False);
            Assert.Throws<ArgumentException>(() => s.RestoreCheckpoint(cp));
            Assert.That(BattleAbilityTests.FullSnapshot(s), Is.EqualTo(before));
        }
        [TestCase(0, 1)] [TestCase(600, 2)] [TestCase(421, 3)] [TestCase(0, long.MaxValue)]
        public void RegBattle198_ActualCooldownSequenceAndSuccessfulDirtyRestore(int remaining, long sequence)
        {
            var s = Create(); Choose(s); var cp = s.ExportCheckpoint();
            cp.ability = new AbilityCheckpoint("rift-seal-v1", "rift-seal", remaining, sequence);
            s.StartWave(); s.Step(); s.CastAbility(Command(s));
            s.RestoreCheckpoint(cp);
            Assert.That(s.AbilityState.CooldownRemainingTicks, Is.EqualTo(remaining));
            Assert.That(s.AbilityState.NextCastSequence, Is.EqualTo(sequence));
            Assert.That(s.State.Enemies, Is.Empty);
            Assert.That(s.AttackEvents, Is.Empty); Assert.That(s.ReactionEvents, Is.Empty);
            Assert.That(s.DamageEvents, Is.Empty); Assert.That(s.AbilityEvents, Is.Empty);
            Assert.That(Snapshot(s), Is.EqualTo(WaveCheckpointJson.Serialize(new WaveCheckpointEnvelope
            { schemaVersion = 4, scenarioContentId = Content, state = cp })));
        }
        [Test]
        public void RegBattle199_InitialPendingCannotHaveCastHistory()
        {
            var s = Create(); var cp = s.ExportCheckpoint(); cp.ability = new AbilityCheckpoint("rift-seal-v1", "rift-seal", 0, 2);
            Assert.That(s.TryValidateCheckpoint(cp, out _), Is.False);
        }
        [Test]
        public void RegBattle198_DefeatRoundTrip()
        {
            var s = Create(health: 1); Choose(s); s.StartWave();
            for (var n = 0; n < 20000 && s.State.Phase == BattlePhase.Combat; n++) s.Step();
            Assert.That(s.State.Phase, Is.EqualTo(BattlePhase.Defeat));
            var restored = Create(health: 1); restored.RestoreCheckpoint(s.ExportCheckpoint());
            Assert.That(Snapshot(restored), Is.EqualTo(Snapshot(s)));
        }
    }
}
