namespace Veilward.Domain
{
    public readonly struct ChooseElementCommand
    {
        public ChooseElementCommand(string choiceId, string optionId)
        {
            ChoiceId = choiceId;
            OptionId = optionId;
        }
        public string ChoiceId { get; }
        public string OptionId { get; }
    }
}
