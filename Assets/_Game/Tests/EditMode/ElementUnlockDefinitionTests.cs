using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using Veilward.Domain;
using Veilward.Presentation;

namespace Veilward.Tests.EditMode
{
    public sealed class ElementUnlockDefinitionTests
    {
        [Test]
        public void RegBattle164_Rules_RejectUnknownDuplicateAndUnorderedData()
        {
            var starts = new[] { ElementKind.Flame, ElementKind.Gale };
            Assert.Throws<ArgumentNullException>(() => new ElementUnlockRules(null, new[] { 3, 6 }));
            Assert.Throws<ArgumentNullException>(() => new ElementUnlockRules(starts, null));
            foreach (var invalid in new[] { Array.Empty<ElementKind>(), new[] { ElementKind.Flame, ElementKind.Flame }, new[] { (ElementKind)99 } })
                Assert.Throws<ArgumentException>(() => new ElementUnlockRules(invalid, new[] { 3, 6 }));
            foreach (var waves in new[] { new[] { 0 }, new[] { -1 }, new[] { 3, 3 }, new[] { 6, 3 } })
                Assert.Throws<ArgumentException>(() => new ElementUnlockRules(starts, waves));
            var config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWave.asset");
            foreach (var waves in new[] { new[] { 8 }, new[] { 9 }, new[] { 3, 5 }, Array.Empty<int>() })
                Assert.Throws<ArgumentException>(() => config.CreateSimulation(new ElementUnlockRules(starts, waves)));
            Assert.Throws<ArgumentException>(() => config.CreateSimulation(new ElementUnlockRules(new[] { ElementKind.Tide }, new[] { 3, 6 })));
            Assert.Throws<ArgumentNullException>(() => config.CreateSimulation((ElementUnlockRules)null));
            var single = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/TestWave.asset");
            Assert.Throws<ArgumentException>(() => single.CreateSimulation(BattleElementUnlockTests.Rules()));
            var legacy = single.CreateSimulation();
            Assert.Throws<ArgumentException>(() => new BattleSimulation(legacy.Grid.Map, legacy.Path,
                new WaveDefinition(new EnemyDefinition(new EnemyDefinitionId("test"), 10, 1f, 1, 0), 1, 0, 1),
                legacy.TowerCatalog, 20, 150, legacy.ReactionCatalog, BattleElementUnlockTests.Rules()));
        }

        [Test]
        public void RegBattle164_Collections_AreCopiedAndReadOnly()
        {
            var starts = new[] { ElementKind.Flame, ElementKind.Gale };
            var waves = new[] { 3, 6 };
            var rules = new ElementUnlockRules(starts, waves);
            starts[0] = ElementKind.Stone; waves[0] = 2;
            Assert.That(rules.StartingOptions, Is.EqualTo(new[] { ElementKind.Flame, ElementKind.Gale }));
            Assert.That(rules.ChoiceWaves, Is.EqualTo(new[] { 3, 6 }));
            Assert.Throws<NotSupportedException>(() => ((IList<ElementKind>)rules.StartingOptions)[0] = ElementKind.Tide);
            Assert.Throws<NotSupportedException>(() => ((IList<int>)rules.ChoiceWaves)[0] = 2);
            var s = BattleElementUnlockTests.Native();
            var archetype = s.TowerCatalog.Get(new TowerDefinitionId("flame"));
            var requirements = new[] { ElementKind.Flame };
            var copy = new TowerArchetypeDefinition(archetype.Id, true, archetype.Levels, requirements);
            requirements[0] = ElementKind.Tide;
            Assert.That(copy.RequiredElements, Is.EqualTo(new[] { ElementKind.Flame }));
            Assert.Throws<NotSupportedException>(() => ((IList<ElementKind>)copy.RequiredElements).Clear());
            BattleElementUnlockTests.Choose(s, "start-resonance", "flame");
            Assert.Throws<NotSupportedException>(() => ((IList<ElementKind>)s.State.ElementProgression.UnlockedElements).Clear());
            BattleElementUnlockTests.Reach(s, 3); BattleElementUnlockTests.Choose(s, "after-wave-3", "tide");
            Assert.Throws<NotSupportedException>(() => ((IList<SpentElementChoice>)s.State.ElementProgression.SpentChoices).Clear());
            s.ContinueToNextWave();
            var availability = s.GetTowerAvailability(new PlaceTowerCommand(new TowerDefinitionId("storm"), new GridCoordinate(0, 1)));
            Assert.Throws<NotSupportedException>(() => ((IList<ElementKind>)availability.MissingElements).Clear());
        }

