using System;
using Veilward.Domain;

namespace Veilward.Infrastructure
{
    public static class WaveCheckpointCompatibility
    {
        public const string NativeContentId = "eight-wave-unlocks-v1";
        public const string LegacyEightWaveContentId = "eight-wave-v1";
        public const string LegacySingleWaveContentId = "legacy-single-wave-v1";

        public static bool TryNormalize(WaveCheckpointEnvelope source, string requestedScenarioContentId,
            out WaveCheckpointEnvelope normalized, out string error) =>
            TryNormalize(source, requestedScenarioContentId, false, out normalized, out error);

        // Старый validator overload сохраняет exact-ID synthetic test paths; production использует factory overload.
        internal static bool TryNormalize(WaveCheckpointEnvelope source, string requestedScenarioContentId,
            bool allowSynthetic, out WaveCheckpointEnvelope normalized, out string error)
        {
            normalized = null;
            error = "Checkpoint schema, content or rules are incompatible.";
            if (source == null || source.state == null || source.schemaVersion < 1 || source.schemaVersion > 3 ||
                string.IsNullOrWhiteSpace(source.scenarioContentId)) return false;
            var id = source.scenarioContentId;
            var known = id == NativeContentId || id == LegacyEightWaveContentId || id == LegacySingleWaveContentId;
            if ((!known && !allowSynthetic) ||
                (id != requestedScenarioContentId && !(requestedScenarioContentId == NativeContentId && id == LegacyEightWaveContentId)))
                return false;
            var native = id == NativeContentId;
            if (native && source.schemaVersion != 3) return false;
            var progression = source.state.elementProgression;
            if (source.schemaVersion == 3 && (progression == null ||
                progression.rulesId != (native ? "wave-unlocks-v1" : "legacy-all-open-v1"))) return false;
            if (source.schemaVersion < 3 && !string.IsNullOrEmpty(progression?.rulesId) && progression.rulesId != "legacy-all-open-v1") return false;
            if (source.schemaVersion == 1 && source.state.towers != null)
                foreach (var tower in source.state.towers)
                    if (tower != null && !string.IsNullOrEmpty(tower.towerTypeId) && tower.towerTypeId != "test-tower")
                        return false;
            var state = source.state.DeepCopy(source.schemaVersion == 1);
            if (source.schemaVersion < 3) state.elementProgression = ElementProgressionCheckpoint.CreateLegacy();
            normalized = new WaveCheckpointEnvelope { schemaVersion = 3, scenarioContentId = id, state = state };
            error = null;
            return true;
        }
    }
}
