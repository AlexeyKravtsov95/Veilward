using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Veilward.Tests.EditMode
{
    public sealed class ProjectBootstrapTests
    {
        private static readonly string[] ScenePaths =
        {
            "Assets/_Game/Scenes/Bootstrap.unity",
            "Assets/_Game/Scenes/MainMenu.unity",
            "Assets/_Game/Scenes/Battle.unity"
        };

        [Test]
        public void GameScenes_ArePresentAndEnabledInLaunchOrder()
        {
            var enabledScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            CollectionAssert.AreEqual(ScenePaths, enabledScenes);

            foreach (var scenePath in ScenePaths)
            {
                Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath), Is.Not.Null, scenePath);
            }
        }

        [Test]
        public void SavedIosSettings_UseLandscapeIl2CppArm64AndMinimumIos17()
        {
            Assert.That(PlayerSettings.GetScriptingBackend(NamedBuildTarget.iOS), Is.EqualTo(ScriptingImplementation.IL2CPP));
            Assert.That(PlayerSettings.GetArchitecture(NamedBuildTarget.iOS), Is.EqualTo(1));
            Assert.That(PlayerSettings.iOS.targetOSVersionString, Is.EqualTo("17.0"));
            Assert.That(PlayerSettings.defaultInterfaceOrientation, Is.EqualTo(UIOrientation.LandscapeLeft));
            Assert.That(PlayerSettings.allowedAutorotateToPortrait, Is.False);
            Assert.That(PlayerSettings.allowedAutorotateToPortraitUpsideDown, Is.False);
            Assert.That(PlayerSettings.allowedAutorotateToLandscapeLeft, Is.True);
            Assert.That(PlayerSettings.allowedAutorotateToLandscapeRight, Is.True);
        }

        [Test]
        public void EditorSettings_UseForceTextAndVisibleMetaFiles()
        {
            Assert.That(EditorSettings.serializationMode, Is.EqualTo(SerializationMode.ForceText));
            var versionControlSettings = File.ReadAllText("ProjectSettings/VersionControlSettings.asset");
            Assert.That(versionControlSettings, Does.Contain("m_Mode: Visible Meta Files"));
        }
    }
}
