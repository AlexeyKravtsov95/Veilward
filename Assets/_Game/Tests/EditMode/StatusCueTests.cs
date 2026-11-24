using System.Linq;
using System;
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Veilward.Domain;
using Veilward.Presentation;
using static Veilward.Tests.EditMode.BattleReactionTests;

namespace Veilward.Tests.EditMode
{
    public sealed class StatusCueTests
    {
        [TestCase("Assets/_Game/Prefabs/Shade.prefab")]
        [TestCase("Assets/_Game/Prefabs/ART5/P_Enemy_RiftWarden.prefab")]
        public void Vfx1B04_TwoPrebuiltStatusGraphicsKeepExistingLabelsAndOwners(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab.GetComponentsInChildren<Component>(true).Count(c => c != null && c.GetType().Name == "StatusCueGraphic"), Is.EqualTo(2));
            Assert.That(prefab.GetComponentsInChildren<UnityEngine.UI.Text>(true).Length, Is.EqualTo(2));
            Assert.That(prefab.GetComponents<EnemyStatusView>().Length, Is.EqualTo(1));
        }

        [TestCase(StatusKind.Burn)] [TestCase(StatusKind.Wet)] [TestCase(StatusKind.Gust)] [TestCase(StatusKind.Crack)]
        public void Vfx1B04_SnapshotApplyRefreshExpiryAndClockFreezeUseSameTwoGraphics(StatusKind kind)
        {
            WithView(view =>
            {
                var enemy = EnemyState.CreateInitial(new EnemyId(1), new EnemyDefinition(new EnemyDefinitionId("shade"),100,1,1,0));
                Status(enemy, kind, duration: 2);
                view.Refresh(enemy,0,Catalog(),null,0,null);
                var cue = view.FirstCue;
                Assert.That(cue.Kind, Is.EqualTo(kind)); Assert.That(view.VisibleSlotCount, Is.EqualTo(1));
                var first = Mesh(cue); var ids = view.GetComponentsInChildren<Component>(true).Select(c => c.GetInstanceID()).ToArray();
                view.Refresh(enemy,0,Catalog(),null,.17,null);
                Assert.That(Mesh(cue), Is.Not.EqualTo(first), "Active presentation clock animates actual vertices");
                var paused = Mesh(cue);
                for (var i = 0; i < 50; i++) view.Refresh(enemy,0,Catalog(),null,.17,null);
                CollectionAssert.AreEqual(paused, Mesh(cue));
                Status(enemy,kind,power:30,duration:4);
                view.Refresh(enemy,0,Catalog(),null,.17,null);
                Assert.That(enemy.Statuses.Single().RemainingTicks, Is.EqualTo(4));
                CollectionAssert.AreEqual(ids, view.GetComponentsInChildren<Component>(true).Select(c => c.GetInstanceID()));
                for(var i=0;i<4;i++) enemy.AdvanceStatusTimers();
                view.Refresh(enemy,4,Catalog(),null,.17,null);
                Assert.That(view.VisibleSlotCount, Is.Zero); Assert.That(cue.Kind, Is.Null); Assert.That(cue.enabled, Is.False);
                // Тот же ID после restore не наследует старые snapshots.
                var fresh = EnemyState.CreateInitial(enemy.Id,enemy.Definition);
                view.Refresh(fresh,0,Catalog(),null,0,null);
                Assert.That(view.FirstCue.Kind, Is.Null); Assert.That(view.SecondCue.Kind, Is.Null);
            });
        }

        [Test]
        public void Vfx1B04_FourShapesAreDistinctAndBoundedWithinSlotWithoutExtraRenderersOrMaterials()
        {
            WithView(view =>
            {
                var shapes = new HashSet<string>();
                foreach(StatusKind kind in Enum.GetValues(typeof(StatusKind)))
                {
                    view.FirstCue.Refresh(kind,.123,Color.white);
                    var mesh=Mesh(view.FirstCue);
                    Assert.That(mesh.Length, Is.InRange(12,100));
                    Assert.That(mesh.All(v => Mathf.Abs(v.x)<=14 && Mathf.Abs(v.y)<=14),Is.True);
                    Assert.That(shapes.Add(string.Join(";",mesh.Select(v=>v.ToString("R")))),Is.True,"Different shapes, not tint only");
                }
                Assert.That(view.GetComponentsInChildren<StatusCueGraphic>(true).Length, Is.EqualTo(2));
                Assert.That(view.FirstCue.material, Is.SameAs(view.SecondCue.material));
            });
        }

