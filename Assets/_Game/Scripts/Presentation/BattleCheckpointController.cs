using System;
using UnityEngine;
using Veilward.Domain;
using Veilward.Infrastructure;

namespace Veilward.Presentation
{
    public sealed class BattleCheckpointController : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Text saveStatusText;
        [SerializeField] private UnityEngine.UI.Button retrySaveButton;
        [SerializeField] private BattleSceneController battle;

        private void Start() { if (retrySaveButton != null) retrySaveButton.onClick.AddListener(HandleRetrySave); }
        private void OnDestroy() { if (retrySaveButton != null) retrySaveButton.onClick.RemoveListener(HandleRetrySave); }
        private void HandleRetrySave() => RetrySave();
        public bool RetrySave()
        {
            if (battle != null && !battle.CanAcceptInput(BattleInputOwner.ElementChoice)) return false;
            return SaveCurrentState();
        }

        private WaveCheckpointStore _store;
        private BattleSimulation _simulation;
        private string _scenarioContentId;
        private BattlePhase _observedPhase;
        private Func<WaveCheckpointEnvelope, BattleSimulation> _simulationFactory;

        public event Action Restored;

        public UnityEngine.UI.Text StatusText => saveStatusText;
        public bool LastSaveSucceeded { get; private set; } = true;

        public void Configure(
            WaveCheckpointStore store,
            BattleSimulation simulation,
            string scenarioContentId,
            Func<WaveCheckpointEnvelope, BattleSimulation> simulationFactory = null)
        {
            _simulationFactory = simulationFactory;
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
                !WaveCheckpointCompatibility.TryNormalize(envelope, _scenarioContentId, _simulationFactory == null, out _, out _))
            {
                throw new ArgumentException("Checkpoint envelope is incompatible with this Battle.", nameof(envelope));
            }

            _simulation.RestoreCheckpoint(envelope.state);
            _scenarioContentId = envelope.scenarioContentId;
            _observedPhase = _simulation.State.Phase;
            LastSaveSucceeded = true;
            SetStatus("Checkpoint restored.");
            Restored?.Invoke();
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
            LastSaveSucceeded = _store.TrySave(envelope, _simulation.TryValidateCheckpoint, out var error, _simulationFactory);
            if (!LastSaveSucceeded)
            {
                SetStatus("Save failed — live changes unsaved; exit may roll back. Retry Save.");
                Debug.LogError(error);
                return false;
            }

            SetStatus("Checkpoint saved.");
            return true;
        }

        public bool SaveForStartWave()
        {
            EnsureConfigured();
            if (_simulation.CheckStartWave() != WaveTransitionResult.Started)
                return false;

            if (SaveCurrentState())
            {
                return true;
            }

            SetStatus("Start Wave requires a successful checkpoint save.");
            return false;
        }

        public bool SaveForNextWave()
        {
            EnsureConfigured();
            if (_simulation.CheckContinueToNextWave() != WaveTransitionResult.Continued)
                return false;
            return SaveCurrentState();
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
            if (retrySaveButton != null) retrySaveButton.gameObject.SetActive(!LastSaveSucceeded);
            if (saveStatusText != null)
            {
                saveStatusText.text = message;
            }
        }
    }
}
