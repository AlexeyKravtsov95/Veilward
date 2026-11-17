using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Veilward.Domain;
using Veilward.Infrastructure;
using Veilward.Presentation;

namespace Veilward.Tests.EditMode
{
    public sealed class ElementProgressionStorageTests
    {
        private const string NativeId = "eight-wave-unlocks-v1";
        private const string LegacyId = "eight-wave-v1";
        private string _root;
        private WaveCheckpointStore _store;
        internal static BattleScenarioConfig Config() => AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWave.asset");
        private static BattleSimulation Native() => Config().CreateSimulation(BattleElementUnlockTests.Rules());
        private static WaveCheckpointEnvelope Envelope(BattleSimulation s, bool native = true) => new WaveCheckpointEnvelope
        { schemaVersion = WaveCheckpointStore.CurrentSchemaVersion, scenarioContentId = native ? NativeId : LegacyId, state = s.ExportCheckpoint() };
        private static string Wire(object value)
        {
            if (!(value is WaveCheckpointEnvelope e)) return JsonUtility.ToJson(value);
            if (e.schemaVersion == 4) return WaveCheckpointJson.Serialize(e);
            var json = JsonUtility.ToJson(e);
            // Реальный legacy shape: нет progression, а не inline defaults от Unity null serialization.
            if (e.schemaVersion < 3 && e.state.elementProgression == null)
                json = json.Substring(0, json.IndexOf(",\"elementProgression\":", StringComparison.Ordinal)) + "}}";
            return json;
        }
        [SetUp] public void SetUp()
        {
            _root = Path.GetFullPath(Path.Combine("TestResults", "ElementProgressionStorage", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(_root); _store = new WaveCheckpointStore(_root);
        }
        [TearDown] public void TearDown() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

        [TestCase("choicesEarned")] [TestCase("choicesSpent")]
        [TestCase("phase")] [TestCase("waveIndex")] [TestCase("tick")] [TestCase("kills")]
        public void RegBattle175_MissingZeroField_IsRejected(string field)
        {
            var s = Native(); var json = Wire(Envelope(s));
            json = Regex.Replace(json, "\\\"" + field + "\\\":0,", "");
            File.WriteAllText(_store.PrimaryPath, json);
            Assert.That(_store.TryLoad(NativeId, s.TryValidateCheckpoint, out var load), Is.False);
            Assert.That(load.Envelope, Is.Null);
        }

        [Test]
        public void RegBattle181_Schema2Migration_LoadIsReadOnlyAndNextSaveIsSchema4()
        {
            var s = Config().CreateHistoricalAllOpenSimulation(); var e = Envelope(s, false); e.schemaVersion = 2;
            File.WriteAllText(_store.PrimaryPath, Wire(e));
            var bytes = File.ReadAllBytes(_store.PrimaryPath);
            Assert.That(_store.TryLoad(LegacyId, s.TryValidateCheckpoint, out var load), Is.True, load.Error);
            Assert.That(load.Envelope.schemaVersion, Is.EqualTo(4));
            Assert.That(load.Envelope.state.elementProgression.rulesId, Is.EqualTo("legacy-all-open-v1"));
            Assert.That(File.ReadAllBytes(_store.PrimaryPath), Is.EqualTo(bytes));
            Assert.That(_store.TrySave(load.Envelope, s.TryValidateCheckpoint, out var error), Is.True, error);
            Assert.That(File.ReadAllText(_store.PrimaryPath), Does.Contain("\"schemaVersion\": 4"));
            Assert.That(File.ReadAllBytes(_store.BackupPath), Is.EqualTo(bytes));
        }

        [TestCase(true)] [TestCase(false)]
        public void RegBattle182_183_CrossModeBackup_RestoresWholeSnapshotWithoutWrites(bool nativeBackup)
        {
            var legacy = Config().CreateHistoricalAllOpenSimulation();
            var cp = legacy.ExportCheckpoint(); cp.gold = 10000; legacy.RestoreCheckpoint(cp);
            foreach (var id in new[] { "steam", "storm" })
                Assert.That(legacy.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId(id),
                    new GridCoordinate(id == "steam" ? -5 : -4, -3))).Success, Is.True);
            legacy.UpgradeTower(new TowerId(1));
            var old = Envelope(legacy, false); old.schemaVersion = 2;
            var native = Native(); native.ChooseElement(new ChooseElementCommand("start-resonance", "gale"));
            var fresh = Envelope(native);
            var backup = nativeBackup ? fresh : old; var bad = nativeBackup ? old : fresh;
            bad.state.elementProgression = null;
            if (!nativeBackup) bad.state.gold = -1;
            else bad.state.towers[0].investedGold++;
            File.WriteAllText(_store.PrimaryPath, Wire(bad));
            File.WriteAllText(_store.BackupPath, Wire(backup));
            var primaryBytes = File.ReadAllBytes(_store.PrimaryPath); var backupBytes = File.ReadAllBytes(_store.BackupPath);
            for (var repeat = 0; repeat < 2; repeat++)
            {
                Assert.That(_store.TryLoad(NativeId, Config().CreateSimulationForCheckpoint, out var load), Is.True, load.Error);
                Assert.That(load.Status, Is.EqualTo(WaveCheckpointLoadStatus.LoadedBackup));
                var restored = Config().CreateSimulationForCheckpoint(load.Envelope);
                restored.RestoreCheckpoint(load.Envelope.state);
                Assert.That(restored.State.Gold, Is.EqualTo(backup.state.gold));
                Assert.That(restored.State.Towers.Count, Is.EqualTo(nativeBackup ? 0 : 2));
                Assert.That(restored.State.ElementProgression.RulesId, Is.EqualTo(nativeBackup ? "wave-unlocks-v1" : "legacy-all-open-v1"));
                Assert.That(load.Envelope.scenarioContentId, Is.EqualTo(nativeBackup ? NativeId : LegacyId));
                Assert.That(File.ReadAllBytes(_store.PrimaryPath), Is.EqualTo(primaryBytes));
                Assert.That(File.ReadAllBytes(_store.BackupPath), Is.EqualTo(backupBytes));
            }
            // Повреждённый primary не вытесняет валидный backup при следующем Save.
            var toSave = nativeBackup ? Envelope(native) : Envelope(legacy, false);
            var validator = nativeBackup ? native : legacy;
            Assert.That(_store.TrySave(toSave, validator.TryValidateCheckpoint, out var error,
                Config().CreateSimulationForCheckpoint), Is.True, error);
            Assert.That(File.ReadAllBytes(_store.BackupPath), Is.EqualTo(backupBytes));
        }

