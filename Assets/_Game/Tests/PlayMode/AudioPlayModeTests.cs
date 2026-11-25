using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Veilward.Application;
using Veilward.Domain;
using Veilward.Infrastructure;
using Veilward.Presentation;
using Object = UnityEngine.Object;

namespace Veilward.Tests.PlayMode
{
    public sealed class AudioPlayModeTests
    {
        private static readonly string[] Keys = { "Master", "Music", "SFX", "Mute" };
        private const string Prefix = AudioPreferences.Prefix;
        private bool[] _existed;
        private float[] _previousLevels;
        private int _previousMute;
        private string _directory;
        private WaveCheckpointStore _store;

        [SetUp]
        public void SetUp()
        {
            _existed = Keys.Select(key => PlayerPrefs.HasKey(Prefix + key)).ToArray();
            _previousLevels = Keys.Take(3).Select(key => PlayerPrefs.GetFloat(Prefix + key)).ToArray();
            _previousMute = PlayerPrefs.GetInt(Prefix + "Mute");
            foreach (var key in Keys) PlayerPrefs.DeleteKey(Prefix + key);
            _directory = Path.GetFullPath(Path.Combine("TestResults", "Audio1Tests", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(_directory);
            _store = new WaveCheckpointStore(_directory);
            MainMenuController.CheckpointStoreFactory = () => _store;
            BattleSessionContext.Clear();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            MainMenuController.CheckpointStoreFactory = null;
            BattleSessionContext.Clear();
            yield return SceneManager.LoadSceneAsync("Battle");
            for (var i = 0; i < 3; i++)
                if (_existed[i]) PlayerPrefs.SetFloat(Prefix + Keys[i], _previousLevels[i]);
                else PlayerPrefs.DeleteKey(Prefix + Keys[i]);
            if (_existed[3]) PlayerPrefs.SetInt(Prefix + "Mute", _previousMute);
            else PlayerPrefs.DeleteKey(Prefix + "Mute");
            PlayerPrefs.Save();
            Directory.Delete(_directory, true);
        }

        [UnityTest]
        public IEnumerator Audio101_MenuAndPauseSettingsPersistAndBlockUnderlyingActions()
        {
            Assert.That(AudioPreferences.Load().Sfx, Is.EqualTo(.8f));
            yield return Load("MainMenu");
            var owner = Owner();
            var menu = Object.FindFirstObjectByType<MainMenuController>();
            AssertMusic(owner, "MenuMusic");
            Button("Audio Button").onClick.Invoke();
            Assert.That(menu.NewGameButton.IsInteractable(), Is.False);
            menu.NewGameButton.onClick.Invoke();
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
            yield return null;
            Canvas.ForceUpdateCanvases();
            var newGameArea = (RectTransform)menu.NewGameButton.transform;
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, newGameArea.TransformPoint(newGameArea.rect.center))
            }, hits);
            var overlay = Find<RectTransform>("Audio Settings Overlay");
            Assert.That(hits.Count, Is.GreaterThan(0));
            Assert.That(hits[0].gameObject.transform == overlay || hits[0].gameObject.transform.IsChildOf(overlay),
                Is.True, string.Join(", ", hits.Select(hit => hit.gameObject.name)));
            Assert.That(hits.All(hit => hit.gameObject != menu.NewGameButton.gameObject), Is.True);

            Find<Slider>("Master Volume").value = .40f;
            Find<Slider>("Music Volume").value = .53f;
            Find<Slider>("SFX Volume").value = .67f;
            var mixer = owner.MusicSource.outputAudioMixerGroup.audioMixer;
            Assert.That(mixer.GetFloat(VeilwardAudio.MusicParameter, out var musicDb), Is.True);
            Assert.That(musicDb, Is.EqualTo(AudioPreferences.ToDecibels(.53f)).Within(.001f));
            Button("Mute Button").onClick.Invoke();
            Assert.That(owner.GetComponents<AudioSource>().All(source => source.mute), Is.True);
            Assert.That(mixer.GetFloat(VeilwardAudio.MasterParameter, out var mutedDb), Is.True);
            Assert.That(mutedDb, Is.EqualTo(-80f));
            Button("Mute Button").onClick.Invoke();
            Assert.That(owner.GetComponents<AudioSource>().All(source => !source.mute), Is.True);
            Assert.That(mixer.GetFloat(VeilwardAudio.MasterParameter, out var masterDb), Is.True);
            Assert.That(masterDb, Is.EqualTo(AudioPreferences.ToDecibels(.4f)).Within(.001f));

