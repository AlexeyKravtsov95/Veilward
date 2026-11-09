using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using Veilward.Editor;

namespace Veilward.Tests.EditMode
{
    public sealed class BattleAssetValidationTests
    {
        private const string BattleScenePath = "Assets/_Game/Scenes/Battle.unity";

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
                    if (path == BattleScenePath)
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
