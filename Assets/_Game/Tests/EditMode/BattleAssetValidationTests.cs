using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Veilward.Editor;
using Veilward.Presentation;

namespace Veilward.Tests.EditMode
{
    public sealed class BattleAssetValidationTests
    {
        private const string BattleScenePath = "Assets/_Game/Scenes/Battle.unity";

        [Test]
        public void SavedBattleScene_PauseOverlayAndReferences_AreValid()
        {
            EditorSceneManager.OpenScene(BattleScenePath);
            var pause = UnityEngine.Object.FindFirstObjectByType<BattlePauseController>(FindObjectsInactive.Include);
            var battle = UnityEngine.Object.FindFirstObjectByType<BattleSceneController>();
            var rects = UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var overlay = rects.SingleOrDefault(rect => rect.name == "Pause Overlay");
            var pauseRect = rects.SingleOrDefault(rect => rect.name == "Pause Button");
            var resumeRect = rects.SingleOrDefault(rect => rect.name == "Resume Button");

            Assert.That(pause, Is.Not.Null);
            Assert.That(overlay, Is.Not.Null);
            Assert.That(pauseRect, Is.Not.Null);
            Assert.That(resumeRect, Is.Not.Null);
            var overlayImage = overlay.GetComponent<UnityEngine.UI.Image>();
            Assert.That(overlayImage, Is.Not.Null);
            Assert.That(overlayImage.raycastTarget, Is.True);
            Assert.That(overlay.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(overlay.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(overlay.offsetMin, Is.EqualTo(Vector2.zero));
            Assert.That(overlay.offsetMax, Is.EqualTo(Vector2.zero));
            Assert.That(overlay.GetSiblingIndex(), Is.EqualTo(overlay.parent.childCount - 1));
            Assert.That(overlay.gameObject.activeSelf, Is.False);

            var serialized = new SerializedObject(pause);
            foreach (var propertyName in new[] { "battle", "placement", "pauseOverlay", "pauseButton", "resumeButton" })
            {
                Assert.That(serialized.FindProperty(propertyName).objectReferenceValue, Is.Not.Null, propertyName);
            }

            Assert.That(serialized.FindProperty("battle").objectReferenceValue, Is.EqualTo(battle));
            Assert.That(serialized.FindProperty("pauseOverlay").objectReferenceValue, Is.EqualTo(overlay.gameObject));
            Assert.That(serialized.FindProperty("pauseButton").objectReferenceValue,
                Is.EqualTo(pauseRect.GetComponent<UnityEngine.UI.Button>()));
            Assert.That(serialized.FindProperty("resumeButton").objectReferenceValue,
                Is.EqualTo(resumeRect.GetComponent<UnityEngine.UI.Button>()));
            var serializedBattle = new SerializedObject(battle);
            Assert.That(serializedBattle.FindProperty("pauseController").objectReferenceValue, Is.EqualTo(pause));
            var checkpoint = UnityEngine.Object.FindFirstObjectByType<BattleCheckpointController>();
            var placement = UnityEngine.Object.FindFirstObjectByType<BattlePlacementController>();
            Assert.That(checkpoint, Is.Not.Null);
            Assert.That(serializedBattle.FindProperty("checkpointController").objectReferenceValue, Is.EqualTo(checkpoint));
            Assert.That(serializedBattle.FindProperty("placementController").objectReferenceValue, Is.EqualTo(placement));
            Assert.That(new SerializedObject(checkpoint).FindProperty("saveStatusText").objectReferenceValue, Is.Not.Null);
            Assert.That(BattleAssetValidator.ValidateSavedBattle(), Is.Empty);
        }

        [Test]
        public void SavedMainMenu_NewGameContinueAndCanvas_AreValid()
        {
            EditorSceneManager.OpenScene("Assets/_Game/Scenes/MainMenu.unity");
            var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
            Assert.That(menu, Is.Not.Null);
            var serialized = new SerializedObject(menu);
            foreach (var propertyName in new[] { "scenario", "newGameButton", "continueButton", "statusText" })
            {
                Assert.That(serialized.FindProperty(propertyName).objectReferenceValue, Is.Not.Null, propertyName);
            }

            Assert.That(UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(1));
            var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>(), Is.Not.Null);
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            Assert.That(scaler, Is.Not.Null);
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));
            Assert.That(BattleAssetValidator.ValidateSavedMainMenu(), Is.Empty);
        }

        [Test]
        public void SavedBattleScene_ConfigReferencesPrefabsAndMaterials_AreValid()
        {
            var errors = BattleAssetValidator.ValidateSavedBattle();

            Assert.That(errors, Is.Empty, string.Join(Environment.NewLine, errors));
        }

