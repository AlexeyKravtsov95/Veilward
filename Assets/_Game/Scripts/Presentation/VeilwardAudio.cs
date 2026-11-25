using System;
using UnityEngine;
using UnityEngine.Audio;
using Veilward.Domain;

namespace Veilward.Presentation
{
    [DefaultExecutionOrder(-200)]
    public sealed class VeilwardAudio : MonoBehaviour
    {
        internal const int CombatCapacity = 10;
        internal const int PriorityCapacity = 3;
        internal const int VoiceCapacity = CombatCapacity + PriorityCapacity + 1;
        internal const string MasterParameter = "MasterVolume";
        internal const string MusicParameter = "MusicVolume";
        internal const string SfxParameter = "SfxVolume";

        [SerializeField] private VeilwardAudioCatalog catalog;
        [SerializeField] private bool battleMusic;

        private readonly Voice[] _voices = new Voice[VoiceCapacity];
        private readonly double[] _nextCue = new double[(int)AudioCue.Count];
        private ulong _voiceOrder;
        private AudioSource _music;
        private AudioPreferences _preferences;
        private bool _paused;
        private bool _background;
        private bool _hasFocus = true;
        private bool _musicPaused;
        private bool _musicStarted;
        private bool Foreground => !_background && _hasFocus;
        internal AudioPreferences Preferences => _preferences;
        internal AudioSource MusicSource => _music;

        private void Start()
        {
            _music = gameObject.AddComponent<AudioSource>();
            _music.playOnAwake = false;
            _music.loop = true;
            _music.volume = .32f;
            _music.priority = 32;
            _music.outputAudioMixerGroup = catalog.Music;
            _music.clip = catalog.MusicClip(battleMusic);
            for (var i = 0; i < _voices.Length; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.outputAudioMixerGroup = catalog.Sfx;
                source.priority = i < CombatCapacity ? 144 : 64;
                _voices[i] = new Voice(source, catalog.Sfx);
            }
            SetPreferences(AudioPreferences.Load());
            _musicPaused = !Foreground || _paused;
            if (!_musicPaused) StartMusic();
        }

        private void Update()
        {
            var now = AudioSettings.dspTime;
            foreach (var voice in _voices)
            {
                if (voice.Source.clip != null && voice.End <= now) voice.Clear();
            }
        }

        internal void SetPreferences(AudioPreferences value)
        {
            _preferences = value;
            if (_music == null) return;
            var masterOff = value.Muted || value.Master == 0f;
            var mixer = catalog.Music.audioMixer;
            mixer.SetFloat(MasterParameter, AudioPreferences.ToDecibels(masterOff ? 0f : value.Master));
            mixer.SetFloat(MusicParameter, AudioPreferences.ToDecibels(value.Music));
            mixer.SetFloat(SfxParameter, AudioPreferences.ToDecibels(value.Sfx));
            _music.mute = masterOff || value.Music == 0f;
            foreach (var voice in _voices)
                voice.Source.mute = masterOff || (voice.Source.outputAudioMixerGroup == catalog.Music
                    ? value.Music == 0f : value.Sfx == 0f);
        }

        internal void SetPaused(bool paused)
        {
            if (_paused == paused) return;
            _paused = paused;
            if (paused) ClearVoices(false);
            UpdateMusic();
        }

        internal void ClearRun() => ClearVoices(true);

        private void OnApplicationPause(bool paused)
        {
            _background = paused;
            UpdateForeground();
        }

        private void OnApplicationFocus(bool focused)
        {
            _hasFocus = focused;
            UpdateForeground();
        }

        private void UpdateForeground()
        {
            if (_music == null) return;
            if (!Foreground) ClearVoices(true);
            UpdateMusic();
        }

        private void UpdateMusic()
        {
            if (_music == null) return;
            var shouldPause = !Foreground || _paused;
            if (shouldPause == _musicPaused) return;
            _musicPaused = shouldPause;
            if (shouldPause) _music.Pause();
            else if (_musicStarted) _music.UnPause();
            else StartMusic();
        }

        private void OnDisable()
        {
            if (_music == null) return;
            ClearVoices(true);
            _music.Stop();
            _musicStarted = _musicPaused = false;
        }

        private void OnEnable()
        {
            if (_music == null) return;
            _musicPaused = !Foreground || _paused;
            if (!_musicPaused) StartMusic();
        }

