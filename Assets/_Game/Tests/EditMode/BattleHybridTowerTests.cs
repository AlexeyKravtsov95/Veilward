using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Veilward.Domain;
using Veilward.Infrastructure;
using Veilward.Presentation;
using Object = UnityEngine.Object;

namespace Veilward.Tests.EditMode
{
    public sealed class BattleHybridTowerTests
    {
        private readonly List<BattleScenarioConfig> _configs = new List<BattleScenarioConfig>();
        private string _root;
        private static readonly GridCoordinate Cell = new GridCoordinate(-4, 0);

        [TearDown]
        public void Cleanup()
        {
            foreach (var config in _configs) Object.DestroyImmediate(config);
            _configs.Clear();
            if (_root != null && Directory.Exists(_root)) Directory.Delete(_root, true);
            _root = null;
        }

        [TestCase("steam", 1, 110, 18, 3f, .8f, 38, 100, 1.25f, 4)]
        [TestCase("steam", 2, 165, 25, 3.2f, .9f, 33, 125, 1.5f, 4)]
        [TestCase("steam", 3, 220, 34, 3.4f, 1f, 30, 150, 1.75f, 4)]
        [TestCase("storm", 1, 110, 9, 3.5f, 1.5f, 20, 75, 2f, 2)]
        [TestCase("storm", 2, 165, 13, 3.5f, 1.7f, 18, 90, 2.25f, 2)]
        [TestCase("storm", 3, 220, 18, 3.75f, 2f, 15, 100, 2.5f, 2)]
        public void RegBattle146_SavedHybridLevels_MatchSpec(string id, int level, int price, int damage,
            float range, float rate, int interval, int percent, float reactionRange, int limit)
        {
            foreach (var config in new[] { Config(), AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>(
                "Assets/_Game/Configs/EightWave.asset") })
            {
                var d = config.CreateSimulation().TowerCatalog.Get(new TowerDefinitionId(id)).Levels[level - 1];
                Assert.That(d.Price, Is.EqualTo(price));
                Assert.That(d.Damage, Is.EqualTo(damage));
                Assert.That(d.Range, Is.EqualTo(range));
                Assert.That(d.FireRate, Is.EqualTo(rate));
                Assert.That(d.AttackIntervalTicks, Is.EqualTo(interval));
                Assert.That(d.ArmorPenetration, Is.Zero);
                Assert.That(d.StatusEffect, Is.Null);
                Assert.That(d.IntrinsicReaction, Is.Not.Null);
                Assert.That(d.IntrinsicReaction.Kind, Is.EqualTo(Kind(id)));
                Assert.That(d.IntrinsicReaction.DamagePercent, Is.EqualTo(percent));
                Assert.That(d.IntrinsicReaction.Range, Is.EqualTo(reactionRange));
                Assert.That(d.IntrinsicReaction.TargetLimit, Is.EqualTo(limit));
            }
        }

        [TestCase("steam", false)] [TestCase("steam", true)]
        [TestCase("storm", false)] [TestCase("storm", true)]
        public void RegBattle147_IntrinsicUsesProductionResolver_OnlyOwnKindWithoutAddingStatuses(string id, bool foreignPair)
        {
            var s = Simulation();
            Place(s, id); s.StartWave();
            var primary = s.State.Enemies[0];
            var neighbor = BattleReactionTests.Add(s, 2, 0, hp: 1000);
            if (foreignPair)
            {
                BattleReactionTests.Status(primary, StatusKind.Wet);
                BattleReactionTests.Status(primary, id == "steam" ? StatusKind.Gust : StatusKind.Burn);
            }
            s.Step();
            Assert.That(s.ReactionEvents.Count, Is.EqualTo(1));
            Assert.That(s.ReactionEvents[0].Kind, Is.EqualTo(Kind(id)));
            Assert.That(s.AttackEvents.Count, Is.EqualTo(1));
            Assert.That(primary.Statuses.Count, Is.EqualTo(foreignPair ? 2 : 0));
            Assert.That(neighbor.Statuses, Is.Empty);
            Assert.That(BattleReactionTests.Last(primary, Kind(id)), Is.EqualTo(1));
            Assert.That(BattleReactionTests.Last(primary, Kind(id == "steam" ? "storm" : "steam")), Is.Null);
            Assert.That(primary.Health, Is.EqualTo(id == "steam" ? (foreignPair ? 961 : 964) : 991));
            Assert.That(neighbor.Health, Is.EqualTo(id == "steam" ? 982 : 994));
            Assert.That(s.DamageEvents.Count(e => e.Origin == DamageOrigin.Reaction), Is.EqualTo(id == "steam" ? 2 : 1));
        }

