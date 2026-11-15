using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Veilward.Domain;

namespace Veilward.Tests.EditMode
{
    public sealed class BattleReactionDefinitionTests
    {
        [Test]
        public void RegBattle128_Catalog_CopiesDefinitionsAndSupportsLookupAndEmpty()
        {
            var steam = Definition(ReactionKind.Steam, 0);
            var storm = Definition(ReactionKind.Storm, 1);
            var source = new List<ReactionDefinition> { steam, storm };
            var catalog = new ReactionCatalog(source);
            source[0] = storm;
            source.Clear();

            Assert.That(catalog.Definitions, Is.EqualTo(new[] { steam, storm }));
            Assert.That(catalog.Get(ReactionKind.Steam), Is.SameAs(steam));
            Assert.That(catalog.TryGet(ReactionKind.Storm, out var found), Is.True);
            Assert.That(found, Is.SameAs(storm));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<ReactionDefinition>)catalog.Definitions)[0] = storm);
            Assert.Throws<NotSupportedException>(() =>
                ((IList<ReactionDefinition>)catalog.Definitions).Clear());
            Assert.That(ReactionCatalog.Empty.Definitions, Is.Empty);
            Assert.That(ReactionCatalog.Empty.TryGet(ReactionKind.Steam, out found), Is.False);
            Assert.That(found, Is.Null);
            Assert.Throws<KeyNotFoundException>(() => ReactionCatalog.Empty.Get(ReactionKind.Storm));
            Assert.That(catalog.TryGet((ReactionKind)999, out found), Is.False);
            Assert.That(found, Is.Null);
            Assert.Throws<KeyNotFoundException>(() => catalog.Get((ReactionKind)999));
        }

        [Test]
        public void RegBattle128_GlobalExamples_PreserveEveryConfiguredValue()
        {
            var steam = Definition(ReactionKind.Steam, 0);
            var storm = Definition(ReactionKind.Storm, 1);
            AssertDefinition(steam, ReactionKind.Steam, 60, 100, 1.25f, 4, 0);
            AssertDefinition(storm, ReactionKind.Storm, 60, 75, 2f, 2, 1);
            Assert.That(new ReactionCatalog(new[] { steam, storm }).Definitions.Count, Is.EqualTo(2));
        }

        [Test]
        public void RegBattle128_CatalogAndDefinition_RejectNullAndDuplicateData()
        {
            var steam = Definition(ReactionKind.Steam, 0);
            Assert.Throws<ArgumentNullException>(() => new ReactionCatalog(null));
            Assert.Throws<ArgumentNullException>(() => new ReactionDefinition(null, 60, 0));
            Assert.Throws<ArgumentException>(() => new ReactionCatalog(
                new ReactionDefinition[] { steam, null }));
            Assert.Throws<ArgumentException>(() => new ReactionCatalog(new[]
            {
                steam, Definition(ReactionKind.Steam, 1)
            }));
            Assert.Throws<ArgumentException>(() => new ReactionCatalog(new[]
            {
                steam, Definition(ReactionKind.Storm, 0)
            }));
        }

        [TestCase(-1)]
        [TestCase(2)]
        [TestCase(int.MaxValue)]
        public void RegBattle129_Payload_RejectsUnknownKind(int kind)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReactionPayload(
                (ReactionKind)kind, 100, 1f, 1));
        }

        [TestCase(int.MinValue)]
        [TestCase(-1)]
        [TestCase(0)]
        public void RegBattle129_Definition_RejectsNonpositiveCooldown(int cooldown)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReactionDefinition(
                Payload(ReactionKind.Steam), cooldown, 0));
        }

        [TestCase(ReactionKind.Steam, int.MinValue)]
        [TestCase(ReactionKind.Steam, -1)]
        [TestCase(ReactionKind.Steam, 0)]
        [TestCase(ReactionKind.Storm, int.MinValue)]
        [TestCase(ReactionKind.Storm, -1)]
        [TestCase(ReactionKind.Storm, 0)]
        public void RegBattle129_Payload_RejectsNonpositivePercent(ReactionKind kind, int percent)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReactionPayload(kind, percent, 1f, 1));
        }

        [TestCase(ReactionKind.Steam)]
        [TestCase(ReactionKind.Storm)]
        public void RegBattle129_Payload_RejectsNonpositiveAndNonfiniteRange(ReactionKind kind)
        {
            foreach (var range in new[] { float.MinValue, -1f, -0f, 0f,
                         float.NaN, float.NegativeInfinity, float.PositiveInfinity })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    new ReactionPayload(kind, 100, range, 1), range.ToString());
            }
        }

        [TestCase(ReactionKind.Steam, int.MinValue)]
        [TestCase(ReactionKind.Steam, -1)]
        [TestCase(ReactionKind.Steam, 0)]
        [TestCase(ReactionKind.Steam, 5)]
        [TestCase(ReactionKind.Steam, int.MaxValue)]
        [TestCase(ReactionKind.Storm, int.MinValue)]
        [TestCase(ReactionKind.Storm, -1)]
        [TestCase(ReactionKind.Storm, 0)]
        [TestCase(ReactionKind.Storm, 3)]
        [TestCase(ReactionKind.Storm, int.MaxValue)]
        public void RegBattle129_Payload_RejectsInvalidTargetLimit(ReactionKind kind, int limit)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReactionPayload(kind, 100, 1f, limit));
        }

        [TestCase(ReactionKind.Steam, 1)]
        [TestCase(ReactionKind.Steam, 2)]
        [TestCase(ReactionKind.Steam, 3)]
        [TestCase(ReactionKind.Steam, 4)]
        [TestCase(ReactionKind.Storm, 1)]
        [TestCase(ReactionKind.Storm, 2)]
        public void RegBattle129_ValidBoundaries_DoNotHardcodeProductionValues(ReactionKind kind, int limit)
        {
            var lower = new ReactionDefinition(new ReactionPayload(kind, 1, float.Epsilon, limit), 1, 7);
            AssertDefinition(lower, kind, 1, 1, float.Epsilon, limit, 7);
            var upper = new ReactionDefinition(
                new ReactionPayload(kind, int.MaxValue, float.MaxValue, limit), int.MaxValue, 42);
            AssertDefinition(upper, kind, int.MaxValue, int.MaxValue, float.MaxValue, limit, 42);
        }

        [Test]
        public void RegBattle130_LegacyConstructors_PreserveStatusPenetrationAndInterval()
        {
            var id = new TowerDefinitionId("test-tower");
            var legacy = new TowerDefinition(id, 60, 3f, 2f, 25);
            Assert.That(legacy.Id, Is.EqualTo(id));
            Assert.That(legacy.Price, Is.EqualTo(60));
            Assert.That(legacy.Range, Is.EqualTo(3f));
            Assert.That(legacy.FireRate, Is.EqualTo(2f));
            Assert.That(legacy.Damage, Is.EqualTo(25));
            Assert.That(legacy.StatusEffect, Is.Null);
            Assert.That(legacy.IntrinsicReaction, Is.Null);
            Assert.That(legacy.ArmorPenetration, Is.Zero);
            Assert.That(legacy.AttackIntervalTicks, Is.EqualTo(15));
            var status = new StatusEffectDefinition(StatusKind.Burn, 3, 90, 15);
            var statusOnly = new TowerDefinition(id, 10, 3f, 1f, 12, status);
            Assert.That(statusOnly.StatusEffect, Is.SameAs(status));
            Assert.That(statusOnly.ArmorPenetration, Is.Zero);
            Assert.That(statusOnly.IntrinsicReaction, Is.Null);
            Assert.That(statusOnly.AttackIntervalTicks, Is.EqualTo(30));
            var full = new TowerDefinition(id, 110, 4f, 0.8f, 18, status, 7);
            Assert.That(full.StatusEffect, Is.SameAs(status));
            Assert.That(full.ArmorPenetration, Is.EqualTo(7));
            Assert.That(full.IntrinsicReaction, Is.Null);
            Assert.That(full.AttackIntervalTicks, Is.EqualTo(38));
        }

        [TestCase("steam", ReactionKind.Steam)]
        [TestCase("storm", ReactionKind.Storm)]
        public void RegBattle130_HybridArchetype_RequiresMatchingPayloadOnEveryLevel(
            string id, ReactionKind kind)
        {
            var levels = new List<TowerDefinition>();
            for (var index = 0; index < 3; index++)
            {
                levels.Add(Tower(id, new ReactionPayload(kind, 75 + index * 25, 1f + index, 2)));
            }
            var archetype = new TowerArchetypeDefinition(new TowerDefinitionId(id), true, levels);
            levels.Clear();
            Assert.That(archetype.Levels.Count, Is.EqualTo(3));
            for (var index = 0; index < 3; index++)
            {
                Assert.That(archetype.Levels[index].IntrinsicReaction.Kind, Is.EqualTo(kind));
                Assert.That(archetype.Levels[index].IntrinsicReaction.DamagePercent, Is.EqualTo(75 + index * 25));
                Assert.That(archetype.Levels[index].StatusEffect, Is.Null);
                Assert.Throws<NotSupportedException>(() =>
                    ((IList<TowerDefinition>)archetype.Levels)[index] = Tower(id, Payload(kind)));
            }
            for (var index = 0; index < 3; index++)
            {
                foreach (var invalid in new[] { Tower(id, null),
                             Tower(id, Payload(kind == ReactionKind.Steam ? ReactionKind.Storm : ReactionKind.Steam)),
                             TowerWithStatus(id, StatusKind.Wet) })
                {
                    var invalidLevels = new[] { Tower(id, Payload(kind)), Tower(id, Payload(kind)), Tower(id, Payload(kind)) };
                    invalidLevels[index] = invalid;
                    Assert.Throws<ArgumentException>(() => new TowerArchetypeDefinition(
                        new TowerDefinitionId(id), true, invalidLevels), $"{id} L{index + 1}");
                }
            }
        }

        [TestCase(ReactionKind.Steam)]
        [TestCase(ReactionKind.Storm)]
        public void RegBattle130_Tower_RejectsStatusAndIntrinsicTogether(ReactionKind kind)
        {
            foreach (StatusKind statusKind in Enum.GetValues(typeof(StatusKind)))
            {
                var status = new StatusEffectDefinition(statusKind, 1, 60, statusKind == StatusKind.Burn ? 15 : 0);
                Assert.Throws<ArgumentException>(() => new TowerDefinition(
                    new TowerDefinitionId("custom"), 1, 1f, 1f, 1, status, 0, Payload(kind)));
            }
        }

        [TestCase("flame", StatusKind.Burn)]
        [TestCase("tide", StatusKind.Wet)]
        [TestCase("gale", StatusKind.Gust)]
        [TestCase("stone", StatusKind.Crack)]
        public void RegBattle130_BasicArchetypes_StillRequireSingleStatus(string id, StatusKind kind)
        {
            var valid = TowerWithStatus(id, kind);
            Assert.DoesNotThrow(() => new TowerArchetypeDefinition(new TowerDefinitionId(id), true,
                new[] { valid, valid, valid }));
            foreach (var invalid in new[] { Tower(id, null), Tower(id, Payload(ReactionKind.Steam)) })
            {
                Assert.Throws<ArgumentException>(() => new TowerArchetypeDefinition(new TowerDefinitionId(id), true,
                    new[] { valid, invalid, valid }));
            }
        }

        [TestCase(ReactionKind.Steam, ReactionTriggerKind.StatusPair, StatusKind.Burn, StatusKind.Wet)]
        [TestCase(ReactionKind.Storm, ReactionTriggerKind.Intrinsic, StatusKind.Wet, StatusKind.Gust)]
        public void RegBattle131_ReactionEvent_PreservesCauseSourceGeometryAndSequence(
            ReactionKind kind, ReactionTriggerKind trigger, StatusKind first, StatusKind second)
        {
            var position = new PathPosition(1.25f, -2f);
            var value = new ReactionEvent(long.MaxValue, 7, kind, trigger, new TowerId(9), new EnemyId(11),
                position, first, second, 2.5f, 2);
            position = new PathPosition(99f, 99f);
            Assert.That(value.Tick, Is.EqualTo(long.MaxValue));
            Assert.That(value.SequenceInTick, Is.EqualTo(7));
            Assert.That(value.Kind, Is.EqualTo(kind));
            Assert.That(value.TriggerKind, Is.EqualTo(trigger));
            Assert.That(value.SourceTowerId, Is.EqualTo(new TowerId(9)));
            Assert.That(value.PrimaryEnemyId, Is.EqualTo(new EnemyId(11)));
            AssertPosition(value.PrimaryPosition, 1.25f, -2f);
            Assert.That(value.CauseFirstKind, Is.EqualTo(first));
            Assert.That(value.CauseSecondKind, Is.EqualTo(second));
            Assert.That(value.RadiusOrHopRange, Is.EqualTo(2.5f));
            Assert.That(value.TargetLimit, Is.EqualTo(2));
        }

        [TestCase(DamageOrigin.Direct, null, null, 0, true)]
        [TestCase(DamageOrigin.Direct, null, null, 0, false)]
        [TestCase(DamageOrigin.Burn, null, null, 0, true)]
        [TestCase(DamageOrigin.Burn, null, null, 0, false)]
        [TestCase(DamageOrigin.Reaction, ReactionKind.Steam, 7, 0, true)]
        [TestCase(DamageOrigin.Reaction, ReactionKind.Steam, 7, 0, false)]
        [TestCase(DamageOrigin.Reaction, ReactionKind.Storm, 7, 1, true)]
        [TestCase(DamageOrigin.Reaction, ReactionKind.Storm, 7, 1, false)]
        [TestCase(DamageOrigin.Reaction, ReactionKind.Storm, 7, 2, true)]
        [TestCase(DamageOrigin.Reaction, ReactionKind.Storm, 7, 2, false)]
        public void RegBattle131_DamageEvent_PreservesDistinctDamageStagesAndCapturedPositions(
            DamageOrigin origin, ReactionKind? reaction, int? reactionSequence, int hitIndex, bool lethal)
        {
            var from = new PathPosition(-3f, 4f);
            var target = new PathPosition(5f, -6f);
            var raw = origin == DamageOrigin.Reaction ? 31 : 25;
            var resolved = origin == DamageOrigin.Burn ? 22 : 28;
            var applied = lethal ? 7 : resolved;
            var healthAfter = lethal ? 0 : 100 - applied;
            var value = new DamageResolvedEvent(60, 8, origin, new TowerId(9), new EnemyId(11), from, target,
                25, raw, resolved, applied, healthAfter, lethal, reaction, reactionSequence, hitIndex);
            from = target = new PathPosition(99f, 99f);
            Assert.That(value.Tick, Is.EqualTo(60));
            Assert.That(value.SequenceInTick, Is.EqualTo(8));
            Assert.That(value.Origin, Is.EqualTo(origin));
            Assert.That(value.SourceTowerId, Is.EqualTo(new TowerId(9)));
            Assert.That(value.TargetEnemyId, Is.EqualTo(new EnemyId(11)));
            AssertPosition(value.FromPosition, -3f, 4f);
            AssertPosition(value.TargetPosition, 5f, -6f);
            Assert.That(value.ConfiguredBaseDamage, Is.EqualTo(25));
            Assert.That(value.RawDamage, Is.EqualTo(raw));
            Assert.That(value.ResolvedDamage, Is.EqualTo(resolved));
            Assert.That(value.AppliedHpLoss, Is.EqualTo(applied));
            Assert.That(value.HealthAfter, Is.EqualTo(healthAfter));
            Assert.That(value.WasLethal, Is.EqualTo(lethal));
            Assert.That(value.ReactionKind, Is.EqualTo(reaction));
            Assert.That(value.ReactionSequenceInTick, Is.EqualTo(reactionSequence));
            Assert.That(value.HitIndex, Is.EqualTo(hitIndex));
        }

        [TestCase(typeof(ReactionPayload))]
        [TestCase(typeof(ReactionDefinition))]
        [TestCase(typeof(ReactionCatalog))]
        [TestCase(typeof(ReactionEvent))]
        [TestCase(typeof(DamageResolvedEvent))]
        public void RegBattle131_DefinitionsAndEvents_ExposeOnlyImmutableData(Type type)
        {
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                Assert.That(property.GetSetMethod(true), Is.Null, property.Name);
            }
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                Assert.That(field.IsInitOnly, Is.True, field.Name);
            }
            Assert.That(type.IsValueType || type.IsSealed, Is.True);
        }

        [TestCase(100, false, 82)]
        [TestCase(3, true, 0)]
        public void RegBattle131_LegacyCombat_KeepsConfiguredAttackDamageAndDirectLethal(
            int health, bool lethal, int healthAfter)
        {
            var tower = new TowerDefinition(new TowerDefinitionId("legacy"), 0, 3f, 2f, 25, null, 3);
            var simulation = new BattleSimulation(
                new GridMapDefinition(20, 3, new GridCoordinate(0, 0), 1f),
                new PathDefinition(new[] { new GridCoordinate(0, 0), new GridCoordinate(19, 0) }),
                new WaveDefinition(new EnemyDefinition(new EnemyDefinitionId("target"), health, 1f, 1, 5, 10), 1, 0, 30),
                tower, 20, 0);
            Assert.That(simulation.PlaceTower(new PlaceTowerCommand(tower.Id, new GridCoordinate(0, 1))).Success, Is.True);
            simulation.StartWave();
            simulation.Step();
            Assert.That(simulation.AttackEvents.Count, Is.EqualTo(1));
            var attack = simulation.AttackEvents[0];
            Assert.That(attack.Damage, Is.EqualTo(25), "Configured base survives armor and overkill.");
            Assert.That(attack.WasLethal, Is.EqualTo(lethal));
            Assert.That(attack.Tick, Is.EqualTo(1));
            Assert.That(attack.AttackerTowerId, Is.EqualTo(new TowerId(1)));
            Assert.That(attack.TargetEnemyId, Is.EqualTo(new EnemyId(1)));
            AssertPosition(attack.TargetPosition, 1f / 30f, 0f);
            Assert.That(simulation.State.Kills, Is.EqualTo(lethal ? 1 : 0));
            Assert.That(simulation.State.Enemies.Count, Is.EqualTo(lethal ? 0 : 1));
            if (!lethal)
            {
                Assert.That(simulation.State.Enemies[0].Health, Is.EqualTo(healthAfter));
            }
        }

        private static ReactionPayload Payload(ReactionKind kind)
        {
            return new ReactionPayload(kind, kind == ReactionKind.Steam ? 100 : 75,
                kind == ReactionKind.Steam ? 1.25f : 2f, kind == ReactionKind.Steam ? 4 : 2);
        }

        private static ReactionDefinition Definition(ReactionKind kind, int priority)
        {
            return new ReactionDefinition(Payload(kind), 60, priority);
        }

        private static TowerDefinition Tower(string id, ReactionPayload payload)
        {
            return new TowerDefinition(new TowerDefinitionId(id), 110, 3f, 0.8f, 18,
                intrinsicReaction: payload);
        }

        private static TowerDefinition TowerWithStatus(string id, StatusKind kind)
        {
            return new TowerDefinition(new TowerDefinitionId(id), 10, 3f, 1f, 10,
                new StatusEffectDefinition(kind, 1, 60, kind == StatusKind.Burn ? 15 : 0));
        }

        private static void AssertDefinition(ReactionDefinition definition, ReactionKind kind,
            int cooldown, int percent, float range, int limit, int priority)
        {
            Assert.That(definition.Payload.Kind, Is.EqualTo(kind));
            Assert.That(definition.Payload.DamagePercent, Is.EqualTo(percent));
            Assert.That(definition.Payload.Range, Is.EqualTo(range));
            Assert.That(definition.Payload.TargetLimit, Is.EqualTo(limit));
            Assert.That(definition.CooldownTicks, Is.EqualTo(cooldown));
            Assert.That(definition.Priority, Is.EqualTo(priority));
        }

        private static void AssertPosition(PathPosition position, float x, float y)
        {
            Assert.That(position.X, Is.EqualTo(x));
            Assert.That(position.Y, Is.EqualTo(y));
        }
    }
}
