using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Veilward.Application;
using Veilward.Domain;
using Veilward.Infrastructure;

namespace Veilward.Presentation
{
    public sealed class MainMenuController : MonoBehaviour
    {
        private const string BattleSceneName = "Battle";

        [SerializeField] private BattleScenarioConfig scenario;
        [SerializeField] private UnityEngine.UI.Button newGameButton;
        [SerializeField] private UnityEngine.UI.Button continueButton;
        [SerializeField] private UnityEngine.UI.Text statusText;

        private WaveCheckpointStore _store;
        private WaveCheckpointEnvelope _availableCheckpoint;

        public static Func<WaveCheckpointStore> CheckpointStoreFactory { get; set; }
        public UnityEngine.UI.Button NewGameButton => newGameButton;
        public UnityEngine.UI.Button ContinueButton => continueButton;
        public UnityEngine.UI.Text StatusText => statusText;

        private void Start()
        {
            ValidateReferences();
            newGameButton.onClick.AddListener(StartNewGame);
            continueButton.onClick.AddListener(Continue);
            RefreshCheckpointStatus();
        }

        private void OnDestroy()
        {
            if (newGameButton != null)
            {
                newGameButton.onClick.RemoveListener(StartNewGame);
            }

            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(Continue);
            }
        }

        public void ConfigureStore(WaveCheckpointStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            if (scenario != null && continueButton != null && statusText != null)
            {
                RefreshCheckpointStatus();
            }
        }

        public void RefreshCheckpointStatus()
        {
            ValidateReferences();
            EnsureStore();
            _availableCheckpoint = null;
            var simulation = scenario.CreateSimulation();
            var loaded = _store.TryLoad(
                scenario.ScenarioContentId,
                simulation.TryValidateCheckpoint,
                out var result);
            if (loaded)
            {
                var phase = result.Envelope.state.phase;
                if (phase == BattlePhase.Preparation || phase == BattlePhase.WaveComplete)
                {
                    _availableCheckpoint = result.Envelope;
                    continueButton.interactable = true;
                    statusText.text = result.Status == WaveCheckpointLoadStatus.LoadedBackup
                        ? "Checkpoint ready — backup restored."
                        : "Checkpoint ready.";
                    return;
                }

                continueButton.interactable = false;
                statusText.text = "Match finished — start a New Game.";
                return;
            }

            continueButton.interactable = false;
            switch (result.Status)
            {
                case WaveCheckpointLoadStatus.Missing:
                    statusText.text = "No checkpoint — start a New Game.";
                    break;
                case WaveCheckpointLoadStatus.Incompatible:
                    statusText.text = "Checkpoint is incompatible — start a New Game.";
                    break;
                case WaveCheckpointLoadStatus.Corrupt:
                    statusText.text = "Checkpoint is damaged — start a New Game.";
                    break;
                default:
                    statusText.text = "Checkpoint unavailable — check storage.";
                    break;
            }
        }

        public bool PrepareNewGameSession()
        {
            ValidateReferences();
            EnsureStore();
            if (!_store.Delete(out var error))
            {
                statusText.text = "Unable to reset checkpoint.";
                Debug.LogError(error);
                return false;
            }

            _availableCheckpoint = null;
            continueButton.interactable = false;
            statusText.text = "New Game ready.";
            BattleSessionContext.SetNewGame(_store, scenario.ScenarioContentId);
            return true;
        }

        public bool PrepareContinueSession()
        {
            RefreshCheckpointStatus();
            if (_availableCheckpoint == null)
            {
                return false;
            }

            BattleSessionContext.SetContinue(_store, _availableCheckpoint);
            return true;
        }

        public void StartNewGame()
        {
            if (PrepareNewGameSession())
            {
                SceneManager.LoadScene(BattleSceneName, LoadSceneMode.Single);
            }
        }

        public void Continue()
        {
            if (PrepareContinueSession())
            {
                SceneManager.LoadScene(BattleSceneName, LoadSceneMode.Single);
            }
        }

        private void EnsureStore()
        {
            if (_store == null)
            {
                _store = CheckpointStoreFactory?.Invoke() ??
                         new WaveCheckpointStore(UnityEngine.Application.persistentDataPath);
            }
        }

        private void ValidateReferences()
        {
            if (scenario == null || newGameButton == null || continueButton == null || statusText == null)
            {
                throw new InvalidOperationException("Main Menu scenario, buttons and status references are required.");
            }
        }
    }
}
