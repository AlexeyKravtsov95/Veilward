using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Veilward.Domain;
using Veilward.Presentation;

namespace Veilward.Tests.EditMode
{
    public sealed class BattleEnvironmentTests
    {
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

        [TestCase("Environment_SanctuaryIsland", 5000)]
        [TestCase("Environment_SanctuaryPath", 400)]
        [TestCase("Environment_BuildSocket", 400)]
        [TestCase("Environment_SpawnGate", 2000)]
        [TestCase("Environment_SealHeart", 2000)]
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
