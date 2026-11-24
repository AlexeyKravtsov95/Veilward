// Editor-only evidence. The realtime segment advances through the regular Battle Update.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Veilward.Application;
using Veilward.Domain;
using Veilward.Infrastructure;
using Veilward.Presentation;
using Veilward.Tests.Shared;
using Object = UnityEngine.Object;

public sealed class HybridEditorRecording : MonoBehaviour
{
    private const string Root = "TestResults/vfx1c/video/";
    private static readonly BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
    private readonly List<object> _frames = new List<object>();
    private readonly List<string> _console = new List<string>();
    private WaveCheckpointStore _store;
    private string _stage;
    private bool _recording;
    private double _startTime;
    private MethodInfo _encode;
    private MethodInfo _prepare;
    private BattleSceneController Battle => Object.FindFirstObjectByType<BattleSceneController>();

    public static string StartRecording()
    {
        Require(EditorApplication.isPlaying, "Play Mode required.");
        Require(Object.FindFirstObjectByType<HybridEditorRecording>() == null, "Recording already present.");
        var root = new GameObject("VFX.1c realtime Editor evidence");
        DontDestroyOnLoad(root);
        root.AddComponent<HybridEditorRecording>().Begin();
        return "Recording production Update from earned FLAME Preparation8 and file Continue.";
    }

    private void Begin()
    {
        Require(!Directory.Exists(Root), "Use a new task-owned capture directory.");
        Directory.CreateDirectory(Root + "frames");
        var capture = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("Unity.Pipeline.Editor.Commands.Capture.CaptureCommands")).First(t => t != null);
        _prepare = capture.GetMethod("PrepareGameView", PrivateStatic);
        _encode = capture.GetMethod("EncodeScreenToPng", PrivateStatic);
        var target = (RenderTexture)capture.GetMethod("GetGameViewTargetTexture", PrivateStatic)
            .Invoke(null, new object[] { false });
        Require(target != null && target.width == 1600 && target.height == 900, "Composited 1600×900 GameView required.");
        Require(Time.timeScale == 1 && Time.captureFramerate == 0, "The realtime segment requires the normal Unity clock.");

        _store = new WaveCheckpointStore(Path.GetFullPath(Root + "store"));
        var config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWaveAbilityBossV2.asset");
        var replay = new AbilityBossTrace.Run(config.CreateSimulation(), AbilityBossTrace.Load("flame"));
        WaveCheckpointEnvelope earned = null;
        replay.stable = (r, phase) =>
        {
            if (phase == "preparation" && r.sim.State.WaveIndex == 7) earned = AbilityBossTrace.Envelope(r.sim);
        };
        replay.Play();
        Require(replay.sim.State.Phase == BattlePhase.Victory && earned != null, "Production Victory8 and earned checkpoint.");
        Require(_store.TrySave(earned, replay.sim.TryValidateCheckpoint, out var error, config.CreateSimulationForCheckpoint), error);
        var reference = config.CreateSimulationForCheckpoint(earned);
        reference.RestoreCheckpoint(earned.state);
        File.WriteAllBytes(Root + "earned-preparation8.json", File.ReadAllBytes(_store.PrimaryPath));
        File.WriteAllText(Root + "earned-state.txt", AbilityBossTrace.Snapshot(reference));

