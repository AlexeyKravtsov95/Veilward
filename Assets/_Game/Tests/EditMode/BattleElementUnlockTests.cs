using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Veilward.Domain;
using Veilward.Presentation;

namespace Veilward.Tests.EditMode
{
    public sealed class BattleElementUnlockTests
    {
        private static readonly GridCoordinate Cell = new GridCoordinate(0, 1);
        internal static ElementUnlockRules Rules() => new ElementUnlockRules(
            new[] { ElementKind.Flame, ElementKind.Gale }, new[] { 3, 6 });

        internal static BattleSimulation Native(int gold = 10000, int health = 20, TowerCatalog catalog = null)
        {
            var production = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWave.asset")
                .CreateSimulation();
            var enemy = new EnemyDefinition(new EnemyDefinitionId("fixture"), 100, 30f, 1, 5);
            var waves = Enumerable.Range(0, 8).Select(_ => new WaveDefinition(enemy, 1, 0, 1)).ToArray();
            return new BattleSimulation(new GridMapDefinition(5, 3, new GridCoordinate(0, 0), 1f),
                new PathDefinition(new[] { new GridCoordinate(0, 0), new GridCoordinate(2, 0) }),
                waves, catalog ?? production.TowerCatalog, health, gold, 20, production.ReactionCatalog, Rules());
        }

        internal static void Choose(BattleSimulation simulation, string choice, string option)
        {
            var before = CombatSnapshot(simulation);
            var attacks = simulation.AttackEvents.ToArray();
            var damage = simulation.DamageEvents.ToArray();
            var reactions = simulation.ReactionEvents.ToArray();
            var previous = simulation.State.ElementProgression;
            var pending = previous.PendingChoiceId;
            var result = simulation.ChooseElement(new ChooseElementCommand(choice, option));
            Assert.That(result.Success, Is.True, result.FailureReason.ToString());
            Assert.That(CombatSnapshot(simulation), Is.EqualTo(before));
            Assert.That(simulation.AttackEvents, Is.EqualTo(attacks));
            Assert.That(simulation.DamageEvents, Is.EqualTo(damage));
            Assert.That(simulation.ReactionEvents, Is.EqualTo(reactions));
            Assert.That(previous.PendingChoiceId, Is.EqualTo(pending));
        }

        internal static void Complete(BattleSimulation simulation)
        {
            Assert.That(simulation.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
            for (var ticks = 0; ticks < 100 && simulation.State.Phase == BattlePhase.Combat; ticks++)
                simulation.Step();
            Assert.That(simulation.State.Phase, Is.Not.EqualTo(BattlePhase.Combat));
        }

        internal static void Reach(BattleSimulation simulation, int wave)
        {
            while (simulation.State.WaveIndex + 1 < wave)
            {
                Complete(simulation);
                Assert.That(simulation.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.Continued));
            }
            Complete(simulation);
        }

