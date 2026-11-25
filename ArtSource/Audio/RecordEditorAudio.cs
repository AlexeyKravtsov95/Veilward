// Invoked by the resident Editor Pipeline. Captures GameView and the engine's master output per frame.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Veilward.Domain;
using Veilward.Infrastructure;
using Veilward.Presentation;
using Veilward.Tests.Shared;
using Object = UnityEngine.Object;

public sealed class RecordEditorAudio : MonoBehaviour
{
    private const string Root = "TestResults/audio1/capture";
    private const string Prefix = "Veilward.Audio.v1.";
    private const int FrameRate = 24;
    private const int Width = 960;
    private const int Height = 444;
    private static readonly string[] Keys = { "Master", "Music", "SFX", "Mute" };
    private readonly List<object> _marks = new List<object>();
    private readonly HashSet<string> _observedClips = new HashSet<string>();
    private readonly HashSet<string> _loops = new HashSet<string>();
    private readonly Dictionary<string, int> _maximumVoices = new Dictionary<string, int>();
    private readonly Dictionary<string, object> _budgetAtPeak = new Dictionary<string, object>();
    private readonly List<string> _errors = new List<string>();
    private BinaryWriter _audio;
    private MethodInfo _capture;
    private MethodInfo _prepare;
    private WaveCheckpointStore _store;
    private bool[] _had;
    private float[] _before;
    private int _mute;
    private int _oldRate;
    private int _frames;
    private long _samples;
    private float _peak;
    private bool _started;
    private bool _finished;
    private string _stage = "menu-loop";
    private AudioSource _lastMusic;
    private int _lastPosition;

    private BattleSceneController Battle => FindFirstObjectByType<BattleSceneController>();
    private VeilwardAudio Owner => FindFirstObjectByType<VeilwardAudio>();

