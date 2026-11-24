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

        [SerializeField] private UnityEngine.UI.Button retryButton;

        private bool _leaving;
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
            if (retryButton != null) retryButton.onClick.AddListener(RefreshCheckpointStatus);
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
            if (retryButton != null) retryButton.onClick.RemoveListener(RefreshCheckpointStatus);
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
            ShowRetry(false);
            var loaded = _store.TryLoad(
                scenario.ScenarioContentId,
                scenario.CreateSimulationForCheckpoint,
                out var result);
            if (loaded)
            {
                var phase = result.Envelope.state.phase;
                if (phase == BattlePhase.Preparation || phase == BattlePhase.WaveComplete)
                {
                    _availableCheckpoint = result.Envelope;
                    continueButton.interactable = true;
                    var wave = result.Envelope.state.waveIndex + 1;
                    statusText.text = result.Status == WaveCheckpointLoadStatus.LoadedBackup
                        ? $"Wave {wave} is available from a backup."
                        : $"Saved at wave {wave}.";
                    if (result.Envelope.state.elementProgression.rulesId == "legacy-all-open-v1")
                        statusText.text += "\nEarlier rules: all elements unlocked.";
                    return;
                }

                continueButton.interactable = false;
                statusText.text = "The last battle finished. Start a new game.";
                return;
            }

            continueButton.interactable = false;
            ShowRetry(result.Status != WaveCheckpointLoadStatus.Missing);
            switch (result.Status)
            {
                case WaveCheckpointLoadStatus.Missing:
                    statusText.text = "No saved battle.";
                    break;
                case WaveCheckpointLoadStatus.Incompatible:
                    statusText.text = "This save is from an incompatible version.";
                    break;
                case WaveCheckpointLoadStatus.Corrupt:
                    statusText.text = "The save is damaged.";
                    break;
                default:
                    statusText.text = "The save could not be read. Check storage.";
                    break;
            }
        }

        public bool PrepareNewGameSession()
        {
            ValidateReferences();
            EnsureStore();
            if (!_store.Delete(out var error))
            {
                statusText.text = "Unable to reset the save. Try New Game again.";
                Debug.LogError(error);
                return false;
            }

            _availableCheckpoint = null;
            continueButton.interactable = false;
            ShowRetry(false);
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
            if (!_leaving && PrepareNewGameSession())
            {
                _leaving = true;
                SceneManager.LoadScene(BattleSceneName, LoadSceneMode.Single);
            }
        }

        public void Continue()
        {
            if (!_leaving && PrepareContinueSession())
            {
                _leaving = true;
                SceneManager.LoadScene(BattleSceneName, LoadSceneMode.Single);
            }
        }

        private void ShowRetry(bool visible)
        {
            if (retryButton != null) retryButton.gameObject.SetActive(visible);
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