        [TestCase("flame", ElementKind.Flame, 10)]
        [TestCase("gale", ElementKind.Gale, 70)]
        public void RegBattle165_StartChoice_UsesRealBudgetWithoutBonus(string option, ElementKind element, int remaining)
        {
            var config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWave.asset");
            var s = config.CreateSimulation(Rules());
            Assert.That(s.State.ElementProgression.RulesId, Is.EqualTo("wave-unlocks-v1"));
            Assert.That(s.State.ElementProgression.PendingChoiceId, Is.EqualTo("start-resonance"));
            Assert.That(s.State.ElementProgression.StartResonanceId, Is.EqualTo("unselected"));
            Assert.That(s.State.ElementProgression.UnlockedElements, Is.Empty);
            Assert.That(s.State.Towers, Is.Empty);
            Assert.That(s.State.BaseHealth, Is.EqualTo(20));
            var before = CombatSnapshot(s);
            Choose(s, "start-resonance", option);
            Assert.That(CombatSnapshot(s), Is.EqualTo(before));
            Assert.That(s.State.ElementProgression.UnlockedElements, Is.EqualTo(new[] { element }));
            Assert.That(s.State.ElementProgression.ChoicesEarned, Is.Zero);
            Assert.That(s.State.ElementProgression.ChoicesSpent, Is.Zero);
            Assert.That(s.State.ElementProgression.SpentChoices, Is.Empty);
            Assert.That(s.State.ElementProgression.StartResonanceId, Is.EqualTo(option));
            var cells = new[] { new GridCoordinate(-5, -3), new GridCoordinate(-4, -3) };
            Assert.That(s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId(option), cells[0])).Success, Is.True);
            var result = s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId(option), cells[1]));
            Assert.That(result.Success, Is.EqualTo(option == "flame"));
            if (option == "gale") Assert.That(result.FailureReason, Is.EqualTo(PlaceTowerFailureReason.InsufficientGold));
            Assert.That(s.State.Gold, Is.EqualTo(remaining));
        }

        [Test]
        public void RegBattle165_StartPending_BlocksCommandsAndAllowsUnarmedStartAfterConfirm()
        {
            var s = Native();
            var before = Snapshot(s);
            Assert.That(s.CheckStartWave(), Is.EqualTo(WaveTransitionResult.RejectedPendingElementChoice));
            Assert.That(s.StartWave(), Is.EqualTo(s.CheckStartWave()));
            Assert.That(s.CheckContinueToNextWave(), Is.EqualTo(WaveTransitionResult.RejectedInvalidPhase));
            Assert.That(s.ContinueToNextWave(), Is.EqualTo(s.CheckContinueToNextWave()));
            Assert.That(s.PlaceTower(new PlaceTowerCommand(default, Cell)).FailureReason,
                Is.EqualTo(PlaceTowerFailureReason.PendingElementChoice));
            Assert.That(s.UpgradeTower(new TowerId(99)).FailureReason, Is.EqualTo(TowerOperationFailureReason.PendingElementChoice));
            Assert.That(s.SellTower(new TowerId(99)).FailureReason, Is.EqualTo(TowerOperationFailureReason.PendingElementChoice));
            Assert.That(Snapshot(s), Is.EqualTo(before));
            Choose(s, "start-resonance", "gale");
            AssertAvailability(s, "tide", PlaceTowerFailureReason.ElementLocked, ElementKind.Tide);
            AssertAvailability(s, "test-tower", PlaceTowerFailureReason.LegacyTowerUnavailable);
            before = Snapshot(s);
            Assert.That(s.CheckStartWave(), Is.EqualTo(WaveTransitionResult.Started));
            Assert.That(Snapshot(s), Is.EqualTo(before));
            Assert.That(s.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
        }

        public static IEnumerable<TestCaseData> Routes()
        {
            foreach (var start in new[] { "flame", "gale" })
            foreach (var first in new[] { "flame", "tide", "gale", "stone" }.Where(x => x != start))
            foreach (var second in new[] { "flame", "tide", "gale", "stone" }.Where(x => x != start && x != first))
                yield return new TestCaseData(start, first, second);
        }

        [TestCaseSource(nameof(Routes))]
        public void RegBattle166_167_AllChoices_AreAtomicAndAwardedOnce(string start, string first, string second)
        {
            var s = Native();
            Choose(s, "start-resonance", start);
            s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId(start), Cell));
            for (var wave = 1; wave <= 8; wave++)
            {
                var gold = s.State.Gold;
                var kills = s.State.Kills;
                Complete(s);
                Assert.That(s.State.Gold, Is.EqualTo(gold + 20 + 5 * (s.State.Kills - kills)));
                Assert.That(s.State.ElementProgression.ChoicesEarned, Is.EqualTo(wave < 3 ? 0 : wave < 6 ? 1 : 2));
                var before = Snapshot(s);
                s.Step(); s.Step();
                Assert.That(Snapshot(s), Is.EqualTo(before));
                if (wave == 3 || wave == 6)
                {
                    var choice = "after-wave-" + wave;
                    Assert.That(s.State.ElementProgression.PendingChoiceId, Is.EqualTo(choice));
                    Assert.That(s.CheckContinueToNextWave(), Is.EqualTo(WaveTransitionResult.RejectedPendingElementChoice));
                    Assert.That(s.ContinueToNextWave(), Is.EqualTo(s.CheckContinueToNextWave()));
                    Assert.That(Snapshot(s), Is.EqualTo(before));
                    var combat = CombatSnapshot(s);
                    var attacks = s.AttackEvents.ToArray();
                    var damage = s.DamageEvents.ToArray();
                    var reactions = s.ReactionEvents.ToArray();
                    Choose(s, choice, wave == 3 ? first : second);
                    Assert.That(CombatSnapshot(s), Is.EqualTo(combat));
                    Assert.That(s.AttackEvents, Is.EqualTo(attacks));
                    Assert.That(s.DamageEvents, Is.EqualTo(damage));
                    Assert.That(s.ReactionEvents, Is.EqualTo(reactions));
                    Assert.That(s.State.Phase, Is.EqualTo(BattlePhase.WaveComplete));
                    Assert.That(s.State.ElementProgression.ChoicesSpent, Is.EqualTo(wave == 3 ? 1 : 2));
                    Assert.That(s.State.ElementProgression.PendingChoiceId, Is.EqualTo("none"));
                    Assert.That(s.State.ElementProgression.SpentChoices.Last().ChoiceId, Is.EqualTo(choice));
                    Assert.That(s.State.ElementProgression.SpentChoices.Last().ElementKind.ToString().ToLowerInvariant(),
                        Is.EqualTo(wave == 3 ? first : second));
                }
                else Assert.That(s.State.ElementProgression.PendingChoiceId, Is.EqualTo("none"));
                if (wave < 8)
                    Assert.That(s.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.Continued));
            }
            Assert.That(s.State.Phase, Is.EqualTo(BattlePhase.Victory));
            Assert.That(s.State.ElementProgression.UnlockedElements.Count, Is.EqualTo(3));
            Assert.That(s.AttackEvents.Count, Is.GreaterThan(0));
            Reject(s, "after-wave-6", "stone", ElementChoiceFailureReason.InvalidPhase);
        }

        [TestCase(3)] [TestCase(6)]
        public void RegBattle166_DefeatAtMilestone_DoesNotAwardBonusOrChoice(int wave)
        {
            var s = Native(health: wave);
            Choose(s, "start-resonance", "flame");
            if (wave == 6)
            {
                Reach(s, 3); Choose(s, "after-wave-3", "tide"); s.ContinueToNextWave();
            }
            while (s.State.WaveIndex + 1 < wave) { Complete(s); s.ContinueToNextWave(); }
            var beforeGold = s.State.Gold;
            var earned = s.State.ElementProgression.ChoicesEarned;
            Complete(s);
            Assert.That(s.State.Phase, Is.EqualTo(BattlePhase.Defeat));
            Assert.That(s.State.Gold, Is.EqualTo(beforeGold));
            Assert.That(s.State.ElementProgression.ChoicesEarned, Is.EqualTo(earned));
            Assert.That(s.State.ElementProgression.PendingChoiceId, Is.EqualTo("none"));
            var before = Snapshot(s); s.Step(); Assert.That(Snapshot(s), Is.EqualTo(before));
            Reject(s, "after-wave-" + wave, "gale", ElementChoiceFailureReason.InvalidPhase);
        }

        [Test]
        public void RegBattle168_169_170_ValidationOrderAndStaleConfirm_PreserveFullState()
        {
            var s = Native();
            Reject(s, null, "flame", ElementChoiceFailureReason.ChoiceMismatch);
            Reject(s, "after-wave-3", "flame", ElementChoiceFailureReason.ChoiceMismatch);
            Reject(s, "start-resonance", "tide", ElementChoiceFailureReason.InvalidOption);
            Reject(s, "start-resonance", null, ElementChoiceFailureReason.InvalidOption);
            Choose(s, "start-resonance", "flame");
            Reject(s, "start-resonance", "gale", ElementChoiceFailureReason.NoPendingChoice);
            Reach(s, 3);
            Assert.That(s.AttackEvents, Is.Empty);
            Reject(s, "start-resonance", "gale", ElementChoiceFailureReason.ChoiceMismatch);
            foreach (var id in new[] { null, "", "unknown", "after-wave-6" })
                Reject(s, id, "unknown", ElementChoiceFailureReason.ChoiceMismatch);
            foreach (var option in new[] { null, "", "unknown", "FLAME" })
                Reject(s, "after-wave-3", option, ElementChoiceFailureReason.InvalidOption);
            Reject(s, "after-wave-3", "flame", ElementChoiceFailureReason.AlreadyUnlocked);
            Assert.That(s.StartWave(), Is.EqualTo(WaveTransitionResult.RejectedInvalidPhase));
            AssertAvailability(s, "flame", PlaceTowerFailureReason.InvalidPhase);
            Assert.That(s.UpgradeTower(new TowerId(99)).FailureReason, Is.EqualTo(TowerOperationFailureReason.InvalidPhase));
            Assert.That(s.SellTower(new TowerId(99)).FailureReason, Is.EqualTo(TowerOperationFailureReason.InvalidPhase));
            Choose(s, "after-wave-3", "tide");
            Reject(s, "after-wave-3", "tide", ElementChoiceFailureReason.NoPendingChoice);
            Reject(s, "after-wave-3", "stone", ElementChoiceFailureReason.NoPendingChoice);
            Assert.That(s.State.WaveIndex, Is.EqualTo(2));
            Assert.That(s.CheckContinueToNextWave(), Is.EqualTo(WaveTransitionResult.Continued));
            s.ContinueToNextWave();
            Assert.That(s.State.WaveIndex, Is.EqualTo(3));
            Assert.That(s.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.RejectedInvalidPhase));
            var tower = s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId("steam"), Cell)).Tower;
            Assert.That(tower, Is.Not.Null);
            s.StartWave(); s.Step();
            Reject(s, "after-wave-3", "stone", ElementChoiceFailureReason.InvalidPhase);
            Assert.That(s.AttackEvents.Count, Is.GreaterThan(0));
            while (s.State.Phase == BattlePhase.Combat) s.Step();
            s.ContinueToNextWave(); Reach(s, 6);
            Reject(s, "after-wave-3", "gale", ElementChoiceFailureReason.ChoiceMismatch);
            Assert.That(s.ReactionEvents.Count, Is.GreaterThan(0));
            Choose(s, "after-wave-6", "gale");
            Reject(s, "after-wave-6", "stone", ElementChoiceFailureReason.NoPendingChoice);
        }

        [Test]
        public void RegBattle170_LegacyModeAndMissingCredit_AreRejected()
        {
            var legacy = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWave.asset").CreateSimulation();
            Reject(legacy, null, null, ElementChoiceFailureReason.ProgressionDisabled);
            legacy.StartWave();
            Reject(legacy, null, null, ElementChoiceFailureReason.InvalidPhase);
            var s = Native(); Choose(s, "start-resonance", "flame"); Reach(s, 3);
            // Недостижимое через команды состояние: проверяем последний защитный guard.
            typeof(ElementProgressionState).GetField("<ChoicesEarned>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(s.State.ElementProgression, 0);
            Reject(s, "after-wave-3", "flame", ElementChoiceFailureReason.AlreadyUnlocked);
            Reject(s, "after-wave-3", "tide", ElementChoiceFailureReason.NoChoiceCredit);
        }

        [TestCase("flame")] [TestCase("tide")] [TestCase("gale")]
        [TestCase("stone")] [TestCase("steam")] [TestCase("storm")]
        public void RegBattle171_172_RequirementsAcrossLevels_KeepEconomyIdentityAndCooldown(string id)
        {
            var s = Native();
            var start = id == "stone" ? "gale" : "flame";
            Choose(s, "start-resonance", start);
            Reach(s, 3); Choose(s, "after-wave-3", "tide"); s.ContinueToNextWave();
            Reach(s, 6); Choose(s, "after-wave-6", id == "stone" ? "stone" : "gale"); s.ContinueToNextWave();
            var progression = ProgressionSnapshot(s);
            var beforeGold = s.State.Gold;
            var result = s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId(id), Cell));
            Assert.That(result.Success, Is.True);
            var tower = result.Tower;
            Assert.That(tower.Id.Value, Is.EqualTo(1));
            s.StartWave(); s.Step();
            Assert.That(tower.NextAttackTick, Is.GreaterThan(0));
            while (s.State.Phase == BattlePhase.Combat) s.Step();
            s.ContinueToNextWave();
            var cooldown = tower.NextAttackTick;
            var gold = s.State.Gold;
            var levels = s.TowerCatalog.Get(new TowerDefinitionId(id)).Levels;
            Assert.That(beforeGold - levels[0].Price + 20, Is.EqualTo(gold));
            for (var level = 1; level < 3; level++)
            {
                Assert.That(s.UpgradeTower(tower.Id).Success, Is.True);
                gold -= levels[level].Price;
                Assert.That(s.State.Gold, Is.EqualTo(gold));
                Assert.That(tower.Level, Is.EqualTo(level + 1));
                Assert.That(tower.Id.Value, Is.EqualTo(1));
                Assert.That(tower.Coordinate, Is.EqualTo(Cell));
                Assert.That(tower.NextAttackTick, Is.EqualTo(cooldown));
            }
            var before = Snapshot(s);
            Assert.That(s.UpgradeTower(tower.Id).FailureReason, Is.EqualTo(TowerOperationFailureReason.MaxLevel));
            Assert.That(Snapshot(s), Is.EqualTo(before));
            var invested = levels.Sum(level => level.Price);
            Assert.That(tower.InvestedGold, Is.EqualTo(invested));
            var sold = s.SellTower(tower.Id);
            Assert.That(sold.Success, Is.True);
            Assert.That(s.State.Gold, Is.EqualTo(gold + invested * 70 / 100));
            before = Snapshot(s);
            Assert.That(s.SellTower(tower.Id).FailureReason, Is.EqualTo(TowerOperationFailureReason.TowerNotFound));
            Assert.That(Snapshot(s), Is.EqualTo(before));
            Assert.That(ProgressionSnapshot(s), Is.EqualTo(progression));
            Assert.That(s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId(id), Cell)).Tower.Id.Value, Is.EqualTo(2));
        }

        [Test]
        public void RegBattle171_Availability_PreservesOrderAndMatchesPlace()
        {
            var s = Native(); Choose(s, "start-resonance", "flame");
            AssertAvailability(s, "unknown", PlaceTowerFailureReason.UnknownTowerDefinition);
            AssertAvailability(s, "tide", PlaceTowerFailureReason.ElementLocked, ElementKind.Tide);
            AssertAvailability(s, "steam", PlaceTowerFailureReason.ElementLocked, ElementKind.Tide);
            AssertAvailability(s, "storm", PlaceTowerFailureReason.ElementLocked, ElementKind.Tide, ElementKind.Gale);
            var outside = new PlaceTowerCommand(new TowerDefinitionId("storm"), new GridCoordinate(99, 99));
            Assert.That(s.GetTowerAvailability(outside).FailureReason, Is.EqualTo(PlaceTowerFailureReason.ElementLocked));
            var other = Native(); Choose(other, "start-resonance", "gale");
            Reach(s, 3); Choose(s, "after-wave-3", "tide"); s.ContinueToNextWave(); Reach(s, 6);
            Choose(s, "after-wave-6", "gale"); s.ContinueToNextWave();
            Reach(other, 3); Choose(other, "after-wave-3", "tide"); other.ContinueToNextWave(); Reach(other, 6);
            Choose(other, "after-wave-6", "flame"); other.ContinueToNextWave();
            Assert.That(s.State.Towers, Is.Empty);
            foreach (var id in new[] { "flame", "tide", "gale", "stone", "steam", "storm" })
            {
                var command = new PlaceTowerCommand(new TowerDefinitionId(id), Cell);
                Assert.That(s.GetTowerAvailability(command).FailureReason, Is.EqualTo(other.GetTowerAvailability(command).FailureReason));
                Assert.That(s.GetTowerAvailability(command).MissingElements, Is.EqualTo(other.GetTowerAvailability(command).MissingElements));
            }
            AssertAvailability(s, "steam", PlaceTowerFailureReason.None);
            AssertAvailability(other, "storm", PlaceTowerFailureReason.None);
        }

        [TestCase(0, PlaceTowerFailureReason.InsufficientGold)]
        [TestCase(1, PlaceTowerFailureReason.OutsideMap)]
        [TestCase(2, PlaceTowerFailureReason.Blocked)]
        [TestCase(3, PlaceTowerFailureReason.Occupied)]
        [TestCase(4, PlaceTowerFailureReason.IdentifierExhausted)]
        public void RegBattle171_AvailabilityAndPlace_ShareAllLegacyChecks(int condition, PlaceTowerFailureReason expected)
        {
            var s = Native(gold: condition == 0 ? 0 : 10000);
            Choose(s, "start-resonance", "flame");
            var cell = condition == 1 ? new GridCoordinate(99, 99) : condition == 2 ? new GridCoordinate(0, 0) : Cell;
            if (condition == 3) s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId("flame"), cell));
            if (condition == 4) typeof(BattleSimulation).GetField("_nextTowerId", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(s, int.MaxValue - 1);
            var command = new PlaceTowerCommand(new TowerDefinitionId("flame"), cell);
            var before = Snapshot(s);
            var available = s.GetTowerAvailability(command);
            Assert.That(available.FailureReason, Is.EqualTo(expected));
            Assert.That(available.CanPlace, Is.False);
            Assert.That(s.PlaceTower(command).FailureReason, Is.EqualTo(expected));
            Assert.That(Snapshot(s), Is.EqualTo(before));
        }

        [Test]
        public void RegBattle168_StartQuery_RejectsExhaustedIdsWithoutSpawning()
        {
            var s = Native(); Choose(s, "start-resonance", "gale");
            typeof(BattleSimulation).GetField("_nextEnemyId", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(s, int.MaxValue - 1);
            var before = Snapshot(s);
            Assert.That(s.CheckStartWave(), Is.EqualTo(WaveTransitionResult.RejectedIdExhausted));
            Assert.That(s.StartWave(), Is.EqualTo(s.CheckStartWave()));
            Assert.That(Snapshot(s), Is.EqualTo(before));
        }

        [Test]
        public void RegBattle172_LockedUpgrade_IsRejectedBeforeMaxOrGold()
        {
            var s = Native(gold: 0); Choose(s, "start-resonance", "flame");
            var tower = new TowerState(new TowerId(1), s.TowerCatalog.Get(new TowerDefinitionId("tide")).Levels[0], Cell);
            s.State.AddTower(tower); s.Grid.TryOccupy(Cell);
            var before = Snapshot(s);
            Assert.That(s.UpgradeTower(tower.Id).FailureReason, Is.EqualTo(TowerOperationFailureReason.ElementLocked));
            Assert.That(Snapshot(s), Is.EqualTo(before));
            Assert.That(s.SellTower(tower.Id).Success, Is.True);
        }

        [Test]
        public void RegBattle174_NativeInitialCheckpoint_RoundTripsProgression()
        {
            var s = Native();
            var checkpoint = s.ExportCheckpoint();
            Assert.That(s.TryValidateCheckpoint(checkpoint, out var error), Is.True, error);
            Choose(s, "start-resonance", "flame");
            s.RestoreCheckpoint(checkpoint);
            Assert.That(s.State.ElementProgression.PendingChoiceId, Is.EqualTo("start-resonance"));
            Assert.That(s.State.ElementProgression.UnlockedElements, Is.Empty);
            Assert.That(JsonUtility.ToJson(checkpoint), Does.Contain("elementProgression"));
        }

        private static void AssertAvailability(BattleSimulation s, string id, PlaceTowerFailureReason reason,
            params ElementKind[] missing)
        {
            var command = new PlaceTowerCommand(new TowerDefinitionId(id), Cell);
            var before = Snapshot(s);
            var query = s.GetTowerAvailability(command);
            Assert.That(query.CanPlace, Is.EqualTo(reason == PlaceTowerFailureReason.None));
            Assert.That(query.FailureReason, Is.EqualTo(reason));
            Assert.That(query.MissingElements, Is.EqualTo(missing));
            Assert.That(Snapshot(s), Is.EqualTo(before));
            var result = s.PlaceTower(command);
            Assert.That(result.FailureReason, Is.EqualTo(query.FailureReason));
            if (!result.Success) Assert.That(Snapshot(s), Is.EqualTo(before));
        }

        private static void Reject(BattleSimulation s, string choice, string option, ElementChoiceFailureReason reason)
        {
            var before = Snapshot(s);
            var attacks = s.AttackEvents.ToArray(); var reactions = s.ReactionEvents.ToArray(); var damage = s.DamageEvents.ToArray();
            var result = s.ChooseElement(new ChooseElementCommand(choice, option));
            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(reason));
            Assert.That(Snapshot(s), Is.EqualTo(before));
            Assert.That(s.AttackEvents, Is.EqualTo(attacks)); Assert.That(s.ReactionEvents, Is.EqualTo(reactions));
            Assert.That(s.DamageEvents, Is.EqualTo(damage));
        }

        private static string ProgressionSnapshot(BattleSimulation s)
        {
            var p = s.State.ElementProgression;
            return p.RulesId + ":" + p.StartResonanceId + ":" + p.PendingChoiceId + ":" + p.ChoicesEarned + ":" + p.ChoicesSpent +
                ":" + string.Join(",", p.UnlockedElements) + ":" + string.Join(",", p.SpentChoices.Select(c => c.ChoiceId + c.ElementKind));
        }

        internal static string Snapshot(BattleSimulation s) => CombatSnapshot(s) + ProgressionSnapshot(s);

        private static string CombatSnapshot(BattleSimulation s)
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var checkpoint = BattleCheckpoint.FromState(s.State,
                (int)typeof(BattleSimulation).GetField("_nextEnemyId", flags).GetValue(s),
                (int)typeof(BattleSimulation).GetField("_nextTowerId", flags).GetValue(s));
            var json = JsonUtility.ToJson(checkpoint);
            var result = new StringBuilder(json.Substring(0, json.IndexOf(",\"elementProgression\":", StringComparison.Ordinal)) + "}");
            for (var y = 0; y < s.Grid.Map.Height; y++)
            for (var x = 0; x < s.Grid.Map.Width; x++)
                result.Append(s.Grid.GetState(new GridCoordinate(s.Grid.Map.Origin.X + x, s.Grid.Map.Origin.Y + y)));
            foreach (var enemy in s.State.Enemies)
            {
                result.Append(enemy.Id).Append(enemy.Health).Append(enemy.DistanceAlongPath).Append(enemy.LastSteamTick).Append(enemy.LastStormTick);
                foreach (var status in enemy.Statuses) result.Append(status.Kind).Append(status.Power).Append(status.RemainingTicks);
            }
            return result.ToString();
        }
    }
}
