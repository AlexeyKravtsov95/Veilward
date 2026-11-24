// Editor-only capture using the existing GameView target and normal Battle Update.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Veilward.Domain;
using Veilward.Infrastructure;
using Veilward.Presentation;
using Veilward.Tests.Shared;
using Object = UnityEngine.Object;

public sealed class RiftVfxRecording : MonoBehaviour
{
    private const string Root = "TestResults/vfx1d/";
    private const string BattleScene = "Assets/_Game/Scenes/Battle.unity";
    private static readonly string[] Families = { "flame", "tide", "gale", "stone", "steam", "storm" };
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
    private readonly List<object> _frames = new List<object>();
    private readonly List<string> _errors = new List<string>();
    private readonly HashSet<string> _shotsSeen = new HashSet<string>();
    private readonly HashSet<string> _reactionsSeen = new HashSet<string>();
    private readonly HashSet<string> _statusesSeen = new HashSet<string>();
    private string _output;
    private string _stage;
    private bool _capture;
    private bool _running;
    private double _start;
    private MethodInfo _prepare;
    private MethodInfo _encode;
    private WaveCheckpointStore _store;
    private BattleScenarioConfig _config;
    private BattleSceneController Battle => Object.FindFirstObjectByType<BattleSceneController>();

    public static string Earned() => StartRun("earned", true);
    public static string Dense() => StartRun("dense-all-open", true);
    public static string Profile() => StartRun("profile-all-open", false);

    private static string StartRun(string name, bool capture)
    {
        Require(EditorApplication.isPlaying && Object.FindFirstObjectByType<RiftVfxRecording>() == null, "Fresh Play Mode required.");
        var path = Root + name + "/";
        Require(!Directory.Exists(path), "Use a new task-owned capture directory.");
        Directory.CreateDirectory(path);
        var root = new GameObject("VFX.1d Editor evidence");
        DontDestroyOnLoad(root);
        root.AddComponent<RiftVfxRecording>().Begin(path, capture, name);
        return path;
    }

