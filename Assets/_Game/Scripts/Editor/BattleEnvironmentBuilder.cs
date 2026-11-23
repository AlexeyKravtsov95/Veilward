using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Veilward.Domain;
using Veilward.Presentation;
using Object = UnityEngine.Object;

namespace Veilward.Editor
{
    // Статический kit первой карты. Не владеет Domain/input и не генерирует runtime levels.
    public static class BattleEnvironmentBuilder
    {
        public const string RootName = "Sanctuary Environment ART4";
        public const string ModelFolder = "Assets/_Game/Art/Models/ART4/";
        public const string PrefabFolder = "Assets/_Game/Prefabs/ART4/";
        public static readonly string[] ModelNames = { "Environment_SanctuaryIsland", "Environment_SanctuaryPath",
            "Environment_BuildSocket", "Environment_SpawnGate", "Environment_SealHeart", "Environment_RimPine" };

        [MenuItem("VEILWARD/ART.4/Import environment kit")]
        public static void Import()
        {
            if (UnityEngine.Application.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            Directory.CreateDirectory(PrefabFolder); AssetDatabase.Refresh();
            var surface = Shared("MAT_Art1_Surface"); var glow = Shared("MAT_Art1_Glow");
            var mask = Shared("MAT_Art21_TowerMask");
            foreach (var name in ModelNames)
            {
                var path = ModelFolder + name + ".fbx";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) throw new InvalidOperationException("Missing original model: " + path);
                importer.globalScale = 1; importer.useFileScale = true; importer.bakeAxisConversion = true;
                importer.isReadable = false; importer.importAnimation = false; importer.importCameras = false;
                importer.importLights = false; importer.addCollider = false;
                importer.importNormals = ModelImporterNormals.Import; importer.importTangents = ModelImporterTangents.None;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Art1Surface"), surface);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Art1Glow"), glow);
                importer.SaveAndReimport();
                var root = new GameObject("P_" + name);
                try
                {
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                    model.transform.SetParent(root.transform, false);
                    if (name == "Environment_SpawnGate" || name == "Environment_SealHeart")
                        ShadeOcclusionView.Create(model.transform, mask);
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(name));
                }
                finally { Object.DestroyImmediate(root); }
            }
            AssetDatabase.SaveAssets();
        }

        public static string PrefabPath(string name) => PrefabFolder + "P_" + name + ".prefab";

        public static GameObject Create(BattleScenarioConfig config)
        {
            var simulation = config.CreateSimulation(); var map = simulation.Grid.Map;
            // Asset рассчитан на принятую первую карту; чужие map contracts не нормализуются молча.
            if (map.Width != 12 || map.Height != 8 || map.Origin != new GridCoordinate(-6, -4) || map.CellSize != 1)
                throw new InvalidOperationException("ART4 kit requires the unchanged first mission geometry.");
            foreach (var name in ModelNames)
                if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(name)) == null)
                    throw new InvalidOperationException("Import ART4 environment kit first: " + name);
            var root = new GameObject(RootName);
            Place("Environment_SanctuaryIsland", Vector3.zero, root.transform);
            var sockets = Child("Build Sockets", root.transform);
            for (var y = map.Origin.Y; y < map.Origin.Y + map.Height; y++)
            for (var x = map.Origin.X; x < map.Origin.X + map.Width; x++)
            {
                var cell = new GridCoordinate(x, y);
                if (simulation.Grid.GetState(cell) == GridCellState.Blocked) continue;
                var p = map.GetCellCenter(cell);
                var socket = Place("Environment_BuildSocket", new Vector3(p.X, 0, p.Y), sockets);
                socket.name = $"Socket_{x}_{y}";
            }
            var path = simulation.Path; var route = Child("Sanctuary Route", root.transform);
            for (var distance = 0f; distance <= path.TotalLength; distance += map.CellSize)
            {
                var p = path.GetPosition(distance);
                var next = path.GetPosition(Mathf.Min(distance + .05f, path.TotalLength));
                var previous = path.GetPosition(Mathf.Max(0, distance - .05f));
                var tile = Place("Environment_SanctuaryPath", new Vector3(p.X, 0, p.Y), route);
                tile.name = "Paver_" + distance;
                var direction = distance < path.TotalLength
                    ? new Vector3(next.X - p.X, 0, next.Y - p.Y)
                    : new Vector3(p.X - previous.X, 0, p.Y - previous.Y);
                tile.rotation = Quaternion.LookRotation(direction);
            }
            var start = path.GetPosition(0); var end = path.GetPosition(path.TotalLength);
            var spawn = Place("Environment_SpawnGate", new Vector3(start.X, 0, start.Y), root.transform);
            spawn.name = "Spawn Entrance";
            var forward = path.GetPosition(.1f);
            spawn.rotation = Quaternion.LookRotation(new Vector3(forward.X - start.X, 0, forward.Y - start.Y));
            Place("Environment_SealHeart", new Vector3(end.X, 0, end.Y), root.transform).name = "Seal Heart";
            CreateEdgeClusters(root.transform);
            return root;
        }

        public static void ApplyAtmosphere(Camera camera, Light sun)
        {
            // Только перечисленные ART4 lighting deltas; projection/pose/quality/pipeline прежние.
            camera.backgroundColor = new Color(.105f, .18f, .23f);
            sun.color = new Color(1, .94f, .83f); sun.intensity = 1.2f;
            RenderSettings.sun = sun; RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.64f, .70f, .74f);
            RenderSettings.fog = false;
        }

        private static void CreateEdgeClusters(Transform parent)
        {
            var root = Child("Peripheral Sanctuary Garden", parent);
            foreach (var point in new[] { new Vector3(-7.05f, 0, -3.7f), new Vector3(-5.2f, 0, -5.12f),
                new Vector3(-7.1f, 0, -.9f), new Vector3(2.0f, 0, -5.15f) })
            {
                var tree = Place("Environment_RimPine", point, root);
                ShadeOcclusionView.Create(tree, Shared("MAT_Art21_TowerMask"));
            }
            // Все footprints снаружи playable rectangle [-6.5,5.5]x[-4.5,3.5].
            var points = new[] { new Vector3(-7.05f, 0, -2.7f), new Vector3(-7.05f, 0, .6f),
                new Vector3(6.05f, 0, -2.6f), new Vector3(6.05f, 0, .5f),
                new Vector3(-4.4f, 0, -5.12f), new Vector3(.9f, 0, -5.12f),
                new Vector3(-3.9f, 0, 4.12f), new Vector3(2.4f, 0, 4.12f) };
            for (var i = 0; i < points.Length; i++)
            {
                var rock = PlaceArt1("Prop_MossRock", points[i], root); rock.localScale = Vector3.one * .55f;
                var shrub = PlaceArt1("Prop_RitualShrub", points[i] + new Vector3(.08f, .02f, 0), root);
                shrub.localScale = Vector3.one * .65f;
                ShadeOcclusionView.Create(rock, Shared("MAT_Art21_TowerMask"));
                ShadeOcclusionView.Create(shrub, Shared("MAT_Art21_TowerMask"));
            }
        }

        private static Material Shared(string name) => AssetDatabase.LoadAssetAtPath<Material>(
            ArtReferenceBuilder.MaterialFolder + name + ".mat") ?? throw new InvalidOperationException("Missing shared material " + name);
        private static Transform Child(string name, Transform parent)
        { var child = new GameObject(name).transform; child.SetParent(parent, false); return child; }
        private static Transform Place(string name, Vector3 position, Transform parent) =>
            Instance(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(name)), position, parent);
        private static Transform PlaceArt1(string name, Vector3 position, Transform parent) =>
            Instance(ArtReferenceBuilder.Prefab(name), position, parent);
        private static Transform Instance(GameObject prefab, Vector3 position, Transform parent)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(parent, false); instance.transform.position = position;
            return instance.transform;
        }
    }
}
