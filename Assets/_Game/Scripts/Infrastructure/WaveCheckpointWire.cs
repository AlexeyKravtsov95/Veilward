using System;
using UnityEngine;
using Veilward.Domain;

namespace Veilward.Infrastructure
{
    // JsonUtility не сериализует readonly/get-only Domain values. Явная wire-проекция не ослабляет immutable API.
    internal static class WaveCheckpointWire
    {
        [Serializable] private sealed class AbilityData
        {
            public string rulesId;
            public string abilityId;
            public int cooldownRemainingTicks;
            public long nextCastSequence;
            public AbilityCheckpoint Snapshot() => new AbilityCheckpoint(rulesId, abilityId, cooldownRemainingTicks, nextCastSequence);
        }
        [Serializable] private sealed class StateData
        {
            public BattlePhase phase;
            public int waveIndex;
            public long tick;
            public int baseHealth;
            public int gold;
            public int kills;
            public int nextEnemyId;
            public int nextTowerId;
            public TowerCheckpoint[] towers;
            public ElementProgressionCheckpoint elementProgression;
            public AbilityData ability;
            public BattleCheckpoint Snapshot() => new BattleCheckpoint
            {
                phase = phase, waveIndex = waveIndex, tick = tick, baseHealth = baseHealth, gold = gold, kills = kills,
                nextEnemyId = nextEnemyId, nextTowerId = nextTowerId, towers = towers,
                elementProgression = elementProgression, ability = ability?.Snapshot()
            };
        }
        [Serializable] private sealed class EnvelopeData
        {
            public int schemaVersion;
            public string scenarioContentId;
            public StateData state;
        }
        internal static string Serialize(WaveCheckpointEnvelope envelope, bool pretty)
        {
            if (envelope.schemaVersion < 4) return JsonUtility.ToJson(envelope, pretty);
            var cp = envelope.state;
            var state = cp == null ? null : new StateData
            {
                phase = cp.phase, waveIndex = cp.waveIndex, tick = cp.tick, baseHealth = cp.baseHealth, gold = cp.gold, kills = cp.kills,
                nextEnemyId = cp.nextEnemyId, nextTowerId = cp.nextTowerId, towers = cp.towers, elementProgression = cp.elementProgression,
                ability = cp.ability == null || envelope.schemaVersion < 4 ? null : new AbilityData
                { rulesId = cp.ability.RulesId, abilityId = cp.ability.AbilityId, cooldownRemainingTicks = cp.ability.CooldownRemainingTicks, nextCastSequence = cp.ability.NextCastSequence }
            };
            return JsonUtility.ToJson(new EnvelopeData { schemaVersion = envelope.schemaVersion, scenarioContentId = envelope.scenarioContentId, state = state }, pretty);
        }
        internal static WaveCheckpointEnvelope Deserialize(string json)
        {
            var data = JsonUtility.FromJson<EnvelopeData>(json);
            return new WaveCheckpointEnvelope { schemaVersion = data.schemaVersion, scenarioContentId = data.scenarioContentId, state = data.state?.Snapshot() };
        }
    }
}
