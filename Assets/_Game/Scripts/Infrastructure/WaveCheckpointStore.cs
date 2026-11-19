using System;
using System.IO;
using System.Security;
using System.Text;
using UnityEngine;
using Veilward.Domain;

namespace Veilward.Infrastructure
{
    public delegate bool WaveCheckpointValidator(BattleCheckpoint checkpoint, out string error);

    public sealed class WaveCheckpointStore
    {
        public const int CurrentSchemaVersion = 4;
        private readonly IWaveCheckpointWriter _writer;
        private const string PrimaryFileName = "wave-checkpoint-v1.json";

        public WaveCheckpointStore(string rootDirectory) : this(rootDirectory, new FileWaveCheckpointWriter()) { }

        internal WaveCheckpointStore(string rootDirectory, IWaveCheckpointWriter writer)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            if (string.IsNullOrWhiteSpace(rootDirectory))
            {
                throw new ArgumentException("Checkpoint root directory is required.", nameof(rootDirectory));
            }

            RootDirectory = Path.GetFullPath(rootDirectory);
            PrimaryPath = Path.Combine(RootDirectory, PrimaryFileName);
            BackupPath = PrimaryPath + ".bak";
            TempPath = PrimaryPath + ".tmp";
        }

        public string RootDirectory { get; }
        public string PrimaryPath { get; }
        public string BackupPath { get; }
        public string TempPath { get; }

        public bool TrySave(
            WaveCheckpointEnvelope envelope,
            WaveCheckpointValidator validator,
            out string error,
            Func<WaveCheckpointEnvelope, BattleSimulation> simulationFactory = null)
        {
            RequireValidator(validator);
            if (!TryValidateEnvelope(envelope, envelope?.scenarioContentId, validator, out error))
            {
                return false;
            }

            if (simulationFactory != null)
            {
                if (!WaveCheckpointCompatibility.TryNormalize(envelope, envelope.scenarioContentId, out var normalized, out error)) return false;
                try
                {
                    var simulation = simulationFactory(normalized);
                    if (simulation == null) throw new InvalidOperationException("Checkpoint factory returned no simulation.");
                    if (!simulation.TryValidateCheckpoint(normalized.state, out error)) return false;
                }
                catch (Exception e) when (IsValidationException(e)) { error = e.Message; return false; }
            }
            // Каждый previous primary проверяется собственной factory, включая new→old session.
            var eightWave = WaveCheckpointCompatibility.IsAbilityBossContent(envelope.scenarioContentId) ||
                envelope.scenarioContentId == WaveCheckpointCompatibility.NativeContentId ||
                envelope.scenarioContentId == WaveCheckpointCompatibility.LegacyEightWaveContentId;
            var requestedId = simulationFactory != null && eightWave
                ? WaveCheckpointCompatibility.BalancedAbilityBossContentId : envelope.scenarioContentId;
            try
            {
                Directory.CreateDirectory(RootDirectory);
                var json = WaveCheckpointJson.Serialize(envelope, pretty: true);
                _writer.WriteTemporary(TempPath, json);

                if (ReadEnvelope(TempPath, requestedId, validator, simulationFactory, out _, out error) !=
                    FileReadStatus.Valid)
                {
                    error = "Checkpoint temporary file verification failed: " + error;
                    return false;
                }

                if (!File.Exists(PrimaryPath))
                {
                    _writer.Commit(TempPath, PrimaryPath, null);
                }
                else
                {
                    var primaryStatus = ReadEnvelope(
                        PrimaryPath,
                        requestedId,
                        validator,
                        simulationFactory,
                        out _,
                        out _);
                    _writer.Commit(
                        TempPath,
                        PrimaryPath,
                        primaryStatus == FileReadStatus.Valid ? BackupPath : null);
                }

                error = null;
                return true;
            }
            catch (Exception exception) when (IsFileSystemException(exception))
            {
                error = "Checkpoint save failed: " + exception.Message;
                return false;
            }
        }

        public bool TryLoad(
            string scenarioContentId,
            WaveCheckpointValidator validator,
            out WaveCheckpointLoadResult result)
        {
            RequireValidator(validator);
            return TryLoadCore(scenarioContentId, validator, null, out result);
        }

        public bool TryLoad(string requestedScenarioContentId,
            Func<WaveCheckpointEnvelope, BattleSimulation> simulationFactory, out WaveCheckpointLoadResult result)
        {
            if (simulationFactory == null) throw new ArgumentNullException(nameof(simulationFactory));
            return TryLoadCore(requestedScenarioContentId, null, simulationFactory, out result);
        }

        private bool TryLoadCore(string requestedId, WaveCheckpointValidator validator,
            Func<WaveCheckpointEnvelope, BattleSimulation> factory, out WaveCheckpointLoadResult result)
        {
            var primaryStatus = ReadEnvelope(PrimaryPath, requestedId, validator, factory, out var primary, out var primaryError);
            if (primaryStatus == FileReadStatus.Valid)
            {
                result = new WaveCheckpointLoadResult(WaveCheckpointLoadStatus.LoadedPrimary, primary, null);
                return true;
            }
            var backupStatus = ReadEnvelope(BackupPath, requestedId, validator, factory, out var backup, out var backupError);
            if (backupStatus == FileReadStatus.Valid)
            {
                result = new WaveCheckpointLoadResult(WaveCheckpointLoadStatus.LoadedBackup, backup, primaryError);
                return true;
            }
            result = new WaveCheckpointLoadResult(CombineFailureStatuses(primaryStatus, backupStatus), null,
                string.IsNullOrEmpty(primaryError) ? backupError : primaryError);
            return false;
        }