        [TestCase("flame", new[] { ElementKind.Flame })]
        [TestCase("tide", new[] { ElementKind.Tide })]
        [TestCase("gale", new[] { ElementKind.Gale })]
        [TestCase("stone", new[] { ElementKind.Stone })]
        [TestCase("steam", new[] { ElementKind.Flame, ElementKind.Tide })]
        [TestCase("storm", new[] { ElementKind.Tide, ElementKind.Gale })]
        public void RegBattle164_171_ExplicitRequirements_ValidateNativeCatalog(string id, ElementKind[] expected)
        {
            var s = BattleElementUnlockTests.Native();
            var archetype = s.TowerCatalog.Get(new TowerDefinitionId(id));
            Assert.That(archetype.Levels.Count, Is.EqualTo(3));
            Assert.That(archetype.RequiredElements, Is.EqualTo(expected));
            foreach (var invalid in new[] { new[] { (ElementKind)99 }, new[] { ElementKind.Flame, ElementKind.Flame } })
                Assert.Throws<ArgumentException>(() => new TowerArchetypeDefinition(archetype.Id, true, archetype.Levels, invalid));
            var sameCountWrongElements = expected.Length == 1
                ? new[] { expected[0] == ElementKind.Tide ? ElementKind.Flame : ElementKind.Tide }
                : new[] { ElementKind.Flame, ElementKind.Gale };
            foreach (var invalid in new[] { Array.Empty<ElementKind>(), sameCountWrongElements,
                new[] { ElementKind.Stone, ElementKind.Tide, ElementKind.Gale } })
            {
                var replacement = new TowerArchetypeDefinition(archetype.Id, true, archetype.Levels, invalid);
                var catalog = new TowerCatalog(s.TowerCatalog.Archetypes.Select(a => a.Id == archetype.Id ? replacement : a).ToArray());
                Assert.Throws<ArgumentException>(() => BattleElementUnlockTests.Native(catalog: catalog));
            }
            var custom = new TowerDefinition(new TowerDefinitionId("custom"), 0, 1f, 1f, 1);
            var unknown = new TowerArchetypeDefinition(custom.Id, false, new[] { custom, custom, custom });
            Assert.Throws<ArgumentException>(() => BattleElementUnlockTests.Native(catalog: new TowerCatalog(new[] { unknown })));
        }

        [Test]
        public void RegBattle172_ExistingFailureValuesAndPublicAllOpenFactories_ArePreserved()
        {
            Assert.That((int)PlaceTowerFailureReason.IdentifierExhausted, Is.EqualTo(7));
            Assert.That((int)TowerOperationFailureReason.ConfirmationRequired, Is.EqualTo(7));
            Assert.That((int)WaveTransitionResult.RejectedIdExhausted, Is.EqualTo(4));
            var s = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWave.asset").CreateSimulation();
            Assert.That(s.State.ElementProgression.RulesId, Is.EqualTo("legacy-all-open-v1"));
            Assert.That(s.State.ElementProgression.UnlockedElements.Count, Is.EqualTo(4));
            Assert.That(s.State.ElementProgression.PendingChoiceId, Is.EqualTo("none"));
            Assert.DoesNotThrow(() => s.ExportCheckpoint());
        }
    }
}