        [Test]
        public void SavedBattleScene_TowerStatsRect_FitsAllEconomyLines()
        {
            EditorSceneManager.OpenScene(BattleScenePath);
            var stats = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Text>(
                    UnityEngine.FindObjectsInactive.Include,
                    UnityEngine.FindObjectsSortMode.None)
                .Single(text => text.name == "Tower Stats");
            var measurementScene = EditorSceneManager.NewPreviewScene();
            UnityEngine.GameObject temporary = null;
            try
            {
                temporary = (UnityEngine.GameObject)UnityEngine.Object.Instantiate(stats.gameObject, measurementScene);
                var copy = temporary.GetComponent<UnityEngine.UI.Text>();
                copy.text = "Level: 3\nDamage: 50\nRange: 3\nFireRate: 2/s\nUpgrade: MAX\nSell refund: 133 gold";

                Assert.That(
                    stats.rectTransform.rect.height,
                    Is.GreaterThanOrEqualTo(copy.preferredHeight),
                    "Tower Stats must display every economy line without clipping.");
            }
            finally
            {
                if (temporary != null)
                {
                    UnityEngine.Object.DestroyImmediate(temporary);
                }

                EditorSceneManager.ClosePreviewScene(measurementScene);
            }
        }

        [Test]
        public void SavedBattleScene_TowerStatsMeasurement_DoesNotMutateScene()
        {
            UnityEngine.UI.Text FindStats() => UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Text>(
                    UnityEngine.FindObjectsInactive.Include,
                    UnityEngine.FindObjectsSortMode.None)
                .Single(text => text.name == "Tower Stats");

            var scene = EditorSceneManager.OpenScene(BattleScenePath);
            Assert.That(scene.isDirty, Is.False, "The measurement test requires a clean Battle scene.");
            var originalText = FindStats().text;
            try
            {
                SavedBattleScene_TowerStatsRect_FitsAllEconomyLines();

                Assert.That(FindStats().text, Is.EqualTo(originalText));
                Assert.That(EditorSceneManager.GetActiveScene().isDirty, Is.False);
            }
            finally
            {
                FindStats().text = originalText;
                if (EditorSceneManager.GetActiveScene().isDirty)
                {
                    EditorSceneManager.OpenScene(BattleScenePath);
                }
            }
        }

        [Test]
        public void BattleBuilder_TwoConsecutiveBuilds_ProduceIdenticalContentAndRestoreOriginals()
        {
            var originals = Snapshot(OutputPaths());
            try
            {
                BattleSceneBuilder.Build();
                var first = Snapshot(OutputPaths());
                BattleSceneBuilder.Build();
                var second = Snapshot(OutputPaths());

                CollectionAssert.AreEqual(first.Keys, second.Keys);
                foreach (var path in first.Keys)
                {
                    if (path == BattleScenePath || path == "Assets/_Game/Scenes/MainMenu.unity")
                    {
                        AssertTextEqual(CanonicalScene(first[path]), CanonicalScene(second[path]), path);
                    }
                    else
                    {
                        AssertYamlBytesEqual(first[path], second[path], path);
                    }
                }
            }
            finally
            {
                Restore(originals);
                AssetDatabase.Refresh();
                EditorSceneManager.OpenScene(BattleScenePath);
            }

            var restored = Snapshot(OutputPaths());
            CollectionAssert.AreEqual(originals.Keys, restored.Keys);
            foreach (var path in originals.Keys)
            {
                CollectionAssert.AreEqual(originals[path], restored[path], $"Original was not restored: {path}");
            }
        }

