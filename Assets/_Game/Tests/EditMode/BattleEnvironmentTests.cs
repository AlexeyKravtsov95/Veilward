using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Veilward.Domain;
using Veilward.Presentation;
using Veilward.Tests.Shared;

namespace Veilward.Tests.EditMode
{
    public sealed class BattleEnvironmentTests
    {
        [Serializable] private sealed class CellBaseline
        { public int x = 0, y = 0; public string state = ""; public float[] center = null, screen = null; }
        [Serializable] private sealed class CameraBaseline
        { public float[] position = null, rotation = null; public float orthographicSize = 0, nearClipPlane = 0, farClipPlane = 0, aspect = 0; public bool orthographic = false; }
        [Serializable] private sealed class GeometryBaseline
        { public int Width = 0, Height = 0; public int[] origin = null; public float CellSize = 0, distance = 0; public CellBaseline[] cells = null; public CameraBaseline camera = null; }

        [Test]
        public void Art401_AllProductionCellsAndCameraMatchPreArtBaseline()
        {
            EditorSceneManager.OpenScene("Assets/_Game/Scenes/Battle.unity");
            var baseline = JsonUtility.FromJson<GeometryBaseline>(File.ReadAllText("Docs/review/ART.4/reports/before-geometry.json"));
            var config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWaveAbilityBossV2.asset");
            var simulation = config.CreateSimulation(); var map = simulation.Grid.Map; var camera = Camera.main;
            Assert.That(map.Width, Is.EqualTo(baseline.Width)); Assert.That(map.Height, Is.EqualTo(baseline.Height));
            Assert.That(map.Origin, Is.EqualTo(new GridCoordinate(baseline.origin[0], baseline.origin[1])));
            Assert.That(map.CellSize, Is.EqualTo(baseline.CellSize));
            Assert.That(simulation.Path.TotalLength, Is.EqualTo(baseline.distance));
            Assert.That(baseline.cells.Length, Is.EqualTo(map.Width * map.Height));
            foreach (var expected in baseline.cells)
            {
                var cell = new GridCoordinate(expected.x, expected.y); var center = map.GetCellCenter(cell);
                Assert.That(simulation.Grid.GetState(cell).ToString(), Is.EqualTo(expected.state));
                Assert.That(new[] { center.X, center.Y }, Is.EqualTo(expected.center));
                var screen = camera.WorldToViewportPoint(new Vector3(center.X, 0, center.Y));
                Assert.That(screen.x, Is.EqualTo(expected.screen[0] / 1600).Within(.000001));
                Assert.That(screen.y, Is.EqualTo(expected.screen[1] / 900).Within(.000001));
                Assert.That(screen.z, Is.EqualTo(expected.screen[2]).Within(.00001));
            }
            Assert.That(camera.transform.position, Is.EqualTo(new Vector3(baseline.camera.position[0], baseline.camera.position[1], baseline.camera.position[2])));
            var q = camera.transform.rotation; var expectedRotation = baseline.camera.rotation;
            Assert.That(Quaternion.Angle(q, new Quaternion(expectedRotation[0], expectedRotation[1], expectedRotation[2], expectedRotation[3])), Is.LessThan(.001));
            Assert.That(camera.orthographic, Is.EqualTo(baseline.camera.orthographic));
            Assert.That(camera.orthographicSize, Is.EqualTo(baseline.camera.orthographicSize));
            Assert.That(camera.nearClipPlane, Is.EqualTo(baseline.camera.nearClipPlane));
            Assert.That(camera.farClipPlane, Is.EqualTo(baseline.camera.farClipPlane));
        }