        private void StartMusic()
        {
            _music.Play();
            _musicStarted = true;
        }

        private void ClearVoices(bool includeUi)
        {
            var end = includeUi ? VoiceCapacity : VoiceCapacity - 1;
            for (var i = 0; i < end; i++) _voices[i]?.Clear();
            Array.Clear(_nextCue, 0, includeUi ? _nextCue.Length : (int)AudioCue.Select);
        }

        internal void ClearBoss(EnemyId enemy)
        {
            for (var i = CombatCapacity; i < VoiceCapacity - 1; i++)
            {
                var voice = _voices[i];
                if (voice != null && voice.Boss == enemy) voice.Clear();
            }
        }

        internal void PreviewMusic()
        {
            if (_paused) PlayOnBus(AudioCue.Confirm, throughMusic: true);
        }

        internal bool Play(AudioCue cue, float pan = 0, float gain = 1f, float delay = 0f,
            bool variation = false, EnemyId? boss = null) =>
            PlayOnBus(cue, pan, gain, delay, variation, boss, false);

        private bool PlayOnBus(AudioCue cue, float pan = 0, float gain = 1f, float delay = 0f,
            bool variation = false, EnemyId? boss = null, bool throughMusic = false)
        {
            var ui = cue >= AudioCue.Select || cue == AudioCue.SealTarget;
            if (_music == null || !isActiveAndEnabled || !Foreground || (_paused && !ui) ||
                _preferences.Muted || _preferences.Master == 0f || (throughMusic ? _preferences.Music : _preferences.Sfx) == 0f) return false;

            var now = AudioSettings.dspTime;
            var index = (int)cue;
            if (now < _nextCue[index]) return false;
            var priority = Priority(cue);
            var start = ui ? VoiceCapacity - 1 : cue >= AudioCue.SealCast ? CombatCapacity : 0;
            var end = ui ? VoiceCapacity : start == 0 ? CombatCapacity : VoiceCapacity - 1;
            Voice slot = null;
            for (var i = start; i < end; i++)
            {
                var current = _voices[i];
                if (current.End <= now) { slot = current; break; }
                if (current.Priority <= priority && (slot == null || current.Priority < slot.Priority ||
                    (current.Priority == slot.Priority && current.Order < slot.Order))) slot = current;
            }
            if (slot == null) return false;

            var clip = catalog.Clip(cue, variation);
            if (clip == null) return false;
            slot.Clear();
            slot.Priority = priority;
            slot.Boss = boss;
            slot.Order = ++_voiceOrder;
            var startTime = now + delay;
            slot.End = startTime + clip.length;
            slot.Source.clip = clip;
            slot.Source.outputAudioMixerGroup = throughMusic ? catalog.Music : catalog.Sfx;
            slot.Source.mute = false;
            slot.Source.panStereo = pan;
            slot.Source.volume = gain * (ui ? .15f : start == 0 ? .058f : .13f);
            if (delay > 0f) slot.Source.PlayScheduled(startTime);
            else slot.Source.Play();

            _nextCue[index] = now + (cue == AudioCue.Reject ? .35 : ui ? .12 : start == 0 ? .06 : .1);
            return true;
        }

        private static int Priority(AudioCue cue)
        {
            switch (cue)
            {
                case AudioCue.Victory:
                case AudioCue.Defeat: return 4;
                case AudioCue.SealCast:
                case AudioCue.BossDeath:
                case AudioCue.BossLeak: return 3;
                case AudioCue.BossSpawn:
                case AudioCue.BossThreat:
                case AudioCue.SteamBurst:
                case AudioCue.StormChain: return 2;
                default: return 1;
            }
        }

        private sealed class Voice
        {
            private readonly AudioMixerGroup _sfx;

            internal Voice(AudioSource source, AudioMixerGroup sfx)
            {
                Source = source;
                _sfx = sfx;
            }

            internal AudioSource Source { get; }
            internal ulong Order { get; set; }
            internal double End { get; set; }
            internal int Priority { get; set; }
            internal EnemyId? Boss { get; set; }

            internal void Clear()
            {
                Source.Stop();
                Source.clip = null;
                Source.outputAudioMixerGroup = _sfx;
                Source.loop = false;
                Source.volume = 1f;
                Source.pitch = 1f;
                Source.panStereo = 0f;
                Boss = null;
                Order = 0;
                End = 0;
                Priority = 0;
            }
        }
    }
}
