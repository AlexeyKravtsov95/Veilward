using System;
using System.IO;
using System.Collections.Generic;
using NUnit.Framework;
using Veilward.Domain;
using Veilward.Infrastructure;

namespace Veilward.Tests.EditMode
{
    public sealed class AbilityCheckpointStorageTests
    {
        private string _root;
        private WaveCheckpointStore _store;
        [SetUp] public void Setup() { _root = Path.Combine("TestResults", "AbilityStorage", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_root); _store = new WaveCheckpointStore(_root); }
        [TearDown] public void Cleanup() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
        public static IEnumerable<TestCaseData> Malformed()
        {
            foreach (var field in new[] { "rulesId", "abilityId", "cooldownRemainingTicks", "nextCastSequence" })
            { yield return new TestCaseData(field, "missing"); yield return new TestCaseData(field, "null"); }
            foreach (var value in new[] { "true", "0.0", "1e0", "9223372036854775808", "\"1\"", "-1", "0" })
                yield return new TestCaseData("nextCastSequence", value);
            foreach (var value in new[] { "false", "0.0", "2147483648", "-1", "601" })
                yield return new TestCaseData("cooldownRemainingTicks", value);
            yield return new TestCaseData("rulesId", "\"unknown\"");
            yield return new TestCaseData("abilityId", "\"none\"");
            yield return new TestCaseData("ability", "null");
            yield return new TestCaseData("ability", "missing");
            yield return new TestCaseData("extra", "1");
            yield return new TestCaseData("duplicate", "1");
        }
        [TestCaseSource(nameof(Malformed))]
        public void RegBattle199_MalformedWireRejectsWithoutWrites(string field, string value)
        {
            var s = AbilityCheckpointTests.Create(); var json = AbilityCheckpointTests.Snapshot(s);
            var key = "\"" + field + "\":";
            if (value == "missing") json = json.Replace(key, "\"absent_" + field + "\":");
            else if (field == "extra") json = json.Replace("\"ability\":{", "\"ability\":{\"targets\":1,");
            else if (field == "duplicate") json = json.Replace("\"nextCastSequence\":1", "\"nextCastSequence\":1,\"nextCastSequence\":1");
            else if (field == "ability") json = json.Replace("\"ability\":{\"rulesId\":\"rift-seal-v1\",\"abilityId\":\"rift-seal\",\"cooldownRemainingTicks\":0,\"nextCastSequence\":1}", "\"ability\":null");
            else
            {
                var old = field == "rulesId" ? "\"rift-seal-v1\"" : field == "abilityId" ? "\"rift-seal\"" : field == "nextCastSequence" ? "1" : "0";
                json = json.Replace(key + old, key + value);
            }
            Assert.That(json, Is.Not.EqualTo(AbilityCheckpointTests.Snapshot(s)), "corruption must alter wire");
            File.WriteAllText(_store.PrimaryPath, json); var bytes = File.ReadAllBytes(_store.PrimaryPath);
            Assert.That(_store.TryLoad(AbilityCheckpointTests.Content, s.TryValidateCheckpoint, out _), Is.False);
            Assert.That(File.ReadAllBytes(_store.PrimaryPath), Is.EqualTo(bytes)); Assert.That(File.Exists(_store.TempPath), Is.False);
        }
        [Test]
        public void RegBattle201_SyntheticValidatedBackupAndNoWriteLoad()
        {
            var s = AbilityCheckpointTests.Create(); AbilityCheckpointTests.Choose(s);
            Assert.That(_store.TrySave(AbilityCheckpointTests.Envelope(s), s.TryValidateCheckpoint, out var error), Is.True, error);
            File.Copy(_store.PrimaryPath, _store.BackupPath); File.WriteAllText(_store.PrimaryPath, "{}");
            var backup = File.ReadAllBytes(_store.BackupPath);
            for (var i = 0; i < 2; i++)
            {
                Assert.That(_store.TryLoad(AbilityCheckpointTests.Content, s.TryValidateCheckpoint, out var load), Is.True, load.Error);
                Assert.That(load.Status, Is.EqualTo(WaveCheckpointLoadStatus.LoadedBackup));
                s.RestoreCheckpoint(load.Envelope.state);
                Assert.That(File.ReadAllText(_store.PrimaryPath), Is.EqualTo("{}"));
            }
            Assert.That(_store.TrySave(AbilityCheckpointTests.Envelope(s), s.TryValidateCheckpoint, out error), Is.True, error);
            Assert.That(File.ReadAllBytes(_store.BackupPath), Is.EqualTo(backup));
            Assert.That(_store.TryLoad(AbilityCheckpointTests.Content, ElementProgressionStorageTests.Config().CreateSimulationForCheckpoint, out _), Is.False);
        }
        [TestCase(1, "eight-wave-v1")] [TestCase(2, "eight-wave-v1")] [TestCase(3, "eight-wave-v1")] [TestCase(4, "eight-wave-v1")]
        [TestCase(1, "legacy-single-wave-v1")] [TestCase(2, "legacy-single-wave-v1")] [TestCase(3, "legacy-single-wave-v1")] [TestCase(4, "legacy-single-wave-v1")]
        [TestCase(3, "eight-wave-unlocks-v1")] [TestCase(4, "eight-wave-unlocks-v1")]
        public void RegBattle200_SourceMatrixIdempotentNoWriteThenSave4SameId(int schema, string id)
        {
            var config = id == "legacy-single-wave-v1" ? UnityEditor.AssetDatabase.LoadAssetAtPath<Veilward.Presentation.BattleScenarioConfig>("Assets/_Game/Configs/TestWave.asset") : ElementProgressionStorageTests.Config();
            var s = id == "eight-wave-unlocks-v1" ? config.CreateSimulation() : config.CreateHistoricalAllOpenSimulation();
            var e = AbilityCheckpointTests.Envelope(s, id); e.schemaVersion = schema;
            var json = schema == 4 ? WaveCheckpointJson.Serialize(e) : UnityEngine.JsonUtility.ToJson(e);
            File.WriteAllText(_store.PrimaryPath, json); var bytes = File.ReadAllBytes(_store.PrimaryPath);
            Assert.That(_store.TryLoad(id, config.CreateSimulationForCheckpoint, out var load), Is.True, load.Error);
            Assert.That(load.Envelope.schemaVersion, Is.EqualTo(4)); Assert.That(load.Envelope.scenarioContentId, Is.EqualTo(id));
            Assert.That(load.Envelope.state.ability.RulesId, Is.EqualTo("ability-disabled-v1"));
            Assert.That(WaveCheckpointCompatibility.TryNormalize(load.Envelope, id, out var twice, out _), Is.True);
            Assert.That(WaveCheckpointJson.Serialize(twice), Is.EqualTo(WaveCheckpointJson.Serialize(load.Envelope)));
            Assert.That(File.ReadAllBytes(_store.PrimaryPath), Is.EqualTo(bytes));
            var restored = config.CreateSimulationForCheckpoint(load.Envelope); restored.RestoreCheckpoint(load.Envelope.state);
            Assert.That(_store.TrySave(load.Envelope, restored.TryValidateCheckpoint, out var error, config.CreateSimulationForCheckpoint), Is.True, error);
            Assert.That(File.ReadAllText(_store.PrimaryPath), Does.Contain("\"schemaVersion\": 4"));
            Assert.That(File.ReadAllBytes(_store.BackupPath), Is.EqualTo(bytes));
        }
        [TestCase(1, "eight-wave-unlocks-v1")] [TestCase(2, "eight-wave-unlocks-v1")]
        [TestCase(3, AbilityCheckpointTests.Content)] [TestCase(4, "unknown")]
        public void RegBattle200_ImpossibleSourcePairsRejected(int schema, string id)
        {
            var e = AbilityCheckpointTests.Envelope(AbilityCheckpointTests.Create(), id); e.schemaVersion = schema;
            File.WriteAllText(_store.PrimaryPath, WaveCheckpointJson.Serialize(e)); var bytes = File.ReadAllBytes(_store.PrimaryPath);
            Assert.That(_store.TryLoad(id, AbilityCheckpointTests.Create().TryValidateCheckpoint, out _), Is.False);
            Assert.That(File.ReadAllBytes(_store.PrimaryPath), Is.EqualTo(bytes));
        }
        [Test]
        public void RegBattle200_EnabledOldProductionIdAndDamagedSourceProgressionReject()
        {
            var s = ElementProgressionStorageTests.Config().CreateHistoricalAllOpenSimulation();
            var e = AbilityCheckpointTests.Envelope(s, "eight-wave-v1");
            e.state.ability = new AbilityCheckpoint("rift-seal-v1", "rift-seal", 0, 1);
            Assert.That(_store.TrySave(e, s.TryValidateCheckpoint, out _), Is.False);
            e.schemaVersion = 2; e.state.ability = AbilityCheckpoint.Disabled(); e.state.elementProgression.choicesEarned = 1;
            File.WriteAllText(_store.PrimaryPath, WaveCheckpointJson.Serialize(e));
            Assert.That(_store.TryLoad("eight-wave-v1", s.TryValidateCheckpoint, out _), Is.False);
        }
        private sealed class FaultWriter : IWaveCheckpointWriter
        {
            private readonly int _fault;
            private readonly FileWaveCheckpointWriter _files = new FileWaveCheckpointWriter();
            public FaultWriter(int fault) { _fault = fault; }
            public void WriteTemporary(string path, string json)
            {
                if (_fault == 0) throw new IOException("synthetic write failure");
                _files.WriteTemporary(path, _fault == 1 ? "{}" : json);
            }
            public void Commit(string temporary, string primary, string backup)
            {
                if (_fault == 2) throw new IOException("synthetic commit failure");
                _files.Commit(temporary, primary, backup);
            }
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void RegBattle202_FailedWriteReadbackCommitPreservesSlotsAndRetriesWholeSnapshot(int fault)
        {
            var s = AbilityCheckpointTests.Create();
            Assert.That(_store.TrySave(AbilityCheckpointTests.Envelope(s), s.TryValidateCheckpoint, out var error), Is.True, error);
            AbilityCheckpointTests.Choose(s);
            Assert.That(_store.TrySave(AbilityCheckpointTests.Envelope(s), s.TryValidateCheckpoint, out error), Is.True, error);
            var primary = File.ReadAllBytes(_store.PrimaryPath); var backup = File.ReadAllBytes(_store.BackupPath);
            s.StartWave(); s.Step(); Assert.That(s.CastAbility(AbilityCheckpointTests.Command(s)).Succeeded, Is.True);
            var expected = AbilityCheckpointTests.Snapshot(s);
            var faulty = new WaveCheckpointStore(_root, new FaultWriter(fault));
            Assert.That(faulty.TrySave(AbilityCheckpointTests.Envelope(s), s.TryValidateCheckpoint, out _), Is.False);
            Assert.That(File.ReadAllBytes(_store.PrimaryPath), Is.EqualTo(primary)); Assert.That(File.ReadAllBytes(_store.BackupPath), Is.EqualTo(backup));
            Assert.That(AbilityCheckpointTests.Snapshot(s), Is.EqualTo(expected));
            Assert.That(_store.TrySave(AbilityCheckpointTests.Envelope(s), s.TryValidateCheckpoint, out error), Is.True, error);
            Assert.That(_store.TryLoad(AbilityCheckpointTests.Content, s.TryValidateCheckpoint, out var load), Is.True, load.Error);
            Assert.That(WaveCheckpointJson.Serialize(load.Envelope), Is.EqualTo(expected));
        }
        [TestCase(AbilityCheckpointTests.Content)] [TestCase("unknown")]
        public void RegBattle200_ProductionSessionRejectsUnactivatedContentWithoutReplacingPending(string id)
        {
            Veilward.Application.BattleSessionContext.Clear();
            try
            {
                Veilward.Application.BattleSessionContext.SetNewGame(_store, "eight-wave-unlocks-v1");
                Assert.Throws<ArgumentException>(() => Veilward.Application.BattleSessionContext.SetNewGame(_store, id));
                Assert.Throws<ArgumentException>(() => Veilward.Application.BattleSessionContext.SetContinue(_store, AbilityCheckpointTests.Envelope(AbilityCheckpointTests.Create(), id)));
                Assert.That(Veilward.Application.BattleSessionContext.Consume().ScenarioContentId, Is.EqualTo("eight-wave-unlocks-v1"));
                var legacy = ElementProgressionStorageTests.Config().CreateHistoricalAllOpenSimulation();
                Assert.That(_store.TrySave(AbilityCheckpointTests.Envelope(legacy, "unknown"), legacy.TryValidateCheckpoint, out _), Is.False);
            }
            finally { Veilward.Application.BattleSessionContext.Clear(); }
        }
        [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void RegBattle200_FinalIdOldSchemasRejectedAfterActivation(int schema)
        {
            var s = AbilityCheckpointTests.Create(); var e = AbilityCheckpointTests.Envelope(s, "eight-wave-ability-boss-v1"); e.schemaVersion = schema;
            File.WriteAllText(_store.PrimaryPath, WaveCheckpointJson.Serialize(e));
            Assert.That(_store.TryLoad(e.scenarioContentId, s.TryValidateCheckpoint, out _), Is.False);
        }
    }
}
