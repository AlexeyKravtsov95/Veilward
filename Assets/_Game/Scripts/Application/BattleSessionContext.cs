using System;
using Veilward.Infrastructure;

namespace Veilward.Application
{
    public sealed class BattleSessionContext
    {
        private static BattleSessionContext _pending;

        private BattleSessionContext(
            WaveCheckpointStore store,
            string scenarioContentId,
            WaveCheckpointEnvelope checkpoint)
        {
            Store = store ?? throw new ArgumentNullException(nameof(store));
            ScenarioContentId = string.IsNullOrWhiteSpace(scenarioContentId)
                ? throw new ArgumentException("Scenario content ID is required.", nameof(scenarioContentId))
                : scenarioContentId;
            Checkpoint = checkpoint;
        }

        public WaveCheckpointStore Store { get; }
        public string ScenarioContentId { get; }
        public WaveCheckpointEnvelope Checkpoint { get; }

        public static void SetNewGame(WaveCheckpointStore store, string scenarioContentId)
        {
            _pending = new BattleSessionContext(store, scenarioContentId, checkpoint: null);
        }

        public static void SetContinue(WaveCheckpointStore store, WaveCheckpointEnvelope checkpoint)
        {
            if (checkpoint == null)
            {
                throw new ArgumentNullException(nameof(checkpoint));
            }

            _pending = new BattleSessionContext(store, checkpoint.scenarioContentId, checkpoint);
        }

        public static BattleSessionContext Consume()
        {
            var pending = _pending;
            _pending = null;
            return pending;
        }

        public static void Clear()
        {
            _pending = null;
        }
    }
}