        [TestCase("flame", "tide", ReactionKind.Steam, 968)]
        [TestCase("tide", "gale", ReactionKind.Storm, 984)]
        public void RegBattle153_PublicFactoryEnablesBasicStatusPairs(string first, string second, ReactionKind kind, int hp)
        {
            var s = Simulation(); Place(s, first);
            Assert.That(s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId(second), new GridCoordinate(-3, 0))).Success, Is.True);
            s.StartWave(); BattleReactionTests.Add(s, 2, 0);
            s.Step();
            Assert.That(s.ReactionEvents.Single().Kind, Is.EqualTo(kind));
            Assert.That(s.State.Enemies[0].Health, Is.EqualTo(hp));
        }

        [TestCase("steam", "lethal")] [TestCase("storm", "lethal")]
        [TestCase("steam", "locked")] [TestCase("storm", "locked")]
        [TestCase("storm", "isolated")]
        public void RegBattle147_SuppressionLeavesOnlyDirectAndDoesNotSpendReactionWindow(string id, string reason)
        {
            var s = Simulation(hp: reason == "lethal" ? 1 : 1000);
            Place(s, id); s.StartWave();
            var primary = s.State.Enemies[0];
            if (reason != "isolated") BattleReactionTests.Add(s, 2, 0, hp: 1000);
            if (reason == "locked") primary.ReserveReaction(Kind(id), 0);
            s.Step();
            Assert.That(s.ReactionEvents, Is.Empty);
            Assert.That(s.DamageEvents.Count, Is.EqualTo(1));
            Assert.That(s.DamageEvents[0].Origin, Is.EqualTo(DamageOrigin.Direct));
            Assert.That(BattleReactionTests.Last(primary, Kind(id)), Is.EqualTo(reason == "locked" ? (long?)0 : null));
            Assert.That(s.State.Towers[0].NextAttackTick, Is.EqualTo(id == "steam" ? 39 : 21));
            Assert.That(s.State.Kills, Is.EqualTo(reason == "lethal" ? 1 : 0));
        }

        [TestCase("steam")] [TestCase("storm")]
        public void RegBattle149_SellRebuild_OnlyReleasesOwnCellAndNeverReusesIdentity(string id)
        {
            var s = Simulation(); Place(s, id);
            var otherCell = new GridCoordinate(-3, 0);
            Assert.That(s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId(id), otherCell)).Success, Is.True);
            Assert.That(s.SellTower(new TowerId(1)).Refund, Is.EqualTo(77));
            Assert.That(s.SellTower(new TowerId(1)).Success, Is.False);
            Assert.That(s.Grid.GetState(otherCell), Is.EqualTo(GridCellState.Occupied));
            Place(s, id);
            Assert.That(s.State.Towers.Last().Id.Value, Is.EqualTo(3));
            Assert.That(s.State.Gold, Is.EqualTo(1747));
        }

        [TestCase("steam", 1, 110, 77)] [TestCase("steam", 2, 275, 192)] [TestCase("steam", 3, 495, 346)]
        [TestCase("storm", 1, 110, 77)] [TestCase("storm", 2, 275, 192)] [TestCase("storm", 3, 495, 346)]
        public void RegBattle148_UpgradeAndSell_PreserveIdentityCellCooldownAndExactInvestment(string id, int level, int invested, int refund)
        {
            var s = Simulation();
            Place(s, id);
            var checkpoint = s.ExportCheckpoint(); checkpoint.towers[0].nextAttackTick = 123;
            s.RestoreCheckpoint(checkpoint);
            // Живой объект с timestamps подтверждает, что Upgrade не пересоздаёт врагов/историю.
            var enemy = BattleReactionTests.Add(s, 1, 0);
            enemy.ReserveReaction(ReactionKind.Steam, 0); enemy.ReserveReaction(ReactionKind.Storm, 0);
            for (var i = 1; i < level; i++) Assert.That(s.UpgradeTower(new TowerId(1)).Success, Is.True);
            var tower = s.State.Towers.Single();
            Assert.That(tower.Id.Value, Is.EqualTo(1)); Assert.That(tower.Coordinate, Is.EqualTo(Cell));
            Assert.That(tower.NextAttackTick, Is.EqualTo(123)); Assert.That(tower.Level, Is.EqualTo(level));
            Assert.That(tower.Definition, Is.SameAs(s.TowerCatalog.Get(new TowerDefinitionId(id)).Levels[level - 1]));
            Assert.That(tower.InvestedGold, Is.EqualTo(invested)); Assert.That(s.State.Gold, Is.EqualTo(2000 - invested));
            Assert.That(s.State.Enemies.Single(), Is.SameAs(enemy));
            Assert.That(enemy.LastSteamTick, Is.EqualTo(0)); Assert.That(enemy.LastStormTick, Is.EqualTo(0));
            Assert.That(s.SellTower(tower.Id).Refund, Is.EqualTo(refund));
            Assert.That(s.Grid.GetState(Cell), Is.EqualTo(GridCellState.Available));
            Assert.That(s.SellTower(tower.Id).Success, Is.False);
            Assert.That(s.State.Gold, Is.EqualTo(2000 - invested + refund));
        }

        [TestCase("steam")] [TestCase("storm")]
        public void RegBattle150_InvalidRepeatedAndCombatCommands_AreAtomic(string id)
        {
            var s = Simulation(gold: 110);
            Place(s, id);
            AssertUnchanged(s, () => s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId(id), Cell)).Success);
            AssertUnchanged(s, () => s.UpgradeTower(new TowerId(1)).Success);
            AssertUnchanged(s, () => s.SellTower(new TowerId(999)).Success);
            AssertUnchanged(s, () => s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId("unknown"), new GridCoordinate(-3, 0))).Success);
            AssertUnchanged(s, () => s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId(id), new GridCoordinate(-4, -2))).Success);
            var rich = Simulation(); Place(rich, id);
            Assert.That(rich.UpgradeTower(new TowerId(1)).Success, Is.True);
            Assert.That(rich.UpgradeTower(new TowerId(1)).Success, Is.True);
            AssertUnchanged(rich, () => rich.UpgradeTower(new TowerId(1)).Success);
            rich.StartWave();
            var gold = rich.State.Gold; var tower = rich.State.Towers.Single();
            Assert.That(rich.UpgradeTower(tower.Id).Success, Is.False);
            Assert.That(rich.SellTower(tower.Id).Success, Is.False);
            Assert.That(rich.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId(id), new GridCoordinate(-3, 0))).Success, Is.False);
            Assert.That(rich.State.Gold, Is.EqualTo(gold)); Assert.That(tower.Level, Is.EqualTo(3));
            Assert.That(rich.AttackEvents, Is.Empty); Assert.That(rich.ReactionEvents, Is.Empty); Assert.That(rich.DamageEvents, Is.Empty);
        }

        [TestCase("steam", 1)] [TestCase("steam", 2)] [TestCase("steam", 3)]
        [TestCase("storm", 1)] [TestCase("storm", 2)] [TestCase("storm", 3)]
        public void RegBattle151_Schema2_RoundTripsEachHybridLevelWithoutRewritingBytes(string id, int level)
        {
            var s = Simulation(); Place(s, id);
            for (var i = 1; i < level; i++) Assert.That(s.UpgradeTower(new TowerId(1)).Success, Is.True);
            var cp = s.ExportCheckpoint(); cp.towers[0].nextAttackTick = 345;
            RoundTrip(s, cp);
        }

        [Test]
        public void RegBattle151_Schema2_MixedArmyAndPreviousTypesRemainCompatible()
        {
            var s = Simulation();
            var ids = new[] { "flame", "tide", "gale", "stone", "steam", "storm", "test-tower" };
            for (var i = 0; i < ids.Length; i++)
            {
                Assert.That(s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId(ids[i]), new GridCoordinate(i - 5, -3))).Success, Is.True);
                Assert.That(s.UpgradeTower(new TowerId(i + 1)).Success, Is.True);
            }
            RoundTrip(s, s.ExportCheckpoint());
        }

        [TestCase("type")] [TestCase("level")] [TestCase("investment")] [TestCase("content")]
        public void RegBattle152_InvalidPrimaryLoadsValidHybridBackupWithoutMutationOrWrites(string invalid)
        {
            var s = Simulation(); Place(s, "storm");
            var store = Store(); var valid = Envelope(s.ExportCheckpoint());
            Assert.That(store.TrySave(valid, s.TryValidateCheckpoint, out var error), Is.True, error);
            var bad = Envelope(s.ExportCheckpoint());
            switch (invalid)
            {
                case "type": bad.state.towers[0].towerTypeId = "unknown"; break;
                case "level": bad.state.towers[0].level = 4; break;
                case "investment": bad.state.towers[0].investedGold = 111; break;
                default: bad.scenarioContentId = "unknown-content"; break;
            }
            File.Copy(store.PrimaryPath, store.BackupPath);
            File.WriteAllText(store.PrimaryPath, JsonUtility.ToJson(bad, true));
            var primary = File.ReadAllBytes(store.PrimaryPath); var backup = File.ReadAllBytes(store.BackupPath);
            s.StartWave(); BattleReactionTests.Add(s, 2, 0); s.Step();
            var attacks = s.AttackEvents.ToArray(); var reactions = s.ReactionEvents.ToArray(); var damage = s.DamageEvents.ToArray();
            Assert.That(reactions, Is.Not.Empty);
            var state = s.State; var grid = s.Grid;
            if (invalid != "content") Assert.Throws<ArgumentException>(() => s.RestoreCheckpoint(bad.state));
            Assert.That(store.TryLoad("eight-wave-v1", s.TryValidateCheckpoint, out var load), Is.True, load.Error);
            Assert.That(load.Status, Is.EqualTo(WaveCheckpointLoadStatus.LoadedBackup));
            Assert.That(s.State, Is.SameAs(state)); Assert.That(s.Grid, Is.SameAs(grid));
            Assert.That(s.AttackEvents, Is.EqualTo(attacks)); Assert.That(s.ReactionEvents, Is.EqualTo(reactions));
            Assert.That(s.DamageEvents, Is.EqualTo(damage));
            CollectionAssert.AreEqual(primary, File.ReadAllBytes(store.PrimaryPath));
            CollectionAssert.AreEqual(backup, File.ReadAllBytes(store.BackupPath));
            s.RestoreCheckpoint(load.Envelope.state);
            Assert.That(s.State.Towers.Single().Definition.Id.Value, Is.EqualTo("storm"));
        }

        [Test]
        public void RegBattle152_V1MigrationStillMapsOnlyToHiddenLegacyWithoutWritingBytes()
        {
            var s = Simulation(); Place(s, "test-tower");
            var cp = s.ExportCheckpoint(); cp.towers[0].towerTypeId = null;
            var store = Store(); var envelope = Envelope(cp); envelope.schemaVersion = 1;
            File.WriteAllText(store.PrimaryPath, JsonUtility.ToJson(envelope, true));
            var bytes = File.ReadAllBytes(store.PrimaryPath);
            Assert.That(store.TryLoad("eight-wave-v1", s.TryValidateCheckpoint, out var load), Is.True, load.Error);
            Assert.That(load.Envelope.state.towers.Single().towerTypeId, Is.EqualTo("test-tower"));
            Assert.That(load.Envelope.schemaVersion, Is.EqualTo(2));
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(store.PrimaryPath));
        }

        [Test]
        public void RegBattle153_PublicFactoryIncludesProductionReactionsAndSixVisibleTypesFromPreparation()
        {
            var config = Config(); var s = config.CreateSimulation();
            Assert.That(s.TowerCatalog.Archetypes.Where(a => a.ShowInCatalog).Select(a => a.Id.Value),
                Is.EqualTo(new[] { "flame", "tide", "gale", "stone", "steam", "storm" }));
            Assert.That(s.State.Phase, Is.EqualTo(BattlePhase.Preparation));
            Assert.That(s.TowerCatalog.Get(new TowerDefinitionId("test-tower")).ShowInCatalog, Is.False);
            Assert.That(s.ReactionCatalog.Definitions.Count, Is.EqualTo(2));
            var steam = s.ReactionCatalog.Get(ReactionKind.Steam); var storm = s.ReactionCatalog.Get(ReactionKind.Storm);
            Assert.That(steam.CooldownTicks, Is.EqualTo(60)); Assert.That(storm.CooldownTicks, Is.EqualTo(60));
            Assert.That(steam.Priority, Is.EqualTo(0)); Assert.That(storm.Priority, Is.EqualTo(1));
            Assert.That(steam.Payload.DamagePercent, Is.EqualTo(100)); Assert.That(steam.Payload.Range, Is.EqualTo(1.25f));
            Assert.That(steam.Payload.TargetLimit, Is.EqualTo(4));
            Assert.That(storm.Payload.DamagePercent, Is.EqualTo(75)); Assert.That(storm.Payload.Range, Is.EqualTo(2f));
            Assert.That(storm.Payload.TargetLimit, Is.EqualTo(2));
            var historical = config.CreateSimulation(ReactionCatalog.Empty);
            Assert.That(historical.ReactionCatalog, Is.SameAs(ReactionCatalog.Empty));
            Assert.That(historical.TowerCatalog.Archetypes.Count, Is.EqualTo(5));
        }

        [TestCase("empty")] [TestCase("partial")] [TestCase("unknown")] [TestCase("hidden")]
        [TestCase("wrong-status")] [TestCase("wrong-payload")]
        public void RegBattle153_InvalidCatalogIsRejected(string invalid)
        {
            var config = Config(); var serialized = new SerializedObject(config);
            var catalog = serialized.FindProperty("towerArchetypes");
            Assert.That(catalog.arraySize, Is.EqualTo(6));
            switch (invalid)
            {
                case "empty": catalog.arraySize = 0; break;
                case "partial": catalog.arraySize = 5; break;
                case "unknown": catalog.GetArrayElementAtIndex(5).FindPropertyRelative("id").stringValue = "other"; break;
                case "hidden": catalog.GetArrayElementAtIndex(5).FindPropertyRelative("showInCatalog").boolValue = false; break;
                case "wrong-status": catalog.GetArrayElementAtIndex(0).FindPropertyRelative("levels").GetArrayElementAtIndex(0)
                    .FindPropertyRelative("statusEffect.kind").enumValueIndex = 1; break;
                default: catalog.GetArrayElementAtIndex(5).FindPropertyRelative("levels").GetArrayElementAtIndex(0)
                    .FindPropertyRelative("intrinsicReaction.kind").enumValueIndex = 0; break;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.Catch<ArgumentException>(() => config.CreateSimulation());
        }

        [TestCase("percent")] [TestCase("range")] [TestCase("limit")]
        [TestCase("disabled")] [TestCase("status")] [TestCase("penetration")] [TestCase("levels")]
        public void RegBattle153_InvalidHybridPayloadIsNeverReplacedWithDefaults(string invalid)
        {
            var config = Config(); var serialized = new SerializedObject(config);
            var levels = serialized.FindProperty("towerArchetypes").GetArrayElementAtIndex(4).FindPropertyRelative("levels");
            var level = levels.GetArrayElementAtIndex(0);
            switch (invalid)
            {
                case "percent": level.FindPropertyRelative("intrinsicReaction.damagePercent").intValue = 0; break;
                case "range": level.FindPropertyRelative("intrinsicReaction.range").floatValue = float.NaN; break;
                case "limit": level.FindPropertyRelative("intrinsicReaction.targetLimit").intValue = 5; break;
                case "disabled": level.FindPropertyRelative("hasIntrinsicReaction").boolValue = false; break;
                case "status": level.FindPropertyRelative("statusEffect.power").intValue = 1; break;
                case "penetration": level.FindPropertyRelative("armorPenetration").intValue = 1; break;
                default: levels.arraySize = 2; break;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.Catch<ArgumentException>(() => config.CreateSimulation());
        }

        private BattleScenarioConfig Config()
        {
            var c = BattleScenarioConfig.CreateDefault(); _configs.Add(c); return c;
        }
        private BattleSimulation Simulation(int hp = 1000, int gold = 2000)
        {
            var c = Config(); var serialized = new SerializedObject(c);
            serialized.FindProperty("maxHealth").intValue = hp;
            serialized.FindProperty("speed").floatValue = .01f;
            serialized.FindProperty("startingGold").intValue = gold;
            serialized.ApplyModifiedPropertiesWithoutUndo(); return c.CreateSimulation();
        }
        private static ReactionKind Kind(string id) => id == "steam" ? ReactionKind.Steam : ReactionKind.Storm;
        private static void Place(BattleSimulation s, string id) => Assert.That(s.PlaceTower(
            new PlaceTowerCommand(new TowerDefinitionId(id), Cell)).Success, Is.True, id);
        private static void AssertUnchanged(BattleSimulation s, Func<bool> command)
        {
            var before = JsonUtility.ToJson(s.ExportCheckpoint()); var grid = s.Grid;
            Assert.That(command(), Is.False); Assert.That(JsonUtility.ToJson(s.ExportCheckpoint()), Is.EqualTo(before));
            Assert.That(s.Grid, Is.SameAs(grid)); Assert.That(s.AttackEvents, Is.Empty);
            Assert.That(s.ReactionEvents, Is.Empty); Assert.That(s.DamageEvents, Is.Empty);
        }
        private WaveCheckpointStore Store()
        {
            _root = Path.GetFullPath(Path.Combine("TestResults", "Hybrids", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(_root); return new WaveCheckpointStore(_root);
        }
        private static WaveCheckpointEnvelope Envelope(BattleCheckpoint cp) => new WaveCheckpointEnvelope
            { schemaVersion = 2, scenarioContentId = "eight-wave-v1", state = cp };
        private void RoundTrip(BattleSimulation s, BattleCheckpoint cp)
        {
            Assert.That(WaveCheckpointStore.CurrentSchemaVersion, Is.EqualTo(2));
            var store = Store(); Assert.That(store.TrySave(Envelope(cp), s.TryValidateCheckpoint, out var error), Is.True, error);
            var bytes = File.ReadAllBytes(store.PrimaryPath);
            Assert.That(store.TryLoad("eight-wave-v1", s.TryValidateCheckpoint, out var load), Is.True, load.Error);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(store.PrimaryPath));
            var restored = Simulation(); restored.RestoreCheckpoint(load.Envelope.state);
            Assert.That(JsonUtility.ToJson(restored.ExportCheckpoint()), Is.EqualTo(JsonUtility.ToJson(cp)));
            foreach (var tower in restored.State.Towers)
                Assert.That(restored.Grid.GetState(tower.Coordinate), Is.EqualTo(GridCellState.Occupied));
            Assert.That(restored.State.Enemies, Is.Empty); Assert.That(restored.ReactionEvents, Is.Empty);
            Assert.That(restored.DamageEvents, Is.Empty);
            var json = System.Text.Encoding.UTF8.GetString(bytes);
            foreach (var forbidden in new[] { "enemies", "statuses", "lastSteamTick", "lastStormTick", "reactionEvents", "damageEvents" })
                Assert.That(json, Does.Not.Contain(forbidden));
        }
    }
}