    public static string Begin()
    {
        if (!EditorApplication.isPlaying || Directory.Exists(Root))
            throw new InvalidOperationException("Use a clean capture directory and a running Editor Play Mode.");
        Directory.CreateDirectory(Root + "/frames");
        var host = new GameObject("Audio.1 evidence recorder");
        DontDestroyOnLoad(host);
        var recorder = host.AddComponent<RecordEditorAudio>();
        recorder._store = new WaveCheckpointStore(Path.GetFullPath(Root + "/saves"));
        MainMenuController.CheckpointStoreFactory = () => recorder._store;
        recorder._had = Keys.Select(key => PlayerPrefs.HasKey(Prefix + key)).ToArray();
        recorder._before = Keys.Take(3).Select(key => PlayerPrefs.GetFloat(Prefix + key)).ToArray();
        recorder._mute = PlayerPrefs.GetInt(Prefix + "Mute");
        foreach (var key in Keys) PlayerPrefs.DeleteKey(Prefix + key);
        recorder._oldRate = Time.captureFramerate;
        recorder._audio = new BinaryWriter(File.OpenWrite(Root + "/mix.f32"));
        var pipeline = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("Unity.Pipeline.Editor.Commands.Capture.CaptureCommands"))
            .First(type => type != null);
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
        recorder._capture = pipeline.GetMethod("EncodeScreenToPng", flags);
        recorder._prepare = pipeline.GetMethod("PrepareGameView", flags);
        Application.logMessageReceived += recorder.TrackErrors;
        recorder.StartCoroutine(recorder.Run());
        return Root;
    }

    private IEnumerator Run()
    {
        yield return SceneManager.LoadSceneAsync("MainMenu");
        yield return WaitUntil(() => Owner != null && Music() != null);
        Time.captureFramerate = FrameRate;
        _started = AudioRenderer.Start();
        if (!_started) throw new InvalidOperationException("Unity failed to start main-output capture.");
        StartCoroutine(RecordFrames());
        Mark("menu-loop");
        yield return WaitUntil(() => _loops.Contains("MenuMusic"), 32);
        yield return Hold(1.3f);
        Click("Audio Button");
        Mark("main-menu-audio");
        yield return Hold(.7f);
        Find<Slider>("Music Volume").value = .43f;
        yield return Hold(.75f);
        Click("Mute Button");
        Mark("menu-mute-on");
        yield return Hold(1.4f);
        Click("Mute Button");
        Mark("menu-mute-off-restored");
        yield return Hold(1.15f);
        Find<Slider>("Music Volume").value = .65f;
        Click("Close Audio Button");
        yield return Hold(.45f);

        Click("New Game Button");
        yield return WaitUntil(() => Battle != null && Battle.Simulation != null);
        Mark("ordinary-command-flow");
        yield return Hold(.45f);
        Click("flame");
        yield return Hold(.45f);
        Click("Confirm Element Button");
        yield return Hold(.45f);
        var battle = Battle;
        var placement = battle.GetComponent<BattlePlacementController>();
        Need(placement.TrySelectCell(new GridCoordinate(-4, 0)), "Select build socket.");
        Click("Catalog flame");
        yield return Hold(.55f);
        Click("Confirm Button");
        Need(battle.Simulation.State.Towers.Count == 1, "The build command succeeds.");
        yield return Hold(.45f);
        Click("Start Wave Button");
        Mark("ordinary-wave-one");
        var simulation = battle.Simulation;
        yield return WaitUntil(() => simulation.State.Enemies.Count > 0);
        yield return Hold(1f);
        Click("Ability Button");
        var ability = battle.GetComponent<AbilityController>();
        Need(ability.SelectTarget(new PathPosition(-3, 1.5f)), "Track targeting.");
        yield return Hold(.6f);
        Click("Cancel Ability Button");
        yield return Hold(.4f);
        Click("Ability Button");
        Need(simulation.State.Enemies.Count > 0, "An active target is available.");
        Need(ability.SelectTarget(simulation.Path.GetPosition(simulation.State.Enemies[0].DistanceAlongPath)), "Aim at the real enemy.");
        yield return Hold(.08f);
        Click("Confirm Ability Button");
        yield return WaitUntil(() => ability.LastCast.HasValue, 3);
        Need(ability.LastCast.Value.Succeeded && ability.LastCast.Value.TotalAppliedHpLoss > 0, "The real seal applies HP loss.");
        Mark("accepted-seal");
        yield return Hold(1.6f);

        Click("Pause Button");
        Click("Audio Button");
        Mark("pause-audio");
        yield return Hold(.6f);
        Find<Slider>("Master Volume").value = .72f;
        yield return Hold(.5f);
        Find<Slider>("SFX Volume").value = .5f;
        yield return Hold(.5f);
        Find<Slider>("Music Volume").value = .53f;
        yield return Hold(.7f);
        Click("Close Audio Button");
        yield return Hold(.4f);
        Click("Resume Button");
        Mark("resume");
        yield return WaitUntil(() => _loops.Contains("BattleMusic"), 33);
        yield return Hold(1.6f);
        Click("Pause Button");
        Click("Main Menu Button");
        yield return WaitUntil(() => FindFirstObjectByType<MainMenuController>() != null);
        yield return Hold(.5f);
        Click("Continue Button");
        yield return WaitUntil(() => Battle != null && Battle.Simulation != null);
        Mark("file-continue");
        yield return Hold(1.4f);
        yield return Dense();
        yield return EarnedVictory();
        yield return LeakDefeat();
        Finish();
    }

    private IEnumerator Dense()
    {
        yield return SceneManager.LoadSceneAsync("Battle");
        yield return WaitUntil(() => Battle != null && Battle.Simulation != null);
        Mark("synthetic-six-tower-density");
        var battle = Battle;
        var production = battle.Simulation;
        var shade = new EnemyDefinition(new EnemyDefinitionId("shade"), 1000, 1.1f, 1, 0, 5);
        var boss = production.Waves[7].Schedule.First(group => group.Enemy.Id.Value == BossHudController.BossId).Enemy;
        var wave = new WaveDefinition(new[] { new WaveSpawnGroup(shade, 8, 0, 23), new WaveSpawnGroup(boss, 1, 115, 1) });
        var staged = new BattleSimulation(production.Grid.Map, production.Path, new[] { wave }, production.TowerCatalog,
            20, 1200, 20, production.ReactionCatalog, null, AbilityDefinition.RiftSeal);
        SetFixture(battle, staged, "editor-audio-density");
        var types = new[] { "flame", "tide", "gale", "stone", "steam", "storm" };
        var cells = new[] { new GridCoordinate(-5, -1), new GridCoordinate(-4, -1), new GridCoordinate(-3, -1),
            new GridCoordinate(-2, -1), new GridCoordinate(-5, 0), new GridCoordinate(-4, 0) };
        for (var i = 0; i < types.Length; i++)
            Need(battle.PlaceTower(new TowerDefinitionId(types[i]), cells[i]).Success, "Place six fixture towers.");
        yield return null;
        Click("Start Wave Button");
        yield return Hold(10f);
        Need(staged.State.Enemies.Count >= 6, "Dense fixture has at least six live enemies.");
    }

    private IEnumerator EarnedVictory()
    {
        yield return SceneManager.LoadSceneAsync("MainMenu");
        yield return WaitUntil(() => FindFirstObjectByType<MainMenuController>() != null);
        var menu = FindFirstObjectByType<MainMenuController>();
        Mark("earned-wave-eight-continue");
        var config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWaveAbilityBossV2.asset");
        var script = AbilityBossTrace.Load("flame");
        var run = new AbilityBossTrace.Run(config.CreateSimulation(), script);
        WaveCheckpointEnvelope earned = null;
        run.stable = (replay, phase) =>
        {
            if (phase == "preparation" && replay.sim.State.WaveIndex == 7)
                earned = AbilityBossTrace.Envelope(replay.sim);
        };
        run.Play();
        Need(earned != null && run.sim.State.Phase == BattlePhase.Victory, "Winning route earned the wave eight checkpoint.");
        Need(_store.TrySave(earned, run.sim.TryValidateCheckpoint, out var error, config.CreateSimulationForCheckpoint), error);
        menu.RefreshCheckpointStatus();
        Click("Continue Button");
        yield return WaitUntil(() => Battle != null && Battle.Simulation != null);
        yield return Hold(.7f);
        Click("Start Wave Button");
        var battle = Battle;
        var simulation = battle.Simulation;
        var ability = battle.GetComponent<AbilityController>();
        foreach (var cast in script.casts.Where(row => row.wave == 8))
        {
            yield return WaitUntil(() => simulation.WaveElapsedTicks >= cast.relativeTick, 25);
            Need(ability.BeginPreview() && ability.SelectTarget(new PathPosition(cast.x, cast.y)) && ability.ConfirmPreview(),
                "Cast from the winning replay.");
            yield return WaitUntil(() => ability.LastCast.HasValue, 3);
            Need(ability.LastCast.Value.Succeeded, "The earned cast is accepted.");
        }
        yield return WaitUntil(() => simulation.State.Phase != BattlePhase.Combat, 40);
        Need(simulation.State.Phase == BattlePhase.Victory, "Production Update reaches the earned Victory.");
        Mark("earned-victory");
        yield return Hold(2.6f);
    }

    private IEnumerator LeakDefeat()
    {
        yield return SceneManager.LoadSceneAsync("Battle");
        yield return WaitUntil(() => Battle != null && Battle.Simulation != null);
        Mark("synthetic-boss-leak");
        var battle = Battle;
        var production = battle.Simulation;
        var boss = new EnemyDefinition(new EnemyDefinitionId(BossHudController.BossId), 600, 4f, 20, 70, 5);
        var wave = new WaveDefinition(new[] { new WaveSpawnGroup(boss, 1, 0, 1) });
        var staged = new BattleSimulation(production.Grid.Map, production.Path, new[] { wave }, production.TowerCatalog,
            20, 150, 20, production.ReactionCatalog, null, AbilityDefinition.RiftSeal);
        SetFixture(battle, staged, "editor-audio-boss-leak");
        yield return null;
        Click("Start Wave Button");
        yield return WaitUntil(() => staged.State.Phase == BattlePhase.Defeat, 18);
        Need(staged.State.BaseHealth == 0, "The test Warden actually leaks and causes Defeat.");
        Mark("synthetic-leak-defeat");
        yield return Hold(2.6f);
    }

    private static void SetFixture(BattleSceneController battle, BattleSimulation simulation, string id)
    {
        typeof(BattleSceneController).GetField("_simulation", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(battle, simulation);
        battle.GetComponent<BattleCheckpointController>().Configure(null, simulation, id);
        battle.ResetPresentation();
    }

    private IEnumerator RecordFrames()
    {
        while (_started)
        {
            yield return new WaitForEndOfFrame();
            if (!_started) yield break;
            var count = AudioRenderer.GetSampleCountForCaptureFrame();
            using (var frame = new NativeArray<float>(count, Allocator.Temp))
            {
                Need(AudioRenderer.Render(frame), "Render frame master output.");
                foreach (var sample in frame)
                {
                    _audio.Write(sample);
                    _peak = Mathf.Max(_peak, Mathf.Abs(sample));
                }
                _samples += frame.Length;
            }

            _prepare.Invoke(null, null);
            var png = (byte[])_capture.Invoke(null, new object[] { Width, Height });
            File.WriteAllBytes($"{Root}/frames/{_frames:D05}.png", png);
            _frames++;

            var music = Music();
            if (music == null) continue;
            if (music == _lastMusic && music.isPlaying && music.timeSamples < _lastPosition &&
                _loops.Add(music.clip.name))
                Mark(music.clip.name + "-wrap", false);
            _lastMusic = music;
            _lastPosition = music.timeSamples;
            var voices = Owner.GetComponents<AudioSource>().Where(source => source != music && source.clip != null).ToArray();
            foreach (var source in voices)
                if (source.isPlaying && !source.mute && _observedClips.Add(source.clip.name)) Mark(source.clip.name, false);
            if (!_maximumVoices.TryGetValue(_stage, out var previous) || voices.Length > previous)
            {
                _maximumVoices[_stage] = voices.Length;
                var clips = Resources.FindObjectsOfTypeAll<AudioClip>().Where(clip =>
                    AssetDatabase.GetAssetPath(clip).StartsWith("Assets/_Game/Audio/Clips/", StringComparison.Ordinal)).ToArray();
                _budgetAtPeak[_stage] = new
                {
                    sources = Owner.GetComponents<AudioSource>().Length,
                    registeredClips = clips.Length,
                    loadedSfx = clips.Count(clip => clip.loadType == AudioClipLoadType.DecompressOnLoad &&
                        clip.loadState == AudioDataLoadState.Loaded),
                    streamingReady = clips.Count(clip => clip.loadType == AudioClipLoadType.Streaming &&
                        clip.loadState == AudioDataLoadState.Loaded),
                    musicPlaying = music.isPlaying
                };
            }
        }
    }

    private AudioSource Music() => Owner != null ? Owner.GetComponents<AudioSource>().SingleOrDefault(source => source.loop) : null;

    private void Mark(string label, bool stage = true)
    {
        if (stage) _stage = label;
        _marks.Add(new { label, frame = _frames, UnityEngine.Time.frameCount, scene = SceneManager.GetActiveScene().name,
            tick = Battle?.Simulation?.State.Tick, dspTime = AudioSettings.dspTime });
    }

    private void LateUpdate()
    {
        if (_errors.Count > 0) Finish();
    }

    private void Finish()
    {
        if (_finished) return;
        _finished = true;
        File.WriteAllText(Root + "/manifest.json", JsonConvert.SerializeObject(new
        {
            frameRate = FrameRate, width = Width, height = Height, frames = _frames, sampleRate = AudioSettings.outputSampleRate,
            samples = _samples, peak = _peak, marks = _marks, activeVoices = _maximumVoices,
            budgetAtPeak = _budgetAtPeak, errors = _errors
        }, Formatting.Indented));
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= TrackErrors;
        if (_started) AudioRenderer.Stop();
        _started = false;
        _audio?.Dispose();
        MainMenuController.CheckpointStoreFactory = null;
        Time.captureFramerate = _oldRate;
        if (_had == null) return;
        for (var i = 0; i < 3; i++)
            if (_had[i]) PlayerPrefs.SetFloat(Prefix + Keys[i], _before[i]);
            else PlayerPrefs.DeleteKey(Prefix + Keys[i]);
        if (_had[3]) PlayerPrefs.SetInt(Prefix + Keys[3], _mute);
        else PlayerPrefs.DeleteKey(Prefix + Keys[3]);
        PlayerPrefs.Save();
    }

    private void TrackErrors(string message, string trace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            _errors.Add(message);
    }

    private static IEnumerator WaitUntil(Func<bool> ready, float maximum = 14)
    {
        var remaining = Mathf.RoundToInt(maximum * FrameRate);
        while (!ready() && remaining-- > 0) yield return null;
        Need(ready(), "Expected production state did not arrive.");
    }

    private static IEnumerator Hold(float seconds)
    {
        for (var frame = 0; frame < Mathf.RoundToInt(seconds * FrameRate); frame++) yield return null;
    }

    private static void Click(string name)
    {
        var button = Find<Button>(name);
        Need(button.IsInteractable(), "Button disabled: " + name);
        button.onClick.Invoke();
    }

    private static T Find<T>(string name) where T : Component =>
        FindObjectsByType<T>(FindObjectsSortMode.None).Single(obj => obj.name == name);

    private static void Need(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
