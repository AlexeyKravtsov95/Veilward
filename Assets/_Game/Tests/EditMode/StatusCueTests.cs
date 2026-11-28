using System.Linq;
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
        private const string InkShade = "Assets/_Game/Prefabs/InkAndLanterns/Shade.prefab";

        [TestCase(StatusKind.Burn, StatusKind.Wet)] [TestCase(StatusKind.Burn, StatusKind.Gust)]
        [TestCase(StatusKind.Burn, StatusKind.Crack)] [TestCase(StatusKind.Wet, StatusKind.Gust)]
        [TestCase(StatusKind.Wet, StatusKind.Crack)] [TestCase(StatusKind.Gust, StatusKind.Crack)]
        public void UiRework2_EachPairUsesDistinctArtworkInPriorityOrder(StatusKind first, StatusKind second)
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(InkShade));
            try
            {
                var enemy = NewEnemy();
                Status(enemy, second);
                Status(enemy, first);
                root.GetComponent<EnemyStatusView>().Refresh(enemy, 0, Catalog(), null, 0, null);
                var images = root.GetComponentsInChildren<Image>(true);
                Assert.That(images.Select(image => image.sprite.name), Is.EqualTo(new[] { first + "Badge", second + "Badge" }));
                Assert.That(images[0].sprite.rect.Overlaps(images[1].sprite.rect), Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void UiRework2_WarmedStableBadgeRefreshAndLayoutAllocateNoManagedBytes()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(InkShade));
            var cameraObject = new GameObject("Badge allocation camera");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.position = Vector3.back * 10;
                var view = root.GetComponent<EnemyStatusView>();
                var enemy = NewEnemy();
                Status(enemy, StatusKind.Wet);
                Status(enemy, StatusKind.Gust);
                var catalog = Catalog();
                var health = new System.Collections.Generic.List<Rect>();
                var occupied = new System.Collections.Generic.List<Rect>(1);
                void Refresh()
                {
                    occupied.Clear();
                    view.Refresh(enemy, 0, catalog, null, 0, camera);
                    view.Layout(camera, Vector3.zero, health, occupied);
                }
                for (var i = 0; i < 20; i++) Refresh();
                Assert.That(view.VisibleSlotCount, Is.EqualTo(2));
                var before = System.GC.GetAllocatedBytesForCurrentThread();
                for (var i = 0; i < 1000; i++) Refresh();
                var after = System.GC.GetAllocatedBytesForCurrentThread();
                Assert.That(after - before, Is.Zero);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(cameraObject); }
        }

        [TestCase("Assets/_Game/Prefabs/Shade.prefab", 2)]
        [TestCase(InkShade, 2)]
        [TestCase("Assets/_Game/Prefabs/InkAndLanterns/Warden.prefab", 0)]
        [TestCase("Assets/_Game/Prefabs/ART5/P_Enemy_RiftWarden.prefab", 0)]
        public void UiRework2_OnlyOrdinaryEnemiesHaveTwoPrebuiltDecorativeSlots(string path, int count)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var graphics = prefab.GetComponentsInChildren<Graphic>(true);
            Assert.That(graphics.Length, Is.EqualTo(count));
            Assert.That(graphics.All(g => g is Image && !g.raycastTarget), Is.True);
            Assert.That(prefab.GetComponentsInChildren<GraphicRaycaster>(true), Is.Empty);
        }

        [TestCase(StatusKind.Burn)] [TestCase(StatusKind.Wet)] [TestCase(StatusKind.Gust)] [TestCase(StatusKind.Crack)]
        public void UiRework2_ActualSnapshotApplyRefreshAndExpiryReuseGraphics(StatusKind kind)
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(InkShade));
            try
            {
                var view = root.GetComponent<EnemyStatusView>();
                var slots = root.GetComponentsInChildren<Image>(true);
                var enemy = NewEnemy();
                Status(enemy, kind, duration: 2);
                view.Refresh(enemy, 0, Catalog(), null, 0, null);
                var sprite = slots[0].sprite;
                Assert.That(sprite.name, Is.EqualTo(kind + "Badge"));
                Assert.That(slots[0].enabled, Is.True);
                Assert.That(slots[1].enabled, Is.False);
                Status(enemy, kind, power: 30, duration: 4);
                view.Refresh(enemy, 0, Catalog(), null, 0, null);
                Assert.That(enemy.Statuses.Single().RemainingTicks, Is.EqualTo(4));
                Assert.That(slots[0].sprite, Is.SameAs(sprite));
                for (var i = 0; i < 4; i++) enemy.AdvanceStatusTimers();
                view.Refresh(enemy, 4, Catalog(), null, 0, null);
                Assert.That(slots.All(slot => !slot.enabled && slot.sprite == null), Is.True);
                CollectionAssert.AreEqual(slots, root.GetComponentsInChildren<Image>(true));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(ReactionKind.Steam, ReactionTriggerKind.StatusPair, StatusKind.Gust)]
        [TestCase(ReactionKind.Storm, ReactionTriggerKind.StatusPair, StatusKind.Burn)]
        [TestCase(ReactionKind.Steam, ReactionTriggerKind.Intrinsic, StatusKind.Burn)]
        [TestCase(ReactionKind.Storm, ReactionTriggerKind.Intrinsic, StatusKind.Burn)]
        public void UiRework2_TypedReactionArtworkAndFallbackShareTheSameSlots(
            ReactionKind reaction, ReactionTriggerKind trigger, StatusKind remaining)
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(InkShade));
            try
            {
                var view = root.GetComponent<EnemyStatusView>();
                var images = root.GetComponentsInChildren<Image>(true);
                var enemy = NewEnemy();
                foreach (var kind in new[] { StatusKind.Crack, StatusKind.Gust, StatusKind.Wet, StatusKind.Burn })
                    Status(enemy, kind);
                var presentation = new ReactionPresentationState();
                var steam = reaction == ReactionKind.Steam;
                presentation.Consume(new ReactionEvent(1, 1, reaction, trigger, new TowerId(1), enemy.Id,
                    new PathPosition(1, 0), steam ? StatusKind.Burn : StatusKind.Wet,
                    steam ? StatusKind.Wet : StatusKind.Gust, 1.25f, 2), System.Array.Empty<DamageResolvedEvent>());
                var memory = presentation.GetMemory(enemy.Id);
                var slots = EnemyStatusView.Describe(enemy, 1, Catalog(), memory, 0);
                Assert.That(slots.First.Reaction, Is.EqualTo(reaction));
                Assert.That(slots.First.Trigger, Is.EqualTo(trigger));
                Assert.That(slots.First.Status, Is.Null);
                Assert.That(slots.Second.Status, Is.EqualTo(remaining));

                view.Refresh(enemy, 1, Catalog(), memory, .499, null);
                Assert.That(images.Select(i => i.sprite.name), Is.EqualTo(new[] { reaction + "Badge", remaining + "Badge" }));
                Assert.That(images[0].material, Is.SameAs(images[1].material));
                Assert.That(images[0].sprite.texture, Is.SameAs(images[1].sprite.texture));
                view.Refresh(enemy, 1, Catalog(), memory, .5, null);
                Assert.That(images.Select(i => i.sprite.name), Is.EqualTo(new[] { "BurnBadge", "WetBadge" }));
                Assert.That(enemy.Statuses.Count, Is.EqualTo(4));
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static EnemyState NewEnemy() =>
            EnemyState.CreateInitial(new EnemyId(1), new EnemyDefinition(new EnemyDefinitionId("shade"), 100, 1, 1, 0));
    }
}
