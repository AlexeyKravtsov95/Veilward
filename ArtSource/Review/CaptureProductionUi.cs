// Captures the resident Unity Editor GameView. All files and fault injection use an isolated evidence store.
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
using UnityEngine.UI;
using Veilward.Domain;
using Veilward.Infrastructure;
using Veilward.Presentation;
using Veilward.Tests.Shared;
using Object = UnityEngine.Object;

public sealed class CaptureProductionUi : MonoBehaviour
{
    private const string Root = "TestResults/ui1/media/";
    private const string ConfigPath = "Assets/_Game/Configs/EightWaveAbilityBossV2.asset";
    private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly GridCoordinate First = new GridCoordinate(-4, 0);
    private static readonly GridCoordinate Second = new GridCoordinate(-3, 0);
    private static readonly TowerDefinitionId Flame = new TowerDefinitionId("flame");
    private readonly List<object> _frames = new List<object>();
    private readonly List<object> _shots = new List<object>();
    private readonly List<string> _errors = new List<string>();
    private string _path, _stage;
    private int _width, _height, _dpr, _sideInset, _bottomInset;
    private double _start;
    private bool _record, _saveFault;
    private MethodInfo _prepare, _encode;
    private WaveCheckpointStore _store;
    private BattleScenarioConfig _config;
    private BattleSceneController Battle => Object.FindFirstObjectByType<BattleSceneController>();
    private BattlePlacementController Placement => Battle.GetComponent<BattlePlacementController>();
    private AbilityController Ability => Battle.GetComponent<AbilityController>();

    public static object SetSmall() => Configure(1136, 640);
    public static object SetWide() => Configure(2556, 1179);
    public static object SetTablet() => Configure(2048, 1536);

    public static string Small() => Begin("small", 1136, 640, 2, 0, 0);
    public static string Wide() => Begin("wide", 2556, 1179, 3, 132, 63);
    public static string Tablet() => Begin("tablet", 2048, 1536, 2, 0, 40);
    public static string Final() => Begin("final", 2556, 1179, 3, 132, 63);

