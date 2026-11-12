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
        public const int CurrentSchemaVersion = 1;
        private const string PrimaryFileName = "wave-checkpoint-v1.json";

        public WaveCheckpointStore(string rootDirectory)
        {
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
            out string error)
        {
            RequireValidator(validator);
            if (!TryValidateEnvelope(envelope, envelope?.scenarioContentId, validator, out error))
            {
                return false;
            }

            try
            {
                Directory.CreateDirectory(RootDirectory);
                var json = JsonUtility.ToJson(envelope, prettyPrint: true);
                using (var stream = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
                {
                    writer.Write(json);
                    writer.Flush();
                    stream.Flush(flushToDisk: true);
                }

                if (ReadEnvelope(TempPath, envelope.scenarioContentId, validator, out _, out var tempStatus, out error) !=
                    FileReadStatus.Valid)
                {
                    error = "Checkpoint temporary file verification failed: " + error;
                    return false;
                }

                if (!File.Exists(PrimaryPath))
                {
                    File.Move(TempPath, PrimaryPath);
                }
                else
                {
                    var primaryStatus = ReadEnvelope(
                        PrimaryPath,
                        envelope.scenarioContentId,
                        validator,
                        out _,
                        out _,
                        out _);
                    File.Replace(
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
            var primaryStatus = ReadEnvelope(
                PrimaryPath,
                scenarioContentId,
                validator,
                out var primaryEnvelope,
                out var primaryError);
            if (primaryStatus == FileReadStatus.Valid)
            {
                result = new WaveCheckpointLoadResult(
                    WaveCheckpointLoadStatus.LoadedPrimary,
                    primaryEnvelope,
                    null);
                return true;
            }

            var backupStatus = ReadEnvelope(
                BackupPath,
                scenarioContentId,
                validator,
                out var backupEnvelope,
                out var backupError);
            if (backupStatus == FileReadStatus.Valid)
            {
                result = new WaveCheckpointLoadResult(
                    WaveCheckpointLoadStatus.LoadedBackup,
                    backupEnvelope,
                    primaryError);
                return true;
            }

            var status = CombineFailureStatuses(primaryStatus, backupStatus);
            var error = string.IsNullOrEmpty(primaryError) ? backupError : primaryError;
            result = new WaveCheckpointLoadResult(status, null, error);
            return false;
        }

        public bool Delete(out string error)
        {
            try
            {
                // Keep both checkpoint slots until stale temp cleanup succeeds.
                // The backup remains available for Continue if a later delete fails.
                File.Delete(TempPath);
                File.Delete(PrimaryPath);
                File.Delete(BackupPath);
                error = null;
                return true;
            }
            catch (Exception exception) when (IsFileSystemException(exception))
            {
                error = "Checkpoint delete failed: " + exception.Message;
                return false;
            }
        }

        private static FileReadStatus ReadEnvelope(
            string path,
            string scenarioContentId,
            WaveCheckpointValidator validator,
            out WaveCheckpointEnvelope envelope,
            out string error)
        {
            return ReadEnvelope(path, scenarioContentId, validator, out envelope, out _, out error);
        }

        private static FileReadStatus ReadEnvelope(
            string path,
            string scenarioContentId,
            WaveCheckpointValidator validator,
            out WaveCheckpointEnvelope envelope,
            out FileReadStatus status,
            out string error)
        {
            envelope = null;
            error = null;
            status = FileReadStatus.Missing;
            try
            {
                if (!File.Exists(path))
                {
                    return status;
                }

                WaveCheckpointEnvelope candidate;
                try
                {
                    candidate = JsonUtility.FromJson<WaveCheckpointEnvelope>(File.ReadAllText(path));
                }
                catch (ArgumentException exception)
                {
                    status = FileReadStatus.Corrupt;
                    error = exception.Message;
                    return status;
                }

                if (candidate == null || candidate.schemaVersion != CurrentSchemaVersion ||
                    !string.Equals(candidate.scenarioContentId, scenarioContentId, StringComparison.Ordinal))
                {
                    status = candidate == null ? FileReadStatus.Corrupt : FileReadStatus.Incompatible;
                    error = status == FileReadStatus.Corrupt
                        ? "Checkpoint JSON is empty."
                        : "Checkpoint schema or scenario content is incompatible.";
                    return status;
                }

                if (!TryValidateEnvelope(candidate, scenarioContentId, validator, out error))
                {
                    status = FileReadStatus.Corrupt;
                    return status;
                }

                envelope = candidate;
                status = FileReadStatus.Valid;
                return status;
            }
            catch (Exception exception) when (IsFileSystemException(exception))
            {
                status = FileReadStatus.IoError;
                error = exception.Message;
                return status;
            }
        }

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

            return validator(envelope.state, out error);
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
}
