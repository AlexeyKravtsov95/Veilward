using System;
using System.Collections.Generic;

namespace Veilward.Domain
{
    public sealed class SpentElementChoice
    {
        internal SpentElementChoice(string choiceId, ElementKind elementKind)
        {
            ChoiceId = choiceId;
            ElementKind = elementKind;
        }
        public string ChoiceId { get; }
        public ElementKind ElementKind { get; }
    }

    public sealed class ElementProgressionState
    {
        private ElementProgressionState(string rulesId, string startResonanceId,
            IReadOnlyList<ElementKind> unlocked, int earned, IReadOnlyList<SpentElementChoice> spent, string pending)
        {
            RulesId = rulesId;
            StartResonanceId = startResonanceId;
            UnlockedElements = Array.AsReadOnly(Copy(unlocked));
            ChoicesEarned = earned;
            SpentChoices = Array.AsReadOnly(Copy(spent));
            ChoicesSpent = spent.Count;
            PendingChoiceId = pending;
        }

        public string RulesId { get; }
        public string StartResonanceId { get; }
        public IReadOnlyList<ElementKind> UnlockedElements { get; }
        public int ChoicesEarned { get; }
        public int ChoicesSpent { get; }
        public IReadOnlyList<SpentElementChoice> SpentChoices { get; }
        public string PendingChoiceId { get; }
        internal bool IsNative => RulesId == "wave-unlocks-v1";
        internal bool HasPendingChoice => PendingChoiceId != "none";

        internal static ElementProgressionState CreateInitial(bool native) => native
            ? new ElementProgressionState("wave-unlocks-v1", "unselected", Array.Empty<ElementKind>(), 0,
                Array.Empty<SpentElementChoice>(), "start-resonance")
            : new ElementProgressionState("legacy-all-open-v1", "legacy",
                new[] { ElementKind.Flame, ElementKind.Tide, ElementKind.Gale, ElementKind.Stone }, 0,
                Array.Empty<SpentElementChoice>(), "none");

        internal bool IsUnlocked(ElementKind element)
        {
            foreach (var unlocked in UnlockedElements)
                if (unlocked == element) return true;
            return false;
        }

        internal ElementProgressionState AwardChoice(int wave) => new ElementProgressionState(
            RulesId, StartResonanceId, UnlockedElements, ChoicesEarned + 1, SpentChoices, "after-wave-" + wave);

        internal ElementProgressionState Confirm(ElementKind element, string optionId)
        {
            var unlocked = new List<ElementKind>(UnlockedElements) { element };
            unlocked.Sort();
            var start = PendingChoiceId == "start-resonance";
            var spent = new List<SpentElementChoice>(SpentChoices);
            if (!start) spent.Add(new SpentElementChoice(PendingChoiceId, element));
            return new ElementProgressionState(RulesId, start ? optionId : StartResonanceId,
                unlocked, ChoicesEarned, spent, "none");
        }

        private static T[] Copy<T>(IReadOnlyList<T> source)
        {
            var copy = new T[source.Count];
            for (var index = 0; index < copy.Length; index++) copy[index] = source[index];
            return copy;
        }
    }
}