    private static object Configure(int width, int height)
    {
        var assembly = typeof(UnityEditor.Editor).Assembly;
        var type = assembly.GetType("UnityEditor.GameViewSizes");
        var sizes = typeof(ScriptableSingleton<>).MakeGenericType(type).GetProperty("instance").GetValue(null);
        var group = type.GetMethod("GetGroup").Invoke(sizes, new[] { type.GetProperty("currentGroupType").GetValue(sizes) });
        var view = EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
        var selection = view.GetType().GetProperty("selectedSizeIndex", Instance);
        var previous = (int)selection.GetValue(view);
        var available = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
        var name = $"UI.1 {width}x{height}";
        var index = -1;
        for (var i = 0; i < available; i++)
        {
            var candidate = group.GetType().GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
            if ((string)candidate.GetType().GetProperty("baseText").GetValue(candidate) == name) { index = i; break; }
        }
        if (index < 0)
        {
            var fixedSize = Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"), "FixedResolution");
            var value = Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"), fixedSize, width, height, name);
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { value });
            index = available;
        }
        selection.SetValue(view, index);
        view.Focus();
        return new { previous, index, width, height };
    }

    private static string Begin(string scenario, int width, int height, int dpr, int side, int bottom)
    {
        Need(EditorApplication.isPlaying && Object.FindFirstObjectByType<CaptureProductionUi>() == null, "Start in a fresh Play Mode.");
        var output = Root + scenario + "/";
        Need(!Directory.Exists(output), "A capture directory already exists.");
        Directory.CreateDirectory(output);
        var go = new GameObject("UI.1 proof recorder");
        DontDestroyOnLoad(go);
        var run = go.AddComponent<CaptureProductionUi>();
        run._path = output;
        run._width = width;
        run._height = height;
        run._dpr = dpr;
        run._sideInset = side;
        run._bottomInset = bottom;
        run._store = new WaveCheckpointStore(Path.GetFullPath(output + "saves"));
        run._config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>(ConfigPath);
        Need(run._config != null && Time.timeScale == 1 && Time.captureFramerate == 0, "Normal production Update clock.");
        var capture = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Unity.Pipeline.Editor.Commands.Capture.CaptureCommands"))
            .First(type => type != null);
        var staticPrivate = BindingFlags.NonPublic | BindingFlags.Static;
        run._prepare = capture.GetMethod("PrepareGameView", staticPrivate);
        run._encode = capture.GetMethod("EncodeScreenToPng", staticPrivate);
        var target = (RenderTexture)capture.GetMethod("GetGameViewTargetTexture", staticPrivate).Invoke(null, new object[] { false });
        Need(target != null && target.width == width && target.height == height, "GameView render target does not match the profile.");
        MainMenuController.CheckpointStoreFactory = () => run._store;
        Application.logMessageReceived += run.ReadLog;
        run.StartCoroutine(run.Flow(scenario));
        return output;
    }

    private void OnDestroy() => Application.logMessageReceived -= ReadLog;

    private void ReadLog(string message, string trace, LogType type)
    {
        if (!_saveFault && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
            _errors.Add(message);
    }

    private IEnumerator Flow(string scenario)
    {
        yield return null;
        ApplyInsets();
        var menu = Object.FindFirstObjectByType<MainMenuController>();
        menu.ConfigureStore(_store);
        if (scenario == "final")
        {
            yield return FinalWave(menu);
            Finish();
            yield break;
        }

        if (scenario == "wide")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_store.PrimaryPath));
            _record = true;
            _start = Time.realtimeSinceStartupAsDouble;
            _stage = "main-menu";
            Directory.CreateDirectory(_path + "frames");
            StartCoroutine(Record());
        }
        yield return Shot("menu-no-save");
        if (scenario == "wide")
        {
            File.WriteAllText(_store.PrimaryPath, "{isolated corrupt input");
            menu.RefreshCheckpointStatus();
            Need(!menu.ContinueButton.interactable && menu.StatusText.text.Contains("damaged"), "Corrupt Continue gate.");
            yield return Shot("menu-corrupt-save");
            File.Delete(_store.PrimaryPath);
            Click("Retry Load Button");
        }

        yield return PauseFor(.42f);
        Click("New Game Button");
        yield return Until(() => Battle != null && Battle.Simulation != null);
        ApplyInsets();
        yield return Shot("starting-choice");
        Click("flame");
        yield return Shot("starting-choice-preview");
        Click("Confirm Element Button");
        Need(Battle.Simulation.State.ElementProgression.UnlockedElements.Count == 1, "Native starting choice.");
        yield return Shot("preparation-hud");

        Need(Placement.TrySelectCell(First), "Select production socket.");
        Catalog("steam");
        yield return Shot("build-locked");
        Catalog("flame");
        yield return Shot("build-preview");
        Click("Confirm Button");
        Need(Placement.TrySelectCell(First), "Inspect purchased tower.");
        yield return Shot("tower-upgrade-unaffordable");

        if (scenario != "wide")
        {
            Click("Pause Button");
            yield return Shot("pause");
            Finish();
            yield break;
        }

        Click("Close Tower Button");
        Click("Start Wave Button");
        yield return FirstFight();
        Need(Battle.Simulation.State.Phase == BattlePhase.WaveComplete, "Wave one must clear.");
        yield return Shot("next-wave");
        Click("Next Wave Button");
        Need(Placement.TrySelectCell(First) && Button("Upgrade Button").interactable, "Wave one pays for a real upgrade.");
        Click("Upgrade Button");
        yield return Shot("tower-upgraded");
        Click("Close Tower Button");
        Need(Placement.TrySelectCell(Second), "Second socket.");
        Catalog("flame");
        Need(!Button("Confirm Button").interactable, "Low gold blocks purchase.");
        yield return Shot("build-low-gold");
        Click("Cancel Button");
        Need(Placement.TrySelectCell(First), "Reselect upgraded tower.");
        Click("Sell Button");
        yield return Shot("sell-confirmation");
        Click("Cancel Sell Button");
        Click("Sell Button");
        Click("Confirm Sell Button");
        Need(Battle.Simulation.State.Towers.Count == 0, "Sell removes exactly one tower.");
        Need(Placement.TrySelectCell(Second), "Refund can buy a replacement.");
        Catalog("flame");
        Click("Confirm Button");
        Click("Start Wave Button");
        yield return PauseFor(.9f);
        Click("Pause Button");
        yield return Shot("pause");
        Click("Resume Button");
        yield return PauseFor(.6f);
        Click("Pause Button");
        Click("Main Menu Button");
        yield return Until(() => Object.FindFirstObjectByType<MainMenuController>() != null);
        ApplyInsets();
        yield return Shot("menu-continue");
        Click("Continue Button");
        yield return Until(() => Battle != null && Battle.Simulation != null);
        ApplyInsets();
        Need(Battle.Simulation.State.Phase == BattlePhase.Preparation && Battle.Simulation.State.WaveIndex == 1,
            "Continue restores wave two preparation.");
        yield return Shot("continue-rollback");
        _record = false;
        yield return SaveFaultAndDefeat();
        Finish();
    }

    private IEnumerator FirstFight()
    {
        var sim = Battle.Simulation;
        yield return Until(() => sim.State.Enemies.Count > 0);
        yield return Shot("combat-hud");
        Click("Ability Button");
        Need(Ability.SelectTarget(new PathPosition(-3, 1.5f)), "Preview empty region.");
        Need(!Button("Confirm Ability Button").interactable, "Invalid center cannot be confirmed.");
        yield return Shot("ability-invalid");
        Click("Cancel Ability Button");
        Need(sim.AbilityState.NextCastSequence == 1, "Cancel preserves the charge.");
        yield return Shot("ability-cancelled");
        Click("Ability Button");
        yield return Until(() => sim.State.Enemies.Count > 0);
        Need(Ability.SelectTarget(sim.Path.GetPosition(sim.State.Enemies[0].DistanceAlongPath)), "Preview live enemy.");
        Need(Button("Confirm Ability Button").interactable, "Legal center can be confirmed.");
        yield return Shot("ability-valid", .12f);
        Click("Confirm Ability Button");
        yield return Until(() => Ability.LastCast.HasValue);
        Need(Ability.LastCast.Value.Succeeded && Ability.LastCast.Value.TotalAppliedHpLoss > 0, "Production cast and real HP loss.");
        yield return Shot("ability-cooldown");
        yield return Until(() => sim.State.Phase != BattlePhase.Combat, 45);
    }

    private IEnumerator SaveFaultAndDefeat()
    {
        var checkpoint = Battle.GetComponent<BattleCheckpointController>();
        Directory.CreateDirectory(_store.TempPath);
        _saveFault = true;
        try { Need(!checkpoint.SaveCurrentState(), "Expected isolated temporary-path save error."); }
        finally { _saveFault = false; }
        yield return Shot("save-failed-fixture");
        Need(Placement.TrySelectCell(First), "Open a build drawer while a write is blocked.");
        Catalog("flame");
        yield return Shot("save-error-with-build-fixture");
        Click("Cancel Button");
        yield return null;
        yield return null;
        Directory.Delete(_store.TempPath);
        var state = AbilityBossTrace.Wire(Battle.Simulation);
        Click("Retry Save Button");
        Need(checkpoint.LastSaveSucceeded && state == AbilityBossTrace.Wire(Battle.Simulation), "Retry keeps the live state.");

        var undefended = _config.CreateSimulation();
        Need(undefended.ChooseElement(new ChooseElementCommand("start-resonance", "flame")).Success, "Defeat run chooses Flame.");
        while (undefended.State.Phase != BattlePhase.Defeat)
        {
            Need(undefended.StartWave() == WaveTransitionResult.Started, "Defeat run advances a legal wave.");
            while (undefended.State.Phase == BattlePhase.Combat) undefended.Step();
            if (undefended.State.Phase == BattlePhase.Defeat) break;
            if (undefended.State.ElementProgression.PendingChoiceId != "none")
                Need(undefended.ChooseElement(new ChooseElementCommand(undefended.State.ElementProgression.PendingChoiceId, "tide")).Success,
                    "Defeat run chooses Tide.");
            Need(undefended.ContinueToNextWave() == WaveTransitionResult.Continued, "Defeat run starts the next preparation.");
        }
        checkpoint.RestoreCheckpoint(Envelope(undefended));
        yield return Shot("defeat-undefended-replay");
    }

    private IEnumerator FinalWave(MainMenuController menu)
    {
        var script = AbilityBossTrace.Load("flame");
        var replay = new AbilityBossTrace.Run(_config.CreateSimulation(), script);
        WaveCheckpointEnvelope earned = null;
        replay.stable = (run, phase) =>
        {
            if (phase == "preparation" && run.sim.State.WaveIndex == 7)
                earned = AbilityBossTrace.Envelope(run.sim);
        };
        replay.Play();
        Need(earned != null && replay.sim.State.Phase == BattlePhase.Victory, "Accepted native winning replay.");
        Directory.CreateDirectory(Path.GetDirectoryName(_store.PrimaryPath));
        Need(_store.TrySave(earned, replay.sim.TryValidateCheckpoint, out var error, _config.CreateSimulationForCheckpoint), error);
        File.Copy(_store.PrimaryPath, _store.BackupPath);
        File.WriteAllText(_store.PrimaryPath, "{isolated corrupt primary");
        menu.RefreshCheckpointStatus();
        Need(menu.ContinueButton.interactable && menu.StatusText.text.Contains("backup"), "Validated backup is offered.");
        yield return Shot("menu-backup-recovery");
        Click("Continue Button");
        yield return Until(() => Battle != null && Battle.Simulation != null);
        ApplyInsets();
        yield return Shot("warden-incoming");
        var previewCell = new GridCoordinate(-3, 3);
        if (Battle.Simulation.Grid.GetState(previewCell) == GridCellState.Available && Placement.TrySelectCell(previewCell))
        {
            Catalog("flame");
            yield return Shot("final-wave-build-preview");
            var drawer = Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None).Single(rect => rect.name == "Placement Panel");
            var tile = (Vector2)Camera.main.WorldToScreenPoint(Battle.GetCellCenter(previewCell));
            Need(!ScreenRect(drawer).Contains(tile), "Final-wave build drawer leaves the selected tile uncovered.");
            Click("Cancel Button");
        }
        Click("Start Wave Button");
        var sim = Battle.Simulation;
        foreach (var cast in script.casts.Where(row => row.wave == 8))
        {
            if (cast.relativeTick > 330)
            {
                yield return Until(() => sim.State.Enemies.Any(enemy => enemy.Definition.Id.Value == BossHudController.BossId), 20);
                yield return Until(() => sim.State.Enemies.Count >= 6 || sim.WaveElapsedTicks >= 405, 8);
                yield return Shot("warden-combat");
            }
            yield return Until(() => sim.WaveElapsedTicks >= cast.relativeTick || sim.State.Phase != BattlePhase.Combat, 48);
            Need(Ability.BeginPreview() && Ability.SelectTarget(new PathPosition(cast.x, cast.y)) && Ability.ConfirmPreview(),
                "Final-wave earned cast intent.");
            yield return Until(() => Ability.LastCast.HasValue, 3);
            Need(Ability.LastCast.Value.Succeeded, "Final-wave earned cast.");

        }
        yield return Until(() => sim.State.Phase != BattlePhase.Combat, 48);
        Need(sim.State.Phase == BattlePhase.Victory, "Normal Update must reach the earned victory.");
        yield return Shot("victory");
        yield return DenseInspector();
    }

    private IEnumerator DenseInspector()
    {
        yield return SceneManager.LoadSceneAsync("Battle");
        yield return Until(() => Battle != null && Battle.Simulation != null);
        ApplyInsets();
        var battle = Battle;
        var production = battle.Simulation;
        var schedule = production.Waves[production.Waves.Count - 1].Schedule;
        // Тот же изолированный stress fixture, что в VFX.1d. Запас HP удерживает врагов в кадре.
        var shade = new EnemyDefinition(new EnemyDefinitionId("shade"), 1000, 1.1f, 1, 0, 5);
        var boss = schedule.First(row => row.Enemy.Id.Value == BossHudController.BossId).Enemy;
        var wave = new WaveDefinition(new[] { new WaveSpawnGroup(shade, 8, 0, 23), new WaveSpawnGroup(boss, 1, 115, 1) });
        var staged = new BattleSimulation(production.Grid.Map, production.Path, new[] { wave }, production.TowerCatalog,
            20, 1200, 20, production.ReactionCatalog, null, AbilityDefinition.RiftSeal);
        typeof(BattleSceneController).GetField("_simulation", Instance).SetValue(battle, staged);
        battle.GetComponent<BattleCheckpointController>().Configure(null, staged, "editor-all-open-ui1-density");
        battle.ResetPresentation();
        var types = new[] { "flame", "tide", "gale", "stone", "steam", "storm" };
        var cells = new[] { new GridCoordinate(-5, -1), new GridCoordinate(-4, -1), new GridCoordinate(-3, -1),
            new GridCoordinate(-2, -1), new GridCoordinate(-5, 0), new GridCoordinate(-4, 0) };
        for (var i = 0; i < types.Length; i++)
            Need(battle.PlaceTower(new TowerDefinitionId(types[i]), cells[i]).Success, "Place the six fixture towers.");
        Need(battle.StartWave() == WaveTransitionResult.Started, "Dense fixture uses production Update.");
        yield return Until(() => staged.State.Enemies.Count >= 6 &&
            staged.State.Enemies.Any(enemy => enemy.Definition.Id.Value == BossHudController.BossId), 12);
        yield return Shot("dense-fight-fixture", .04f);

        EnemyState target = null;
        yield return Until(() =>
        {
            target = staged.State.Enemies.FirstOrDefault(enemy =>
            {
                var memory = battle.Presentation.GetMemory(enemy.Id);
                return enemy.Health > 30 && (memory?.Steam?.AppliedHpLoss > 0 || memory?.Storm?.AppliedHpLoss > 0);
            });
            return target != null;
        }, 12);
        var inspector = battle.GetComponent<EnemyInspectorController>();
        Need(inspector.Select(target.Id), "Inspect a live enemy with actual reaction damage.");
        yield return Shot("reaction-inspector", .04f);
        Need(inspector.Details.Contains("HP lost"), "The inspector reports applied damage.");
    }

    private static WaveCheckpointEnvelope Envelope(BattleSimulation simulation) => new WaveCheckpointEnvelope
    {
        schemaVersion = WaveCheckpointStore.CurrentSchemaVersion,
        scenarioContentId = WaveCheckpointCompatibility.BalancedAbilityBossContentId,
        state = simulation.ExportCheckpoint()
    };

    private void ApplyInsets()
    {
        var safe = Object.FindFirstObjectByType<BattleHudSafeArea>();
        Need(safe != null && Screen.width == _width && Screen.height == _height, "Profile has real pixel dimensions.");
        safe.enabled = false;
        typeof(BattleHudSafeArea).GetMethod("ApplySafeArea", Instance).Invoke(safe,
            new object[] { SafePixels(), new Vector2(_width, _height), safe.GetComponent<Canvas>().scaleFactor });
    }

    private Rect SafePixels() => new Rect(_sideInset, _bottomInset, _width - 2 * _sideInset, _height - _bottomInset);

    private IEnumerator Shot(string name, float settle = .28f)
    {
        _stage = name;
        yield return PauseFor(settle);
        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();
        var canvas = Object.FindFirstObjectByType<BattleHudSafeArea>().GetComponent<Canvas>();
        var safe = SafePixels();
        var buttons = canvas.GetComponentsInChildren<Button>().Select(button =>
        {
            var rect = ScreenRect((RectTransform)button.transform);
            Need(rect.xMin >= safe.xMin - 1 && rect.yMin >= safe.yMin - 1 &&
                 rect.xMax <= safe.xMax + 1 && rect.yMax <= safe.yMax + 1, $"{name} {button.name} escaped safe area: {rect}");
            Need(rect.width / _dpr >= 44 && rect.height / _dpr >= 44, $"{name} {button.name} is below 44pt: {rect}");
            return new { button.name, parent = button.transform.parent.name, pixels = Bounds(rect), points = new[] { rect.width / _dpr, rect.height / _dpr }, button.interactable };
        }).ToArray();
        var panelNames = new[] { "Stats Panel", "Phase Plate", "Placement Panel", "Tower Panel", "Boss Panel",
            "Ability Button", "Ability Panel", "Ability Feedback", "Enemy Inspector Panel", "Save Warning",
            "Start Wave Button", "Next Wave Button", "Pause Button" };
        var panels = canvas.GetComponentsInChildren<RectTransform>().Where(rect => panelNames.Contains(rect.name))
            .Select(rect => new { rect.name, pixels = Bounds(ScreenRect(rect)), parent = "battle-hud" }).ToArray();
        var texts = canvas.GetComponentsInChildren<Text>()
            .Where(text => !string.IsNullOrEmpty(text.text)).Select(text =>
                new { text.name, text.text, text.fontSize, available = text.rectTransform.rect.height,
                    preferredHeight = VisiblePreferredHeight(text) }).ToArray();
        var png = Capture();
        File.WriteAllBytes(_path + name + ".png", png);
        var battle = Battle;
        _shots.Add(new { name, sha256 = Hash(png), _width, _height, _dpr, safe = Bounds(safe), canvas.scaleFactor,
            buttons, panels, texts, scene = SceneManager.GetActiveScene().name, tick = battle?.Simulation.State.Tick,
            wave = battle?.Simulation.State.WaveIndex + 1, enemies = battle?.Simulation.State.Enemies.Count, phase = battle?.Simulation.State.Phase.ToString(), frame = Time.frameCount });
    }

    private static Rect ScreenRect(RectTransform element)
    {
        var corners = new Vector3[4];
        element.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }

    private static float VisiblePreferredHeight(Text label)
    {
        // У завершающего пустого ряда нет пикселей. Измеряем только отображаемый текст.
        var visible = label.text.TrimEnd('\r', '\n');
        if (visible.Length == label.text.Length) return label.preferredHeight;
        var settings = label.GetGenerationSettings(new Vector2(label.GetPixelAdjustedRect().width, 0));
        return label.cachedTextGeneratorForLayout.GetPreferredHeight(visible, settings) / label.pixelsPerUnit;
    }

    private static float[] Bounds(Rect rect) => new[] { rect.x, rect.y, rect.width, rect.height };

    private IEnumerator Record()
    {
        double next = 0;
        while (_record)
        {
            yield return new WaitForEndOfFrame();
            if (!_record) yield break;
            var elapsed = Time.realtimeSinceStartupAsDouble - _start;
            if (elapsed < next) continue;
            next = elapsed + 1.0 / 24;
            var index = _frames.Count;
            var png = Capture(1280, 590);
            File.WriteAllBytes(_path + "frames/" + index.ToString("D5") + ".png", png);
            var sim = Battle?.Simulation;
            _frames.Add(new { index, elapsed, stage = _stage, sha256 = Hash(png), frame = Time.frameCount,
                tick = sim?.State.Tick, scene = SceneManager.GetActiveScene().name, phase = sim?.State.Phase.ToString() });
        }
    }

    private byte[] Capture(int width = 0, int height = 0)
    {
        _prepare.Invoke(null, null);
        return (byte[])_encode.Invoke(null, new object[] { width == 0 ? _width : width, height == 0 ? _height : height });
    }

    private void Finish()
    {
        var dependencies = AssetDatabase.GetDependencies(new[] { "Assets/_Game/Scenes/MainMenu.unity", "Assets/_Game/Scenes/Battle.unity",
            AssetDatabase.GetAssetPath(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline) }, true);
        var meta = dependencies.Where(path => path.StartsWith("Assets/") && File.Exists(path + ".meta"))
            .Select(path => path + ".meta");
        var fonts = Object.FindFirstObjectByType<BattleHudSafeArea>().GetComponentsInChildren<Text>(true)
            .Where(label => label.font != null).Select(label => new { label.font.name, path = AssetDatabase.GetAssetPath(label.font) })
            .Distinct().ToArray();
        var inputs = dependencies.Concat(meta).Concat(new[] { "Packages/manifest.json", "Packages/packages-lock.json",
            "ProjectSettings/ProjectVersion.txt", "ProjectSettings/GraphicsSettings.asset", "ProjectSettings/QualitySettings.asset",
            "Packages/com.unity.ugui/package.json", "Packages/com.unity.pipeline/package.json",
            "Packages/com.unity.pipeline/Editor/Commands/Capture/CaptureCommands.cs" }).Distinct().OrderBy(path => path);
        var hashes = inputs.Select(path =>
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path);
            var file = package == null ? path : Path.Combine(package.resolvedPath, path.Substring(package.assetPath.Length + 1));
            return new { path, sha256 = File.Exists(file) ? Hash(File.ReadAllBytes(file)) : null };
        }).ToArray();
        var menu = MainMenuController.CheckpointStoreFactory;
        if (menu != null) MainMenuController.CheckpointStoreFactory = null;
        File.WriteAllText(_path + "manifest.json", JsonConvert.SerializeObject(new
        {
            unity = Application.unityVersion, gpu = SystemInfo.graphicsDeviceType.ToString(),
            quality = QualitySettings.names[QualitySettings.GetQualityLevel()], font = "Unity builtin LegacyRuntime.ttf", fonts,
            frames = _frames, shots = _shots, hashes, errors = _errors, complete = _errors.Count == 0
        }, Formatting.Indented));
    }

    private static string Hash(byte[] bytes)
    {
        using var hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }

    private static IEnumerator Until(Func<bool> condition, float seconds = 8)
    {
        var deadline = Time.realtimeSinceStartupAsDouble + seconds;
        while (!condition() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        Need(condition(), "Timed out awaiting production state.");
    }

    private static WaitForSecondsRealtime PauseFor(float seconds) => new WaitForSecondsRealtime(seconds);

    private static Button Button(string name) =>
        Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(button => button.name == name);

    private static void Click(string name)
    {
        var button = Button(name);
        Need(button.isActiveAndEnabled && button.interactable, $"{name} must be tappable.");
        button.onClick.Invoke();
    }

    private static void Catalog(string id) => Click("Catalog " + id);

    private static void Need(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }
}