            Button("Close Audio Button").onClick.Invoke();
            Assert.That(menu.NewGameButton.IsInteractable(), Is.True);
            AssertLevels(AudioPreferences.Load(), .4f, .53f, .67f, false);
            menu.NewGameButton.onClick.Invoke();
            yield return null;
            yield return null;
            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            var pause = battle.GetComponent<BattlePauseController>();
            owner = Owner();
            AssertMusic(owner, "BattleMusic");
            AssertLevels(owner.Preferences, .4f, .53f, .67f, false);
            var before = File.ReadAllBytes(_store.PrimaryPath);
            Button("Pause Button").onClick.Invoke();
            Button("Audio Button").onClick.Invoke();
            Assert.That(pause.Resume(), Is.False);
            battle.ReturnToMenu();
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Battle"));
            Find<Slider>("SFX Volume").value = 0;
            Assert.That(Voices(owner).All(source => source.mute), Is.True);
            Assert.That(owner.MusicSource.mute, Is.False);
            Button("Close Audio Button").onClick.Invoke();
            Assert.That(pause.Resume(), Is.True);
            Assert.That(File.ReadAllBytes(_store.PrimaryPath), Is.EqualTo(before));

            battle.ReturnToMenu();
            yield return null;
            yield return null;
            Object.FindFirstObjectByType<MainMenuController>().ContinueButton.onClick.Invoke();
            yield return null;
            yield return null;
            AssertMusic(Owner(), "BattleMusic");
            AssertLevels(Owner().Preferences, .4f, .53f, 0f, false);
            Assert.That(File.ReadAllBytes(_store.PrimaryPath), Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator Audio102_PoolsStayBoundedAndPriorityWinsOverEarlierCues()
        {
            yield return Load("Battle");
            var audio = Owner();
            for (var i = 0; i < 12; i++) Assert.That(audio.Play((AudioCue)i, .3f, delay: .35f), Is.True);
            Assert.That(Voices(audio).Count(source => source.clip != null), Is.EqualTo(VeilwardAudio.CombatCapacity));
            Assert.That(Voices(audio).Any(source => source.clip != null && source.clip.name == "FlameShot1"), Is.False);
            var after = Voices(audio).Single(source => source.clip != null && source.clip.name == "StormImpact1");
            Assert.That(after.panStereo, Is.EqualTo(.3f));
            Assert.That(audio.Play(AudioCue.SealCast), Is.True);
            Assert.That(audio.Play(AudioCue.BossDeath), Is.True);
            Assert.That(audio.Play(AudioCue.Victory), Is.True);
            Assert.That(audio.Play(AudioCue.WaveStart), Is.False);
            Assert.That(audio.Play(AudioCue.Select), Is.True);
            Assert.That(audio.Play(AudioCue.Select), Is.False);
            Assert.That(Voices(audio).Count(source => source.clip != null), Is.EqualTo(VeilwardAudio.VoiceCapacity));
            Assert.That(Voices(audio).All(source => source.outputAudioMixerGroup.name == "SFX"), Is.True);
            audio.ClearRun();
            Assert.That(Voices(audio).All(source => source.clip == null && source.volume == 1f &&
                source.pitch == 1f && !source.loop && source.panStereo == 0f), Is.True);
            Assert.That(audio.Play(AudioCue.FlameShot, variation: true), Is.True);
            Assert.That(Clips(audio), Is.EqualTo(new[] { "FlameShot2" }));
        }

        [UnityTest]
        public IEnumerator Audio102_ActualCommandResultsChooseTheirOwnCues()
        {
            yield return Load("Battle");
            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            var audio = Owner();
            var choice = battle.GetComponent<ElementChoiceController>();
            Assert.That(choice.SelectOption("flame"), Is.True);
            Assert.That(Clips(audio), Does.Contain("Select"));
            Assert.That(choice.ConfirmChoice().Success, Is.True);
            Assert.That(Clips(audio), Does.Contain("Unlock"));
            var flame = new TowerDefinitionId("flame");
            var cell = new GridCoordinate(-4, 0);
            Assert.That(battle.PlaceTower(flame, cell).Success, Is.True);
            Assert.That(Clips(audio), Does.Contain("Build"));
            Assert.That(battle.PlaceTower(flame, cell).Success, Is.False);
            Assert.That(Clips(audio), Does.Contain("Reject"));
            Assert.That(Clips(audio), Does.Not.Contain("Build"));
            Assert.That(battle.Simulation.State.Towers.Count, Is.EqualTo(1));
            Assert.That(battle.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
            Assert.That(Clips(audio), Does.Contain("WaveStart"));
        }

        [UnityTest]
        public IEnumerator Audio102_ExactHitAttributionUsesCapturedPositionsAndDeduplicatesTicks()
        {
            yield return Load("Battle");
            var audio = Owner();
            var events = new BattleAudioEvents(audio, Camera.main, BattlePhase.Combat);
            var source = new TowerId(1);
            var target = new EnemyId(2);
            var otherEnemy = new EnemyId(3);
            var from = new PathPosition(3, -5);
            var endpoint = new PathPosition(-5, 2);
            var attack = new AttackEvent(source, target, endpoint, 17, 8, true);
            var wrong = Hit(17, 1, DamageOrigin.Direct, source, otherEnemy, from, 0);
            events.Attack(attack, new[] { wrong }, "flame", Vector3.zero, .20f);
            Assert.That(Clips(audio), Is.Empty);
            var matching = Hit(17, 2, DamageOrigin.Direct, source, target, endpoint, 0);
            events.Attack(attack, new[] { wrong, matching }, "flame", Vector3.zero, .20f);
            Assert.That(Clips(audio), Is.EquivalentTo(new[] { "FlameShot1", "FlameImpact1" }));
            var expectedPan = Mathf.Clamp(Camera.main.WorldToViewportPoint(new Vector3(endpoint.X, .6f, endpoint.Y)).x *
                2f - 1f, -1f, 1f) * .43f;
            Assert.That(Voices(audio).Single(voice => voice.clip != null && voice.clip.name == "FlameImpact1").panStereo,
                Is.EqualTo(expectedPan).Within(.0001f));
            audio.ClearRun();
            events.Attack(attack, new[] { matching }, "flame", Vector3.zero, .20f);
            Assert.That(Clips(audio), Is.Empty);
            var later = new AttackEvent(source, target, endpoint, 18, 8, false);
            events.Attack(later, new[] { Hit(18, 0, DamageOrigin.Direct, source, target, endpoint, 0) }, "tide", Vector3.zero, .20f);
            Assert.That(Clips(audio), Is.EquivalentTo(new[] { "TideShot2", "TideImpact2" }));

            audio.ClearRun();
            var storm = new ReactionEvent(18, 2, ReactionKind.Storm, ReactionTriggerKind.Intrinsic, source,
                target, from, StatusKind.Wet, StatusKind.Gust, 3, 3);
            var invalidChain = Hit(18, 3, DamageOrigin.Reaction, source, otherEnemy, endpoint, 1, 7);
            var chain = Hit(18, 4, DamageOrigin.Reaction, source, otherEnemy, endpoint, 1, 2);
            events.Reaction(storm, new[] { invalidChain, chain, chain });
            Assert.That(Clips(audio), Is.EqualTo(new[] { "StormChain" }));
            audio.ClearRun();
            events.Reaction(storm, new[] { chain });
            Assert.That(Clips(audio), Is.Empty);

            var id = new AbilityDefinitionId("rift-seal");
            var cast = new AbilityCastEvent(18, 5, id, 4, from, 1.5f, 1);
            var foreignCast = Hit(18, 6, DamageOrigin.Ability, default, target, endpoint, 0, ability: id, cast: 3);
            var castHit = Hit(18, 7, DamageOrigin.Ability, default, target, endpoint, 0, ability: id, cast: 4);
            events.Cast(cast, new[] { foreignCast, castHit });
            Assert.That(Clips(audio), Is.EquivalentTo(new[] { "SealCast", "SealHit" }));
            audio.ClearRun();
            events.Cast(cast, new[] { castHit });
            Assert.That(Clips(audio), Is.Empty);
        }

        [UnityTest]
        public IEnumerator Audio102_OrdinaryUpdateConsumesEveryStepInAFrame()
        {
            yield return Load("Battle");
            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            var choice = battle.GetComponent<ElementChoiceController>();
            Assert.That(choice.SelectOption("flame") && choice.ConfirmChoice().Success, Is.True);
            Assert.That(battle.PlaceTower(new TowerDefinitionId("flame"), new GridCoordinate(-4, 0)).Success, Is.True);
            var oldRate = Time.captureFramerate;
            try
            {
                Time.captureFramerate = 5;
                Assert.That(battle.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
                var simulation = battle.Simulation;
                var maxSteps = 0L;
                var heardAttack = false;
                for (var frame = 0; frame < 60 && !heardAttack; frame++)
                {
                    var previousTick = simulation.State.Tick;
                    yield return null;
                    maxSteps = Math.Max(maxSteps, simulation.State.Tick - previousTick);
                    heardAttack = Clips(Owner()).Any(clip => clip.StartsWith("FlameShot", StringComparison.Ordinal));
                    Assert.That(simulation.AttackEvents, Is.Empty);
                    Assert.That(simulation.DamageEvents, Is.Empty);
                }
                Assert.That(maxSteps, Is.InRange(2, 8));
                Assert.That(heardAttack, Is.True);
            }
            finally { Time.captureFramerate = oldRate; }
        }

        [UnityTest]
        public IEnumerator Audio103_PauseAndBackgroundDiscardSourcesAndResumeOneMusicStream()
        {
            yield return Load("Battle");
            var audio = Owner();
            var pause = Object.FindFirstObjectByType<BattlePauseController>();
            Assert.That(audio.Play(AudioCue.BossSpawn, delay: .25f), Is.True);
            Assert.That(audio.Play(AudioCue.GaleImpact, delay: .20f), Is.True);
            pause.Pause();
            Assert.That(audio.MusicSource.isPlaying, Is.False);
            Assert.That(Clips(audio), Is.Empty);
            Assert.That(audio.Play(AudioCue.BossLeak), Is.False);
            Assert.That(audio.Play(AudioCue.Cancel), Is.True);
            audio.SendMessage("OnApplicationPause", true);
            pause.OnApplicationPause(true);
            Assert.That(Clips(audio), Is.Empty);
            Assert.That(audio.Play(AudioCue.Select), Is.False);
            Assert.That(pause.Resume(), Is.False);
            audio.SendMessage("OnApplicationPause", false);
            pause.OnApplicationPause(false);
            Assert.That(audio.MusicSource.isPlaying, Is.False);
            Assert.That(pause.Resume(), Is.True);
            yield return null;
            Assert.That(audio.MusicSource.isPlaying, Is.True);
            Assert.That(Clips(audio), Is.Empty);
            AssertMusic(audio, "BattleMusic");
            audio.SendMessage("OnApplicationFocus", false);
            pause.OnApplicationFocus(false);
            Assert.That(audio.MusicSource.isPlaying, Is.False);
            Assert.That(pause.Resume(), Is.False);
            audio.SendMessage("OnApplicationFocus", true);
            pause.OnApplicationFocus(true);
            Assert.That(pause.Resume(), Is.True);
            yield return null;
            AssertMusic(audio, "BattleMusic");
        }

        private static DamageResolvedEvent Hit(long tick, int sequence, DamageOrigin origin, TowerId source,
            EnemyId target, PathPosition position, int hitIndex, int? reaction = null, AbilityDefinitionId? ability = null,
            long? cast = null) =>
            new DamageResolvedEvent(tick, sequence, origin, source, target, default, position,
                8, 8, 8, 8, 0, true, reaction.HasValue ? ReactionKind.Storm : (ReactionKind?)null,
                reaction, hitIndex, ability, cast);

        private static IEnumerator Load(string scene)
        {
            yield return SceneManager.LoadSceneAsync(scene);
            yield return null;
        }

        private static VeilwardAudio Owner() => Object.FindObjectsByType<VeilwardAudio>(FindObjectsSortMode.None).Single();
        private static T Find<T>(string name) where T : Component =>
            Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(item => item.name == name);
        private static Button Button(string name) => Find<Button>(name);
        private static AudioSource[] Voices(VeilwardAudio owner) =>
            owner.GetComponents<AudioSource>().Where(source => source != owner.MusicSource).ToArray();
        private static string[] Clips(VeilwardAudio owner) =>
            Voices(owner).Where(source => source.clip != null).Select(source => source.clip.name).ToArray();

        private static void AssertMusic(VeilwardAudio audio, string name)
        {
            Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(audio.GetComponents<AudioSource>().Length, Is.EqualTo(VeilwardAudio.VoiceCapacity + 1));
            Assert.That(audio.GetComponents<AudioSource>().Count(source => source.loop), Is.EqualTo(1));
            Assert.That(audio.MusicSource.clip.name, Is.EqualTo(name));
            Assert.That(audio.MusicSource.outputAudioMixerGroup.name, Is.EqualTo("Music"));
        }

        private static void AssertLevels(AudioPreferences preferences, float master, float music, float sfx, bool muted)
        {
            Assert.That(preferences.Master, Is.EqualTo(master));
            Assert.That(preferences.Music, Is.EqualTo(music));
            Assert.That(preferences.Sfx, Is.EqualTo(sfx));
            Assert.That(preferences.Muted, Is.EqualTo(muted));
        }
    }
}
