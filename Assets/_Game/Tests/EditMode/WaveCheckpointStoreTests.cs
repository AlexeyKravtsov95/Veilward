using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Veilward.Domain;
using Veilward.Infrastructure;
using Veilward.Presentation;

namespace Veilward.Tests.EditMode
{
    public sealed class WaveCheckpointStoreTests
    {
        private const string ContentId = "test-scenario-v1";
        private string _root;
        private WaveCheckpointStore _store;
        private BattleSimulation _simulation;

        [SetUp]
        public void SetUp()
        {
            _root = Path.GetFullPath(Path.Combine("TestResults", "CheckpointStoreTests", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(_root);
            _store = new WaveCheckpointStore(_root);
            _simulation = BattleScenarioConfig.CreateDefault().CreateSimulation();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        [Test]
        public void SaveTwoCheckpoints_ReplacesPrimaryAndPreservesPreviousAsBackup()
        {
            AssertSave(CreateEnvelope(150));
            AssertSave(CreateEnvelope(90));

            Assert.That(File.ReadAllText(_store.PrimaryPath), Does.Contain("\"gold\": 90"));
            Assert.That(File.ReadAllText(_store.BackupPath), Does.Contain("\"gold\": 150"));
            Assert.That(File.Exists(_store.TempPath), Is.False);
            AssertLoad(90, WaveCheckpointLoadStatus.LoadedPrimary);
        }

        [Test]
        public void TruncatedPrimary_LoadsCompatibleBackup()
        {
            AssertSave(CreateEnvelope(150));
            AssertSave(CreateEnvelope(90));
            File.WriteAllText(_store.PrimaryPath, "{truncated");

            Assert.That(_store.TryLoad(ContentId, Validate, out var load), Is.True, load.Error);
            Assert.That(load.Status, Is.EqualTo(WaveCheckpointLoadStatus.LoadedBackup));
            Assert.That(load.Envelope.state.gold, Is.EqualTo(150));
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void InvalidNextIdInPrimary_LoadsCompatibleBackup(bool invalidEnemyId, bool invalidTowerId)
        {
            AssertSave(CreateEnvelope(150));
            AssertSave(CreateEnvelope(90));
            var damaged = CreateEnvelope(90);
            if (invalidEnemyId)
            {
                damaged.state.nextEnemyId = int.MaxValue;
            }

            if (invalidTowerId)
            {
                damaged.state.nextTowerId = int.MaxValue;
            }

            File.WriteAllText(_store.PrimaryPath, JsonUtility.ToJson(damaged, prettyPrint: true));

            Assert.That(_store.TryLoad(ContentId, Validate, out var load), Is.True, load.Error);
            Assert.That(load.Status, Is.EqualTo(WaveCheckpointLoadStatus.LoadedBackup));
            Assert.That(load.Envelope.state.gold, Is.EqualTo(150));
        }

        [Test]
        public void CorruptPrimaryAndBackup_ReturnsCorrupt()
        {
            AssertSave(CreateEnvelope(150));
            AssertSave(CreateEnvelope(90));
            File.WriteAllText(_store.PrimaryPath, "{truncated");
            File.WriteAllText(_store.BackupPath, "not-json");

            Assert.That(_store.TryLoad(ContentId, Validate, out var load), Is.False);
            Assert.That(load.Status, Is.EqualTo(WaveCheckpointLoadStatus.Corrupt));
            Assert.That(load.Envelope, Is.Null);
        }

        [TestCase(3, ContentId)]
        [TestCase(WaveCheckpointStore.CurrentSchemaVersion, "other-scenario-v1")]
        public void UnknownSchemaOrContent_IsIncompatible(int schemaVersion, string contentId)
        {
            AssertSave(CreateEnvelope(150));
            var incompatible = CreateEnvelope(150);
            incompatible.schemaVersion = schemaVersion;
            incompatible.scenarioContentId = contentId;
            File.WriteAllText(_store.PrimaryPath, JsonUtility.ToJson(incompatible));

            Assert.That(_store.TryLoad(ContentId, Validate, out var load), Is.False);
            Assert.That(load.Status, Is.EqualTo(WaveCheckpointLoadStatus.Incompatible));
        }

        [Test]
        public void SaveWithDamagedPrimary_DoesNotOverwriteValidBackup()
        {
            AssertSave(CreateEnvelope(150));
            AssertSave(CreateEnvelope(90));
            var backup = File.ReadAllBytes(_store.BackupPath);
            File.WriteAllText(_store.PrimaryPath, "{damaged");

            AssertSave(CreateEnvelope(45));

            Assert.That(File.ReadAllBytes(_store.BackupPath), Is.EqualTo(backup));
            AssertLoad(45, WaveCheckpointLoadStatus.LoadedPrimary);
        }

        [Test]
        public void WriteFailure_KeepsPrimaryAndBackupByteIdentical()
        {
            AssertSave(CreateEnvelope(150));
            AssertSave(CreateEnvelope(90));
            var primary = File.ReadAllBytes(_store.PrimaryPath);
            var backup = File.ReadAllBytes(_store.BackupPath);
            Directory.CreateDirectory(_store.TempPath);

            Assert.That(_store.TrySave(CreateEnvelope(45), Validate, out var error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(File.ReadAllBytes(_store.PrimaryPath), Is.EqualTo(primary));
            Assert.That(File.ReadAllBytes(_store.BackupPath), Is.EqualTo(backup));
        }

        [Test]
        public void InvalidCheckpoint_IsRejectedBeforeCreatingFiles()
        {
            var envelope = CreateEnvelope(150);
            envelope.state.baseHealth = -1;

            Assert.That(_store.TrySave(envelope, Validate, out _), Is.False);
            Assert.That(File.Exists(_store.PrimaryPath), Is.False);
            Assert.That(File.Exists(_store.BackupPath), Is.False);
            Assert.That(File.Exists(_store.TempPath), Is.False);
        }

        [Test]
        public void MissingAndDelete_ReportSlotStateAndRemoveAllFiles()
        {
            Assert.That(_store.TryLoad(ContentId, Validate, out var missing), Is.False);
            Assert.That(missing.Status, Is.EqualTo(WaveCheckpointLoadStatus.Missing));
            AssertSave(CreateEnvelope(150));
            File.WriteAllText(_store.TempPath, "stale");

            Assert.That(_store.Delete(out var error), Is.True, error);
            Assert.That(File.Exists(_store.PrimaryPath), Is.False);
            Assert.That(File.Exists(_store.BackupPath), Is.False);
            Assert.That(File.Exists(_store.TempPath), Is.False);
        }

        private bool Validate(BattleCheckpoint checkpoint, out string error)
        {
            return _simulation.TryValidateCheckpoint(checkpoint, out error);
        }

        private void AssertSave(WaveCheckpointEnvelope envelope)
        {
            Assert.That(_store.TrySave(envelope, Validate, out var error), Is.True, error);
        }

        private void AssertLoad(int expectedGold, WaveCheckpointLoadStatus expectedStatus)
        {
            Assert.That(_store.TryLoad(ContentId, Validate, out var load), Is.True, load.Error);
            Assert.That(load.Status, Is.EqualTo(expectedStatus));
            Assert.That(load.Envelope.state.gold, Is.EqualTo(expectedGold));
        }

        private static WaveCheckpointEnvelope CreateEnvelope(int gold)
        {
            return new WaveCheckpointEnvelope
            {
                schemaVersion = WaveCheckpointStore.CurrentSchemaVersion,
                scenarioContentId = ContentId,
                state = new BattleCheckpoint
                {
                    phase = BattlePhase.Preparation,
                    waveIndex = 0,
                    tick = 0,
                    baseHealth = 20,
                    gold = gold,
                    kills = 0,
                    nextEnemyId = 1,
                    nextTowerId = 1,
                    towers = Array.Empty<TowerCheckpoint>()
                }
            };
        }
    }
}
