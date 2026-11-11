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

        private bool _isPaused;
        private bool _applicationPaused;
        private bool _hasFocus = true;

        public bool IsPaused => _isPaused;
        public bool CanResume => _isPaused && !_applicationPaused && _hasFocus;

        private void Start()
        {
            ValidateReference(battle, nameof(battle));
            ValidateReference(placement, nameof(placement));
            ValidateReference(pauseOverlay, nameof(pauseOverlay));
            ValidateReference(pauseButton, nameof(pauseButton));
            ValidateReference(resumeButton, nameof(resumeButton));
            pauseButton.onClick.AddListener(Pause);
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
                pauseButton.onClick.RemoveListener(Pause);
            }

            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveListener(HandleResumeClicked);
            }
        }

        public void Pause()
        {
            if (_isPaused)
            {
                return;
            }

            _isPaused = true;
            if (placement != null)
            {
                placement.CancelSelection();
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
            Resume();
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
