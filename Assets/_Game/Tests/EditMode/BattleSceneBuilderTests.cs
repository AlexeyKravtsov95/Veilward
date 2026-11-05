using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Veilward.Editor;
using Veilward.Presentation;

namespace Veilward.Tests.EditMode
{
    public sealed class BattleSceneBuilderTests
    {
        [Test]
        public void CreatePathGeometry_ModifiedConfig_MatchesDomainPath()
        {
            var config = BattleScenarioConfig.CreateDefault();
            var serialized = new SerializedObject(config);
            var waypoints = serialized.FindProperty("waypoints");
            waypoints.arraySize = 3;
            waypoints.GetArrayElementAtIndex(0).vector2IntValue = new Vector2Int(-3, -1);
            waypoints.GetArrayElementAtIndex(1).vector2IntValue = new Vector2Int(2, -1);
            waypoints.GetArrayElementAtIndex(2).vector2IntValue = new Vector2Int(2, 4);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            GameObject pathObject = null;

            try
            {
                pathObject = BattleSceneBuilder.CreatePathGeometry(config, material, material, material);
                var path = config.CreateSimulation().Path;

                Assert.That(path.TotalLength, Is.EqualTo(10f).Within(0.0001f));
                Assert.That(pathObject.transform.childCount, Is.EqualTo(path.Points.Count + 1));
                for (var index = 0; index < path.Points.Count - 1; index++)
                {
                    var start = ToWorld(path.Points[index]);
                    var end = ToWorld(path.Points[index + 1]);
                    var delta = end - start;
                    var segment = pathObject.transform.Find($"Path Segment {index}");

                    Assert.That(segment, Is.Not.Null);
                    AssertVector(segment.position, (start + end) * 0.5f, segment.name);
                    AssertVector(segment.localScale, new Vector3(1.2f, 0.1f, delta.magnitude), segment.name);
                    Assert.That(
                        Quaternion.Angle(segment.rotation, Quaternion.LookRotation(delta.normalized)),
                        Is.LessThan(0.001f));
                }

                AssertVector(
                    pathObject.transform.Find("Spawn Marker").position,
                    MarkerPosition(path.Points[0]),
                    "Spawn Marker");
                AssertVector(
                    pathObject.transform.Find("Seal Heart").position,
                    MarkerPosition(path.Points[path.Points.Count - 1]),
                    "Seal Heart");
            }
            finally
            {
                if (pathObject != null)
                {
                    Object.DestroyImmediate(pathObject);
                }

                Object.DestroyImmediate(material);
                Object.DestroyImmediate(config);
            }
        }

        private static Vector3 ToWorld(Veilward.Domain.GridCoordinate point)
        {
            return new Vector3(point.X, 0.05f, point.Y);
        }

        private static Vector3 MarkerPosition(Veilward.Domain.GridCoordinate point)
        {
            return new Vector3(point.X, 0.25f, point.Y);
        }

        private static void AssertVector(Vector3 actual, Vector3 expected, string label)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.0001f), label);
        }
    }
}
