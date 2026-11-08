namespace Veilward.Domain
{
    public enum WaveTransitionResult
    {
        Started,
        Continued,
        RejectedInvalidPhase,
        RejectedNoNextWave
    }
}
