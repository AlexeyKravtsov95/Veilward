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
            // Unity assigns new local fileIDs when objects are recreated. Canonicalize only
            // those IDs and their local references; external GUID references remain exact.
            var text = System.Text.Encoding.UTF8.GetString(content);
            var identifiers = new Dictionary<string, string>(StringComparer.Ordinal);
            var nextIdentifier = 0;
            var canonical = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"(?m)^--- !u!(\d+) &(-?\d+)(.*)$",
                match =>
                {
                    var oldId = match.Groups[2].Value;
                    if (!identifiers.TryGetValue(oldId, out var stableId))
                    {
                        stableId = "L" + nextIdentifier++.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
                        identifiers.Add(oldId, stableId);
                    }

                    return $"--- !u!{match.Groups[1].Value} &{stableId}{match.Groups[3].Value}";
                });
            var lines = canonical.Split('\n');
            for (var index = 0; index < lines.Length; index++)
            {
                if (lines[index].Contains("guid:"))
                {
                    continue;
                }

                lines[index] = System.Text.RegularExpressions.Regex.Replace(
                    lines[index],
                    @"fileID: (-?\d+)",
                    match => identifiers.TryGetValue(match.Groups[1].Value, out var stableId)
                        ? "fileID: " + stableId
                        : match.Value);
            }

            return string.Join("\n", lines);
        }

        private static void AssertYamlBytesEqual(byte[] expected, byte[] actual, string path)
        {
            if (!expected.SequenceEqual(actual))
            {
                AssertTextEqual(
                    System.Text.Encoding.UTF8.GetString(expected),
                    System.Text.Encoding.UTF8.GetString(actual),
                    path);
            }
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
