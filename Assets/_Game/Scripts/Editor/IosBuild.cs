using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Veilward.Presentation;

namespace Veilward.Editor
{
    public static class IosBuild
    {
        private static readonly string[] Scenes =
        {
            "Assets/_Game/Scenes/Bootstrap.unity",
            "Assets/_Game/Scenes/MainMenu.unity",
            "Assets/_Game/Scenes/Battle.unity"
        };

        public static void ValidateVersion(string version, string buildNumber)
        {
            if (version == null || !Regex.IsMatch(version, @"\A[0-9]{1,4}\.[0-9]{1,2}\.[0-9]{1,2}\z") ||
                buildNumber == null || !Regex.IsMatch(buildNumber, @"\A[1-9][0-9]{0,3}\z"))
                throw new ArgumentException("Explicit version (major.minor.patch) and build number (1..9999) required.");
        }

        public static void ValidateProject()
        {
            if (UnityEngine.Application.unityVersion != "6000.3.28f1")
                throw new InvalidOperationException("Unity 6000.3.28f1 required.");
            if (!EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).SequenceEqual(Scenes))
                throw new InvalidOperationException("Enabled scenes must be Bootstrap, MainMenu, Battle in that order.");
            var config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWaveAbilityBossV2.asset");
            if (config == null || config.ScenarioContentId != "eight-wave-ability-boss-v2")
                throw new InvalidOperationException("Production content v2 required.");
            config.CreateSimulation();
            var errors = BattleAssetValidator.ValidateSavedMainMenu().Concat(BattleAssetValidator.ValidateSavedBattle()).ToArray();
            if (errors.Length > 0) throw new InvalidOperationException(string.Join("\n", errors));
            if (string.IsNullOrWhiteSpace(PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS)))
                throw new InvalidOperationException("Existing iOS application identifier required.");
        }

        public static string[] BuildScenes(bool artReference) => artReference
            ? new[] { ArtReferenceBuilder.ScenePath } : (string[])Scenes.Clone();

        public static BattleScenarioConfig ValidateArtReference()
        {
            var scene = SceneManager.GetSceneByPath(ArtReferenceBuilder.ScenePath);
            var opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ArtReferenceBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                var showcases = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ArtReferenceShowcase>(true)).ToArray();
                if (showcases.Length != 1) throw new InvalidOperationException("Exactly one saved ArtReference showcase required.");
                var config = new SerializedObject(showcases[0]).FindProperty("scenario").objectReferenceValue as BattleScenarioConfig;
                if (config == null || AssetDatabase.GetAssetPath(config) != "Assets/_Game/Configs/TestWave.asset" ||
                    config.ScenarioContentId != "legacy-single-wave-v1")
                    throw new InvalidOperationException("ArtReference must use the actual saved TestWave scenario.");
                config.CreateSimulation();
                return config;
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        public static void Export() => ExportProfile(false);
        public static void ExportArtReference() => ExportProfile(true);

        private static void ExportProfile(bool artReference)
        {
            var exitCode = 1;
            try
            {
                var version = Environment.GetEnvironmentVariable("VEILWARD_IOS_VERSION");
                var number = Environment.GetEnvironmentVariable("VEILWARD_IOS_BUILD");
                var output = Environment.GetEnvironmentVariable("VEILWARD_IOS_OUTPUT");
                var source = Environment.GetEnvironmentVariable("VEILWARD_SOURCE_SHA");
                ValidateVersion(version, number);
                if (string.IsNullOrEmpty(output) || string.IsNullOrEmpty(source) || !Regex.IsMatch(source, @"\A[0-9a-f]{40}\z"))
                    throw new ArgumentException("Use Scripts/export-ios.py for output/source provenance.");
                ValidateProject();
                if (artReference) ValidateArtReference();
                if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
                    throw new InvalidOperationException("iOS Build Support is required.");
                Build(output, version, number, source, artReference);
                exitCode = 0;
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
            EditorApplication.Exit(exitCode);
        }

        private static void Build(string output, string version, string number, string source, bool artReference)
        {
            var target = NamedBuildTarget.iOS;
            var oldVersion = PlayerSettings.bundleVersion;
            var oldNumber = PlayerSettings.iOS.buildNumber;
            var oldBackend = PlayerSettings.GetScriptingBackend(target);
            var oldArchitecture = PlayerSettings.GetArchitecture(target);
            var oldApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.iOS);
            var oldAutomatic = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.iOS);
            var oldSdk = PlayerSettings.iOS.sdkVersion;
            var oldTiming = PlayerSettings.enableFrameTimingStats;
            try
            {
                if (artReference) PlayerSettings.enableFrameTimingStats = true;
                PlayerSettings.bundleVersion = version;
                PlayerSettings.iOS.buildNumber = number;
                PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
                PlayerSettings.SetArchitecture(target, 1); // Unity iOS: ARM64.
                PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { GraphicsDeviceType.Metal });
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = BuildScenes(artReference),
                    locationPathName = output,
                    target = BuildTarget.iOS,
                    options = BuildOptions.StrictMode
                });
                if (report.summary.result != BuildResult.Succeeded || report.summary.totalErrors != 0)
                    throw new InvalidOperationException($"iOS export failed: {report.summary.result}; errors={report.summary.totalErrors}");
                var receipt = JsonUtility.ToJson(new Receipt
                {
                    sourceSHA = source, version = version, buildNumber = number,
                    contentId = artReference ? ValidateArtReference().ScenarioContentId : "eight-wave-ability-boss-v2",
                    profile = artReference ? "art-reference" : "production",
                    purpose = artReference ? "ART.2b device microbenchmark" : "QA.1 production smoke",
                    scenes = BuildScenes(artReference),
                    scenarioPath = artReference ? "Assets/_Game/Configs/TestWave.asset" : "Assets/_Game/Configs/EightWaveAbilityBossV2.asset",
                    unityVersion = UnityEngine.Application.unityVersion,
                    warnings = report.summary.totalWarnings
                }, true);
                File.WriteAllText(Path.Combine(output, "veilward-build.json"), receipt);
                if (artReference)
                {
                    var raw = Path.Combine(output, "Data/Raw");
                    Directory.CreateDirectory(raw);
                    File.WriteAllText(Path.Combine(raw, "art2b-build.json"), receipt);
                }
            }
            finally
            {
                PlayerSettings.enableFrameTimingStats = oldTiming;
                PlayerSettings.bundleVersion = oldVersion;
                PlayerSettings.iOS.buildNumber = oldNumber;
                PlayerSettings.SetScriptingBackend(target, oldBackend);
                PlayerSettings.SetArchitecture(target, oldArchitecture);
                PlayerSettings.iOS.sdkVersion = oldSdk;
                PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, oldApis);
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, oldAutomatic);
            }
        }

        [Serializable]
        private sealed class Receipt
        {
            public string sourceSHA, version, buildNumber, contentId, unityVersion, profile, purpose, scenarioPath;
            public string[] scenes;
            public int warnings;
        }
    }
}