    private void Begin(string output, bool capture, string name)
    {
        _output = output;
        _capture = capture;
        _config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWaveAbilityBossV2.asset");
        Require(_config != null && Time.timeScale == 1 && Time.captureFramerate == 0, "Saved config and realtime clock.");
        if (capture)
        {
            Directory.CreateDirectory(output + "frames");
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("Unity.Pipeline.Editor.Commands.Capture.CaptureCommands")).First(t => t != null);
            _prepare = type.GetMethod("PrepareGameView", PrivateStatic);
            _encode = type.GetMethod("EncodeScreenToPng", PrivateStatic);
            var target = (RenderTexture)type.GetMethod("GetGameViewTargetTexture", PrivateStatic).Invoke(null, new object[] { false });
            Require(target != null && target.width == 1600 && target.height == 900, "Actual 1600×900 GameView.");
        }
        _stage = "initial";
        _start = Time.realtimeSinceStartupAsDouble;
        _running = true;
        Application.logMessageReceived += ObserveError;
        if (capture) StartCoroutine(Record());
        StartCoroutine(name == "earned" ? EarnedFlow() : capture ? DenseFlow() : ProfileFlow());
    }

    private IEnumerator EarnedFlow()
    {
        var script = AbilityBossTrace.Load("flame");
        var replay = new AbilityBossTrace.Run(_config.CreateSimulation(), script);
        WaveCheckpointEnvelope earned = null;
        replay.stable = (r, phase) =>
        {
            if (phase == "preparation" && r.sim.State.WaveIndex == 7) earned = AbilityBossTrace.Envelope(r.sim);
        };
        replay.Play();
        Require(replay.sim.State.Phase == BattlePhase.Victory && earned != null, "Earned wave-eight state.");
        _store = new WaveCheckpointStore(Path.GetFullPath(_output + "store"));
        Require(_store.TrySave(earned, replay.sim.TryValidateCheckpoint, out var error, _config.CreateSimulationForCheckpoint), error);
        var reference = _config.CreateSimulationForCheckpoint(earned);
        reference.RestoreCheckpoint(earned.state);
        var snapshot = AbilityBossTrace.Snapshot(reference);
        SaveCheckpoint("earned", snapshot);
        MainMenuController.CheckpointStoreFactory = () => _store;

        for (var attempt = 0; attempt < 2; attempt++)
        {
            _stage = "menu";
            if (SceneManager.GetActiveScene().name != "MainMenu") yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return new WaitForSecondsRealtime(.45f);
            var menu = Object.FindFirstObjectByType<MainMenuController>();
            menu.ConfigureStore(_store);
            Require(menu.ContinueButton.interactable, "Earned file Continue available.");
            menu.Continue();
            yield return WaitBattle();
            var battle = Battle;
            Require(AbilityBossTrace.Snapshot(battle.Simulation) == snapshot && Clean(battle), "Continue restores whole snapshot, Grid and clean owners.");
            if (attempt == 0) WriteRenderInputs(battle);
            else
            {
                Require(File.ReadAllBytes(_store.PrimaryPath).SequenceEqual(File.ReadAllBytes(_output + "safe-file.json")),
                    "File Continue preserves original bytes.");
                SaveCheckpoint("continued", AbilityBossTrace.Snapshot(battle.Simulation));
            }
            _stage = attempt == 0 ? "earned-preparation" : "file-continue";
            yield return new WaitForSecondsRealtime(.55f);
            Require(battle.StartWave() == WaveTransitionResult.Started, "Public Start Wave.");
            if (attempt == 0) SaveCheckpoint("safe", snapshot);
            _stage = "earned-combat";
            foreach (var cast in script.casts.Where(c => c.wave == 8))
            {
                var sim = battle.Simulation;
                yield return Until(() => sim.WaveElapsedTicks >= cast.relativeTick || sim.State.Phase != BattlePhase.Combat, 48);
                var ability = battle.GetComponent<AbilityController>();
                Require(ability.BeginPreview() && ability.SelectTarget(new PathPosition(cast.x, cast.y)) &&
                    ability.ConfirmPreview(), "Public earned cast intent.");
                yield return Until(() => ability.LastCast.HasValue, 2);
                Require(ability.LastCast.Value.Succeeded, "Accepted earned cast.");
                if (attempt == 0)
                {
                    var pause = battle.GetComponent<BattlePauseController>();
                    var tick = sim.State.Tick;
                    var geometry = JsonConvert.SerializeObject(Seal(battle));
                    Require(battle.GetComponentsInChildren<AbilityEffectView>().Any(v => v.Hit.HasValue), "First cast has visible credited hits.");
                    pause.Pause();
                    _stage = "pause";
                    yield return new WaitForSecondsRealtime(.65f);
                    Require(sim.State.Tick == tick && geometry == JsonConvert.SerializeObject(Seal(battle)), "Pause freezes effect clock and geometry.");
                    pause.OnApplicationFocus(true);
                    Require(pause.Resume(), "Production Resume.");
                    _stage = "resume";
                    yield return new WaitForSecondsRealtime(.6f);
                    battle.ReturnToMenu();
                    yield return null;
                    break;
                }
            }
            if (attempt == 1)
            {
                yield return Until(() => battle.Simulation.State.Phase != BattlePhase.Combat, 8);
                Require(battle.Simulation.State.Phase == BattlePhase.Victory, "Production realtime Victory8.");
                _stage = "victory";
                yield return new WaitForSecondsRealtime(.75f);
            }
        }
        Finish(true);
    }

    private IEnumerator DenseFlow()
    {
        yield return LoadDense();
        var battle = Battle;
        var sim = battle.Simulation;
        var placement = battle.GetComponent<BattlePlacementController>();
        Require(placement.TrySelectCell(sim.State.Towers[0].Coordinate), "Public tower selection and range.");
        _stage = "dense-range";
        WriteRenderInputs(battle);
        yield return new WaitForSecondsRealtime(.60f);
        placement.CancelSelection();
        Require(battle.StartWave() == WaveTransitionResult.Started, "Dense public Start Wave.");
        _stage = "dense-combat";
        yield return Until(() => sim.State.Enemies.Count >= 6 && Warden(sim) != null, 15);
        var warden = Warden(sim);
        var ability = battle.GetComponent<AbilityController>();
        Require(ability.BeginPreview() && ability.SelectTarget(new PathPosition(5, 3)), "Invalid world center preview.");
        _stage = "invalid-preview";
        yield return new WaitForSecondsRealtime(.48f);
        Require(ability.Status == "NoTargets", "Invalid preview uses Domain validation.");
        ability.CancelPreview();
        _stage = "cancel";
        yield return new WaitForSecondsRealtime(.45f);
        Require(!ability.HasTarget && sim.AbilityState.NextCastSequence == 1 &&
            !battle.GetComponentsInChildren<AbilityEffectView>().Any(), "Cancel clears and preserves charge.");

        var location = sim.Path.GetPosition(warden.DistanceAlongPath);
        Require(ability.BeginPreview() && ability.SelectTarget(new PathPosition(location.X + .65f, location.Y)), "Valid world preview.");
        _stage = "valid-preview";
        yield return new WaitForSecondsRealtime(.52f);
        Require(ability.ConfirmPreview(), "Dense public Confirm.");
        yield return Until(() => ability.LastCast.HasValue, 2);
        Require(ability.LastCast.Value.Succeeded, "Dense cast accepted.");
        _stage = "dense-impact";
        yield return new WaitForSecondsRealtime(.35f);

        var inspector = battle.GetComponent<EnemyInspectorController>();
        var four = sim.State.Enemies.FirstOrDefault(e => e.Statuses.Count == 4);
        Require(inspector.Select((four ?? warden).Id), "Select live enemy in dense battle.");
        _stage = "dense-inspector";
        yield return new WaitForSecondsRealtime(1.1f);
        inspector.Close();
        Require(_shotsSeen.SetEquals(Families), "Each of the six families fired through production Update.");
        Require(_statusesSeen.Count == 4, "Observed all four basic statuses.");
        _stage = "dense-live";
        yield return Until(() => sim.State.Phase != BattlePhase.Combat, 45);
        Require(sim.State.Phase == BattlePhase.Defeat, "Boss reached base despite pressure.");
        _stage = "dense-leak";
        yield return new WaitForSecondsRealtime(.75f);
        Finish(true);
    }

    private IEnumerator LoadDense()
    {
        yield return SceneManager.LoadSceneAsync("Battle");
        yield return WaitBattle();
        var battle = Battle;
        var production = battle.Simulation;
        var shade = new EnemyDefinition(new EnemyDefinitionId("shade"), 1000, 1.1f, 1, 0, 5);
        var boss = production.Waves[7].Schedule.Single(e => e.Enemy.Id.Value == BossHudController.BossId).Enemy;
        // All-open/extra HP intentionally retain a swarm long enough to stress production presentation.
        var wave = new WaveDefinition(new[] { new WaveSpawnGroup(shade, 8, 0, 23), new WaveSpawnGroup(boss, 1, 115, 1) });
        var scenario = new BattleSimulation(production.Grid.Map, production.Path, new[] { wave }, production.TowerCatalog,
            20, 1200, 20, production.ReactionCatalog, null, AbilityDefinition.RiftSeal);
        typeof(BattleSceneController).GetField("_simulation", PrivateInstance).SetValue(battle, scenario);
        battle.GetComponent<BattleCheckpointController>().Configure(null, scenario, "editor-all-open-vfx1d");
        battle.ResetPresentation();
        var cells = new[] { new GridCoordinate(-5, -1), new GridCoordinate(-4, -1), new GridCoordinate(-3, -1),
            new GridCoordinate(-2, -1), new GridCoordinate(-5, 0), new GridCoordinate(-4, 0) };
        for (var i = 0; i < Families.Length; i++)
            Require(battle.PlaceTower(new TowerDefinitionId(Families[i]), cells[i]).Success, "All-open public placement " + Families[i]);
        yield return null;
    }

    private IEnumerator ProfileFlow()
    {
        var passes = new List<object>();
        for (var pass = 0; pass < 2; pass++)
        {
            yield return LoadDense();
            var battle = Battle;
            var before = Counts(battle);
            var checkpoint = battle.Simulation.ExportCheckpoint();
            Require(battle.StartWave() == WaveTransitionResult.Started, "Profile public Start Wave.");
            yield return new WaitForSecondsRealtime(5);
            using (var draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1))
            using (var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", 1))
            using (var allocations = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1))
            {
                var rows = new List<ProfileSample>(2048);
                var start = Time.realtimeSinceStartupAsDouble;
                var startTick = battle.Simulation.State.Tick;
                var dense = Counts(battle);
                while (Time.realtimeSinceStartupAsDouble - start < 6)
                {
                    yield return null;
                    rows.Add(new ProfileSample { frame = Time.frameCount, tick = battle.Simulation.State.Tick,
                        draws = Last(draws), batches = Last(batches), allocations = Last(allocations) });
                }
                var after = Counts(battle);
                var endTick = battle.Simulation.State.Tick;
                var seconds = Time.realtimeSinceStartupAsDouble - start;
                battle.Simulation.RestoreCheckpoint(checkpoint);
                battle.ResetPresentation();
                yield return null;
                Require(Clean(battle), "Profile rollback releases owners and effects.");
                var restored = Counts(battle);
                Require(JsonConvert.SerializeObject(before) == JsonConvert.SerializeObject(restored), "Whole-scene counts return to baseline.");
                passes.Add(new { pass, before, dense, after, restored, startTick, endTick, seconds, rows });
            }
        }
        Write(_output + "profile.json", new { method = "ProfilerRecorder, 5s warmup, 6s sampling, no PNG/encoding, two fresh Battle scenes",
            unity = Application.unityVersion, graphics = SystemInfo.graphicsDeviceType.ToString(), passes });
        Finish(true);
    }

    private static long? Last(ProfilerRecorder recorder) => recorder.Valid && recorder.Count > 0 ? recorder.LastValue : (long?)null;

    private struct ProfileSample
    {
        public int frame;
        public long tick;
        public long? draws;
        public long? batches;
        public long? allocations;
    }

    private IEnumerator Record()
    {
        double next = 0;
        while (_running)
        {
            yield return new WaitForEndOfFrame();
            var elapsed = Time.realtimeSinceStartupAsDouble - _start;
            if (elapsed < next) continue;
            next = elapsed + 1.0 / 30;
            var battle = Battle;
            if (battle != null && battle.Simulation == null) battle = null;
            var index = _frames.Count;
            _prepare.Invoke(null, null);
            var png = (byte[])_encode.Invoke(null, new object[] { 1600, 900 });
            File.WriteAllBytes(_output + "frames/" + index.ToString("D5") + ".png", png);
            var sim = battle == null ? null : battle.Simulation;
            var shots = battle == null ? null : Shots(battle);
            var reactions = battle == null ? null : Reactions(battle);
            var warden = sim == null ? null : Warden(sim);
            if (sim != null)
                foreach (var enemy in sim.State.Enemies)
                    foreach (var status in enemy.Statuses) _statusesSeen.Add(status.Kind.ToString());
            var preview = battle == null ? null : battle.GetComponentInChildren<AbilityPreviewView>();
            var inspector = battle == null ? null : battle.GetComponent<EnemyInspectorController>();
            _frames.Add(new { index, elapsed, sha256 = AbilityBossTrace.Hash(png), frame = Time.frameCount, stage = _stage,
                timeScale = Time.timeScale, captureFramerate = Time.captureFramerate, productionUpdate = battle != null && battle.enabled,
                paused = battle != null && battle.IsPaused, tick = sim?.State.Tick, phase = sim?.State.Phase.ToString(),
                wave = sim?.State.WaveIndex + 1, hp = sim?.State.BaseHealth, kills = sim?.State.Kills, enemies = sim?.State.Enemies.Count,
                boss = warden == null ? null : new { id = warden.Id.Value, hp = warden.Health, distance = warden.DistanceAlongPath },
                shots, reactions, seal = battle == null ? null : Seal(battle),
                cues = battle == null ? null : battle.GetComponentsInChildren<BossCueView>().Select(v => new {
                    kind = v.Kind.ToString(), enemy = v.EnemyId.Value, position = Point(v.Position), remaining = v.RemainingSeconds, lines = Lines(v.transform) }).ToArray(),
                preview = preview == null ? null : Lines(preview.transform),
                inspector = inspector?.SelectedEnemyId.HasValue == true ? inspector.Details : null,
                slots = battle == null ? null : battle.GetComponentsInChildren<EnemyStatusView>().Select(v => new {
                    owner = v.name, first = v.FirstCue.Kind?.ToString(), second = v.SecondCue.Kind?.ToString(), slots = v.VisibleSlotCount }).ToArray() });
        }
    }

    private object[] Shots(BattleSceneController battle)
    {
        var rows = new List<object>();
        void Add(string kind, AttackEvent e, int level, float remaining, bool flying)
        {
            _shotsSeen.Add(kind);
            rows.Add(new { kind, tick = e.Tick, tower = e.AttackerTowerId.Value, enemy = e.TargetEnemyId.Value,
                position = Point(e.TargetPosition), level, e.WasLethal, remaining, flying });
        }
        for (var i = 0; i < FlameProjectilePool.Capacity; i++)
        {
            var pool = battle.FlameProjectiles;
            var shot = pool.GetView(i);
            if (shot.Remaining > 0) Add("flame", pool.GetEvent(i), pool.GetLevel(i), shot.Remaining, shot.IsFlying);
        }
        for (var i = 0; i < BasicProjectilePool.Capacity; i++)
        {
            var pool = battle.BasicProjectiles;
            var shot = pool.GetView(i);
            if (shot.Remaining > 0) Add(shot.Kind.ToString().ToLowerInvariant(), pool.GetEvent(i), pool.GetLevel(i), shot.Remaining, shot.IsFlying);
        }
        for (var i = 0; i < HybridProjectilePool.Capacity; i++)
        {
            var shot = battle.HybridProjectiles.GetView(i);
            if (shot.Remaining > 0) Add(shot.Kind.ToString().ToLowerInvariant(), shot.Event, shot.Level, shot.Remaining, shot.IsFlying);
        }
        return rows.ToArray();
    }

    private object[] Reactions(BattleSceneController battle) => battle.GetComponentsInChildren<ReactionEffectView>().Select(v =>
    {
        var e = v.Event;
        _reactionsSeen.Add(e.Kind + "/" + e.TriggerKind);
        return (object)new { kind = e.Kind.ToString(), trigger = e.TriggerKind.ToString(), tick = e.Tick, sequence = e.SequenceInTick,
            tower = e.SourceTowerId.Value, primary = e.PrimaryEnemyId.Value, limit = e.TargetLimit, hits = Enumerable.Range(0, v.HitCount)
                .Select(v.GetHit).Select(h => new { index = h.HitIndex, enemy = h.TargetEnemyId.Value, from = Point(h.FromPosition),
                    to = Point(h.TargetPosition), loss = h.AppliedHpLoss, lethal = h.WasLethal }).ToArray() };
    }).ToArray();

    private static object[] Seal(BattleSceneController battle) => battle.GetComponentsInChildren<AbilityEffectView>().Select(v =>
    {
        var hit = v.Hit;
        return (object)new { id = v.Cast.AbilityId.Value, tick = v.Cast.Tick, sequence = v.CastSequence, center = Point(v.Cast.Center),
            radius = v.Cast.Radius, v.Cast.HitCount, position = Point(v.CapturedPosition), index = v.HitIndex,
            enemy = hit?.TargetEnemyId.Value, loss = hit?.AppliedHpLoss, lethal = hit?.WasLethal, origin = hit?.Origin.ToString(),
            remaining = v.RemainingSeconds, lines = Lines(v.transform) };
    }).ToArray();

    private static object[] Lines(Transform root)
    {
        var block = new MaterialPropertyBlock();
        return root.GetComponentsInChildren<LineRenderer>().Where(l => l.enabled).Select(l =>
        {
            l.GetPropertyBlock(block);
            var color = block.GetColor("_BaseColor");
            var camera = Camera.main;
            return (object)new { l.name, l.loop, l.startWidth, tint = new[] { color.r, color.g, color.b },
                screen = Enumerable.Range(0, l.positionCount).Select(i => camera.WorldToViewportPoint(l.GetPosition(i)))
                    .Select(p => new[] { p.x, p.y }).ToArray() };
        }).ToArray();
    }

    private static bool Clean(BattleSceneController battle) =>
        !battle.GetComponentsInChildren<AbilityEffectView>().Any() && !battle.GetComponentsInChildren<BossCueView>().Any() &&
        !battle.GetComponentsInChildren<EnemyStatusView>().Any() && !battle.GetComponentsInChildren<ReactionEffectView>().Any() &&
        !battle.GetComponentsInChildren<AbilityPreviewView>().Any() && battle.Presentation.LastSummary.Length == 0 &&
        !Enumerable.Range(0, FlameProjectilePool.Capacity).Any(i => battle.FlameProjectiles.GetView(i).Remaining > 0) &&
        !Enumerable.Range(0, BasicProjectilePool.Capacity).Any(i => battle.BasicProjectiles.GetView(i).Remaining > 0) &&
        !Enumerable.Range(0, HybridProjectilePool.Capacity).Any(i => battle.HybridProjectiles.GetView(i).Remaining > 0);

    private static object Counts(BattleSceneController battle)
    {
        var roots = battle.gameObject.scene.GetRootGameObjects();
        var transforms = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var renderers = roots.SelectMany(r => r.GetComponentsInChildren<Renderer>(true)).ToArray();
        var active = renderers.Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
        var lines = renderers.OfType<LineRenderer>().ToArray();
        var seal = battle.GetComponentsInChildren<AbilityEffectView>(true);
        var warden = battle.GetComponentsInChildren<BossCueView>(true);
        var reactions = battle.GetComponentsInChildren<ReactionEffectView>(true);
        return new { objects = transforms.Length, activeObjects = transforms.Count(t => t.gameObject.activeInHierarchy),
            renderers = renderers.Length, activeRenderers = active.Length, lines = lines.Length,
            activeLines = active.OfType<LineRenderer>().Count(), materialSlots = renderers.Sum(r => r.sharedMaterials.Length),
            uniqueMaterials = renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().Count(),
            activeMaterials = active.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().Count(),
            pooled = new { flame = FlameProjectilePool.Capacity, basic = BasicProjectilePool.Capacity, hybrid = HybridProjectilePool.Capacity,
                reaction = reactions.Length, ability = seal.Length, boss = warden.Length,
                flameLines = lines.Count(l => l.transform.parent != null && l.transform.parent.name.StartsWith("Flame Projectile Pool", StringComparison.Ordinal)),
                basicLines = lines.Count(l => l.transform.parent != null && l.transform.parent.name.StartsWith("Basic Projectile Pool", StringComparison.Ordinal)),
                hybridLines = lines.Count(l => l.transform.parent != null && l.transform.parent.name.StartsWith("Hybrid Projectile Pool", StringComparison.Ordinal)),
                reactionLines = reactions.Sum(v => v.GetComponentsInChildren<LineRenderer>(true).Length),
                abilityLines = seal.Sum(v => v.GetComponentsInChildren<LineRenderer>(true).Length),
                bossLines = warden.Sum(v => v.GetComponentsInChildren<LineRenderer>(true).Length) },
            genericBeams = battle.GetComponentsInChildren<AttackBeamView>(true).Length, clean = Clean(battle) };
    }

    private void WriteRenderInputs(BattleSceneController battle)
    {
        var pipeline = AssetDatabase.GetAssetPath(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline);
        var paths = AssetDatabase.GetDependencies(new[] { BattleScene, pipeline }, true).Concat(new[]
        {
            "ProjectSettings/GraphicsSettings.asset", "ProjectSettings/QualitySettings.asset", "Packages/manifest.json", "Packages/packages-lock.json",
            "Packages/com.unity.pipeline/package.json", "Packages/com.unity.pipeline/Editor/Commands/Capture/CaptureCommands.cs",
            "Packages/com.unity.render-pipelines.universal/package.json", "Packages/com.unity.render-pipelines.universal/Shaders/UnlitInput.hlsl",
            "Packages/com.unity.render-pipelines.universal/Shaders/UnlitForwardPass.hlsl", "Packages/com.unity.render-pipelines.core/package.json",
        }).Distinct().OrderBy(p => p, StringComparer.Ordinal);
        var dependencies = paths.Select(path =>
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path);
            var resolved = package == null ? path : Path.Combine(package.resolvedPath, path.Substring(package.assetPath.Length + 1));
            return new { path, sha256 = File.Exists(resolved) ? AbilityBossTrace.Hash(File.ReadAllBytes(resolved)) : null };
        }).ToArray();
        var camera = Camera.main;
        Write(_output + "render-inputs.json", new { unity = Application.unityVersion, graphics = SystemInfo.graphicsDeviceType.ToString(),
            quality = QualitySettings.names[QualitySettings.GetQualityLevel()], pipeline, counts = Counts(battle),
            camera = new { position = camera.transform.position.ToString("F6"), rotation = camera.transform.rotation.eulerAngles.ToString("F6"),
                camera.orthographicSize, camera.aspect }, dependencies });
    }

    private void SaveCheckpoint(string name, string snapshot)
    {
        File.WriteAllBytes(_output + name + "-file.json", File.ReadAllBytes(_store.PrimaryPath));
        File.WriteAllText(_output + name + "-state.txt", snapshot);
    }

    private static EnemyState Warden(BattleSimulation sim) => sim.State.Enemies.FirstOrDefault(e => e.Definition.Id.Value == BossHudController.BossId);
    private static float[] Point(PathPosition p) => new[] { p.X, p.Y };
    private static IEnumerator WaitBattle() => Until(() =>
    {
        var battle = Object.FindFirstObjectByType<BattleSceneController>();
        return battle != null && battle.Simulation != null;
    }, 8);
    private static IEnumerator Until(Func<bool> test, double timeout)
    {
        var deadline = Time.realtimeSinceStartupAsDouble + timeout;
        while (!test() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        Require(test(), "Realtime stage timed out.");
    }

    private void ObserveError(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        _errors.Add(message + "\n" + stack);
        Finish(false);
    }

    private void Finish(bool ok)
    {
        if (!_running) return;
        _running = false;
        StopAllCoroutines();
        Application.logMessageReceived -= ObserveError;
        if (_store != null) MainMenuController.CheckpointStoreFactory = null;
        if (_capture) Write(_output + "frames.json", _frames);
        Write(_output + "completion.json", new { ok, errors = _errors, scope = _output, frames = _frames.Count,
            elapsed = Time.realtimeSinceStartupAsDouble - _start, shots = _shotsSeen.OrderBy(x => x).ToArray(),
            statuses = _statusesSeen.OrderBy(x => x).ToArray(), reactions = _reactionsSeen.OrderBy(x => x).ToArray() });
    }

    private void OnDestroy() => Finish(false);
    private static void Write(string path, object value) => File.WriteAllText(path, JsonConvert.SerializeObject(value) + "\n");
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
