using System;
using System.Linq;
using NUnit.Framework;
using Veilward.Editor;

namespace Veilward.Tests.EditMode
{
    public sealed class IosBuildTests
    {
        [Test]
        public void SavedProjectHasProductionBuildContract() => IosBuild.ValidateProject();

        [Test]
        public void ArtProfilePreservesProductionAndReportsActualSavedScenario()
        {
            IosBuild.ValidateProject();
            var before = UnityEditor.EditorBuildSettings.scenes.Select(s => s.path + ":" + s.enabled).ToArray();
            Assert.That(IosBuild.ValidateArtReference().ScenarioContentId, Is.EqualTo("legacy-single-wave-v1"));
            CollectionAssert.AreEqual(new[] { "Assets/_Game/Scenes/ArtReference.unity" }, IosBuild.BuildScenes(true));
            CollectionAssert.AreEqual(new[] { "Assets/_Game/Scenes/Bootstrap.unity", "Assets/_Game/Scenes/MainMenu.unity", "Assets/_Game/Scenes/Battle.unity" }, IosBuild.BuildScenes(false));
            CollectionAssert.AreEqual(before, UnityEditor.EditorBuildSettings.scenes.Select(s => s.path + ":" + s.enabled).ToArray());
            IosBuild.ValidateProject();
        }

        [Test]
        public void Art3A_BuildSceneModelReferencesAreIsolatedAndIdempotent()
        {
            const string path = "Assets/_Game/Scenes/ArtReference.unity";
            var before = System.IO.File.ReadAllBytes(path);
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            try
            {
                scene.name = "ArtReference";
                var root = new UnityEngine.GameObject("Build-only showcase");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                root.AddComponent<Veilward.Presentation.ArtReferenceShowcase>();
                FlameArtProfileSceneProcessor.Configure(scene);
                FlameArtProfileSceneProcessor.Configure(scene);
                var probes = root.GetComponents<Veilward.Presentation.ArtDeviceProbe>();
                Assert.That(probes.Length, Is.EqualTo(1));
                var references = new UnityEditor.SerializedObject(probes[0]).FindProperty("flameLevelPrefabs");
                Assert.That(references.arraySize, Is.EqualTo(3));
                for (var i = 0; i < 3; i++)
                    Assert.That(UnityEditor.AssetDatabase.GetAssetPath(references.GetArrayElementAtIndex(i).objectReferenceValue),
                        Is.EqualTo(FlameModelBuilder.PrefabPath(i + 1)));
                scene.name = "Battle";
                Assert.Throws<InvalidOperationException>(() => FlameArtProfileSceneProcessor.Configure(scene));
                CollectionAssert.AreEqual(before, System.IO.File.ReadAllBytes(path));
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
        }

        [TestCase("0.1.0", "42")]
        [TestCase("9999.99.99", "9999")]
        public void AcceptsExplicitVersion(string version, string number) => IosBuild.ValidateVersion(version, number);

        [TestCase(null, "42")]
        [TestCase("1.0", "42")]
        [TestCase("1.0.0\n", "42")]
        [TestCase("1.0.0", "0")]
        [TestCase("1.0.0", "01")]
        [TestCase("1.0.0", "10000")]
        [TestCase("1.0.0", null)]
        public void RejectsInvalidVersion(string version, string number) =>
            Assert.Throws<ArgumentException>(() => IosBuild.ValidateVersion(version, number));
    }
}
