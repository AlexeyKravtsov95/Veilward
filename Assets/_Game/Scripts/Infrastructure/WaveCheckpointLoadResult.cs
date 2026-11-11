namespace Veilward.Infrastructure
{
    public enum WaveCheckpointLoadStatus
    {
        Missing,
        LoadedPrimary,
        LoadedBackup,
        Incompatible,
        Corrupt,
        IoError
    }

    public sealed class WaveCheckpointLoadResult
    {
        internal WaveCheckpointLoadResult(
            WaveCheckpointLoadStatus status,
            WaveCheckpointEnvelope envelope,
            string error)
        {
            Status = status;
            Envelope = envelope;
            Error = error;
        }

        public WaveCheckpointLoadStatus Status { get; }
        public WaveCheckpointEnvelope Envelope { get; }
        public string Error { get; }
    }
}
