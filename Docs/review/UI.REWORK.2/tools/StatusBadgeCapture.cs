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
    public sealed class StatusBadgeCapture : MonoBehaviour
    {
        private const int Width = 1600, Height = 900, Fps = 20;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly WaitForEndOfFrame End = new WaitForEndOfFrame();
        private static readonly MethodInfo ApplyArea = typeof(BattleHudSafeArea).GetMethod("ApplySafeArea", Flags);
        private static readonly MethodInfo DispatchPointer = typeof(BattlePlacementController).GetMethod("DispatchPointer", Flags);
        private static readonly string Root = Path.GetFullPath("TestResults/UIREWORK2/capture");
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
            var root = new GameObject("UI.REWORK.2 GameView evidence");
            DontDestroyOnLoad(root);
            var driver = root.AddComponent<StatusBadgeCapture>();
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
            switch (mode)
            {
                case "earned": yield return Earned(); break;
                case "fixture": yield return Fixture(); break;
                case "profiles": yield return Profiles(); break;
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

        private static BossHudController Boss => Battle.GetComponent<BossHudController>();
        private static EnemyInspectorController Inspector => Battle.GetComponent<EnemyInspectorController>();
        private static void Sync() => typeof(BattleSceneController).GetMethod("LateUpdate", Flags).Invoke(Battle, null);

        private IEnumerator Earned()
        {
            yield return OpenMenu(true);
            Click("Continue Button");
            yield return Hold(.4f);
            var sim = Battle.Simulation;
            Ensure(sim.State.WaveIndex == 7 && sim.State.BaseHealth == 16, "earned v2 preparation 8");
            Click("Start Wave Button");
            var start = sim.State.Tick;
            var captures = new HashSet<string>();
            var deadline = Time.unscaledTimeAsDouble + 50;
            var casts = 0;
            var previewFrames = 0;
            while (sim.State.Phase == BattlePhase.Combat && Time.unscaledTimeAsDouble < deadline)
            {
                yield return End;
                var ability = Battle.GetComponent<AbilityController>();
                var elapsed = sim.State.Tick - start;
                if (previewFrames > 0)
                {
                    previewFrames--;
                    if (previewFrames == 3)
                    {
                        var target = casts == 0 ? new PathPosition(-2.5f, -2f) : new PathPosition(3.487f, 2f);
                        Ensure(ability.SelectTarget(target), "earned Rift Seal target");
                    }
                    if (previewFrames == 0) { Click("Confirm Ability Button"); casts++; }
                }
                else if (casts < 2 && elapsed >= (casts == 0 ? 169 : 769) &&
                         sim.AbilityState.CooldownRemainingTicks == 0 && Battle.CanAcceptInput())
                {
                    Click("Ability Button");
                    previewFrames = 5;
                }

                var boss = sim.State.Enemies.FirstOrDefault(e => e.Definition.Id.Value == BossHudController.BossId);
                if (boss != null && boss.DistanceAlongPath > .7f && captures.Add("boss-combat"))
                    SaveStill("boss-combat");
                if (Boss.VisibleBadgeCount == 2 && captures.Add("boss-two"))
                    SaveStill("boss-two");
                if (Battle.GetComponentsInChildren<EnemyStatusView>().Any(view => view.VisibleSlotCount == 2) &&
                    sim.State.Enemies.Any(e => e.Definition.Id.Value == "shade" && e.Statuses.Count >= 2) && captures.Add("shade-two"))
                    SaveStill("shade-two");
            }

            yield return Still("victory", .1f);
            Ensure(casts == 2 && captures.Contains("boss-combat") && sim.State.Phase == BattlePhase.Victory &&
                sim.State.BaseHealth == 16 && sim.State.Kills == 64, "earned Victory");
        }

        private IEnumerator OpenFixture(int ticks = 330)
        {
            yield return OpenMenu(true);
            Click("Continue Button");
            yield return Hold(.25f);
            var battle = Battle;
            battle.enabled = false;
            var sim = battle.Simulation;
            foreach (var tower in sim.State.Towers.ToArray())
                Ensure(battle.SellTower(tower.Id).Success, "fixture removes towers");
            Click("Start Wave Button");
            for (var i = 0; i < ticks; i++) sim.Step();
            Sync();
            if (ticks == 330) Ensure(Boss.BadgeOwner.HasValue, "fixture spawned real Warden");
        }

        private static EnemyState FindBoss() =>
            Battle.Simulation.State.Enemies.Single(enemy => enemy.Definition.Id.Value == BossHudController.BossId);

        private static void Apply(EnemyState enemy, StatusKind kind, int power, int duration)
        {
            typeof(EnemyState).GetMethod("ApplyStatus", Flags).Invoke(enemy, new object[]
            {
                new StatusEffectDefinition(kind, power, duration, kind == StatusKind.Burn ? 90 : 0),
                new TowerId(9), Battle.Simulation.State.Tick
            });
        }

        private static void Advance(EnemyState enemy, float distance) =>
            typeof(EnemyState).GetMethod("Advance", Flags).Invoke(enemy, new object[] { distance });

        private static void Cue(EnemyState enemy, ReactionKind kind, ReactionTriggerKind trigger)
        {
            var sim = Battle.Simulation;
            var steam = kind == ReactionKind.Steam;
            var point = sim.Path.GetPosition(enemy.DistanceAlongPath);
            var source = new TowerId(9);
            var sequence = steam ? 1 : 3;
            var damage = steam ? 4 : 6;
            var reaction = new ReactionEvent(sim.State.Tick, sequence, kind, trigger, source, enemy.Id, point,
                steam ? StatusKind.Burn : StatusKind.Wet, steam ? StatusKind.Wet : StatusKind.Gust, 2, 2);
            var applied = new DamageResolvedEvent(sim.State.Tick, sequence + 1, DamageOrigin.Reaction, source, enemy.Id,
                point, point, damage, damage, damage, damage, 0, false, kind, sequence, 0);
            typeof(EnemyState).GetMethod("ReserveReaction", Flags).Invoke(enemy, new object[] { kind, sim.State.Tick });
            Battle.Presentation.Consume(reaction, new[] { applied });
        }

        private IEnumerator Fixture()
        {
            yield return OpenFixture();
            var boss = FindBoss();
            var sim = Battle.Simulation;
            Advance(boss, 3.2f);
            var shades = sim.State.Enemies.Where(e => e.Definition.Id.Value == "shade").ToArray();
            var shade = shades.OrderBy(e => e.DistanceAlongPath).First();
            var placement = Battle.GetComponent<BattlePlacementController>();
            var shadePoint = sim.Path.GetPosition(shade.DistanceAlongPath);
            Apply(shade, StatusKind.Wet, 8, 100);
            Apply(shade, StatusKind.Gust, 2, 100);
            Apply(boss, StatusKind.Wet, 7, 61);
            Apply(boss, StatusKind.Gust, 2, 70);
            Sync();
            StartVideo();
            yield return Still("two-slots", .5f);
            Ensure(Boss.VisibleBadgeCount == 2, "both Warden slots drawn");
            var point = (Vector2)Camera.main.WorldToScreenPoint(new Vector3(shadePoint.X, .6f, shadePoint.Y));
            _hits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, _hits);
            Ensure(_hits.Count == 0, "world Shade tap has no decorative intersection");
            DispatchPointer.Invoke(placement, new object[] { point, 915, TouchPhase.Began });
            DispatchPointer.Invoke(placement, new object[] { point, 915, TouchPhase.Ended });
            Ensure(Inspector.SelectedEnemyId == shade.Id, "world Shade tap selected its ID");
            yield return Still("shade-inspector", .5f);
            Click("Close Inspector Button");

            Cue(boss, ReactionKind.Steam, ReactionTriggerKind.StatusPair);
            Sync();
            yield return Still("steam", .3f);
            Click("Pause Button");
            Sync();
            Battle.enabled = true;
            var frozenTick = sim.State.Tick;
            var frozenClock = Battle.Presentation.ActiveTime;
            yield return Still("pause", .7f);
            Ensure(sim.State.Tick == frozenTick && Battle.Presentation.ActiveTime == frozenClock, "Pause holds both clocks");
            Click("Resume Button");
            yield return Still("resumed-cue", .05f);
            Ensure(Boss.VisibleBadgeCount == 2, "resumed before cue expiry");
            yield return Hold(.62f);
            Battle.enabled = false;
            Sync();
            yield return Still("cue-expired", .6f);
            Ensure(Boss.VisibleBadgeCount == 2, "underlying statuses return after cue expiry");

            Apply(boss, StatusKind.Burn, 7, 120);
            Apply(boss, StatusKind.Crack, 3, 120);
            Apply(boss, StatusKind.Wet, 7, 120);
            Apply(boss, StatusKind.Gust, 2, 120);
            var refreshedTick = sim.State.Tick;
            Cue(boss, ReactionKind.Storm, ReactionTriggerKind.Intrinsic);
            Sync();
            yield return Still("storm", .55f);
            Click("Inspect Warden");
            Ensure(Inspector.SelectedEnemyId == boss.Id && Inspector.Details.Contains("4 HP lost") &&
                Inspector.Details.Contains("6 HP lost"), "shared Inspector routes actual owner and displays fixture hit totals");
            yield return Still("inspector-full", 1.05f);
            Click("Close Inspector Button");
            Battle.enabled = true;
            Time.timeScale = 2;
            var active = Battle.Presentation.ActiveTime;
            yield return Hold(.48f);
            Ensure(Battle.Presentation.ActiveTime > active + .55, "scaled clock > unscaled elapsed");
            yield return Still("x2", .4f);
            var expiryDeadline = Time.unscaledTimeAsDouble + 4;
            while (sim.State.Tick < refreshedTick + 120 && Time.unscaledTimeAsDouble < expiryDeadline) yield return End;
            Ensure(Boss.BadgeOwner == boss.Id && Boss.VisibleBadgeCount == 0, "boss effects expired on the current owner");
            yield return Still("status-expired", .55f);
            Time.timeScale = 1;
            yield return StopVideo();
            // Отдельный плотный fixture с контролируемыми дистанциями и ранжированием.
            yield return OpenFixture(160);
            var crowd = Battle.Simulation.State.Enemies.Where(enemy => enemy.Definition.Id.Value == "shade").ToArray();
            Ensure(crowd.Length >= 4, "dense fixture contains at least four current Shade");
            var distance = crowd.Max(enemy => enemy.DistanceAlongPath);
            foreach (var (enemy, index) in crowd.Select((enemy, index) => (enemy, index)))
            {
                Advance(enemy, distance + index * .09f);
                Apply(enemy, StatusKind.Wet, 7, 120);
                Apply(enemy, StatusKind.Gust, 2, 120);
            }
            Sync();
            yield return Still("dense-fixture", 0);
        }

        private IEnumerator Profiles()
        {
            yield return OpenFixture();
            var boss = FindBoss();
            Advance(boss, 3.2f);
            Apply(boss, StatusKind.Burn, 7, 120);
            Apply(boss, StatusKind.Wet, 7, 120);
            var shade = Battle.Simulation.State.Enemies.First(enemy => enemy.Definition.Id.Value == "shade");
            Apply(shade, StatusKind.Gust, 2, 120);
            Apply(shade, StatusKind.Crack, 3, 120);
            foreach (var (width, height, dpr, side, bottom, label) in new[]
                     {
                         (1600, 900, 1, 0, 0, "baseline"),
                         (1136, 640, 2, 0, 0, "narrow"),
                         (2556, 1179, 3, 132, 63, "modern"),
                         (2048, 1536, 2, 0, 40, "ipad")
                     })
            {
                ConfigureGameView(width, height);
                _sideInset = side;
                _bottomInset = bottom;
                yield return Hold(.45f);
                Sync();
                var target = (RectTransform)Button("Inspect Warden").transform;
                var corners = new Vector3[4];
                target.GetWorldCorners(corners);
                var screen = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
                var safe = FindFirstObjectByType<BattleHudSafeArea>().AppliedArea;
                var center = screen.center;
                Ensure(safe.Contains(screen.min) && safe.Contains(screen.max), "boss hit area within safe area");
                Ensure(screen.width / dpr >= 44 && screen.height / dpr >= 44, "boss target at least 44 logical points");
                _hits.Clear();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = center }, _hits);
                Ensure(_hits[0].gameObject == target.gameObject, "profile rays hit correct control");
                Write("target", $"{label}	{screen}	{dpr}	{screen.width / dpr:F2}x{screen.height / dpr:F2}pt");
                yield return Still(label, 0);
            }
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
            if (Battle != null)
            {
                var glyphs = FindObjectsByType<Image>(FindObjectsSortMode.None)
                    .Where(image => image.name.StartsWith("Boss Badge ") && image.enabled)
                    .Select(image => image.sprite.name).OrderBy(value => value);
                var shadePairs = Battle.GetComponentsInChildren<EnemyStatusView>().Count(view => view.VisibleSlotCount == 2);
                Write("badges", $"{name}\t{Boss.BadgeOwner?.Value}\t{Boss.Health}\t{string.Join(",", glyphs)}\tShadePairs={shadePairs}\t" +
                    $"clock={Battle.Presentation.ActiveTime:F4}\tscale={Time.timeScale:R}");
            }
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
                if (!_record) yield break;
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
            if (!condition) throw new InvalidOperationException("UI.REWORK.2 capture: " + step);
        }
    }
}
#endif
