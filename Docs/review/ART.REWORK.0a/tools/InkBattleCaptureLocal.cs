#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Veilward.Domain;
using Veilward.Infrastructure;
using Object = UnityEngine.Object;

namespace Veilward.Presentation
{
    public sealed class InkBattleCaptureLocal : MonoBehaviour
    {
        private const int Width = 1600, Height = 900, Fps = 24;
        private static readonly string Output = Path.GetFullPath("TestResults/ARTREWORK0A/capture");
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly WaitForEndOfFrame EndOfFrame = new WaitForEndOfFrame();
        private RenderTexture _screen;
        private System.Diagnostics.Process _encoder;
        private StreamWriter _receipt;
        private int _pending, _frames;
        private bool _failed;

        public static void Begin(string mode)
        {
            var root = new GameObject("Local ART.REWORK.0a capture");
            DontDestroyOnLoad(root);
            root.AddComponent<InkBattleCaptureLocal>().StartCoroutine(root.GetComponent<InkBattleCaptureLocal>().Record(mode));
        }

        private IEnumerator Record(string mode)
        {
            var folder = Path.Combine(Output, mode);
            Directory.CreateDirectory(folder);
            ConfigureGameView();
            _receipt = new StreamWriter(Path.Combine(folder, "frames.tsv"), false) { AutoFlush = true };

            var checkpoint = new WaveCheckpointStore(Path.Combine(folder, Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(checkpoint.RootDirectory);
            if (mode == "earned")
                File.Copy("Docs/review/VFX.1d/reports/safe-file.json", checkpoint.PrimaryPath);
            MainMenuController.CheckpointStoreFactory = () => checkpoint;
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            var menu = FindFirstObjectByType<MainMenuController>();
            if (mode == "earned")
            {
                Ensure(menu.ContinueButton.interactable, "earned Continue available");
                menu.ContinueButton.onClick.Invoke();
            }
            else menu.NewGameButton.onClick.Invoke();
            yield return null;
            yield return null;
            var battle = FindFirstObjectByType<BattleSceneController>();
            Ensure(battle != null && battle.Simulation != null, "Battle loaded");
            var pause = battle.GetComponent<BattlePauseController>();
            pause.OnApplicationFocus(true);
            if (pause.CanResume) pause.Resume();

            if (mode == "stress") SetUpStress(battle);
            var s = battle.Simulation;
            for (var i = 0; i < 20; i++) yield return null;
            while (ShaderUtil.anythingCompiling) yield return null;
            yield return EndOfFrame;
            Ensure(Screen.width == Width && Screen.height == Height, $"GameView {Screen.width}x{Screen.height}");
            Still(folder, "preparation", s.State.Tick);
            if (mode == "stress") Ensure(s.StartWave() == WaveTransitionResult.Started, "stress started");
            else Ensure(battle.StartWave() == WaveTransitionResult.Started, "earned started");

            StartEncoder(folder);
            var startTick = s.State.Tick;
            _receipt.WriteLine($"scenario={mode} width={Width} height={Height} fps={Fps} startTick={startTick} hp={s.State.BaseHealth} gold={s.State.Gold}");
            _receipt.WriteLine("frame\tseconds\ttick\tenemies\tkills\tpaused\tflight\timpact\ttails");
            var begin = Time.unscaledTimeAsDouble;
            var next = 0d;
            var length = mode == "earned" ? 34 : 21;
            var phase = 0;
            var savedLaunch = false; var savedImpact = false; var savedDeath = false; var savedDense = false; var savedTurn = false;
            var savedNear = false; var savedOverlap = false;
            var casts = 0;
            while (Time.unscaledTimeAsDouble - begin < length && !_failed)
            {
                yield return EndOfFrame;
                var elapsed = Time.unscaledTimeAsDouble - begin;
                var relative = s.State.Tick - startTick;
                if (phase == 0 && elapsed >= 7d)
                {
                    pause.Pause();
                    phase++;
                    yield return EndOfFrame;
                    Still(folder, "pause", s.State.Tick);
                }
                else if (phase == 1 && elapsed >= 8.65d)
                {
                    Ensure(pause.Resume(), "resume action");
                    phase++;
                }

                if (mode == "earned" && casts < 2 && relative >= (casts == 0 ? 169 : 769) && !pause.IsPaused)
                {
                    var ability = battle.GetComponent<AbilityController>();
                    var target = casts == 0 ? new PathPosition(-2.5f, -2f) : new PathPosition(3.487f, 2f);
                    var fired = ability.BeginPreview() && ability.SelectTarget(target) && ability.ConfirmPreview();
                    _receipt.WriteLine($"cast {relative} {fired} {target.X} {target.Y}");
                    if (!fired) ability.CancelPreview();
                    casts++;
                }
                var shots = Enumerable.Range(0, 8).Select(i => battle.FlameProjectiles.GetView(i)).ToArray();
                var flight = shots.FirstOrDefault(shot => shot.IsFlying && shot.TrailVisible);
                var impact = shots.FirstOrDefault(shot => shot.ImpactVisible);
                var tails = battle.GetComponentsInChildren<EnemyModelView>().Where(v => v.IsTail).ToArray();
                var warden = s.State.Enemies.FirstOrDefault(enemy => enemy.Definition.Id.Value == BossHudController.BossId);
                if (!savedLaunch && flight != null) { savedLaunch = true; Still(folder, "flight", s.State.Tick); }
                if (!savedImpact && impact != null) { savedImpact = true; Still(folder, "impact", s.State.Tick); }
                if (!savedDeath && (mode == "stress" ? tails.Any(v => v.Pose == "Death") :
                     warden == null && relative > 335 && tails.Any(v => v.Pose == "Death")))
                { savedDeath = true; Still(folder, "death", s.State.Tick); }
                if (!savedDense && warden != null && s.State.Enemies.Count >= 4 && relative > 35)
                { savedDense = true; Still(folder, "shade-warden", s.State.Tick); }
                if (!savedTurn && warden != null && warden.DistanceAlongPath > 4.8f)
                { savedTurn = true; Still(folder, "turn", s.State.Tick); }
                if (!savedOverlap && mode == "stress" && warden != null && warden.DistanceAlongPath > 2f)
                { savedOverlap = true; Still(folder, "overlap", s.State.Tick); }
                if (!savedNear && warden != null && warden.DistanceAlongPath > 13.25f)
                { savedNear = true; Still(folder, "heart", s.State.Tick); }

                if (elapsed >= next)
                {
                    next += 1d / Fps;
                    if (elapsed - next > 1d / Fps) next = elapsed;
                    if (_pending >= 4) continue;
                    _receipt.WriteLine($"{_frames++}\t{elapsed:F4}\t{s.State.Tick}\t{s.State.Enemies.Count}\t{s.State.Kills}\t{pause.IsPaused}\t{(flight != null)}\t{(impact != null)}\t{tails.Length}");
                    ScreenCapture.CaptureScreenshotIntoRenderTexture(_screen);
                    _pending++;
                    AsyncGPUReadback.Request(_screen, 0, TextureFormat.RGBA32, request =>
                    {
                        _pending--;
                        if (request.hasError) { _failed = true; return; }
                        var bytes = request.GetData<byte>().ToArray();
                        _encoder.StandardInput.BaseStream.Write(bytes, 0, bytes.Length);
                    });
                }
            }
            while (_pending > 0) yield return null;
            _encoder.StandardInput.Close();
            _encoder.WaitForExit(8000);
            _receipt.WriteLine($"done frames={_frames} endTick={s.State.Tick} hp={s.State.BaseHealth} kills={s.State.Kills} phase={s.State.Phase} failed={_failed} encoder={_encoder.ExitCode}");
            _receipt.Dispose();
            _receipt = null;
            _encoder.Dispose();
            _encoder = null;
            _screen.Release();
            Destroy(_screen);
            MainMenuController.CheckpointStoreFactory = null;
            File.WriteAllText(Path.Combine(folder, "complete.txt"), _frames.ToString());
            Destroy(gameObject);
        }

        private void StartEncoder(string folder)
        {
            _screen = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32);
            _screen.Create();
            _encoder = new System.Diagnostics.Process();
            _encoder.StartInfo.FileName = "/opt/homebrew/bin/ffmpeg";
            _encoder.StartInfo.Arguments = $"-hide_banner -loglevel error -y -f rawvideo -pix_fmt rgba -video_size {Width}x{Height} -framerate {Fps} -i pipe:0 -threads 3 -c:v libx264 -preset veryfast -crf 19 -pix_fmt yuv420p \"{Path.Combine(folder, "battle.mp4")}\"";
            _encoder.StartInfo.UseShellExecute = false;
            _encoder.StartInfo.RedirectStandardInput = true;
            _encoder.Start();
        }

        private void OnDestroy()
        {
            _receipt?.Dispose();
            _encoder?.Dispose();
            MainMenuController.CheckpointStoreFactory = null;
        }

        private void Still(string folder, string name, long tick)
        {
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            Destroy(image);
            _receipt.WriteLine($"png {name} tick={tick}");
        }

        private static void SetUpStress(BattleSceneController battle)
        {
            var existing = battle.Simulation;
            var shade = new EnemyDefinition(new EnemyDefinitionId("shade"), 95, 1.05f, 1, 5, 0);
            var warden = new EnemyDefinition(new EnemyDefinitionId("rift-warden"), 430, .92f, 20, 50, 5);
            var wave = new WaveDefinition(new[] { new WaveSpawnGroup(shade, 12, 0, 15), new WaveSpawnGroup(warden, 1, 25, 30) });
            var s = new BattleSimulation(existing.Grid.Map, existing.Path, new[] { wave }, existing.TowerCatalog, 25, 1200, 0,
                existing.ReactionCatalog, null, AbilityDefinition.RiftSeal);
            typeof(BattleSceneController).GetField("_simulation", Flags).SetValue(battle, s);
            var cells = new[] { new GridCoordinate(-3, -1), new GridCoordinate(1, -1), new GridCoordinate(-1, 1) };
            for (var i = 0; i < cells.Length; i++)
            {
                Ensure(s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId("flame"), cells[i])).Success, $"place {i + 1}");
                for (var level = 1; level <= i; level++)
                    Ensure(s.UpgradeTower(s.State.Towers[i].Id).Success, $"upgrade {i + 1} to {level + 1}");
            }
            battle.ResetPresentation();
        }

        private static void ConfigureGameView()
        {
            var asm = typeof(UnityEditor.Editor).Assembly;
            var sizesType = asm.GetType("UnityEditor.GameViewSizes");
            var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { sizesType.GetProperty("currentGroupType").GetValue(sizes) });
            var sizeType = asm.GetType("UnityEditor.GameViewSizeType");
            var size = Activator.CreateInstance(asm.GetType("UnityEditor.GameViewSize"),
                new object[] { Enum.Parse(sizeType, "FixedResolution"), Width, Height, "ART.REWORK.0a" });
            var type = group.GetType();
            type.GetMethod("AddCustomSize").Invoke(group, new[] { size });
            var view = EditorWindow.GetWindow(asm.GetType("UnityEditor.GameView"));
            view.GetType().GetProperty("selectedSizeIndex", Flags).SetValue(view, (int)type.GetMethod("GetTotalCount").Invoke(group, null) - 1);
            view.Focus();
        }

        private static void Ensure(bool ok, string message)
        {
            if (!ok) throw new InvalidOperationException("ART.REWORK.0a capture " + message);
        }
    }
}
#endif
