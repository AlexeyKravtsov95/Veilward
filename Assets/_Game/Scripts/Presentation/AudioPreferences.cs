using UnityEngine;

namespace Veilward.Presentation
{
    internal readonly struct AudioPreferences
    {
        internal const string Prefix = "Veilward.Audio.v1.";
        internal const float DefaultMaster = 1f;
        internal const float DefaultMusic = .65f;
        internal const float DefaultSfx = .8f;
        internal const float MinimumDecibels = -80f;

        internal AudioPreferences(float master, float music, float sfx, bool muted)
        {
            Master = Sanitize(master, DefaultMaster);
            Music = Sanitize(music, DefaultMusic);
            Sfx = Sanitize(sfx, DefaultSfx);
            Muted = muted;
        }

        internal float Master { get; }
        internal float Music { get; }
        internal float Sfx { get; }
        internal bool Muted { get; }

        internal static AudioPreferences Load() => new AudioPreferences(
            PlayerPrefs.GetFloat(Prefix + "Master", DefaultMaster),
            PlayerPrefs.GetFloat(Prefix + "Music", DefaultMusic),
            PlayerPrefs.GetFloat(Prefix + "SFX", DefaultSfx),
            PlayerPrefs.GetInt(Prefix + "Mute", 0) == 1);

        internal void Save()
        {
            PlayerPrefs.SetFloat(Prefix + "Master", Master);
            PlayerPrefs.SetFloat(Prefix + "Music", Music);
            PlayerPrefs.SetFloat(Prefix + "SFX", Sfx);
            PlayerPrefs.SetInt(Prefix + "Mute", Muted ? 1 : 0);
            PlayerPrefs.Save();
        }

        internal static float ToDecibels(float linear) =>
            linear <= .0001f ? MinimumDecibels : 20f * Mathf.Log10(linear);

        private static float Sanitize(float value, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp01(value);
    }
}
