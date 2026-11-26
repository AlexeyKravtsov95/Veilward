#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Veilward.Domain;
using Veilward.Infrastructure;
using Object = UnityEngine.Object;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Veilward.Presentation
{
    // Локальный сценарий для воспроизведения evidence. В Player и в коммитируемых сценах его нет.
    public sealed class UiReworkCaptureLocal : MonoBehaviour
    {
        private const int Width = 1600, Height = 900, Fps = 20;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly WaitForEndOfFrame End = new WaitForEndOfFrame();
        private static readonly GridCoordinate FirstCell = new GridCoordinate(-4, 0);
        private static readonly MethodInfo ApplyArea = typeof(BattleHudSafeArea).GetMethod("ApplySafeArea", Flags);
        private static readonly MethodInfo DispatchPointer = typeof(BattlePlacementController).GetMethod("DispatchPointer", Flags);
        private static readonly string Root = Path.GetFullPath("TestResults/UIREWORK1/capture");
        private const string EarnedSave = "Docs/review/VFX.1d/reports/safe-file.json";
        private readonly List<RaycastResult> _hits = new List<RaycastResult>();
        private int _sideInset, _bottomInset, _frames;
        private string _folder, _marker = "start";
        private StreamWriter _receipt;
        private RenderTexture _videoTarget;
        private Process _encoder;
        private byte[] _videoBytes;
        private bool _record, _pending, _videoFailed;

        public static void Begin(string mode)
        {
            var root = new GameObject("UI.REWORK.1 GameView evidence");
            DontDestroyOnLoad(root);
            var driver = root.AddComponent<UiReworkCaptureLocal>();
            driver.StartCoroutine(driver.Run(mode));
        }

        private IEnumerator Run(string mode)
        {
            _folder = Path.Combine(Root, mode);
            Directory.CreateDirectory(_folder);
            _receipt = new StreamWriter(Path.Combine(_folder, "events.tsv"), false) { AutoFlush = true };
            ConfigureGameView(Width, Height);
            yield return null;
            yield return null;
            yield return End;
            Ensure(Screen.width == Width && Screen.height == Height, "baseline GameView dimensions");
            switch (mode.StartsWith("polish-") ? mode.Substring("polish-".Length) : mode)
            {
                case "story": yield return Story(); break;
                case "earned": yield return Earned(); break;
                case "profiles": yield return Profiles(); break;
                case "defeat": yield return Defeat(); break;
                case "stress": yield return Stress(); break;
                case "milestones": yield return Milestones(); break;
                case "review": yield return ReviewCases(); break;
                default: throw new ArgumentOutOfRangeException(nameof(mode));
            }

            _receipt.WriteLine($"done\t{_frames}\t{_videoFailed}");
            File.WriteAllText(Path.Combine(_folder, "complete.txt"), $"{mode} {_frames} {_videoFailed}\n");
            Destroy(gameObject);
        }

        private IEnumerator OpenMenu(bool earned)
        {
            NewStore(earned);
            yield return SceneManager.LoadSceneAsync("Bootstrap");
            yield return null;
            yield return null;
            Ensure(FindFirstObjectByType<MainMenuController>().ContinueButton.interactable == earned, "Continue state");
        }

        private IEnumerator Story()
        {
            yield return OpenMenu(false);
            StartVideo();
            yield return Still("menu-new", .8f);
            var store = MainMenuController.CheckpointStoreFactory();
            File.WriteAllText(store.PrimaryPath, "{capture-corrupt");
            FindFirstObjectByType<MainMenuController>().RefreshCheckpointStatus();
            yield return Still("menu-load-error", .75f);
            File.Delete(store.PrimaryPath);
            Click("Retry Load Button");
            yield return Hold(.4f);
            Click("New Game Button");
            yield return Hold(.8f);
            yield return Still("first-element", .75f);
            Click("flame");
            yield return Still("element-preview", .55f);
            Directory.CreateDirectory(store.TempPath);
            Click("Confirm Element Button");
            yield return Hold(.4f);
            Ensure(!Battle.GetComponent<BattleCheckpointController>().LastSaveSucceeded, "save really failed");
            yield return Still("save-error", 1.0f);
            Directory.Delete(store.TempPath);
            Click("Retry Save Button");
            yield return Hold(.45f);
            Ensure(Battle.GetComponent<BattleCheckpointController>().LastSaveSucceeded, "Retry really saved");
            yield return Still("preparation", .65f);
            TapCell(FirstCell);
            yield return Hold(.35f);
            Click("Catalog tide");
            yield return Still("build-locked", .75f);
            Click("Catalog flame");
            yield return Still("build-preview", .9f);
            Click("Confirm Button");
            yield return Hold(.65f);
            Ensure(Battle.Simulation.State.Towers.Count == 1, "one purchased tower");
            TapCell(FirstCell);
            yield return Still("tower-low-gold", .95f);
            Click("Sell Button");
            yield return Still("sell-confirmation", .85f);
            Click("Cancel Sell Button");
            yield return Hold(.4f);
            Click("Sell Button");
            yield return Hold(.35f);
            Click("Confirm Sell Button");
            yield return Hold(.55f);
            Ensure(Battle.Simulation.State.Towers.Count == 0, "one sale");
            Click("Pause Button");
            yield return Still("pause", 1.2f);
            Click("Resume Button");
            yield return Hold(.65f);
            Click("Pause Button");
            yield return Hold(.35f);
            Click("Main Menu Button");
            yield return Hold(.75f);
            yield return Still("menu-continue", .8f);
            Click("Continue Button");
            yield return Hold(.85f);
            Ensure(Battle.Simulation.State.Gold == 129, "Continue preserved the sale snapshot");
            yield return StopVideo();
        }

        private IEnumerator Earned()
        {
            yield return OpenMenu(true);
            StartVideo();
            yield return Still("menu-earned", .8f);
            Click("Continue Button");
            yield return Hold(.85f);
            var battle = Battle;
            var simulation = battle.Simulation;
            Ensure(simulation.State.WaveIndex == 7 && simulation.State.BaseHealth == 16, "earned v2 wave 8");
            yield return Still("earned-preparation", .65f);
            Click("Start Wave Button");
            var start = simulation.State.Tick;
            var casts = 0;
            var framesUntilCast = 0;
            var showedBoss = false;
            var showedReady = false;
            var showedCooldown = false;
            var inspected = false;
            var inspectionTime = 0d;
            var deadline = Time.unscaledTimeAsDouble + 49;
            while (simulation.State.Phase == BattlePhase.Combat && Time.unscaledTimeAsDouble < deadline)
            {
                yield return End;
                var elapsedTicks = simulation.State.Tick - start;
                var ability = battle.GetComponent<AbilityController>();
                if ((!showedReady || !showedCooldown) && !ability.HasInputOwnership)
                {
                    var seal = Button("Ability Button");
                    var caption = seal.GetComponentInChildren<Text>().text;
                    if (!showedReady && seal.interactable && caption.EndsWith("Ready"))
                    {
                        SaveStill("ability-ready");
                        showedReady = true;
                    }
                    if (!showedCooldown && casts == 1 && elapsedTicks > 205 &&
                        !seal.interactable && caption.EndsWith("s left"))
                    {
                        SaveStill("ability-cooldown");
                        showedCooldown = true;
                    }
                }
                if (framesUntilCast > 0)
                {
                    framesUntilCast--;
                    if (framesUntilCast == 3)
                    {
                        var target = casts == 0 ? new PathPosition(-2.5f, -2f) : new PathPosition(3.487f, 2f);
                        Ensure(ability.SelectTarget(target), "Rift Seal accepts target");
                    }
                    else if (framesUntilCast == 0)
                    {
                        Ensure(Button("Confirm Ability Button").interactable, "Rift Seal cast can confirm");
                        if (casts == 0) SaveStill("ability-preview");
                        Click("Confirm Ability Button");
                        casts++;
                    }
                }
                else if (casts < 2 && elapsedTicks >= (casts == 0 ? 169 : 769) &&
                    simulation.AbilityState.CooldownRemainingTicks == 0 && battle.CanAcceptInput())
                {
                    Click("Ability Button");
                    framesUntilCast = 5;
                }
                var boss = simulation.State.Enemies.FirstOrDefault(e => e.Definition.Id.Value == BossHudController.BossId);
                if (!showedBoss && boss != null && boss.DistanceAlongPath > .7f)
                {
                    SaveStill("boss-combat");
                    showedBoss = true;
                }
                if (showedBoss && !inspected && boss != null && boss.DistanceAlongPath > 2.5f)
                {
                    var inspector = battle.GetComponent<EnemyInspectorController>();
                    Ensure(inspector.Select(boss.Id), "Warden inspector opens on live enemy");
                    inspected = true;
                    inspectionTime = Time.unscaledTimeAsDouble;
                }
                else if (inspectionTime > 0 && Time.unscaledTimeAsDouble - inspectionTime > .9)
                {
                    SaveStill("enemy-inspector");
                    Click("Close Inspector Button");
                    inspectionTime = 0;
                }
            }

            yield return Still("victory", 1.25f);
            Ensure(casts == 2 && showedReady && showedCooldown && showedBoss && inspected && simulation.State.Phase == BattlePhase.Victory &&
                simulation.State.BaseHealth == 16 && simulation.State.Kills == 64, "earned result");
            Click("Result Menu Button");
            yield return Hold(.6f);
            yield return Still("menu-finished", .6f);
            yield return StopVideo();
        }

        private IEnumerator Profiles()
        {
            foreach (var (width, height, side, bottom, label) in new[]
                     {
                         (1136, 640, 0, 0, "narrow"),
                         (2556, 1179, 132, 63, "modern"),
                         (2048, 1536, 0, 40, "ipad")
                     })
            {
                _sideInset = side;
                _bottomInset = bottom;
                ConfigureGameView(width, height);
                yield return Hold(.6f);
                Ensure(Screen.width == width && Screen.height == height, label);
                yield return OpenMenu(true);
                yield return Still(label + "-menu", 0);
                Click("Continue Button");
                yield return Hold(.65f);
                yield return Still(label + "-hud", 0);
                TapCell(FirstCell);
                yield return Hold(.3f);
                Click("Catalog flame");
                yield return Still(label + "-build", 0);
                Click("Cancel Button");
                yield return Hold(.2f);
                TapCell(new GridCoordinate(3, 3));
                yield return Hold(.32f);
                Click("Catalog flame");
                yield return Still(label + "-build-center", 0);
                Click("Pause Button");
                yield return Still(label + "-pause", 0);
            }
            _sideInset = _bottomInset = 0;
            ConfigureGameView(Width, Height);
        }

        private IEnumerator Defeat()
        {
            yield return OpenMenu(false);
            Click("New Game Button");
            yield return Hold(.65f);
            Click("flame");
            Click("Confirm Element Button");
            yield return Hold(.35f);
            var battle = Battle;
            var simulation = battle.Simulation;
            var fixture = simulation.ExportCheckpoint();
            fixture.baseHealth = 1;
            simulation.RestoreCheckpoint(fixture);
            var store = MainMenuController.CheckpointStoreFactory();
            Click("Start Wave Button");
            Directory.CreateDirectory(store.TempPath);
            for (var tick = 0; tick < 10000 && simulation.State.Phase == BattlePhase.Combat; tick++) simulation.Step();
            yield return Hold(.4f);
            Ensure(simulation.State.Phase == BattlePhase.Defeat, "defeat fixture");
            Ensure(!battle.GetComponent<BattleCheckpointController>().LastSaveSucceeded, "terminal write failed");
            yield return Still("defeat-save-error", 0);
            Directory.Delete(store.TempPath);
            Click("Retry Save Button");
            yield return Hold(.35f);
            Ensure(battle.GetComponent<BattleCheckpointController>().LastSaveSucceeded, "terminal Retry succeeded");
            yield return Still("defeat", 0);
        }

        private IEnumerator Stress()
        {
            yield return OpenMenu(false);
            Click("New Game Button");
            yield return Hold(.65f);
            Click("flame");
            Click("Confirm Element Button");
            yield return Hold(.35f);
            var battle = Battle;
            var simulation = battle.Simulation;
            var fixture = simulation.ExportCheckpoint();
            fixture.gold = 1200;
            simulation.RestoreCheckpoint(fixture);
            TapCell(FirstCell);
            yield return Hold(.35f);
            Click("Catalog flame");
            Click("Confirm Button");
            yield return Hold(.35f);
            TapCell(FirstCell);
            yield return Hold(.35f);
            Click("Upgrade Button");
            Ensure(simulation.State.Towers.Single().Level == 2, "fixture upgrade passed through UI");
            yield return Still("upgrade", 0);
            Click("Close Tower Button");

            var shade = new EnemyDefinition(new EnemyDefinitionId("shade"), 95, 1.05f, 1, 5, 0);
            var warden = new EnemyDefinition(new EnemyDefinitionId("rift-warden"), 430, .92f, 20, 50, 5);
            var wave = new WaveDefinition(new[] { new WaveSpawnGroup(shade, 12, 0, 15), new WaveSpawnGroup(warden, 1, 25, 30) });
            var dense = new BattleSimulation(simulation.Grid.Map, simulation.Path, new[] { wave }, simulation.TowerCatalog,
                25, 1200, 0, simulation.ReactionCatalog, null, AbilityDefinition.RiftSeal);
            typeof(BattleSceneController).GetField("_simulation", Flags).SetValue(battle, dense);
            battle.GetComponent<BattleCheckpointController>().Configure(null, dense, "capture-dense");
            Ensure(dense.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId("flame"), new GridCoordinate(-3, -1))).Success, "dense fixture tower");
            battle.ResetPresentation();
            yield return Hold(.4f);
            Click("Start Wave Button");
            var deadline = Time.unscaledTimeAsDouble + 12;
            while (dense.State.Enemies.Count < 8 && Time.unscaledTimeAsDouble < deadline) yield return End;
            Ensure(dense.State.Enemies.Count >= 8, "dense wave reached eight simultaneous enemies");
            yield return Still("dense-wave", 0);
        }

        private IEnumerator Milestones()
        {
            foreach (var wave in new[] { 3, 6 })
            {
                yield return OpenMenu(false);
                Click("New Game Button");
                yield return Hold(.65f);
                Click("flame");
                Click("Confirm Element Button");
                yield return Hold(.35f);
                var state = Battle.Simulation.ExportCheckpoint();
                state.phase = BattlePhase.WaveComplete;
                state.waveIndex = wave - 1;
                state.tick = wave * 100;
                state.elementProgression.choicesEarned = wave == 3 ? 1 : 2;
                state.elementProgression.pendingChoiceId = $"after-wave-{wave}";
                if (wave == 6)
                {
                    state.elementProgression.choicesSpent = 1;
                    state.elementProgression.unlockedElementIds = new[] { "flame", "tide" };
                    state.elementProgression.spentChoices = new[]
                    {
                        new SpentElementChoiceCheckpoint { choiceId = "after-wave-3", elementId = "tide" }
                    };
                }
                Battle.Simulation.RestoreCheckpoint(state);
                yield return Hold(.4f);
                yield return Still($"after-wave-{wave}", 0);
                Click(wave == 3 ? "tide" : "stone");
                Click("Confirm Element Button");
                yield return Hold(.3f);
                Click("Next Wave Button");
                yield return Hold(.35f);
                Ensure(Battle.Simulation.State.Phase == BattlePhase.Preparation, "next wave leads to preparation");
            }
        }

        private IEnumerator ReviewCases()
        {
            var cell = new GridCoordinate(3, 3);
            foreach (var (width, height, side, bottom, label) in new[]
                     {
                         (1136, 640, 0, 0, "narrow"),
                         (2556, 1179, 132, 63, "modern")
                     })
            {
                _sideInset = side;
                _bottomInset = bottom;
                ConfigureGameView(width, height);
                yield return Hold(.6f);
                yield return OpenMenu(false);
                Click("New Game Button");
                yield return Hold(.65f);
                Click("flame");
                Click("Confirm Element Button");
                yield return Hold(.35f);
                TapCell(cell);
                yield return Hold(.3f);
                Click("Catalog flame");
                var drawer = FindFirstObjectByType<BattleHudSafeArea>().GetComponentsInChildren<RectTransform>()
                    .Single(rect => rect.name == "Placement Panel");
                var point = (Vector2)Camera.main.WorldToScreenPoint(Battle.GetCellCenter(cell));
                Ensure(!RectTransformUtility.RectangleContainsScreenPoint(drawer, point), "central preview remains visible");
                Ensure(drawer.GetComponentInChildren<GridLayoutGroup>().constraintCount == 6, "all six schools are in one row");
                yield return Still(label + "-build-center", 0);
                if (label == "modern")
                {
                    Click("Cancel Button");
                    var store = MainMenuController.CheckpointStoreFactory();
                    Directory.CreateDirectory(store.TempPath);
                    Ensure(!Battle.GetComponent<BattleCheckpointController>().SaveCurrentState(), "fixture write failed");
                    yield return Hold(.3f);
                    TapCell(new GridCoordinate(3, 1));
                    yield return Hold(.3f);
                    Ensure(drawer.anchorMin.x == .5f && drawer.GetComponentInChildren<GridLayoutGroup>().constraintCount == 6,
                        "centered build strip is active");
                    Ensure(!Button("Retry Save Button").interactable, "open drawer guards Retry");
                    foreach (var school in new[] { "tide", "gale", "flame" }) Click("Catalog " + school);
                    yield return Still("modern-build-save-warning", 0);
                    Click("Cancel Button");
                    Directory.Delete(store.TempPath);
                    yield return Hold(.3f);
                    Click("Retry Save Button");
                }
            }

            _sideInset = _bottomInset = 0;
            ConfigureGameView(Width, Height);
            yield return Hold(.6f);
            yield return OpenMenu(false);
            Click("New Game Button");
            yield return Hold(.65f);
            Click("flame");
            Click("Confirm Element Button");
            yield return Hold(.35f);
            Click("Start Wave Button");
            yield return Hold(.35f);
            var battle = Battle;
            battle.enabled = false;
            var enemy = battle.Simulation.State.Enemies[0];
            var tick = battle.Simulation.State.Tick;
            var tower = new TowerId(9);
            foreach (StatusKind kind in Enum.GetValues(typeof(StatusKind)))
                typeof(EnemyState).GetMethod("ApplyStatus", Flags).Invoke(enemy, new object[]
                {
                    new StatusEffectDefinition(kind, 7, 90, kind == StatusKind.Burn ? 30 : 0), tower, tick
                });
            foreach (ReactionKind kind in Enum.GetValues(typeof(ReactionKind)))
                typeof(EnemyState).GetMethod("ReserveReaction", Flags).Invoke(enemy, new object[] { kind, tick });

            var center = battle.Simulation.Path.GetPosition(enemy.DistanceAlongPath);
            var steam = new ReactionEvent(tick, 1, ReactionKind.Steam, ReactionTriggerKind.StatusPair, tower, enemy.Id,
                center, StatusKind.Burn, StatusKind.Wet, 1.25f, 4);
            var storm = new ReactionEvent(tick, 5, ReactionKind.Storm, ReactionTriggerKind.Intrinsic, tower, enemy.Id,
                center, StatusKind.Wet, StatusKind.Gust, 2, 2);
            var steamHit = new DamageResolvedEvent(tick, 2, DamageOrigin.Reaction, tower, enemy.Id, center, center,
                4, 4, 4, 4, 0, false, ReactionKind.Steam, 1, 0);
            var stormHit = new DamageResolvedEvent(tick, 6, DamageOrigin.Reaction, tower, new EnemyId(99), center, center,
                6, 6, 6, 6, 0, false, ReactionKind.Storm, 5, 1);
            battle.Presentation.Consume(steam, new[] { steamHit, stormHit });
            battle.Presentation.Consume(storm, new[] { steamHit, stormHit });
            var inspector = battle.GetComponent<EnemyInspectorController>();
            Ensure(inspector.Select(enemy.Id), "full inspector fixture opened");
            Ensure(inspector.Details.Contains("4 HP lost") && inspector.Details.Contains("6 HP lost"), "both reaction totals");
            yield return Still("inspector-full", 0);
        }

        private IEnumerator Hold(float seconds)
        {
            var until = Time.unscaledTimeAsDouble + seconds;
            while (Time.unscaledTimeAsDouble < until) yield return End;
        }

        private IEnumerator Still(string name, float hold)
        {
            yield return null;
            yield return Hold(.14f);
            yield return End;
            SaveStill(name);
            yield return Hold(hold);
        }

        private void SaveStill(string name)
        {
            _marker = name;
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(_folder, name + ".png"), image.EncodeToPNG());
            Destroy(image);
            var viewport = FindFirstObjectByType<BattleHudSafeArea>();
            Write("png", $"{name}\t{Screen.width}x{Screen.height}\t{viewport.AppliedArea}");
            foreach (var label in viewport.GetComponentsInChildren<Text>())
            {
                if (label.text.Length > 0 && label.verticalOverflow == VerticalWrapMode.Truncate &&
                    label.preferredHeight > label.rectTransform.rect.height + 2)
                    Write("text-height", $"{name}\t{label.name}\t{label.preferredHeight:F2}\t{label.rectTransform.rect.height:F2}");
            }
        }

        private WaveCheckpointStore NewStore(bool earned)
        {
            var path = Path.Combine(_folder, "sessions", Guid.NewGuid().ToString("N"));
            var store = new WaveCheckpointStore(path);
            Directory.CreateDirectory(store.RootDirectory);
            if (earned) File.Copy(EarnedSave, store.PrimaryPath);
            MainMenuController.CheckpointStoreFactory = () => store;
            return store;
        }

        private static BattleSceneController Battle => FindFirstObjectByType<BattleSceneController>();

        private static Button Button(string name) =>
            FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(button => button.name == name);

        private void Click(string name)
        {
            Canvas.ForceUpdateCanvases();
            var button = Button(name);
            Ensure(button.isActiveAndEnabled && button.interactable, "active button " + name);
            var rect = (RectTransform)button.transform;
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)),
                button = PointerEventData.InputButton.Left
            };
            _hits.Clear();
            EventSystem.current.RaycastAll(pointer, _hits);
            Ensure(_hits.Count > 0 && _hits[0].gameObject == button.gameObject, "raycast " + name);
            pointer.pointerCurrentRaycast = _hits[0];
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Write("tap", $"{name}\t{pointer.position}");
        }

        private void TapCell(GridCoordinate coordinate)
        {
            var placement = Battle.GetComponent<BattlePlacementController>();
            var position = (Vector2)Camera.main.WorldToScreenPoint(Battle.GetCellCenter(coordinate));
            _hits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, _hits);
            Ensure(_hits.Count == 0, "world cell isn't behind UI");
            DispatchPointer.Invoke(placement, new object[] { position, 915, TouchPhase.Began });
            DispatchPointer.Invoke(placement, new object[] { position, 915, TouchPhase.Ended });
            Ensure(placement.HasSelection && placement.SelectedCell == coordinate, "world tap selected requested cell");
            Write("world", $"{coordinate}\t{position}");
        }

        private void LateUpdate()
        {
            if (_sideInset == 0 && _bottomInset == 0) return;
            var safe = FindFirstObjectByType<BattleHudSafeArea>();
            if (safe == null) return;
            var size = new Vector2(Screen.width, Screen.height);
            var rectangle = new Rect(_sideInset, _bottomInset, size.x - _sideInset * 2, size.y - _bottomInset);
            ApplyArea.Invoke(safe, new object[] { rectangle, size, 1f });
        }

        private void StartVideo()
        {
            _videoTarget = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32);
            _videoTarget.Create();
            _videoBytes = new byte[Width * Height * 4];
            _encoder = new Process
            {
                StartInfo = new ProcessStartInfo(Environment.GetEnvironmentVariable("VEILWARD_CAPTURE_FFMPEG") ?? "/opt/homebrew/bin/ffmpeg",
                    $"-hide_banner -loglevel error -y -f rawvideo -pix_fmt rgba -video_size {Width}x{Height} -framerate {Fps} -i pipe:0 " +
                    $"-threads 3 -c:v libx264 -preset veryfast -crf 21 -pix_fmt yuv420p -movflags +faststart \"{Path.Combine(_folder, "gameview.mp4")}\"")
                {
                    UseShellExecute = false,
                    RedirectStandardInput = true
                }
            };
            Ensure(_encoder.Start(), "FFmpeg started");
            _record = true;
            StartCoroutine(RecordFrames());
        }

        private IEnumerator RecordFrames()
        {
            var next = Time.unscaledTimeAsDouble;
            while (_record && !_videoFailed)
            {
                yield return End;
                var now = Time.unscaledTimeAsDouble;
                if (_pending || now < next) continue;
                // Сохраняем темп записи, повторяя текущий GameView кадр при задержке выдачи кадра.
                var copies = Math.Min(Fps, (int)((now - next) * Fps) + 1);
                next += copies / (double)Fps;
                ScreenCapture.CaptureScreenshotIntoRenderTexture(_videoTarget);
                _pending = true;
                AsyncGPUReadback.Request(_videoTarget, 0, TextureFormat.RGBA32, request =>
                {
                    _pending = false;
                    if (request.hasError)
                    {
                        _videoFailed = true;
                        return;
                    }
                    request.GetData<byte>().CopyTo(_videoBytes);
                    for (var i = 0; i < copies; i++)
                        _encoder.StandardInput.BaseStream.Write(_videoBytes, 0, _videoBytes.Length);
                });
                for (var i = 0; i < copies; i++) Write("frame", $"{_frames++}\t{_marker}");
            }
        }

        private IEnumerator StopVideo()
        {
            _record = false;
            while (_pending) yield return null;
            _encoder.StandardInput.Close();
            Ensure(_encoder.WaitForExit(8000) && _encoder.ExitCode == 0 && !_videoFailed && _frames > 0, "video encoded");
        }

        private void Write(string kind, string details)
        {
            var battle = Battle;
            var state = battle != null ? battle.Simulation?.State : null;
            _receipt.WriteLine($"{kind}\t{Time.unscaledTimeAsDouble:F4}\t{state?.Phase}\t{state?.Tick}\t" +
                $"{state?.Gold}\t{state?.BaseHealth}\t{state?.Kills}\t{details}");
        }

        private void OnDestroy()
        {
            _record = false;
            _receipt?.Dispose();
            if (_encoder != null)
            {
                if (!_encoder.HasExited) _encoder.StandardInput.Close();
                _encoder.Dispose();
            }
            if (_videoTarget != null)
            {
                _videoTarget.Release();
                Destroy(_videoTarget);
            }
            MainMenuController.CheckpointStoreFactory = null;
        }

        private static void ConfigureGameView(int width, int height)
        {
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { sizesType.GetProperty("currentGroupType").GetValue(sizes) });
            var size = Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),
                new[] { Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"), "FixedResolution"), (object)width, height, $"UI {width}×{height}" });
            var groupType = group.GetType();
            groupType.GetMethod("AddCustomSize").Invoke(group, new[] { size });
            var view = EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
            view.GetType().GetProperty("selectedSizeIndex", Flags).SetValue(view, (int)groupType.GetMethod("GetTotalCount").Invoke(group, null) - 1);
            view.Focus();
        }

        private static void Ensure(bool condition, string step)
        {
            if (!condition) throw new InvalidOperationException("UI.REWORK.1 capture: " + step);
        }
    }
}
#endif