        [Test]
        public void Vfx1B04_AllStatusesAndReactionPriorityDriveOnlyTheSelectedSlotGraphics()
        {
            WithView(view =>
            {
                var enemy=EnemyState.CreateInitial(new EnemyId(1),new EnemyDefinition(new EnemyDefinitionId("shade"),100,1,1,0));
                foreach(StatusKind kind in Enum.GetValues(typeof(StatusKind)))Status(enemy,kind);
                view.Refresh(enemy,0,Catalog(),null,0,null);
                Assert.That(view.FirstCue.Kind,Is.EqualTo(StatusKind.Burn));Assert.That(view.SecondCue.Kind,Is.EqualTo(StatusKind.Wet));
                Reserve(enemy,ReactionKind.Steam,0);
                view.Refresh(enemy,59,Catalog(),null,0,null);
                Assert.That(view.FirstCue.Kind,Is.EqualTo(StatusKind.Wet));Assert.That(view.SecondCue.Kind,Is.EqualTo(StatusKind.Gust));
                var presentation=new ReactionPresentationState();
                presentation.Consume(new ReactionEvent(60,1,ReactionKind.Steam,ReactionTriggerKind.StatusPair,new TowerId(1),enemy.Id,
                    new PathPosition(1,0),StatusKind.Burn,StatusKind.Wet,1.25f,4),Array.Empty<DamageResolvedEvent>());
                view.Refresh(enemy,60,Catalog(),presentation.GetMemory(enemy.Id),0,null);
                Assert.That(view.FirstCue.Kind,Is.Null,"Reaction label keeps its slot; no extra basic cue");
                Assert.That(view.SecondCue.Kind,Is.EqualTo(StatusKind.Gust));Assert.That(view.VisibleSlotCount,Is.EqualTo(2));
                view.Refresh(enemy,60,Catalog(),presentation.GetMemory(enemy.Id),.5,null);
                Assert.That(view.FirstCue.Kind,Is.EqualTo(StatusKind.Burn));Assert.That(view.SecondCue.Kind,Is.EqualTo(StatusKind.Wet));
                Assert.That(enemy.Statuses.Count,Is.EqualTo(4));
            });
        }

        [TestCase(StatusKind.Burn,StatusKind.Wet)] [TestCase(StatusKind.Burn,StatusKind.Gust)]
        [TestCase(StatusKind.Burn,StatusKind.Crack)] [TestCase(StatusKind.Wet,StatusKind.Gust)]
        [TestCase(StatusKind.Wet,StatusKind.Crack)] [TestCase(StatusKind.Gust,StatusKind.Crack)]
        public void Vfx1B04_EachStatusPairPreservesDescribeLayoutAndTwoSlotOwnership(StatusKind first,StatusKind second)
        {
            WithView(view=>
            {
                var enemy=EnemyState.CreateInitial(new EnemyId(1),new EnemyDefinition(new EnemyDefinitionId("shade"),100,1,1,0));
                Status(enemy,first);Status(enemy,second);
                view.Refresh(enemy,0,Catalog(),null,0,null);
                Assert.That(view.FirstCue.Kind,Is.EqualTo(first));Assert.That(view.SecondCue.Kind,Is.EqualTo(second));
                var labels=view.GetComponentsInChildren<Text>(true);
                Assert.That(labels[0].text,Does.Contain(first.ToString()));Assert.That(labels[1].text,Does.Contain(second.ToString()));
                Assert.That(view.FirstCue.rectTransform.anchoredPosition.x + 14,Is.LessThan(-labels[0].preferredWidth*.5f));
                var camera=view.gameObject.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=6;
                camera.transform.position=new Vector3(0,0,-10);
                var occupied=new List<Rect>();view.Layout(camera,occupied);
                Assert.That(occupied.Count,Is.EqualTo(1));Assert.That(occupied[0].width,Is.GreaterThan(0));
            });
        }

        private static Vector3[] Mesh(StatusCueGraphic graphic)
        {
            using(var vh=new VertexHelper())
            {
                typeof(StatusCueGraphic).GetMethod("OnPopulateMesh",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(VertexHelper)},null).Invoke(graphic,new object[]{vh});
                var mesh=new Mesh();vh.FillMesh(mesh);var vertices=mesh.vertices;UnityEngine.Object.DestroyImmediate(mesh);return vertices;
            }
        }
        private static void WithView(Action<EnemyStatusView> action)
        {
            var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Shade.prefab"));
            try { action(root.GetComponent<EnemyStatusView>()); }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