        [TestCase("flame")] [TestCase("gale")]
        public void Art406_ProductionFullEventsAndVictoryMatchPreArtReplay(string start)
        {
            var config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWaveAbilityBossV2.asset");
            var run = new AbilityBossTrace.Run(config.CreateSimulation(), AbilityBossTrace.Load(start)); run.Play();
            var expected = JsonUtility.FromJson<AbilityBossTrace.Report>(File.ReadAllText("Docs/review/ART.4/reports/before-" + start + ".json"));
            var bytes = System.Text.Encoding.UTF8.GetBytes(string.Join("\n", run.trace) + "\n");
            Assert.That(AbilityBossTrace.Hash(bytes), Is.EqualTo(expected.traceSha256), "whole snapshots/Grid/statuses/commands/attack/reaction/damage/ability events");
            Assert.That(run.sim.State.Phase, Is.EqualTo(BattlePhase.Victory));
            Assert.That(run.report.waves.Count, Is.EqualTo(8));
            Assert.That(run.report.bossKills, Is.EqualTo(1));
        }

        [Test]
        public void Art401_ProductionEnvironmentCoversExactMapWithoutCollidersOrGraybox()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Game/Scenes/Battle.unity");
            var roots = scene.GetRootGameObjects();
            var environment = roots.SingleOrDefault(g => g.name == "Sanctuary Environment ART4");
            Assert.That(environment, Is.Not.Null, "Production Battle needs the authored environment");
            Assert.That(environment.transform.position, Is.EqualTo(Vector3.zero));
            Assert.That(environment.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(environment.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(environment.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
            Assert.That(roots.Any(g => new[] { "Ground", "Build Grid", "Path Geometry" }.Contains(g.name)), Is.False);
            var config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWaveAbilityBossV2.asset");
            var simulation = config.CreateSimulation(); var map = simulation.Grid.Map;
            var sockets = environment.transform.Find("Build Sockets");
            var available = 0;
            for (var y = map.Origin.Y; y < map.Origin.Y + map.Height; y++)
            for (var x = map.Origin.X; x < map.Origin.X + map.Width; x++)
            {
                var cell = new GridCoordinate(x, y); var socket = sockets.Find($"Socket_{x}_{y}");
                if (simulation.Grid.GetState(cell) == GridCellState.Blocked) { Assert.That(socket, Is.Null); continue; }
                available++; Assert.That(socket, Is.Not.Null);
                var center = map.GetCellCenter(cell);
                Assert.That(socket.position, Is.EqualTo(new Vector3(center.X, 0, center.Y)));
                var bounds = socket.GetComponentInChildren<Renderer>().bounds;
                Assert.That(bounds.size.x, Is.LessThanOrEqualTo(map.CellSize));
                Assert.That(bounds.size.z, Is.LessThanOrEqualTo(map.CellSize));
                Assert.That(bounds.max.y, Is.LessThanOrEqualTo(.025f));
            }
            Assert.That(sockets.childCount, Is.EqualTo(available));
            var camera = roots.SelectMany(g => g.GetComponentsInChildren<Camera>()).Single();
            Assert.That(camera.transform.position, Is.EqualTo(new Vector3(8, 11, 8)));
            Assert.That(camera.orthographicSize, Is.EqualTo(8.5f));
            Assert.That(Veilward.Editor.BattleAssetValidator.ValidateSavedBattle(), Is.Empty);
        }

        [Test]
        public void Art403_EnvironmentBudgetsSharedMaterialsPeripheralClearanceAndRouteContinuity()
        {
            EditorSceneManager.OpenScene("Assets/_Game/Scenes/Battle.unity");
            var root = GameObject.Find("Sanctuary Environment ART4").transform;
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            Assert.That(renderers.Length, Is.InRange(1, 160));
            var triangles = root.GetComponentsInChildren<MeshFilter>(true).Sum(f => f.sharedMesh.triangles.Length / 3);
            Assert.That(triangles, Is.InRange(1, 25000), "includes deliberate stencil duplicate passes");
            foreach (var material in renderers.SelectMany(r => r.sharedMaterials).Distinct())
                Assert.That(AssetDatabase.GetAssetPath(material), Does.StartWith("Assets/_Game/Art/Materials/ART1/"));
            foreach (var renderer in root.Find("Peripheral Sanctuary Garden").GetComponentsInChildren<Renderer>())
            {
                var b = renderer.bounds;
                Assert.That(b.max.x <= -6.5f || b.min.x >= 5.5f || b.max.z <= -4.5f || b.min.z >= 3.5f,
                    Is.True, "Decor footprint intersects a playable cell: " + renderer.name);
            }
            var simulation = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWaveAbilityBossV2.asset").CreateSimulation();
            var pavers = root.Find("Sanctuary Route").GetComponentsInChildren<MeshRenderer>();
            for (var d = 0f; d <= simulation.Path.TotalLength; d += .02f)
            {
                var p = simulation.Path.GetPosition(d); var point = new Vector3(p.X, .02f, p.Y);
                Assert.That(pavers.Any(r => { var b = r.bounds; b.Expand(.06f); return b.Contains(point); }), Is.True,
                    "Route discontinuity beyond the authored <=.06m grout at distance " + d);
            }
        }

        [Test]
        public void Art402_RepeatedKitImportKeepsGuidLocalIdsAndPrefabMaterialReferences()
        {
            var names = Veilward.Editor.BattleEnvironmentBuilder.ModelNames;
            var paths = names.SelectMany(name => new[] {
                Veilward.Editor.BattleEnvironmentBuilder.ModelFolder + name + ".fbx.meta",
                Veilward.Editor.BattleEnvironmentBuilder.PrefabPath(name),
                Veilward.Editor.BattleEnvironmentBuilder.PrefabPath(name) + ".meta" }).ToArray();
            var saved = paths.ToDictionary(path => path, File.ReadAllBytes);
            string[] References() => names.SelectMany(name => AssetDatabase.LoadAllAssetsAtPath(
                Veilward.Editor.BattleEnvironmentBuilder.ModelFolder + name + ".fbx")).Select(asset => {
                    Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long id), Is.True);
                    return asset.name + ":" + guid + ":" + id;
                }).OrderBy(s => s).ToArray();
            var references = References();
            try
            {
                Veilward.Editor.BattleEnvironmentBuilder.Import();
                Veilward.Editor.BattleEnvironmentBuilder.Import();
                CollectionAssert.AreEqual(references, References());
                foreach (var pair in saved) CollectionAssert.AreEqual(pair.Value, File.ReadAllBytes(pair.Key), pair.Key);
                var scene = EditorSceneManager.OpenScene("Assets/_Game/Scenes/Battle.unity");
                Assert.That(scene.GetRootGameObjects().Count(g => g.name == "Sanctuary Environment ART4"), Is.EqualTo(1));
                Assert.That(Veilward.Editor.BattleAssetValidator.ValidateSavedBattle(), Is.Empty);
            }
            finally
            {
                foreach (var pair in saved) if (!pair.Value.SequenceEqual(File.ReadAllBytes(pair.Key))) File.WriteAllBytes(pair.Key, pair.Value);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }

        [TestCase("Environment_SanctuaryIsland", 5000)]
        [TestCase("Environment_SanctuaryPath", 400)]
        [TestCase("Environment_BuildSocket", 400)]
        [TestCase("Environment_SpawnGate", 2000)]
        [TestCase("Environment_SealHeart", 2000)]
        [TestCase("Environment_RimPine", 1000)]
        public void Art403_OriginalImportHasAppliedUnitsNormalsUvAndSharedPalette(string name, int budget)
        {
            var path = "Assets/_Game/Art/Models/ART4/" + name + ".fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.globalScale, Is.EqualTo(1));
            Assert.That(importer.useFileScale && importer.bakeAxisConversion, Is.True);
            Assert.That(importer.isReadable || importer.importAnimation || importer.importLights || importer.importCameras || importer.addCollider, Is.False);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var meshes = model.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh).ToArray();
            Assert.That(meshes, Is.Not.Empty);
            Assert.That(meshes.Sum(m => m.triangles.Length / 3), Is.InRange(1, budget));
            foreach (var mesh in meshes)
            {
                Assert.That(mesh.normals.Length, Is.EqualTo(mesh.vertexCount));
                Assert.That(mesh.normals.All(n => Mathf.Abs(n.magnitude - 1) < .0001f), Is.True);
                Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount));
                Assert.That(mesh.uv.All(uv => uv.x > 0 && uv.x < 1 && uv.y > 0 && uv.y < 1), Is.True);
                Assert.That(mesh.subMeshCount, Is.InRange(1, 2));
            }
        }
    }
}
