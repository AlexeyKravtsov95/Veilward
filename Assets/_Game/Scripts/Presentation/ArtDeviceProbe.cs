using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace Veilward.Presentation
{
    // Только явный art-device запуск. Synthetic probes не вызывают gameplay commands/Step.
    public sealed class ArtDeviceProbe : MonoBehaviour
    {
        private const int Capacity = 8192;
        private double _warmup = 5, _duration = 30;
        private readonly double[] _intervals = new double[Capacity];
        private readonly double[] _cpu = new double[Capacity], _gpu = new double[Capacity];
        private readonly FrameTiming[] _timing = new FrameTiming[1];
        private readonly List<Rect> _rects = new List<Rect>(12);
        private readonly EnemyHealthBarView[] _health = new EnemyHealthBarView[12];
        private ArtReferenceShowcase _showcase;
        private Transform _root;
        private FlameStrikeView _strike;
        private string _scenario = "normal";
        private int _count, _repeat, _oldTarget, _oldVsync;
        private double _started, _previous, _phaseSeconds;
        private long _baseline, _peak, _reservedPeak;
        private bool _measuring, _configured, _samplingStarted;
        private ulong _lastTimingTimestamp;
        private string _label = "ART2B";
        private bool _initialPaused, _focused = true, _applicationPaused, _skipVisualDelta = true;
        private long _advancedTicks, _previousTick;
        private int _attackSamples, _previousAttacks;
        private float _visualAccumulator;
        private int _visualTick;
        public int ProbeCount { get; private set; }
        public bool MeasurementActive => _measuring;
        public string MeasurementDirectory { get; private set; }
        private string _outputRoot;

        public static bool ShouldAttach(string scene, string[] args) =>
            scene == "ArtReference" && Array.IndexOf(args, "-art2b") >= 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Attach()
        {
            var args = Environment.GetCommandLineArgs();
            if (!ShouldAttach(SceneManager.GetActiveScene().name, args)) return;
            var showcase = FindFirstObjectByType<ArtReferenceShowcase>();
            if (showcase == null) throw new InvalidOperationException("ART2B requires ArtReference.");
            showcase.gameObject.AddComponent<ArtDeviceProbe>();
        }

        private IEnumerator Start() => InitializeFromArguments(Environment.GetCommandLineArgs());

        public IEnumerator InitializeFromArguments(string[] args, string outputRoot = null)
        {
            if (_configured || !ShouldAttach(gameObject.scene.name, args)) yield break;
            var showcase = GetComponent<ArtReferenceShowcase>();
            var deadline = Time.realtimeSinceStartupAsDouble + 5;
            while (showcase != null && showcase.Simulation == null && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            if (showcase == null || showcase.Simulation == null) throw new InvalidOperationException("ART2B showcase initialization failed.");
            if (_configured) yield break;
            Initialize(showcase, outputRoot);
            var index = Array.IndexOf(args, "-art2b");
            SelectScenario(index + 1 < args.Length ? args[index + 1] : "normal");
            BeginMeasurements();
        }

        public void Initialize(ArtReferenceShowcase showcase, string outputRoot = null)
        {
            if (_configured) return;
            _showcase = showcase != null ? showcase : throw new ArgumentNullException(nameof(showcase));
            _outputRoot = outputRoot ?? Path.Combine(UnityEngine.Application.persistentDataPath, "art2b");
            _initialPaused = showcase.IsPaused;
            _oldTarget = UnityEngine.Application.targetFrameRate; _oldVsync = QualitySettings.vSyncCount;
            UnityEngine.Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0; _configured = true;
        }

        public void SelectScenario(string scenario)
        {
            if (scenario != "normal" && scenario != "front" && scenario != "side" && scenario != "behind" && scenario != "dense")
                throw new ArgumentException("Unknown ART2B scenario.");
            _measuring = false;
            ClearProbes();
            _scenario = scenario; _paused = false; _skipVisualDelta = true;
            _label = "ART2B | " + scenario + (scenario == "normal" ? " actual saved combat" : " SYNTHETIC assigned positions/HP");
            if (scenario == "normal") { _showcase.SetDeviceProbeOverride(false); _showcase.SetPaused(false); return; }
            for (var i = 0; i < ArtReferenceShowcase.StrikeCapacity; i++) _showcase.GetStrike(i).Clear();
            _showcase.SetPaused(true); _showcase.SetDeviceProbeOverride(true);
            _root = new GameObject("ART2B Synthetic Probes").transform;
            _root.SetParent(_showcase.transform, false);
            var near = new Vector3(8, 0, 8).normalized;
            var center = _showcase.TowerView.position;
            if (scenario == "front") center += near * .8f;
            else if (scenario == "side") center += new Vector3(near.z, 0, -near.x) * .85f;
            else if (scenario == "behind") center -= near * .8f;
            else center = new Vector3(-.7f + 1.5f * .48f, .14f, -.45f + .48f);
            ProbeCount = scenario == "dense" ? 12 : 1;
            for (var i = 0; i < ProbeCount; i++)
            {
                var view = _showcase.CreateDeviceProbeEnemy(_root);
                view.position = scenario == "dense" ? new Vector3(-.7f + (i % 4) * .48f, .14f, -.45f + (i / 4) * .48f)
                    : new Vector3(center.x, .14f, center.z);
                view.rotation = Quaternion.LookRotation(Vector3.right);
                var health = _health[i] = view.GetComponent<EnemyHealthBarView>();
                health.SetNormalizedHealth(scenario == "dense" ? .5f + (i % 4) * .1f : .65f);
            }
            _strike = _showcase.CreateDeviceProbeStrike(_root);
            _strike.Play(_showcase.TowerView.position + Vector3.up * .95f, new Vector3(center.x, .72f, center.z));
            _visualTick = 0; _visualAccumulator = 0;
        }

        public void BeginMeasurements(double warmupSeconds = 5, double durationSeconds = 30)
        {
            if (_paused || !_focused || _applicationPaused || (_scenario == "normal" && _showcase.IsPaused))
                throw new InvalidOperationException("Resume focused ART2B before measurement.");
            if (double.IsNaN(warmupSeconds) || double.IsInfinity(warmupSeconds) || warmupSeconds < 0 || warmupSeconds > 60 ||
                double.IsNaN(durationSeconds) || double.IsInfinity(durationSeconds) || durationSeconds <= 0 || durationSeconds > 60)
                throw new ArgumentOutOfRangeException(nameof(durationSeconds));
            _warmup = warmupSeconds; _duration = durationSeconds;
            MeasurementDirectory = Path.Combine(_outputRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(MeasurementDirectory);
            var metadata = Path.Combine(UnityEngine.Application.streamingAssetsPath, "art2b-build.json");
            if (!UnityEngine.Application.isEditor && !File.Exists(metadata))
                throw new InvalidOperationException("ART2B exact source receipt missing from player.");
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            var settings = JsonUtility.ToJson(new Settings { scenario = _scenario,
                renderPipeline = pipeline == null ? "none" : pipeline.name,
                renderScale = pipeline == null ? 1 : pipeline.renderScale,
                effectiveMsaa = pipeline == null ? QualitySettings.antiAliasing : pipeline.msaaSampleCount,
                mainLightShadows = pipeline != null && pipeline.supportsMainLightShadows,
                mainShadowResolution = pipeline == null ? 0 : pipeline.mainLightShadowmapResolution,
                shadowDistance = pipeline == null ? 0 : pipeline.shadowDistance,
                shadowCascades = pipeline == null ? 0 : pipeline.shadowCascadeCount,
                softShadows = pipeline != null && pipeline.supportsSoftShadows,
                cameraPixelWidth = _showcase.WorldCamera.pixelWidth, cameraPixelHeight = _showcase.WorldCamera.pixelHeight,
                cameraScaledPixelWidth = _showcase.WorldCamera.scaledPixelWidth, cameraScaledPixelHeight = _showcase.WorldCamera.scaledPixelHeight,
                sourceReceipt = File.Exists(metadata) ? File.ReadAllText(metadata) : "EDITOR TEST; NOT DEVICE" });
            File.WriteAllText(Path.Combine(MeasurementDirectory, "run.json"), settings);
            _repeat = 0; StartWindow();
            Debug.Log("ART2B_SETTINGS " + settings);
        }

        private void StartWindow()
        {
            _count = 0; _samplingStarted = false; _phaseSeconds = 0; _started = _previous = Time.realtimeSinceStartupAsDouble;
            _advancedTicks = 0; _attackSamples = 0;
            _previousTick = _showcase.Simulation.State.Tick; _previousAttacks = _showcase.ObservedAttacks;
            _baseline = Profiler.GetTotalAllocatedMemoryLong(); _peak = _baseline;
            _reservedPeak = Profiler.GetTotalReservedMemoryLong(); _measuring = true;
        }

        private void LateUpdate()
        {
            if (!_configured || _showcase.Simulation == null) return;
            var now = Time.realtimeSinceStartupAsDouble;
            var interval = now - _previous; _previous = now;
            if (_root != null)
            {
                // Скрываются только live enemy views; Domain остаётся frozen и неизменным.
                _rects.Clear();
                for (var i = 0; i < ProbeCount; i++) { _health[i].Face(_showcase.WorldCamera); _health[i].Layout(_showcase.WorldCamera, _rects); }
                if (!_paused && _focused && !_applicationPaused)
                {
                    if (_skipVisualDelta) { _skipVisualDelta = false; _visualAccumulator = 0; }
                    else _visualAccumulator += Mathf.Min(Time.unscaledDeltaTime, .25f);
                    while (_visualAccumulator >= 1f / 30)
                    {
                        _visualAccumulator -= 1f / 30; _strike.Advance(1f / 30);
                        if (++_visualTick % 30 == 0) _strike.Play(_strike.Source, _strike.Target);
                    }
                }
            }
            if (!_measuring) return;
            FrameTimingManager.CaptureFrameTimings();
            if (now - _started < _warmup) return;
            if (!_samplingStarted)
            {
                _samplingStarted = true;
                _previousTick = _showcase.Simulation.State.Tick; _previousAttacks = _showcase.ObservedAttacks;
                _baseline = _peak = Profiler.GetTotalAllocatedMemoryLong();
                _reservedPeak = Profiler.GetTotalReservedMemoryLong();
                return;
            }
            if (_count >= Capacity) { _measuring = false; throw new InvalidOperationException("ART2B sample buffer full."); }
            _intervals[_count] = interval * 1000;
            _cpu[_count] = _gpu[_count] = 0;
            if (FrameTimingManager.GetLatestTimings(1, _timing) > 0 && _timing[0].frameStartTimestamp != _lastTimingTimestamp)
            {
                _lastTimingTimestamp = _timing[0].frameStartTimestamp;
                _cpu[_count] = _timing[0].cpuFrameTime; _gpu[_count] = _timing[0].gpuFrameTime;
            }
            var tick = _showcase.Simulation.State.Tick;
            var attacks = _showcase.ObservedAttacks;
            _advancedTicks += tick >= _previousTick ? tick - _previousTick : tick;
            _attackSamples += attacks >= _previousAttacks ? attacks - _previousAttacks : attacks;
            _previousTick = tick; _previousAttacks = attacks;
            _count++; _phaseSeconds += interval;
            _peak = Math.Max(_peak, Profiler.GetTotalAllocatedMemoryLong());
            _reservedPeak = Math.Max(_reservedPeak, Profiler.GetTotalReservedMemoryLong());
            if (_phaseSeconds < _duration) return;
            // IO/JSON только между окнами, никогда внутри следующего warmup/sample.
            SaveWindow();
            if (++_repeat < 3) StartWindow(); else
            {
                _measuring = false;
                File.WriteAllText(Path.Combine(MeasurementDirectory, "complete.json"), "{\"complete\":true,\"repeats\":3}");
                Debug.Log("ART2B_COMPLETE " + _scenario);
            }
        }

        private bool _paused;
        public void TogglePause()
        {
            _measuring = false;
            if (!_focused || _applicationPaused) return;
            _paused = !_paused; _visualAccumulator = 0; _skipVisualDelta = true;
            _showcase.SetPaused(_scenario != "normal" || _paused);
        }

        private void Suspend()
        {
            _measuring = false; _paused = true; _visualAccumulator = 0; _skipVisualDelta = true;
            if (_showcase != null) _showcase.SetPaused(true);
        }
        private void OnApplicationPause(bool paused) { _applicationPaused = paused; if (paused) Suspend(); }
        private void OnApplicationFocus(bool focused) { _focused = focused; if (!focused) Suspend(); }

        private void SaveWindow()
        {
            _measuring = false;
            if (_scenario == "normal" && (_advancedTicks == 0 || _attackSamples == 0))
                throw new InvalidOperationException("Frozen normal combat is not measurement evidence.");
            var report = new Report { advancedTicks = _advancedTicks, observedAttacks = _attackSamples, scenario = _scenario, repeat = _repeat + 1, samples = _count,
                durationSeconds = _phaseSeconds, warmupSeconds = _warmup,
                unityAllocatedBaseline = _baseline, unityAllocatedPeak = _peak,
                unityAllocatedAfter = Profiler.GetTotalAllocatedMemoryLong(), unityReservedPeak = _reservedPeak,
                intervalsMs = Copy(_intervals), cpuFrameMs = Copy(_cpu), gpuFrameMs = Copy(_gpu) };
            var json = JsonUtility.ToJson(report);
            File.WriteAllText(Path.Combine(MeasurementDirectory, "window-" + (_repeat + 1) + ".json"), json);
            Debug.Log("ART2B_WINDOW " + _scenario + " repeat=" + (_repeat + 1) + " samples=" + _count);
        }
        private double[] Copy(double[] source) { var values = new double[_count]; Array.Copy(source, values, _count); return values; }

        private void ClearProbes()
        {
            _strike?.Clear(); _strike = null;
            if (_root != null) { _root.gameObject.SetActive(false); Destroy(_root.gameObject); _root = null; }
            Array.Clear(_health, 0, _health.Length); ProbeCount = 0; _paused = false;
        }
        private void OnDisable()
        {
            _measuring = false; ClearProbes();
            if (_configured)
            {
                UnityEngine.Application.targetFrameRate = _oldTarget; QualitySettings.vSyncCount = _oldVsync;
                _scenario = "normal"; _label = "ART2B | normal actual saved combat";
                if (_showcase != null) { _showcase.SetDeviceProbeOverride(false); _showcase.SetPaused(_initialPaused); }
            }
        }
        private void OnEnable()
        {
            if (_configured) { UnityEngine.Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0; }
        }
        private void OnDestroy()
        {
            ClearProbes();
            if (_configured) { UnityEngine.Application.targetFrameRate = _oldTarget; QualitySettings.vSyncCount = _oldVsync; }
        }

        private void OnGUI()
        {
            if (!_configured) return;
            var safe = Screen.safeArea;
            GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / 1311f, Screen.width / 1311f, 1));
            var x = safe.xMin * 1311 / Screen.width;
            var y = (Screen.height - safe.yMax) * 1311 / Screen.width;
            GUI.Box(new Rect(x, y, 790, 30), _label);
            if (GUI.Button(new Rect(x, y + 32, 95, 38), "Pause")) TogglePause();
            if (GUI.Button(new Rect(x + 100, y + 32, 95, 38), "Restart")) { _measuring = false; ClearProbes(); _scenario = "normal"; _label = "ART2B | normal actual saved combat"; _showcase.RestartShowcase(); _showcase.SetPaused(false); }
            GUI.matrix = Matrix4x4.identity;
        }

        [Serializable] private sealed class Report
        {
            public string scenario; public int repeat, samples, observedAttacks; public long advancedTicks;
            public double durationSeconds, warmupSeconds;
            public long unityAllocatedBaseline, unityAllocatedPeak, unityAllocatedAfter, unityReservedPeak;
            public double[] intervalsMs, cpuFrameMs, gpuFrameMs;
        }
        [Serializable] private sealed class Settings
        {
            public string scenario, sourceReceipt, renderPipeline;
            public float renderScale, shadowDistance;
            public int effectiveMsaa, mainShadowResolution, shadowCascades, cameraPixelWidth, cameraPixelHeight,
                cameraScaledPixelWidth, cameraScaledPixelHeight;
            public bool mainLightShadows, softShadows;
            public string model = SystemInfo.deviceModel, os = SystemInfo.operatingSystem, unity = UnityEngine.Application.unityVersion,
                version = UnityEngine.Application.version, graphics = SystemInfo.graphicsDeviceName, api = SystemInfo.graphicsDeviceType.ToString();
            public int width = Screen.width, height = Screen.height, targetFrameRate = UnityEngine.Application.targetFrameRate,
                vsync = QualitySettings.vSyncCount, quality = QualitySettings.GetQualityLevel(), aa = QualitySettings.antiAliasing;
            public double refreshRate = Screen.currentResolution.refreshRateRatio.value;
            public Rect safeArea = Screen.safeArea;
            public bool development = Debug.isDebugBuild, frameTimingEnabled = FrameTimingManager.IsFeatureEnabled();
        }
    }
}