        public bool Delete(out string error)
        {
            var hasRecoveryCopy = false;
            try
            {
                File.Delete(TempPath);
                var recoverySource = File.Exists(PrimaryPath)
                    ? PrimaryPath
                    : File.Exists(BackupPath) ? BackupPath : null;
                if (recoverySource != null)
                {
                    File.Copy(recoverySource, TempPath);
                    hasRecoveryCopy = true;
                }

                File.Delete(PrimaryPath);
                File.Delete(BackupPath);
                File.Delete(TempPath);
                error = null;
                return true;
            }
            catch (Exception exception) when (IsFileSystemException(exception))
            {
                try
                {
                    if (hasRecoveryCopy && File.Exists(TempPath) &&
                        !File.Exists(PrimaryPath) && !File.Exists(BackupPath))
                    {
                        File.Move(TempPath, PrimaryPath);
                    }
                }
                catch (Exception recoveryException) when (IsFileSystemException(recoveryException))
                {
                    error = "Checkpoint delete failed: " + exception.Message +
                            " Recovery restore failed: " + recoveryException.Message;
                    return false;
                }

                error = "Checkpoint delete failed: " + exception.Message;
                return false;
            }
        }

        private static FileReadStatus ReadEnvelope(string path, string requestedId,
            WaveCheckpointValidator validator, Func<WaveCheckpointEnvelope, BattleSimulation> factory,
            out WaveCheckpointEnvelope envelope, out string error)
        {
            envelope = null;
            error = null;
            try
            {
                if (!File.Exists(path)) return FileReadStatus.Missing;
                var json = File.ReadAllText(path);
                if (!WaveCheckpointJson.HasRequiredFields(json, out error)) return FileReadStatus.Corrupt;
                WaveCheckpointEnvelope candidate;
                try { candidate = WaveCheckpointJson.Deserialize(json); }
                catch (ArgumentException e) { error = e.Message; return FileReadStatus.Corrupt; }
                if (factory == null && candidate.scenarioContentId != requestedId)
                { error = "Checkpoint scenario content is incompatible."; return FileReadStatus.Incompatible; }
                if (!WaveCheckpointCompatibility.TryNormalize(candidate, requestedId, factory == null,
                        out var normalized, out error)) return FileReadStatus.Incompatible;
                try
                {
                    var simulation = factory?.Invoke(normalized);
                    if (factory != null && simulation == null) throw new InvalidOperationException("Checkpoint factory returned no simulation.");
                    WaveCheckpointValidator validate = factory == null ? validator : simulation.TryValidateCheckpoint;
                    if (!validate(normalized.state, out error)) return FileReadStatus.Corrupt;
                }
                catch (Exception e) when (IsValidationException(e))
                { error = e.Message; return FileReadStatus.Corrupt; }
                envelope = normalized;
                return FileReadStatus.Valid;
            }
            catch (Exception e) when (IsFileSystemException(e))
            { error = e.Message; return FileReadStatus.IoError; }
        }

        private static bool IsValidationException(Exception e) => e is ArgumentException ||
            e is InvalidOperationException || e is NotSupportedException || e is OverflowException;

        private static bool TryValidateEnvelope(
            WaveCheckpointEnvelope envelope,
            string scenarioContentId,
            WaveCheckpointValidator validator,
            out string error)
        {
            if (envelope == null || envelope.state == null)
            {
                error = "Checkpoint envelope and state are required.";
                return false;
            }

            if (envelope.schemaVersion != CurrentSchemaVersion ||
                string.IsNullOrEmpty(envelope.scenarioContentId) ||
                !string.Equals(envelope.scenarioContentId, scenarioContentId, StringComparison.Ordinal))
            {
                error = "Checkpoint schema or scenario content is incompatible.";
                return false;
            }

            if (!WaveCheckpointCompatibility.TryNormalize(envelope, scenarioContentId, true, out _, out error) ||
                !WaveCheckpointJson.HasRequiredFields(WaveCheckpointJson.Serialize(envelope), out error)) return false;
            try { return validator(envelope.state, out error); }
            catch (Exception e) when (IsValidationException(e)) { error = e.Message; return false; }
        }

        private static WaveCheckpointLoadStatus CombineFailureStatuses(
            FileReadStatus primary,
            FileReadStatus backup)
        {
            if (primary == FileReadStatus.IoError || backup == FileReadStatus.IoError)
            {
                return WaveCheckpointLoadStatus.IoError;
            }

            if (primary == FileReadStatus.Incompatible || backup == FileReadStatus.Incompatible)
            {
                return WaveCheckpointLoadStatus.Incompatible;
            }

            if (primary == FileReadStatus.Corrupt || backup == FileReadStatus.Corrupt)
            {
                return WaveCheckpointLoadStatus.Corrupt;
            }

            return WaveCheckpointLoadStatus.Missing;
        }

        private static void RequireValidator(WaveCheckpointValidator validator)
        {
            if (validator == null)
            {
                throw new ArgumentNullException(nameof(validator));
            }
        }

        private static bool IsFileSystemException(Exception exception)
        {
            return exception is IOException || exception is UnauthorizedAccessException ||
                   exception is SecurityException || exception is NotSupportedException;
        }

        private enum FileReadStatus
        {
            Missing,
            Valid,
            Incompatible,
            Corrupt,
            IoError
        }
    }

    internal interface IWaveCheckpointWriter
    {
        void WriteTemporary(string path, string json);
        void Commit(string temporary, string primary, string backup);
    }

    internal sealed class FileWaveCheckpointWriter : IWaveCheckpointWriter
    {
        public void WriteTemporary(string path, string json)
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
        }
        public void Commit(string temporary, string primary, string backup)
        {
            if (File.Exists(primary)) File.Replace(temporary, primary, backup);
            else File.Move(temporary, primary);
        }
    }

}
