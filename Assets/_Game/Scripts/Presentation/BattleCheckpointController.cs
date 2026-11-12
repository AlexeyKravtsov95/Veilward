using System;
using UnityEngine;
using Veilward.Domain;
using Veilward.Infrastructure;

namespace Veilward.Presentation
{
    public sealed class BattleCheckpointController : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Text saveStatusText;

        private WaveCheckpointStore _store;
        private BattleSimulation _simulation;
        private string _scenarioContentId;
        private BattlePhase _observedPhase;

        public UnityEngine.UI.Text StatusText => saveStatusText;
        public bool LastSaveSucceeded { get; private set; } = true;

        public void Configure(
            WaveCheckpointStore store,
            BattleSimulation simulation,
            string scenarioContentId)
        {
            _store = store;
            _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            _scenarioContentId = scenarioContentId;
            _observedPhase = simulation.State.Phase;
            LastSaveSucceeded = true;
            if (_store == null)
            {
                SetStatus("In-memory Battle — checkpoint disabled.");
            }
        }

        public void RestoreCheckpoint(WaveCheckpointEnvelope envelope)
        {
            EnsureConfigured();
            if (envelope == null || envelope.schemaVersion != WaveCheckpointStore.CurrentSchemaVersion ||
                !string.Equals(envelope.scenarioContentId, _scenarioContentId, StringComparison.Ordinal))
            {
                throw new ArgumentException("Checkpoint envelope is incompatible with this Battle.", nameof(envelope));
            }

            _simulation.RestoreCheckpoint(envelope.state);
            _observedPhase = _simulation.State.Phase;
            LastSaveSucceeded = true;
            SetStatus("Checkpoint restored.");
        }

        public bool SaveCurrentState()
        {
            EnsureConfigured();
            if (_store == null)
            {
                LastSaveSucceeded = true;
                return true;
            }

            BattleCheckpoint state;
            try
            {
                state = _simulation.ExportCheckpoint();
            }
            catch (InvalidOperationException exception)
            {
                LastSaveSucceeded = false;
                SetStatus("Checkpoint save requires a stable battle phase.");
                Debug.LogError(exception.Message);
                return false;
            }

            var envelope = new WaveCheckpointEnvelope
            {
                schemaVersion = WaveCheckpointStore.CurrentSchemaVersion,
                scenarioContentId = _scenarioContentId,
                state = state
            };
            LastSaveSucceeded = _store.TrySave(envelope, _simulation.TryValidateCheckpoint, out var error);
            if (!LastSaveSucceeded)
            {
                SetStatus("Save failed — previous checkpoint kept.");
                Debug.LogError(error);
                return false;
            }

            SetStatus("Checkpoint saved.");
            return true;
        }

        public bool SaveForStartWave()
        {
            EnsureConfigured();
            if (_simulation.State.Phase != BattlePhase.Preparation)
            {
                return true;
            }

            if (SaveCurrentState())
            {
                return true;
            }

            SetStatus("Start Wave requires a successful checkpoint save.");
            return false;
        }

        public bool ObservePhaseTransition()
        {
            EnsureConfigured();
            var phase = _simulation.State.Phase;
            if (phase == _observedPhase)
            {
                return true;
            }

            var previous = _observedPhase;
            _observedPhase = phase;
            if (previous == BattlePhase.Combat &&
                (phase == BattlePhase.WaveComplete || phase == BattlePhase.Victory || phase == BattlePhase.Defeat))
            {
                return SaveCurrentState();
            }

            return true;
        }

        private void EnsureConfigured()
        {
            if (_simulation == null)
            {
                throw new InvalidOperationException("Battle checkpoint controller is not configured.");
            }
        }

        private void SetStatus(string message)
        {
            if (saveStatusText != null)
            {
                saveStatusText.text = message;
            }
        }
    }
}
