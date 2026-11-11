using System;
using Veilward.Domain;

namespace Veilward.Infrastructure
{
    [Serializable]
    public sealed class WaveCheckpointEnvelope
    {
        public int schemaVersion;
        public string scenarioContentId;
        public BattleCheckpoint state;
    }
}
