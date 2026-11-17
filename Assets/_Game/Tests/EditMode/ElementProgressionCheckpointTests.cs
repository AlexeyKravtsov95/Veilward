using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Veilward.Domain;

namespace Veilward.Tests.EditMode
{
    public sealed class ElementProgressionCheckpointTests
    {
        [TestCaseSource(typeof(BattleElementUnlockTests), nameof(BattleElementUnlockTests.Routes))]
        public void RegBattle174_AllRoutes_RoundTripEverySafeBoundary(string start, string first, string second)
        {
            var s = BattleElementUnlockTests.Native();
            RoundTrip(s);
            BattleElementUnlockTests.Choose(s, "start-resonance", start);
            s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId(start), new GridCoordinate(0, 1)));
            RoundTrip(s);
            for (var wave = 1; wave <= 8; wave++)
            {
                BattleElementUnlockTests.Complete(s);
                RoundTrip(s);
                if (wave == 3 || wave == 6)
                {
                    BattleElementUnlockTests.Choose(s, "after-wave-" + wave, wave == 3 ? first : second);
                    RoundTrip(s);
                }
                if (wave < 8)
                {
                    s.ContinueToNextWave();
                    if (wave == 3 && first == "tide")
                        Assert.That(s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId(start == "flame" ? "steam" : "storm"),
                            new GridCoordinate(1, 1))).Success, Is.True);
                    RoundTrip(s);
                }
            }
        }

        [TestCase(3)] [TestCase(6)]
        public void RegBattle174_Defeat_RoundTripsWithoutMilestoneCredit(int wave)
        {
            var s = BattleElementUnlockTests.Native(health: wave);
            BattleElementUnlockTests.Choose(s, "start-resonance", "flame");
            for (var n = 1; n <= wave; n++)
            {
                BattleElementUnlockTests.Complete(s);
                if (n == 3 && wave == 6) BattleElementUnlockTests.Choose(s, "after-wave-3", "tide");
                if (n < wave) s.ContinueToNextWave();
            }
            Assert.That(s.State.Phase, Is.EqualTo(BattlePhase.Defeat));
            var cp = s.ExportCheckpoint();
            s.RestoreCheckpoint(cp);
            Assert.That(JsonUtility.ToJson(s.ExportCheckpoint()), Is.EqualTo(JsonUtility.ToJson(cp)));
        }

        [Test]
        public void RegBattle174_ExportAndRestore_AreDeepCopies()
        {
            var s = BattleElementUnlockTests.Native();
            BattleElementUnlockTests.Choose(s, "start-resonance", "flame");
            BattleElementUnlockTests.Reach(s, 3);
            BattleElementUnlockTests.Choose(s, "after-wave-3", "tide");
            var cp = s.ExportCheckpoint();
            var original = JsonUtility.ToJson(cp);
            s.RestoreCheckpoint(cp);
            // Изменение независимого DTO после Restore не должно менять live progression.
            JsonUtility.FromJsonOverwrite("{\"elementProgression\":{\"unlockedElementIds\":[\"stone\"],\"spentChoices\":[{\"choiceId\":\"bad\",\"elementId\":\"stone\"}]}}", cp);
            Assert.That(JsonUtility.ToJson(s.ExportCheckpoint()), Is.EqualTo(original));
        }

        public static IEnumerable<TestCaseData> Corruptions()
        {
            foreach (var patch in new[] {
                "{\"elementProgression\":null}",
                "{\"elementProgression\":{\"rulesId\":\"legacy-all-open-v1\"}}",
                "{\"elementProgression\":{\"startResonanceId\":\"tide\"}}",
                "{\"elementProgression\":{\"unlockedElementIds\":[\"flame\",\"stone\"]}}",
                "{\"elementProgression\":{\"unlockedElementIds\":[\"flame\",\"flame\"]}}",
                "{\"elementProgression\":{\"choicesEarned\":-1}}",
                "{\"elementProgression\":{\"choicesEarned\":2147483647}}",
                "{\"elementProgression\":{\"choicesSpent\":1}}",
                "{\"elementProgression\":{\"pendingChoiceId\":\"after-wave-6\"}}",
                "{\"elementProgression\":{\"pendingChoiceId\":null}}",
                "{\"elementProgression\":{\"spentChoices\":[{\"choiceId\":\"after-wave-6\",\"elementId\":\"tide\"}]}}",
                "{\"phase\":0,\"waveIndex\":3}",
                "{\"phase\":2,\"waveIndex\":7}",
                "{\"towers\":[{\"towerId\":1,\"towerTypeId\":\"test-tower\",\"x\":0,\"y\":1,\"level\":1,\"investedGold\":60,\"nextAttackTick\":0}],\"nextTowerId\":2}",
                "{\"towers\":[{\"towerId\":1,\"towerTypeId\":\"steam\",\"x\":0,\"y\":1,\"level\":1,\"investedGold\":110,\"nextAttackTick\":0}],\"nextTowerId\":2}"
            }) yield return new TestCaseData(patch);
        }

        [TestCaseSource(nameof(Corruptions))]
        public void RegBattle175_176_InvalidSnapshot_RejectsAtomicallyEvenDuringDirtyCombat(string patch)
        {
            var s = BattleElementUnlockTests.Native();
            BattleElementUnlockTests.Choose(s, "start-resonance", "flame");
            BattleElementUnlockTests.Reach(s, 3);
            var cp = s.ExportCheckpoint();
            JsonUtility.FromJsonOverwrite(patch, cp);
            if (patch == "{\"elementProgression\":null}") cp.elementProgression = null;
            BattleElementUnlockTests.Choose(s, "after-wave-3", "tide"); s.ContinueToNextWave();
            s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId("steam"), new GridCoordinate(0, 1)));
            s.StartWave(); s.Step();
            var state = s.State; var grid = s.Grid;
            var before = BattleElementUnlockTests.Snapshot(s);
            var attacks = s.AttackEvents.ToArray(); var damage = s.DamageEvents.ToArray(); var reactions = s.ReactionEvents.ToArray();
            Assert.That(s.TryValidateCheckpoint(cp, out _), Is.False, patch);
            Assert.Throws<ArgumentException>(() => s.RestoreCheckpoint(cp));
            Assert.That(s.State, Is.SameAs(state)); Assert.That(s.Grid, Is.SameAs(grid));
            Assert.That(BattleElementUnlockTests.Snapshot(s), Is.EqualTo(before));
            Assert.That(s.AttackEvents, Is.EqualTo(attacks)); Assert.That(s.DamageEvents, Is.EqualTo(damage));
            Assert.That(s.ReactionEvents, Is.EqualTo(reactions));
        }


        [TestCase("duplicate-choice")] [TestCase("reversed")] [TestCase("duplicate-element")]
        [TestCase("extra-history")] [TestCase("null-history-entry")] [TestCase("unknown-element")]
        [TestCase("noncanonical-unlocks")] [TestCase("missing-history")]
        [TestCase("pending-after-confirm")] [TestCase("wrong-earned")] [TestCase("wrong-spent")]
        public void RegBattle176_HistoryAndUnlockSetMustMatchExactly(string damage)
        {
            var s = BattleElementUnlockTests.Native(); BattleElementUnlockTests.Choose(s, "start-resonance", "gale");
            BattleElementUnlockTests.Reach(s, 3); BattleElementUnlockTests.Choose(s, "after-wave-3", "tide"); s.ContinueToNextWave();
            BattleElementUnlockTests.Reach(s, 6); BattleElementUnlockTests.Choose(s, "after-wave-6", "flame");
            var cp = s.ExportCheckpoint(); var p = cp.elementProgression;
            switch (damage)
            {
                case "duplicate-choice": p.spentChoices[1].choiceId = "after-wave-3"; break;
                case "reversed": Array.Reverse(p.spentChoices); break;
                case "duplicate-element": p.spentChoices[1].elementId = "tide"; break;
                case "extra-history": p.spentChoices = new[] { p.spentChoices[0], p.spentChoices[1], p.spentChoices[0] }; p.choicesSpent = 3; break;
                case "null-history-entry": p.spentChoices[1] = null; break;
                case "unknown-element": p.spentChoices[1].elementId = "unknown"; break;
                case "noncanonical-unlocks": Array.Reverse(p.unlockedElementIds); break;
                case "missing-history": p.spentChoices = Array.Empty<SpentElementChoiceCheckpoint>(); break;
                case "pending-after-confirm": p.pendingChoiceId = "after-wave-3"; break;
                case "wrong-earned": p.choicesEarned = 1; break;
                case "wrong-spent": p.choicesSpent = int.MaxValue; break;
            }
            var before = JsonUtility.ToJson(s.ExportCheckpoint());
            Assert.That(s.TryValidateCheckpoint(cp, out _), Is.False, damage);
            Assert.Throws<ArgumentException>(() => s.RestoreCheckpoint(cp));
            Assert.That(JsonUtility.ToJson(s.ExportCheckpoint()), Is.EqualTo(before));
        }

        [TestCase("earned")] [TestCase("spent")] [TestCase("history")] [TestCase("start")]
        [TestCase("pending")] [TestCase("unlocks")]
        public void RegBattle181_LegacyProgression_MustBeExactlyAllOpen(string damage)
        {
            var s = ElementProgressionStorageTests.Config().CreateSimulation(); var cp = s.ExportCheckpoint();
            var p = cp.elementProgression;
            switch (damage)
            {
                case "earned": p.choicesEarned = 1; break;
                case "spent": p.choicesSpent = 1; break;
                case "history": p.spentChoices = new[] { new SpentElementChoiceCheckpoint { choiceId = "after-wave-3", elementId = "tide" } }; p.choicesSpent = 1; break;
                case "start": p.startResonanceId = "flame"; break;
                case "pending": p.pendingChoiceId = "after-wave-3"; break;
                case "unlocks": p.unlockedElementIds = new[] { "flame", "tide", "gale" }; break;
            }
            Assert.That(s.TryValidateCheckpoint(cp, out _), Is.False);
        }

        private static void RoundTrip(BattleSimulation s)
        {
            var cp = s.ExportCheckpoint(); var json = JsonUtility.ToJson(cp);
            var restored = BattleElementUnlockTests.Native();
            restored.RestoreCheckpoint(JsonUtility.FromJson<BattleCheckpoint>(json));
            Assert.That(JsonUtility.ToJson(restored.ExportCheckpoint()), Is.EqualTo(json));
            Assert.That(restored.State.ElementProgression.PendingChoiceId, Is.EqualTo(s.State.ElementProgression.PendingChoiceId));
            Assert.That(restored.State.ElementProgression.UnlockedElements, Is.EqualTo(s.State.ElementProgression.UnlockedElements));
        }
    }
}
