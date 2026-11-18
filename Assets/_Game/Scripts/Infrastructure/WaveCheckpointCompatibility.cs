using System;
using Veilward.Domain;

namespace Veilward.Infrastructure
{
    public static class WaveCheckpointCompatibility
    {
        public const string AbilityBossContentId = "eight-wave-ability-boss-v1";
        public const string NativeContentId = "eight-wave-unlocks-v1";
        public const string LegacyEightWaveContentId = "eight-wave-v1";
        public const string LegacySingleWaveContentId = "legacy-single-wave-v1";
        internal const string SyntheticAbilityContentId = "synthetic-p24-ability-v1";

        public static bool IsProductionContent(string id) => id == AbilityBossContentId || id == NativeContentId ||
            id == LegacyEightWaveContentId || id == LegacySingleWaveContentId;

        public static bool TryNormalize(WaveCheckpointEnvelope source, string requestedScenarioContentId,
            out WaveCheckpointEnvelope normalized, out string error) =>
            TryNormalize(source, requestedScenarioContentId, false, out normalized, out error);

        // Validator overload ограничен exact-ID; synthetic ability не попадает в production routing.
        internal static bool TryNormalize(WaveCheckpointEnvelope source, string requestedScenarioContentId,
            bool allowSynthetic, out WaveCheckpointEnvelope normalized, out string error)
        {
            normalized = null;
            error = "Checkpoint schema, content or rules are incompatible.";
            if (source?.state == null || source.schemaVersion < 1 || source.schemaVersion > 4 ||
                string.IsNullOrWhiteSpace(source.scenarioContentId)) return false;
            var id = source.scenarioContentId;
            var synthetic = allowSynthetic && (id == SyntheticAbilityContentId || id == "test-scenario-v1");
            if (!IsProductionContent(id) && !synthetic) return false;
            var eightWave = (id == NativeContentId || id == LegacyEightWaveContentId) &&
                (requestedScenarioContentId == AbilityBossContentId || requestedScenarioContentId == NativeContentId || requestedScenarioContentId == LegacyEightWaveContentId);
            if (id != requestedScenarioContentId && !eightWave) return false;
            var syntheticAbility = id == SyntheticAbilityContentId || id == AbilityBossContentId;
            var native = id == NativeContentId || syntheticAbility;
            if (native && source.schemaVersion < (syntheticAbility ? 4 : 3)) return false;
            var progression = source.state.elementProgression;
            if (source.schemaVersion >= 3 && (progression == null ||
                progression.rulesId != (native ? "wave-unlocks-v1" : "legacy-all-open-v1"))) return false;
            // Не стирать повреждённую source progression при добавлении legacy defaults.
            if (source.schemaVersion < 3 && progression != null && !IsLegacy(progression)) return false;
            if (source.schemaVersion == 1 && source.state.towers != null)
                foreach (var tower in source.state.towers)
                    if (tower != null && !string.IsNullOrEmpty(tower.towerTypeId) && tower.towerTypeId != "test-tower") return false;
            var ability = source.state.ability;
            if (source.schemaVersion >= 4)
            {
                if (ability == null || ability.RulesId != (syntheticAbility ? "rift-seal-v1" : "ability-disabled-v1") ||
                    ability.AbilityId != (syntheticAbility ? "rift-seal" : "none")) return false;
                if (!syntheticAbility && (ability.CooldownRemainingTicks != 0 || ability.NextCastSequence != 1)) return false;
            }
            else if (ability != null && (ability.RulesId != "ability-disabled-v1" || ability.AbilityId != "none" ||
                ability.CooldownRemainingTicks != 0 || ability.NextCastSequence != 1)) return false;
            var state = source.state.DeepCopy(source.schemaVersion == 1);
            if (source.schemaVersion < 3) state.elementProgression = ElementProgressionCheckpoint.CreateLegacy();
            if (source.schemaVersion < 4) state.ability = AbilityCheckpoint.Disabled();
            normalized = new WaveCheckpointEnvelope { schemaVersion = 4, scenarioContentId = id, state = state };
            error = null;
            return true;
        }
        private static bool IsLegacy(ElementProgressionCheckpoint p)
        {
            if (p.rulesId != "legacy-all-open-v1" || p.startResonanceId != "legacy" || p.pendingChoiceId != "none" ||
                p.choicesEarned != 0 || p.choicesSpent != 0 || p.spentChoices == null || p.spentChoices.Length != 0 ||
                p.unlockedElementIds == null || p.unlockedElementIds.Length != 4) return false;
            var ids = new[] { "flame", "tide", "gale", "stone" };
            for (var i = 0; i < ids.Length; i++) if (p.unlockedElementIds[i] != ids[i]) return false;
            return true;
        }
    }
}
