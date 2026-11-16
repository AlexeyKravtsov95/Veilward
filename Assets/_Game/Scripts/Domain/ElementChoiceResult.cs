namespace Veilward.Domain
{
    public enum ElementChoiceFailureReason
    {
        None = 0,
        InvalidPhase,
        ProgressionDisabled,
        NoPendingChoice,
        ChoiceMismatch,
        InvalidOption,
        AlreadyUnlocked,
        NoChoiceCredit
    }

    public sealed class ElementChoiceResult
    {
        internal ElementChoiceResult(ElementChoiceFailureReason reason)
        {
            FailureReason = reason;
        }
        public bool Success => FailureReason == ElementChoiceFailureReason.None;
        public ElementChoiceFailureReason FailureReason { get; }
    }
}
