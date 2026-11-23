using System;
using System.Linq;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Veilward.Domain;
using Veilward.Editor;
using Veilward.Presentation;
using Object = UnityEngine.Object;
namespace Veilward.Tests.EditMode
{
    public sealed class EnemyModelAssetTests
    {
        [TestCase(false, 400)] [TestCase(true, 1312)]
        public void Art501_AuthoredGeometryAndTenEditableClipsReadBack(bool boss, int triangles)
        {
            var path = EnemyModelBuilder.Folder + (boss ? "Enemy_RiftWarden" : "Enemy_Shade") + ".fbx";
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            var meshes = assets.OfType<Mesh>().ToArray();
            Assert.That(meshes.Length, Is.EqualTo(4));
            Assert.That(meshes.Sum(m => (int)Enumerable.Range(0, m.subMeshCount).Sum(i => m.GetIndexCount(i)) / 3), Is.EqualTo(triangles));
            Assert.That(triangles, Is.LessThan(boss ? 15000 : 5000));
            foreach (var mesh in meshes) { Assert.That(mesh.normals.Length, Is.EqualTo(mesh.vertexCount)); Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount)); }
            var clips = assets.OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
            CollectionAssert.AreEquivalent(new[] { "Idle", "Move", "Hit", "Death", "Leak" }, clips.Select(c => c.name));
            foreach (var clip in clips)
            {
                Assert.That(clip.legacy, Is.True);
                Assert.That(AnimationUtility.GetCurveBindings(clip).Length, Is.GreaterThanOrEqualTo(4 * 9));
                Assert.That(clip.length, Is.EqualTo(clip.name == "Hit" ? .4f : clip.name == "Death" || clip.name == "Leak" ? .6f : 1).Within(.001f));
            }
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(boss ? EnemyModelBuilder.BossPath : EnemyModelBuilder.ShadePath));
            try
            {
                var visual = instance.GetComponentInChildren<EnemyModelView>(); Assert.That(visual, Is.Not.Null);
                Assert.That(instance.GetComponent<Renderer>().enabled, Is.False);
                Assert.That(visual.Model.GetComponentsInChildren<SkinnedMeshRenderer>().Length, Is.Zero);
                Assert.That(visual.Model.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Distinct().Count(), Is.EqualTo(3), "Surface/Glow and one shared stencil material");
                Assert.That(visual.Model.GetComponentsInChildren<Animation>().All(a => !a.enabled && !a.playAutomatically), Is.True);
                Assert.That(instance.transform.Find("Health Bar").localPosition.y, Is.EqualTo(2));
                Assert.That(instance.transform.Find("Status slots").GetComponentsInChildren<UnityEngine.UI.Text>(true).Length, Is.EqualTo(2));
                var parts = visual.Model.GetComponentsInChildren<MeshFilter>().Where(m => m.name != "Body occlusion cue").ToArray();
                Assert.That(parts.Length, Is.EqualTo(4));
                foreach (var part in parts)
                {
                    var cue = part.transform.Find("Body occlusion cue"); Assert.That(cue, Is.Not.Null);
                    Assert.That(cue.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(part.sharedMesh));
                    Assert.That(cue.localPosition, Is.EqualTo(Vector3.zero)); Assert.That(cue.localScale, Is.EqualTo(Vector3.one));
                }
                foreach (var clip in clips)
                {
                    clip.SampleAnimation(visual.Model.gameObject, 0); var from = parts.Select(p => p.transform.localToWorldMatrix).ToArray();
                    clip.SampleAnimation(visual.Model.gameObject, clip.length * .25f);
                    Assert.That(parts.Select((p, i) => p.transform.localToWorldMatrix != from[i]).Any(v => v), Is.True, clip.name + " actual animated pose");
                    foreach (var part in parts)
                    {
                        var cue = part.transform.Find("Body occlusion cue");
                        Assert.That(cue.localToWorldMatrix, Is.EqualTo(part.transform.localToWorldMatrix), clip.name + " animated cue follows part exactly");
                    }
                }
                clips.Single(c => c.name == "Idle").SampleAnimation(visual.Model.gameObject, 0);
                instance.transform.position = new Vector3(0, .6f, 0);
                var actualBounds = parts[0].GetComponent<Renderer>().bounds;
                foreach (var part in parts.Skip(1)) actualBounds.Encapsulate(part.GetComponent<Renderer>().bounds);
                Assert.That(actualBounds.min.y, Is.EqualTo(boss ? .157247f : .058384f).Within(.005f), "actual meter hover after legacy root compensation");
                Assert.That(actualBounds.max.y, Is.GreaterThan(boss ? 1.8f : 1.3f));
                Assert.That(actualBounds.size.x, Is.GreaterThan(boss ? 1.5f : .7f));
                Assert.That(visual.Model.Find("MaskCrown").GetComponent<Renderer>().bounds.center.z, Is.GreaterThan(.1f), "face authored toward Unity +Z");
                var rootPosition = instance.transform.position; var rootScale = instance.transform.localScale;
                var baseline = parts.Select(p => p.transform.localToWorldMatrix).ToArray();
                visual.Synchronize(Vector3.forward, true); visual.Advance(.25f);
                Assert.That(parts.Select((p, i) => p.transform.localToWorldMatrix != baseline[i]).Any(v => v), Is.True);
                visual.Hit(); Assert.That(visual.Pose, Is.EqualTo("Hit")); visual.Advance(.2f);
                Assert.That(instance.transform.position, Is.EqualTo(rootPosition)); Assert.That(instance.transform.localScale, Is.EqualTo(rootScale));
                visual.Advance(.3f); Assert.That(visual.Pose, Is.EqualTo("Move"));
                FlameModelBuilder.BindEnemyOcclusion(instance); FlameModelBuilder.BindEnemyOcclusion(instance);
                Assert.That(visual.Model.GetComponentsInChildren<MeshFilter>().Length, Is.EqualTo(8));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void Art502_RepeatedImportAndBindPreserveAuthoredRefsAndOneMultipartOwner()
        {
            var paths = new[] { EnemyModelBuilder.ShadePath, EnemyModelBuilder.BossPath, EnemyModelBuilder.ShadePath + ".meta", EnemyModelBuilder.BossPath + ".meta" }
                .Concat(Directory.GetFiles(EnemyModelBuilder.Folder, "*.meta")).ToArray();
            var saved = paths.ToDictionary(p => p, File.ReadAllBytes);
            string[] References(string path) => AssetDatabase.GetDependencies(path).OrderBy(x => x).ToArray();
            var shadeRefs = References(EnemyModelBuilder.ShadePath); var bossRefs = References(EnemyModelBuilder.BossPath);
            try
            {
                EnemyModelBuilder.Import(); EnemyModelBuilder.Import();
                CollectionAssert.AreEqual(shadeRefs, References(EnemyModelBuilder.ShadePath)); CollectionAssert.AreEqual(bossRefs, References(EnemyModelBuilder.BossPath));
                foreach (var path in new[] { EnemyModelBuilder.ShadePath, EnemyModelBuilder.BossPath })
                {
                    var enemy = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    Assert.That(enemy.GetComponentsInChildren<EnemyModelView>(true).Length, Is.EqualTo(1));
                    var model = enemy.GetComponentInChildren<EnemyModelView>().Model;
                    Assert.That(model.GetComponentsInChildren<MeshFilter>(true).Length, Is.EqualTo(8));
                    Assert.That(enemy.GetComponent<EnemyHealthBarView>(), Is.Not.Null); Assert.That(enemy.GetComponent<EnemyStatusView>(), Is.Not.Null);
                }
                foreach (var path in paths.Where(p => p.EndsWith(".meta"))) CollectionAssert.AreEqual(saved[path], File.ReadAllBytes(path), path);
            }
            finally
            {
                foreach (var pair in saved) File.WriteAllBytes(pair.Key, pair.Value);
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }
        }

        [Test]
        public void Art503_AuthoritativeSelectorPreservesLegacyMissingAndUnknownFallback()
        {
            EditorSceneManager.OpenScene("Assets/_Game/Scenes/Battle.unity");
            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            var shade = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyModelBuilder.ShadePath).transform;
            var boss = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyModelBuilder.BossPath).transform;
            Assert.That(AssetDatabase.AssetPathToGUID(EnemyModelBuilder.ShadePath), Is.EqualTo("1b22335062b814d4ca031183c8751afe"));
            Assert.That(battle.SelectEnemyPrefab(new EnemyDefinitionId("shade")), Is.SameAs(shade));
            Assert.That(battle.SelectEnemyPrefab(new EnemyDefinitionId("rift-warden")), Is.SameAs(boss));
            Assert.That(battle.SelectEnemyPrefab(new EnemyDefinitionId("unknown")), Is.SameAs(shade));
            var so = new SerializedObject(battle); so.FindProperty("bossPrefab").objectReferenceValue = null; so.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(battle.SelectEnemyPrefab(new EnemyDefinitionId("rift-warden")), Is.SameAs(shade));
            EditorSceneManager.OpenScene("Assets/_Game/Scenes/Battle.unity");
        }
    }
}