        [TestCase(1, NativeId)] [TestCase(2, NativeId)] [TestCase(5, NativeId)]
        [TestCase(3, "unknown")] [TestCase(2, "unknown")]
        [TestCase(2, "legacy-single-wave-v1")]
        public void RegBattle181_UnknownOrWrongContent_IsRejectedWithoutFallback(int schema, string content)
        {
            var e = Envelope(Native()); e.schemaVersion = schema; e.scenarioContentId = content;
            File.WriteAllText(_store.PrimaryPath, Wire(e));
            File.WriteAllText(_store.BackupPath, "{}");
            Assert.That(_store.TryLoad(NativeId, Config().CreateSimulationForCheckpoint, out _), Is.False);
        }

        public static System.Collections.Generic.IEnumerable<TestCaseData> LegacyTowers()
        {
            foreach (var id in new[] { "flame", "tide", "gale", "stone", "steam", "storm", "test-tower" })
            for (var level = 1; level <= 3; level++) yield return new TestCaseData(id, level);
        }
        [TestCaseSource(nameof(LegacyTowers))]
        public void RegBattle181_Schema2_ExactTowerIdentityAtEverySafePhase(string type, int level)
        {
            var simulation = Config().CreateHistoricalAllOpenSimulation(); var initial = simulation.ExportCheckpoint();
            initial.gold = 10000; simulation.RestoreCheckpoint(initial);
            var tower = simulation.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId(type), new GridCoordinate(-5, -3))).Tower;
            for (var n = 1; n < level; n++) Assert.That(simulation.UpgradeTower(tower.Id).Success, Is.True);
            foreach (var phase in new[] { BattlePhase.Preparation, BattlePhase.WaveComplete, BattlePhase.Victory, BattlePhase.Defeat })
            foreach (var wave in new[] { 0, 2, 3, 5, 6, 7 })
            {
                if (phase == BattlePhase.Victory && wave != 7 || phase == BattlePhase.WaveComplete && wave == 7) continue;
                var e = Envelope(simulation, false); e.schemaVersion = 2;
                e.state.elementProgression = null; e.state.phase = phase; e.state.waveIndex = wave;
                e.state.baseHealth = phase == BattlePhase.Defeat ? 0 : 20; e.state.towers[0].nextAttackTick = 77;
                File.WriteAllText(_store.PrimaryPath, Wire(e)); var bytes = File.ReadAllBytes(_store.PrimaryPath);
                Assert.That(_store.TryLoad(NativeId, Config().CreateSimulationForCheckpoint, out var load), Is.True, load.Error);
                var restored = Config().CreateSimulationForCheckpoint(load.Envelope); restored.RestoreCheckpoint(load.Envelope.state);
                var actual = restored.ExportCheckpoint();
                Assert.That(Wire(actual.towers[0]), Is.EqualTo(Wire(e.state.towers[0])));
                Assert.That(actual.elementProgression.unlockedElementIds, Is.EqualTo(new[] { "flame", "tide", "gale", "stone" }));
                Assert.That(actual.elementProgression.choicesEarned, Is.Zero); Assert.That(actual.elementProgression.choicesSpent, Is.Zero);
                Assert.That(actual.elementProgression.spentChoices, Is.Empty); Assert.That(actual.elementProgression.pendingChoiceId, Is.EqualTo("none"));
                Assert.That(File.ReadAllBytes(_store.PrimaryPath), Is.EqualTo(bytes));
                if (phase == BattlePhase.Preparation) { restored.SellTower(tower.Id); Assert.That(restored.State.ElementProgression.UnlockedElements.Count, Is.EqualTo(4)); }
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void RegBattle181_Schema1_DeepCopyMapsLegacyTowersAndPreservesSource(bool withTower)
        {
            var legacy = Config().CreateHistoricalAllOpenSimulation();
            if (withTower) legacy.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId("test-tower"), new GridCoordinate(-5, -3)));
            var e = Envelope(legacy, false); e.schemaVersion = 1; e.state.elementProgression = null;
            if (withTower) e.state.towers[0].towerTypeId = null;
            var source = Wire(e);
            Assert.That(WaveCheckpointCompatibility.TryNormalize(e, NativeId, out var normalized, out var error), Is.True, error);
            Assert.That(Wire(e), Is.EqualTo(source));
            Assert.That(normalized.state, Is.Not.SameAs(e.state));
            if (withTower) { Assert.That(normalized.state.towers[0], Is.Not.SameAs(e.state.towers[0])); Assert.That(normalized.state.towers[0].towerTypeId, Is.EqualTo("test-tower")); }
            File.WriteAllText(_store.PrimaryPath, source);
            Assert.That(_store.TryLoad(NativeId, Config().CreateSimulationForCheckpoint, out var load), Is.True, load.Error);
            Assert.That(File.ReadAllText(_store.PrimaryPath), Is.EqualTo(source));
        }

        [TestCase("choicesEarned", "2147483648")] [TestCase("choicesSpent", "-2147483649")]
        [TestCase("choicesEarned", "0.0")] [TestCase("choicesSpent", "null")]
        [TestCase("choicesEarned", "false")] [TestCase("choicesSpent", "\"0\"")]
        [TestCase("choicesSpent", "0,\"choicesSpent\":0")]
        public void RegBattle175_MalformedNumericField_RejectsBeforeJsonDefaults(string field, string value)
        {
            var json = Wire(Envelope(Native())).Replace("\"" + field + "\":0", "\"" + field + "\":" + value);
            File.WriteAllText(_store.PrimaryPath, json);
            Assert.That(_store.TryLoad(NativeId, Config().CreateSimulationForCheckpoint, out _), Is.False);
        }

        [TestCase(false)] [TestCase(true)]
        public void RegBattle181_FactoryException_TriesBackupIndependently(bool returnNull)
        {
            var e = Envelope(Native()); File.WriteAllText(_store.PrimaryPath, Wire(e));
            var legacy = Envelope(Config().CreateHistoricalAllOpenSimulation(), false); File.WriteAllText(_store.BackupPath, Wire(legacy));
            BattleSimulation Factory(WaveCheckpointEnvelope candidate)
            {
                if (candidate.scenarioContentId == NativeId)
                { if (returnNull) return null; throw new ArgumentException("Fixture rejected native content"); }
                return Config().CreateSimulationForCheckpoint(candidate);
            }
            Assert.That(_store.TryLoad(NativeId, Factory, out var load), Is.True, load.Error);
            Assert.That(load.Status, Is.EqualTo(WaveCheckpointLoadStatus.LoadedBackup));
        }

        [Test]
        public void RegBattle181_ValidatorOverload_RetainsExactContentRestriction()
        {
            var legacy = Config().CreateHistoricalAllOpenSimulation();
            File.WriteAllText(_store.PrimaryPath, Wire(Envelope(legacy, false)));
            Assert.That(_store.TryLoad(NativeId, legacy.TryValidateCheckpoint, out _), Is.False);
        }

        [TestCase(true)] [TestCase(false)]
        public void RegBattle182_183_SavePreservesValidatedPreviousPrimaryOfOtherMode(bool nativeFirst)
        {
            var native = Native(); var legacy = Config().CreateHistoricalAllOpenSimulation();
            var first = nativeFirst ? native : legacy; var second = nativeFirst ? legacy : native;
            Assert.That(_store.TrySave(Envelope(first, nativeFirst), first.TryValidateCheckpoint, out var error,
                Config().CreateSimulationForCheckpoint), Is.True, error);
            var primary = File.ReadAllBytes(_store.PrimaryPath);
            Assert.That(_store.TrySave(Envelope(second, !nativeFirst), second.TryValidateCheckpoint, out error,
                Config().CreateSimulationForCheckpoint), Is.True, error);
            Assert.That(File.ReadAllBytes(_store.BackupPath), Is.EqualTo(primary));
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void RegBattle181_UnknownTowerType_IsRejectedInEverySchema(int schema)
        {
            var s = Config().CreateHistoricalAllOpenSimulation();
            s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId("test-tower"), new GridCoordinate(-5, -3)));
            var e = Envelope(s, false); e.schemaVersion = schema; e.state.towers[0].towerTypeId = "unknown";
            File.WriteAllText(_store.PrimaryPath, Wire(e));
            Assert.That(_store.TryLoad(NativeId, Config().CreateSimulationForCheckpoint, out _), Is.False);
        }

        [TestCase("rulesId")] [TestCase("startResonanceId")] [TestCase("unlockedElementIds")]
        [TestCase("spentChoices")] [TestCase("pendingChoiceId")] [TestCase("elementProgression")]
        [TestCase("state")] [TestCase("towers")]
        public void RegBattle175_MissingObjectsStringsOrArrays_Rejects(string field)
        {
            var json = Wire(Envelope(Native()));
            // Переименование key сохраняет валидный JSON, но обязательное поле отсутствует.
            json = json.Replace("\"" + field + "\":", "\"absent_" + field + "\":");
            File.WriteAllText(_store.PrimaryPath, json);
            Assert.That(_store.TryLoad(NativeId, Config().CreateSimulationForCheckpoint, out _), Is.False);
        }

        [TestCase(1)] [TestCase(2)]
        public void RegBattle181_ValidRawSingleWave_LoadsOnlyAsSingleWaveWithoutProgressionFallback(int schema)
        {
            var config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/TestWave.asset");
            var single = config.CreateSimulation();
            var e = new WaveCheckpointEnvelope { schemaVersion = schema, scenarioContentId = "legacy-single-wave-v1", state = single.ExportCheckpoint() };
            var json = Wire(e);
            // Настоящий старый shape: progression key отсутствует, все scalar zero fields присутствуют.
            json = json.Substring(0, json.IndexOf(",\"elementProgression\":", StringComparison.Ordinal)) + "}}";
            File.WriteAllText(_store.PrimaryPath, json); var bytes = File.ReadAllBytes(_store.PrimaryPath);
            Assert.That(_store.TryLoad("legacy-single-wave-v1", config.CreateSimulationForCheckpoint, out var load), Is.True, load.Error);
            Assert.That(load.Envelope.scenarioContentId, Is.EqualTo("legacy-single-wave-v1"));
            var restored = config.CreateSimulationForCheckpoint(load.Envelope); restored.RestoreCheckpoint(load.Envelope.state);
            Assert.That(restored.State.WaveCount, Is.EqualTo(1));
            Assert.That(restored.State.ElementProgression.RulesId, Is.EqualTo("legacy-all-open-v1"));
            Assert.That(_store.TryLoad(NativeId, Config().CreateSimulationForCheckpoint, out _), Is.False);
            Assert.That(File.ReadAllBytes(_store.PrimaryPath), Is.EqualTo(bytes));
        }
    }
}