        MainMenuController.CheckpointStoreFactory = () => _store;
        BattleSessionContext.Clear();
        _stage = "MainMenu / earned Preparation8";
        _startTime = Time.realtimeSinceStartupAsDouble;
        _recording = true;
        Application.logMessageReceived += Log;
        StartCoroutine(Record());
        StartCoroutine(Flow(AbilityBossTrace.Snapshot(reference)));
    }

    private IEnumerator Flow(string earnedState)
    {
        if (SceneManager.GetActiveScene().name != "MainMenu")
            yield return SceneManager.LoadSceneAsync("MainMenu");
        yield return new WaitForSecondsRealtime(.55f);
        ContinueFromMenu();
        yield return WaitForBattle();
        var battle = Battle;
        Require(AbilityBossTrace.Snapshot(battle.Simulation) == earnedState, "MainMenu loaded whole earned state.");
        _stage = "Preparation8 / earned Continue";
        WriteJson(Root + "render-inputs.json", RenderInputs(battle));
        yield return new WaitForSecondsRealtime(.60f);
        Require(battle.StartWave() == WaveTransitionResult.Started, "Public Start Wave.");
        var safe = File.ReadAllBytes(_store.PrimaryPath);
        _stage = "Combat8 / production Update";
        var start = Time.realtimeSinceStartupAsDouble;
        while (!Shots(battle).Any(v => v.Kind == HybridShotKind.Storm && v.IsFlying &&
                   v.Remaining < .275f) && Time.realtimeSinceStartupAsDouble - start < 6)
            yield return null;
        Require(Shots(battle).Any(v => v.IsFlying), "Observed visible realtime attack before Pause.");
        var pause = battle.GetComponent<BattlePauseController>();
        pause.Pause();
        var tick = battle.Simulation.State.Tick;
        var freeze = JsonConvert.SerializeObject(AttackRows(battle));
        _stage = "Pause / frozen hybrid geometry";
        yield return new WaitForSecondsRealtime(.8f);
        Require(tick == battle.Simulation.State.Tick && freeze == JsonConvert.SerializeObject(AttackRows(battle)), "Pause preserves attack geometry and tick.");
        pause.OnApplicationFocus(true);
        Require(pause.Resume(), "Production Resume.");
        _stage = "Combat8 / production Update resumed";
        yield return new WaitForSecondsRealtime(12);
        Require(battle.Simulation.State.Phase == BattlePhase.Combat, "Interrupted live Combat8.");
        battle.ReturnToMenu();
        yield return null;
        yield return null;
        _stage = "MainMenu / interrupted Combat8";
        yield return new WaitForSecondsRealtime(.55f);
        ContinueFromMenu();
        yield return WaitForBattle();
        battle = Battle;
        Require(AbilityBossTrace.Snapshot(battle.Simulation) == earnedState && File.ReadAllBytes(_store.PrimaryPath).SequenceEqual(safe),
            "File Continue rolls back whole state and leaves save bytes unchanged.");
        Require(!Shots(battle).Any() && !battle.GetComponentsInChildren<ReactionEffectView>().Any() &&
            !battle.GetComponentsInChildren<EnemyStatusView>().Any(), "Continue clears presentation owners and effects.");
        File.WriteAllText(Root + "continued-state.txt", AbilityBossTrace.Snapshot(battle.Simulation));
        File.WriteAllBytes(Root + "continued-file.json", File.ReadAllBytes(_store.PrimaryPath));
        File.WriteAllBytes(Root + "combat-safe-file.json", safe);
        _stage = "Continue / preparation and clean effects";
        yield return new WaitForSecondsRealtime(.75f);
        Finish(true, "");
    }

    private IEnumerator WaitForBattle()
    {
        var deadline = Time.realtimeSinceStartupAsDouble + 5;
        while ((Battle == null || Battle.Simulation == null) && Time.realtimeSinceStartupAsDouble < deadline)
            yield return null;
        Require(Battle != null && Battle.Simulation != null, "Continue entered and initialized Battle.");
    }

    private static void ContinueFromMenu()
    {
        var menu = Object.FindFirstObjectByType<MainMenuController>();
        Require(menu != null, "MainMenu ready for file Continue.");
        menu.RefreshCheckpointStatus();
        Require(menu.ContinueButton.interactable, "Earned file Continue available.");
        menu.Continue();
    }

    private IEnumerator Record()
    {
        double next = 0;
        while (_recording)
        {
            yield return new WaitForEndOfFrame();
            var elapsed = Time.realtimeSinceStartupAsDouble - _startTime;
            if (elapsed < next) continue;
            next = elapsed + 1.0 / 30;
            var battle = Battle;
            if (battle != null && battle.Simulation == null) battle = null;
            var index = _frames.Count;
            _prepare.Invoke(null, null);
            var pixels = (byte[])_encode.Invoke(null, new object[] { 1600, 900 });
            File.WriteAllBytes(Root + "frames/" + index.ToString("D5") + ".png", pixels);
            _frames.Add(new
            {
                index, elapsed, sha256 = Hash(pixels), frame = Time.frameCount, stage = _stage,
                timeScale = Time.timeScale, captureFramerate = Time.captureFramerate,
                productionUpdate = battle != null && battle.enabled, paused = battle != null && battle.IsPaused,
                tick = battle == null ? 0 : battle.Simulation.State.Tick,
                wave = battle == null ? 0 : battle.Simulation.State.WaveIndex + 1,
                shots = battle == null ? null : AttackRows(battle),
                reactions = battle == null ? null : ReactionRows(battle),
                cues = battle == null ? null : battle.GetComponentsInChildren<EnemyStatusView>()
                    .Select(v => new { owner = v.name, first = v.FirstCue.Kind?.ToString(), second = v.SecondCue.Kind?.ToString(), slots = v.VisibleSlotCount }).ToArray(),
            });
        }
    }

    private static IEnumerable<HybridStrikeView> Shots(BattleSceneController battle) =>
        Enumerable.Range(0, HybridProjectilePool.Capacity).Select(battle.HybridProjectiles.GetView).Where(v => v.Remaining > 0);

    public static object[] AttackRows(BattleSceneController battle)
    {
        return Shots(battle).Select(v =>
        {
            var slot = Enumerable.Range(0, HybridProjectilePool.Capacity).First(i => battle.HybridProjectiles.GetView(i) == v);
            var root = battle.transform.Find("Hybrid Projectile Pool " + slot);
            return (object)new
            {
                slot, kind = v.Kind.ToString(), tick = v.Event.Tick, tower = v.Event.AttackerTowerId.Value,
                enemy = v.Event.TargetEnemyId.Value, level = v.Level, lethal = v.Event.WasLethal, v.Remaining, v.IsFlying,
                source = Vector(v.Source), head = Vector(v.HeadPosition), target = Vector(v.Target), lines = Lines(root),
            };
        }).ToArray();
    }

    public static object[] ReactionRows(BattleSceneController battle) =>
        battle.GetComponentsInChildren<ReactionEffectView>()
            .Select(v => (object)new
            {
                kind = v.Event.Kind.ToString(), trigger = v.Event.TriggerKind.ToString(), tick = v.Event.Tick,
                sequence = v.Event.SequenceInTick, tower = v.Event.SourceTowerId.Value, primary = v.Event.PrimaryEnemyId.Value,
                primaryPosition = new[] { v.Event.PrimaryPosition.X, v.Event.PrimaryPosition.Y },
                range = v.Event.RadiusOrHopRange, limit = v.Event.TargetLimit, v.RemainingSeconds,
                hits = Enumerable.Range(0, v.HitCount).Select(v.GetHit).Select(h => new
                {
                    index = h.HitIndex, enemy = h.TargetEnemyId.Value, from = new[] { h.FromPosition.X, h.FromPosition.Y },
                    to = new[] { h.TargetPosition.X, h.TargetPosition.Y }, loss = h.AppliedHpLoss, lethal = h.WasLethal,
                }).ToArray(),
                lines = Lines(v.transform),
            }).ToArray();

    private static object[] Lines(Transform root)
    {
        var camera = Camera.main;
        var block = new MaterialPropertyBlock();
        return root.GetComponentsInChildren<LineRenderer>().Where(l => l.enabled).Select(l =>
        {
            l.GetPropertyBlock(block);
            var color = block.GetColor("_BaseColor");
            return (object)new
            {
                l.name, l.startWidth, l.endWidth, tint = new[] { color.r, color.g, color.b },
                screen = Enumerable.Range(0, l.positionCount)
                    .Select(i => camera.WorldToViewportPoint(l.GetPosition(i)))
                    .Select(p => new[] { p.x, p.y }).ToArray(),
            };
        }).ToArray();
    }

    private static object RenderInputs(BattleSceneController battle)
    {
        var all = battle.GetComponentsInChildren<LineRenderer>(true);
        var hybrid = all.Where(l => l.transform.parent.name.StartsWith("Hybrid Projectile Pool", StringComparison.Ordinal)).ToArray();
        var reaction = all.Where(l => l.GetComponentInParent<ReactionEffectView>(true) != null).ToArray();
        var material = hybrid.Concat(reaction).Select(l => l.sharedMaterial).Distinct().Single();
        var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        var path = AssetDatabase.GetAssetPath(pipeline);
        var camera = Camera.main;
        var dependencies = AssetDatabase.GetDependencies(new[] { "Assets/_Game/Scenes/Battle.unity", path }, true)
            .Concat(new[]
            {
                AssetDatabase.GetAssetPath(material.shader), "Packages/packages-lock.json",
                "ProjectSettings/GraphicsSettings.asset", "ProjectSettings/QualitySettings.asset",
                "Packages/com.unity.pipeline/package.json",
                "Packages/com.unity.pipeline/Editor/Commands/Capture/CaptureCommands.cs",
                "Packages/com.unity.render-pipelines.universal/package.json",
                "Packages/com.unity.render-pipelines.universal/Shaders/UnlitInput.hlsl",
                "Packages/com.unity.render-pipelines.universal/Shaders/UnlitForwardPass.hlsl",
                "Packages/com.unity.render-pipelines.core/package.json",
            })
            .Distinct().OrderBy(p => p, StringComparer.Ordinal)
            .Select(p => new { path = p, resolved = Resolve(p) }).Where(p => File.Exists(p.resolved))
            .Select(p => new { p.path, sha256 = Hash(File.ReadAllBytes(p.resolved)) }).ToArray();
        return new
        {
            actualPlayMode = true, unity = Application.unityVersion, graphics = SystemInfo.graphicsDeviceType.ToString(),
            quality = QualitySettings.names[QualitySettings.GetQualityLevel()], pipeline = path,
            camera = new { position = Vector(camera.transform.position), rotation = Vector(camera.transform.rotation.eulerAngles),
                camera.orthographicSize, camera.aspect },
            hybridViews = HybridProjectilePool.Capacity, hybridRenderers = hybrid.Length, hybridSlots = hybrid.Sum(l => l.sharedMaterials.Length),
            reactionViews = battle.GetComponentsInChildren<ReactionEffectView>(true).Length,
            reactionRenderers = reaction.Length, reactionSlots = reaction.Sum(l => l.sharedMaterials.Length),
            basicRenderers = all.Count(l => l.transform.parent.name.StartsWith("Basic Projectile Pool", StringComparison.Ordinal)),
            flameRenderers = all.Count(l => l.transform.parent.name.StartsWith("Flame Projectile Pool", StringComparison.Ordinal)),
            material = new { path = AssetDatabase.GetAssetPath(material), shader = AssetDatabase.GetAssetPath(material.shader),
                name = material.shader.name, passes = material.passCount, queue = material.renderQueue },
            dependencies,
        };
    }

    private static string Resolve(string path)
    {
        if (File.Exists(path)) return path;
        var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path);
        return package == null ? path : Path.Combine(package.resolvedPath, path.Substring(package.assetPath.Length + 1));
    }

    private void Log(string message, string stack, LogType type)
    {
        if (type != LogType.Log) _console.Add(type + "|" + message);
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) Finish(false, message);
    }

    private void Finish(bool ok, string error)
    {
        if (!_recording) return;
        _recording = false;
        StopAllCoroutines();
        Application.logMessageReceived -= Log;
        MainMenuController.CheckpointStoreFactory = null;
        File.WriteAllLines(Root + "console.txt", _console);
        WriteJson(Root + "frames.json", _frames);
        WriteJson(Root + "completion.json", new { ok, error, frames = _frames.Count, realTime = true, productionUpdate = true,
            liveResourceMutation = false, elapsed = Time.realtimeSinceStartupAsDouble - _startTime });
    }

    private void OnDestroy() => Finish(false, "Play Mode ended during capture.");
    private static float[] Vector(Vector3 v) => new[] { v.x, v.y, v.z };
    private static void WriteJson(string path, object value) => File.WriteAllText(path, JsonConvert.SerializeObject(value) + "\n");
    private static string Hash(byte[] bytes)
    {
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }
    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
