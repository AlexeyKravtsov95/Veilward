using System;
using System.Collections.Generic;

namespace Veilward.Domain
{
    [Serializable]
    public sealed class SpentElementChoiceCheckpoint
    {
        public string choiceId;
        public string elementId;
    }

    [Serializable]
    public sealed class ElementProgressionCheckpoint
    {
        public string rulesId;
        public string startResonanceId;
        public string[] unlockedElementIds;
        public int choicesEarned;
        public int choicesSpent;
        public SpentElementChoiceCheckpoint[] spentChoices;
        public string pendingChoiceId;

        private static readonly string[] ElementIds = { "flame", "tide", "gale", "stone" };

        internal static ElementProgressionCheckpoint FromState(ElementProgressionState state)
        {
            var result = new ElementProgressionCheckpoint
            {
                rulesId = state.RulesId, startResonanceId = state.StartResonanceId,
                choicesEarned = state.ChoicesEarned, choicesSpent = state.ChoicesSpent,
                pendingChoiceId = state.PendingChoiceId,
                unlockedElementIds = new string[state.UnlockedElements.Count],
                spentChoices = new SpentElementChoiceCheckpoint[state.SpentChoices.Count]
            };
            for (var i = 0; i < result.unlockedElementIds.Length; i++)
                result.unlockedElementIds[i] = ElementIds[(int)state.UnlockedElements[i]];
            for (var i = 0; i < result.spentChoices.Length; i++)
                result.spentChoices[i] = new SpentElementChoiceCheckpoint
                { choiceId = state.SpentChoices[i].ChoiceId, elementId = ElementIds[(int)state.SpentChoices[i].ElementKind] };
            return result;
        }

        public static ElementProgressionCheckpoint CreateLegacy() => FromState(ElementProgressionState.CreateInitial(false));

        public ElementProgressionCheckpoint DeepCopy()
        {
            var copy = (ElementProgressionCheckpoint)MemberwiseClone();
            copy.unlockedElementIds = unlockedElementIds == null ? null : (string[])unlockedElementIds.Clone();
            if (spentChoices != null)
            {
                copy.spentChoices = new SpentElementChoiceCheckpoint[spentChoices.Length];
                for (var i = 0; i < spentChoices.Length; i++)
                    if (spentChoices[i] != null) copy.spentChoices[i] = new SpentElementChoiceCheckpoint
                    { choiceId = spentChoices[i].choiceId, elementId = spentChoices[i].elementId };
            }
            return copy;
        }

        internal bool TryRestore(BattleCheckpoint checkpoint, bool native, int initialHealth, int initialGold,
            out ElementProgressionState state)
        {
            state = null;
            if (unlockedElementIds == null || spentChoices == null || pendingChoiceId == null ||
                choicesEarned < 0 || choicesSpent < 0 || choicesSpent != spentChoices.Length) return false;
            if (!native)
            {
                if (rulesId != "legacy-all-open-v1" || startResonanceId != "legacy" ||
                    choicesEarned != 0 || choicesSpent != 0 || pendingChoiceId != "none" ||
                    unlockedElementIds.Length != 4) return false;
                for (var i = 0; i < 4; i++) if (unlockedElementIds[i] != ElementIds[i]) return false;
                state = ElementProgressionState.CreateInitial(false);
                return true;
            }
            if (rulesId != "wave-unlocks-v1") return false;
            if (startResonanceId == "unselected")
            {
                if (checkpoint.phase != BattlePhase.Preparation || checkpoint.waveIndex != 0 || checkpoint.tick != 0 ||
                    checkpoint.baseHealth != initialHealth || checkpoint.gold != initialGold || checkpoint.kills != 0 ||
                    checkpoint.nextEnemyId != 1 || checkpoint.nextTowerId != 1 || checkpoint.towers == null ||
                    checkpoint.towers.Length != 0 || choicesEarned != 0 || choicesSpent != 0 ||
                    unlockedElementIds.Length != 0 || pendingChoiceId != "start-resonance") return false;
                state = ElementProgressionState.CreateInitial(true);
                return true;
            }
            if (startResonanceId != "flame" && startResonanceId != "gale") return false;
            var completed = checkpoint.phase == BattlePhase.Victory ? 8 :
                checkpoint.waveIndex + (checkpoint.phase == BattlePhase.WaveComplete ? 1 : 0);
            var earned = (completed >= 3 ? 1 : 0) + (completed >= 6 ? 1 : 0);
            if (choicesEarned != earned || choicesSpent > earned) return false;
            var restored = ElementProgressionState.CreateInitial(true).Confirm(
                startResonanceId == "flame" ? ElementKind.Flame : ElementKind.Gale, startResonanceId);
            var unlocked = new HashSet<string> { startResonanceId };
            for (var i = 0; i < choicesSpent; i++)
            {
                var choice = spentChoices[i];
                var id = i == 0 ? "after-wave-3" : "after-wave-6";
                if (choice == null || choice.choiceId != id || !TryElement(choice.elementId, out var element) ||
                    !unlocked.Add(choice.elementId)) return false;
                restored = restored.AwardChoice(i == 0 ? 3 : 6).Confirm(element, choice.elementId);
            }
            if (earned != choicesSpent)
            {
                var wave = checkpoint.waveIndex + 1;
                if (earned - choicesSpent != 1 || checkpoint.phase != BattlePhase.WaveComplete ||
                    (wave != 3 && wave != 6) || pendingChoiceId != "after-wave-" + wave) return false;
                restored = restored.AwardChoice(wave);
            }
            else if (pendingChoiceId != "none") return false;
            if (unlockedElementIds.Length != unlocked.Count) return false;
            var index = 0;
            foreach (var id in ElementIds)
                if (unlocked.Contains(id) && unlockedElementIds[index++] != id) return false;
            state = restored;
            return true;
        }

        private static bool TryElement(string id, out ElementKind element)
        {
            var index = Array.IndexOf(ElementIds, id);
            element = (ElementKind)index;
            return index >= 0;
        }
    }
}
