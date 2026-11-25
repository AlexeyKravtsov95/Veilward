using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Veilward.Presentation
{
    public sealed class AudioSettingsPanel : MonoBehaviour
    {
        [SerializeField] private VeilwardAudio audioOwner;
        [SerializeField] private GameObject overlay;
        [SerializeField] private CanvasGroup underlyingActions;
        [SerializeField] private Button openButton, closeButton, muteButton;
        [SerializeField] private Slider master, music, sfx;
        [SerializeField] private Text masterValue, musicValue, sfxValue, muteLabel;
        private bool _dirty;

        internal bool IsOpen => overlay != null && overlay.activeSelf;

        private void Start()
        {
            openButton.onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);
            muteButton.onClick.AddListener(ToggleMute);
            master.onValueChanged.AddListener(ChangeVolume);
            music.onValueChanged.AddListener(ChangeVolume);
            sfx.onValueChanged.AddListener(ChangeVolume);
        }

        private void Open()
        {
            if (IsOpen || !openButton.IsInteractable()) return;
            Refresh();
            overlay.SetActive(true);
            underlyingActions.interactable = false;
            Select(closeButton);
            audioOwner.Play(AudioCue.Select);
        }

        internal void Close()
        {
            if (!IsOpen) return;
            overlay.SetActive(false);
            underlyingActions.interactable = true;
            Flush();
            if (openButton.isActiveAndEnabled) Select(openButton);
            audioOwner.Play(AudioCue.Cancel);
        }

        private void ChangeVolume(float _)
        {
            if (!IsOpen) return;
            var previous = audioOwner.Preferences;
            var value = new AudioPreferences(master.value, music.value, sfx.value, previous.Muted);
            Apply(value);
            if (value.Master != previous.Master || value.Sfx != previous.Sfx)
                audioOwner.Play(AudioCue.Select);
            else if (value.Music != previous.Music)
                audioOwner.PreviewMusic();
        }

        private void ToggleMute()
        {
            if (!IsOpen) return;
            var value = audioOwner.Preferences;
            Apply(new AudioPreferences(value.Master, value.Music, value.Sfx, !value.Muted));
            audioOwner.Play(AudioCue.Confirm);
        }

        private void Apply(AudioPreferences value)
        {
            audioOwner.SetPreferences(value);
            Refresh();
            _dirty = true;
        }

        private void Refresh()
        {
            var value = audioOwner.Preferences;
            master.SetValueWithoutNotify(value.Master);
            music.SetValueWithoutNotify(value.Music);
            sfx.SetValueWithoutNotify(value.Sfx);
            masterValue.text = Percent(value.Master);
            musicValue.text = Percent(value.Music);
            sfxValue.text = Percent(value.Sfx);
            muteLabel.text = value.Muted ? "Mute: On" : "Mute: Off";
        }

        private static string Percent(float value) => Mathf.RoundToInt(value * 100) + "%";

        private static void Select(Selectable control)
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(control.gameObject);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Flush();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) Flush();
        }

        private void OnDisable() => Flush();

        private void Flush()
        {
            if (!_dirty) return;
            audioOwner.Preferences.Save();
            _dirty = false;
        }
    }
}