        [Test]
        public void RegBattle090_InvalidSavedElementalDefinition_ReturnsValidationError()
        {
            const string configPath = "Assets/_Game/Configs/TestWave.asset";
            var original = File.ReadAllBytes(configPath);
            EditorSceneManager.OpenScene(BattleScenePath);
            try
            {
                var config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>(configPath);
                var serialized = new SerializedObject(config);
                serialized.FindProperty("towerArchetypes").GetArrayElementAtIndex(1)
                    .FindPropertyRelative("levels").GetArrayElementAtIndex(0)
                    .FindPropertyRelative("statusEffect.power").intValue = 101;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();

                var errors = BattleAssetValidator.ValidateSavedBattle();

                Assert.That(errors, Has.Some.Contains("Battle scenario cannot create a simulation"));
                Assert.That(errors, Has.Some.Contains("Wet power"));
            }
            finally
            {
                File.WriteAllBytes(configPath, original);
                AssetDatabase.ImportAsset(configPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.Refresh();
                EditorSceneManager.OpenScene(BattleScenePath);
            }
        }

        private static string CanonicalScene(byte[] content)
        {
            // Unity assigns new local fileIDs and may reorder YAML documents on every save.
            // Derive stable tokens from object content and several reference-refinement passes,
            // then sort documents so equivalent scenes compare independently of declaration order.
            var text = System.Text.Encoding.UTF8.GetString(content);
            var documents = System.Text.RegularExpressions.Regex.Matches(
                text,
                @"(?ms)^--- !u!(\d+) &(-?\d+)([^\r\n]*)\r?\n(.*?)(?=^--- !u!|\z)");
            var documentIndices = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var index = 0; index < documents.Count; index++)
            {
                documentIndices.Add(documents[index].Groups[2].Value, index);
            }

            string ReplaceLocalIds(string source, Func<string, string> replacement)
            {
                var lines = source.Split('\n');
                for (var index = 0; index < lines.Length; index++)
                {
                    if (lines[index].Contains("guid:"))
                    {
                        continue;
                    }

                    lines[index] = System.Text.RegularExpressions.Regex.Replace(
                        lines[index],
                        @"fileID: (-?\d+)",
                        match => documentIndices.ContainsKey(match.Groups[1].Value)
                            ? "fileID: " + replacement(match.Groups[1].Value)
                            : match.Value);
                }

                return string.Join("\n", lines).TrimEnd('\r', '\n');
            }

            var identifiers = new string[documents.Count];
            for (var index = 0; index < documents.Count; index++)
            {
                identifiers[index] = documents[index].Groups[1].Value;
            }

            using (var hash = System.Security.Cryptography.SHA256.Create())
            {
                for (var iteration = 0; iteration < 8; iteration++)
                {
                    var refined = new string[documents.Count];
                    for (var index = 0; index < documents.Count; index++)
                    {
                        var document = documents[index];
                        var canonicalBody = ReplaceLocalIds(
                            document.Groups[4].Value,
                            oldId => identifiers[documentIndices[oldId]]);
                        var fingerprint = document.Groups[1].Value + "\n" + canonicalBody;
                        refined[index] = BitConverter.ToString(hash.ComputeHash(
                            System.Text.Encoding.UTF8.GetBytes(fingerprint))).Replace("-", string.Empty);
                    }

                    identifiers = refined;
                }
            }

            var canonicalDocuments = new string[documents.Count];
            for (var index = 0; index < documents.Count; index++)
            {
                var document = documents[index];
                var stableId = "L" + identifiers[index].Substring(0, 24);
                var canonicalBody = ReplaceLocalIds(
                    document.Groups[4].Value,
                    oldId => "L" + identifiers[documentIndices[oldId]].Substring(0, 24));
                canonicalDocuments[index] =
                    $"--- !u!{document.Groups[1].Value} &{stableId}{document.Groups[3].Value}\n{canonicalBody}";
            }

            Array.Sort(canonicalDocuments, StringComparer.Ordinal);
            return "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n" + string.Join("\n", canonicalDocuments);
        }

        private static void AssertYamlBytesEqual(byte[] expected, byte[] actual, string path)
        {
            if (expected.SequenceEqual(actual))
            {
                return;
            }

            // Unity can emit trailing spaces for empty YAML values after an in-memory save.
            // Compare semantic YAML content while the restoration check below remains byte-exact.
            string CanonicalYaml(byte[] content) => string.Join("\n",
                System.Text.Encoding.UTF8.GetString(content)
                    .Split('\n')
                    .Select(line => line.TrimEnd(' ', '\t', '\r')));

            AssertTextEqual(CanonicalYaml(expected), CanonicalYaml(actual), path);
        }

        private static void AssertTextEqual(string expected, string actual, string path)
        {
            if (expected == actual)
            {
                return;
            }

            var expectedLines = expected.Split('\n');
            var actualLines = actual.Split('\n');
            for (var index = 0; index < Math.Min(expectedLines.Length, actualLines.Length); index++)
            {
                if (expectedLines[index] != actualLines[index])
                {
                    Assert.Fail(
                        $"{path}, canonical line {index + 1}:\nExpected: {expectedLines[index]}\nActual:   {actualLines[index]}");
                }
            }

            Assert.Fail($"{path}: text lengths differ ({expected.Length} vs {actual.Length}).");
        }

        private static IReadOnlyList<string> OutputPaths()
        {
            var assets = new List<string>
            {
                BattleScenePath,
                "Assets/_Game/Scenes/MainMenu.unity",
                "Assets/_Game/Configs/TestWave.asset",
                "Assets/_Game/Configs/EightWave.asset",
                "Assets/_Game/Prefabs/Shade.prefab",
                "Assets/_Game/Prefabs/TestTower.prefab"
            };
            assets.AddRange(Directory.GetFiles("Assets/_Game/Art/Materials", "*.mat").OrderBy(path => path, StringComparer.Ordinal));
            return assets.SelectMany(path => new[] { path, path + ".meta" }).OrderBy(path => path, StringComparer.Ordinal).ToArray();
        }

        private static Dictionary<string, byte[]> Snapshot(IEnumerable<string> paths)
        {
            var snapshots = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var path in paths)
            {
                Assert.That(File.Exists(path), Is.True, $"Required generated output is missing: {path}");
                snapshots.Add(path, File.ReadAllBytes(path));
            }

            return snapshots;
        }

        private static void Restore(IEnumerable<KeyValuePair<string, byte[]>> snapshots)
        {
            foreach (var snapshot in snapshots)
            {
                File.WriteAllBytes(snapshot.Key, snapshot.Value);
            }
        }
    }
}
