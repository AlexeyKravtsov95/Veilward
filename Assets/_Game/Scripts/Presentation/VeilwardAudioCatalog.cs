using UnityEngine;
using UnityEngine.Audio;

namespace Veilward.Presentation
{
    internal enum AudioCue
    {
        FlameShot, FlameImpact, TideShot, TideImpact, GaleShot, GaleImpact,
        StoneShot, StoneImpact, SteamShot, SteamImpact, StormShot, StormImpact,
        SteamBurst, StormChain, StormHop, SealTarget, SealCast, SealHit,
        BossSpawn, BossThreat, BossDeath, BossLeak, WaveStart, WaveEnd, Victory, Defeat,
        Select, Confirm, Cancel, Reject, Build, Upgrade, Sell, Unlock, SaveError, SaveRecovered,
        Count
    }

    public sealed class VeilwardAudioCatalog : ScriptableObject
    {
        [SerializeField] private AudioMixerGroup music;
        [SerializeField] private AudioMixerGroup sfx;
        [SerializeField] private AudioClip menuMusic;
        [SerializeField] private AudioClip battleMusic;
        [SerializeField] private AudioClip[] clips;
        [SerializeField] private AudioClip[] variations;

        internal AudioMixerGroup Music => music;
        internal AudioMixerGroup Sfx => sfx;
        internal AudioClip MusicClip(bool battle) => battle ? battleMusic : menuMusic;

        internal AudioClip Clip(AudioCue cue, bool variation)
        {
            var index = (int)cue;
            return variation && index < variations.Length ? variations[index] : clips[index];
        }
    }
}
