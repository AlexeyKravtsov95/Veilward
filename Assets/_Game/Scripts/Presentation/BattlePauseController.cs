using System;
using UnityEngine;
using UnityEngine.UI;

namespace Veilward.Presentation
{
    public sealed class BattlePauseController : MonoBehaviour
    {
        [SerializeField] private BattleSceneController battle;
        [SerializeField] private BattlePlacementController placement;
        [SerializeField] private GameObject pauseOverlay;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button resumeButton;
        [SerializeField] private AudioSettingsPanel audioSettings;

        private bool _isPaused;
        private bool _applicationPaused;
        private bool _hasFocus = true;

        public bool IsPaused => _isPaused;
        internal bool AudioSettingsOpen => audioSettings != null && audioSettings.IsOpen;
        public bool CanResume => _isPaused && !_applicationPaused && _hasFocus && !AudioSettingsOpen;

        private void Start()
        {
            ValidateReference(battle, nameof(battle));
            ValidateReference(placement, nameof(placement));
            ValidateReference(pauseOverlay, nameof(pauseOverlay));
            ValidateReference(pauseButton, nameof(pauseButton));
            ValidateReference(resumeButton, nameof(resumeButton));
            pauseButton.onClick.AddListener(HandlePauseClicked);
            resumeButton.onClick.AddListener(HandleResumeClicked);
            RefreshUi();
        }

        private void Update()
        {
            RefreshUi();
        }

        private void OnDestroy()
        {
            if (pauseButton != null)
            {
                pauseButton.onClick.RemoveListener(HandlePauseClicked);
            }

            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveListener(HandleResumeClicked);
            }
        }

        private void HandlePauseClicked()
        {
            if (_isPaused) return;
            Pause();
            battle.PlayUiAudio(AudioCue.Confirm);
        }

        public void Pause()
        {
            if (_isPaused)
            {
                return;
            }

            _isPaused = true;
            if (battle != null) battle.SetAudioPaused(true);
            if (battle != null) battle.FreezePresentationClock();
            if (placement != null)
            {
                placement.ClearSelection();
            }

            if (battle != null)
            {
                var choice = battle.GetComponent<ElementChoiceController>();
                if (choice != null) { choice.ClearPreview(); choice.Refresh(); }
                var inspector = battle.GetComponent<EnemyInspectorController>();
                if (inspector != null) inspector.ClearSelection();
            }
            RefreshUi();
        }

        public bool Resume()
        {
            if (!CanResume)
            {
                return false;
            }

            _isPaused = false;
            if (battle != null) battle.SetAudioPaused(false);
            if (battle != null) battle.FreezePresentationClock();
            if (placement != null)
            {
                placement.SuppressPointerForCurrentFrame();
            }

            RefreshUi();
            return true;
        }

        public void OnApplicationPause(bool paused)
        {
            _applicationPaused = paused;
            if (paused)
            {
                Pause();
            }
            else
            {
                RefreshUi();
            }
        }

        public void OnApplicationFocus(bool focused)
        {
            _hasFocus = focused;
            if (!focused)
            {
                Pause();
            }
            else
            {
                RefreshUi();
            }
        }

        private void HandleResumeClicked()
        {
            if (Resume() && battle != null) battle.PlayUiAudio(AudioCue.Confirm);
        }

        private void RefreshUi()
        {
            if (pauseOverlay != null)
            {
                pauseOverlay.SetActive(_isPaused);
            }

            if (pauseButton != null)
            {
                pauseButton.interactable = !_isPaused;
            }

            if (resumeButton != null)
            {
                resumeButton.interactable = CanResume;
            }
        }

        private static void ValidateReference(UnityEngine.Object value, string name)
        {
            if (value == null)
            {
                throw new InvalidOperationException($"Battle pause reference is required: {name}.");
            }
        }
    }
}
