using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Veilward.Domain;
using Veilward.Presentation;
using static Veilward.Tests.EditMode.BattleReactionTests;

namespace Veilward.Tests.EditMode
{
    public sealed class BattleReactionPresentationTests
    {
        [TestCase("steam")] [TestCase("storm")]
        public void RegBattle154_AllLevelsExplainIntrinsicAndConditionalDirectDamage(string id)
        {
            var config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWave.asset");
            foreach (var definition in config.CreateSimulation().TowerCatalog.Get(new TowerDefinitionId(id)).Levels)
            {
                var text = TowerDisplay.Stats(definition, config.CreateSimulation().ReactionCatalog);
                Assert.That(TowerDisplay.Role(definition), Does.Not.Contain("Legacy"));
                Assert.That(text, Does.Contain("встроенная").And.Contain(definition.IntrinsicReaction.DamagePercent + "%"));
                Assert.That(text, Does.Contain(definition.IntrinsicReaction.Range.ToString("0.##")));
                Assert.That(text, Does.Contain("лимит " + definition.IntrinsicReaction.TargetLimit).And.Contain("60 ticks"));
                Assert.That(text, Does.Contain("выжившая primary").And.Not.Contain("Status: None").And.Not.Contain("DPS"));
                Assert.That(text, Does.Contain("Damage: " + definition.Damage).And.Contain("FireRate:"));
                if (id == "storm") Assert.That(text, Does.Contain("нужен сосед"));
            }
        }

        [TestCase("flame", "Пламя + Прилив → Пар")]
        [TestCase("tide", "Прилив + Ветер → Шторм")]
        [TestCase("gale", "Прилив + Ветер → Шторм")]
        public void RegBattle154_BasicPanelsExplainPairsAndStillFit(string id, string pair)
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Game/Scenes/Battle.unity");
            var config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWave.asset");
            var sim = config.CreateSimulation();
            var panel = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(t => t.name == "Tower Stats");
            var preview = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(t => t.name == "Build Stats");
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var measurement = (GameObject)UnityEngine.Object.Instantiate(panel.gameObject, scene);
            try
            {
                var text = measurement.GetComponent<UnityEngine.UI.Text>();
                foreach (var level in sim.TowerCatalog.Get(new TowerDefinitionId(id)).Levels)
                {
                    var stats = TowerDisplay.Stats(level, sim.ReactionCatalog);
                    Assert.That(stats, Does.Contain(pair).And.Contain("нелетального direct").And.Contain("60 ticks"));
                    if (id == "tide") Assert.That(stats, Does.Contain("Пламя + Прилив → Пар"));
                    text.text = id + " — Level: 3\n" + stats + "\nUpgrade: MAX\nSell refund: 999 gold";
                    Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(panel.rectTransform.rect.height));
                    text.fontSize = preview.fontSize; text.text = id + " — role\nPrice: 999 gold\n" + stats;
                    Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(preview.rectTransform.rect.height));
                    text.fontSize = panel.fontSize;
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(measurement); UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void RegBattle155_TwoSlotsPriorityCooldownAndExactActiveLifetime()
        {
            var enemy = EnemyState.CreateInitial(new EnemyId(1), new EnemyDefinition(new EnemyDefinitionId("e"), 100, 1, 1, 0));
            foreach (StatusKind k in Enum.GetValues(typeof(StatusKind))) Status(enemy, k);
            var state = NewState();
            AssertSlots(enemy, state, 0, "Burn", "Wet");
            Reserve(enemy, ReactionKind.Steam, 0);
            AssertSlots(enemy, state, 59, "Wet", "Gust");
            Reserve(enemy, ReactionKind.Storm, 0);
            AssertSlots(enemy, state, 59, "Burn", "Wet");
            Consume(state, Event(ReactionKind.Storm, 3));
            Consume(state, Event(ReactionKind.Steam, 5));
            Consume(state, Event(ReactionKind.Storm, 7));
            AssertSlots(enemy, state, 60, "Пламя + Прилив → Пар", "Gust");
            Call(state, "Advance", .499);
            AssertSlots(enemy, state, 60, "Пламя + Прилив → Пар", "Gust");
            Call(state, "Advance", .001);
            AssertSlots(enemy, state, 60, "Burn", "Wet");
        }

        [Test]
        public void RegBattle155_IntrinsicDoesNotPretendStatusesExistAndBothCausesRemain()
        {
            var enemy = EnemyState.CreateInitial(new EnemyId(1), new EnemyDefinition(new EnemyDefinitionId("e"), 100, 1, 1, 0));
            var state = NewState();
            Consume(state, Event(ReactionKind.Storm, 3, ReactionTriggerKind.Intrinsic));
            var slots = Describe(enemy, state, 0);
            Assert.That(Read<string>(slots, "First"), Does.Contain("STORM: встроенная"));
            Assert.That(Read<string>(slots, "Second"), Is.Empty);
            Consume(state, Event(ReactionKind.Steam, 5));
            var memory = Call(state, "GetMemory", enemy.Id);
            Assert.That(Read<object>(memory, "Steam"), Is.Not.Null);
            Assert.That(Read<object>(memory, "Storm"), Is.Not.Null);
            Assert.That(enemy.Statuses, Is.Empty);
        }

        [Test]
        public void RegBattle156_CorrelationCountsOnlyActualHitsAndHpLossRetainsLethalSummary()
        {
            var state = NewState();
            var reaction = Event(ReactionKind.Steam, 4);
            var hits = new[] { Hit(4, 0, 1, 2), Hit(4, 1, 2, 3), Hit(8, 0, 3, 99), Hit(4, 2, 4, 99, tick: 2) };
            Call(state, "Consume", reaction, hits);
            Assert.That(Read<string>(state, "LastSummary"), Is.EqualTo("Пар: 2 целей, 5 HP"));
            var memory = Call(state, "GetMemory", new EnemyId(2));
            var steam = Read<object>(memory, "Steam");
            Assert.That(Read<long>(steam, "AppliedHpLoss"), Is.EqualTo(5));
            Assert.That(Read<int>(steam, "HitCount"), Is.EqualTo(2));
            Call(state, "Prune", (object)Array.Empty<EnemyState>());
            Assert.That(Call(state, "GetMemory", new EnemyId(2)), Is.Null);
            Assert.That(Read<string>(state, "LastSummary"), Does.Contain("5 HP"));
        }

        [TestCase(ReactionKind.Steam)] [TestCase(ReactionKind.Storm)]
        public void RegBattle157_CapturedGeometryBoundedPoolFreezeAndReset(ReactionKind kind)
        {
            var go = new GameObject("Pooled captured VFX");
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            try
            {
                var type = Required("ReactionEffectView");
                var view = go.AddComponent(type);
                Call(view, "Initialize", material);
                var reaction = Event(kind, 4);
                var hits = new[] { Hit(4, 2, 3, 2, kind: kind), Hit(4, 1, 2, 3, kind: kind) };
                Call(view, "Play", reaction, hits);
                Assert.That(Read<int>(view, "HitCount"), Is.EqualTo(2));
                Assert.That(Read<float>(view, "RemainingSeconds"), Is.EqualTo(.5f));
                Call(view, "Advance", 0f);
                Assert.That(Read<float>(view, "RemainingSeconds"), Is.EqualTo(.5f));
                var lines = go.GetComponentsInChildren<LineRenderer>(true);
                Assert.That(lines.Length, Is.EqualTo(ReactionEffectView.RendererCount));
                Assert.That(lines.All(l => l.sharedMaterial == material), Is.True);
                var effect = (ReactionEffectView)view;
                Assert.That(effect.Event, Is.EqualTo(reaction));
                Assert.That(effect.GetHit(0), Is.EqualTo(hits[1]));
                Assert.That(effect.GetHit(1), Is.EqualTo(hits[0]));
                var block = new MaterialPropertyBlock(); lines[0].GetPropertyBlock(block);
                Assert.That(block.GetColor("_BaseColor").maxColorComponent, Is.GreaterThan(.8f),
                    "URP Unlit must receive the reaction color, independently of vertex colors.");
                if (kind == ReactionKind.Storm)
                {
                    Assert.That(lines[1].enabled, Is.True);
                    Assert.That(lines[2].enabled, Is.False, "Second captured hop starts after the first.");
                    Call(view, "Advance", .13f);
                    Assert.That(lines[1].GetPosition(0), Is.EqualTo(new Vector3(1, .57f, 0)));
                    Assert.That(lines[1].GetPosition(lines[1].positionCount - 1), Is.EqualTo(new Vector3(2, .57f, 0)));
                    Assert.That(lines[2].GetPosition(0), Is.EqualTo(new Vector3(2, .57f, 0)));
                    Assert.That(lines[2].GetPosition(lines[2].positionCount - 1), Is.EqualTo(new Vector3(3, .57f, 0)));
                }
                else
                {
                    var center = new Vector3(1, .34f, 0);
                    var initial = Vector3.Distance(lines[0].GetPosition(0), center);
                    Call(view, "Advance", .35f);
                    Assert.That(Vector3.Distance(lines[0].GetPosition(0), center), Is.GreaterThan(initial));
                    Assert.That(Vector3.Distance(lines[0].GetPosition(0), center), Is.LessThan(reaction.RadiusOrHopRange));
                    Assert.That(Mathf.Abs(lines[1].GetPosition(0).x - 2), Is.LessThan(.35f));
                    Assert.That(Mathf.Abs(lines[2].GetPosition(0).x - 3), Is.LessThan(.35f));
                }
                Call(view, "Advance", .5f);
                Assert.That(Read<float>(view, "RemainingSeconds"), Is.Zero);
                Assert.That(go.activeSelf, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(material); }
        }

        [Test]
        public void RegBattle158_PresentationClockOnlyAdvancesExplicitActiveTimeAndResetClearsEverything()
        {
            var state = NewState(); Consume(state, Event(ReactionKind.Steam, 1));
            Call(state, "Advance", 0d);
            Assert.That(Read<double>(state, "ActiveTime"), Is.Zero);
            Call(state, "Reset");
            Assert.That(Read<string>(state, "LastSummary"), Is.Empty);
            Assert.That(Call(state, "GetMemory", new EnemyId(1)), Is.Null);
        }

        [Test]
        public void RegBattle159_SavedSceneHasInspectorTwoSlotsAndValidatedReferences()
        {
            var errors = Veilward.Editor.BattleAssetValidator.ValidateSavedBattle();
            Assert.That(errors, Is.Empty);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Shade.prefab");
            var view = prefab.GetComponent(Required("EnemyStatusView"));
            Assert.That(view, Is.Not.Null);
            var serialized = new SerializedObject(view);
            foreach (var field in new[] { "firstSlot", "secondSlot" })
                Assert.That(serialized.FindProperty(field)?.objectReferenceValue, Is.Not.Null, field);
            Assert.That(prefab.GetComponentsInChildren<UnityEngine.UI.Text>(true).Length, Is.EqualTo(2));
        }

        [Test]
        public void RegBattle159_StatusFontProjectsToAtLeastTwelvePixelsAtReferenceHeight()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Game/Scenes/Battle.unity");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Shade.prefab");
            foreach (var text in prefab.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                Assert.That(text.fontSize * text.transform.lossyScale.y * 900f / (2 * Camera.main.orthographicSize),
                    Is.GreaterThanOrEqualTo(12), "Overhead text is unreadable at the 1600×900 reference viewport.");
        }

        [Test]
        public void RegBattle155_DenseLayoutSeparatesExistingTwoSlots()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Game/Scenes/Battle.unity");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Shade.prefab");
            var roots = new System.Collections.Generic.List<GameObject>();
            var rects = new System.Collections.Generic.List<Rect>();
            try
            {
                for (var i = 0; i < 4; i++)
                {
                    var root = UnityEngine.Object.Instantiate(prefab); roots.Add(root);
                    root.transform.position = new Vector3(i * .15f, .6f, 0);
                    var enemy = EnemyState.CreateInitial(new EnemyId(i + 1), new EnemyDefinition(new EnemyDefinitionId("e"), 100, 1, 1, 0));
                    foreach (StatusKind kind in Enum.GetValues(typeof(StatusKind))) Status(enemy, kind);
                    var view = root.GetComponent<EnemyStatusView>();
                    view.Refresh(enemy, 0, Catalog(), null, 0, Camera.main); view.Layout(Camera.main, rects);
                    Assert.That(view.VisibleSlotCount, Is.EqualTo(2));
                    Assert.That(root.GetComponentsInChildren<UnityEngine.UI.Text>(true).Length, Is.EqualTo(2));
                }
                for (var i = 0; i < rects.Count; i++)
                    for (var j = 0; j < i; j++) Assert.That(rects[i].Overlaps(rects[j]), Is.False);
            }
            finally { foreach (var root in roots) UnityEngine.Object.DestroyImmediate(root); }
        }

        private static Type Required(string name)
        {
            var type = typeof(TowerDisplay).Assembly.GetType("Veilward.Presentation." + name);
            Assert.That(type, Is.Not.Null, "Отсутствует Presentation " + name); return type;
        }
        private static object NewState() => Activator.CreateInstance(Required("ReactionPresentationState"));
        private static object Call(object target, string name, params object[] args)
        {
            var method = target.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, name); return method.Invoke(target, args);
        }
        private static ReactionEvent Event(ReactionKind kind, int sequence, ReactionTriggerKind trigger = ReactionTriggerKind.StatusPair) =>
            new ReactionEvent(1, sequence, kind, trigger, new TowerId(1), new EnemyId(1), new PathPosition(1, 0),
                kind == ReactionKind.Steam ? StatusKind.Burn : StatusKind.Wet,
                kind == ReactionKind.Steam ? StatusKind.Wet : StatusKind.Gust, 1.25f, 4);
        private static DamageResolvedEvent Hit(int correlation, int index, int id, int hpLoss, long tick = 1, ReactionKind kind = ReactionKind.Steam) =>
            new DamageResolvedEvent(tick, correlation + index + 1, DamageOrigin.Reaction, new TowerId(1), new EnemyId(id),
                new PathPosition(id - 1, 0), new PathPosition(id, 0), 999, 999, 999, hpLoss, 0, true, kind, correlation, index);
        private static void Consume(object state, ReactionEvent reaction) => Call(state, "Consume", reaction, Array.Empty<DamageResolvedEvent>());
        private static object Describe(EnemyState enemy, object state, long tick) => Required("EnemyStatusView").GetMethod("Describe").Invoke(null,
            new[] { enemy, (object)tick, Catalog(), Call(state, "GetMemory", enemy.Id), Read<double>(state, "ActiveTime") });
        private static void AssertSlots(EnemyState enemy, object state, long tick, string first, string second)
        {
            var slots = Describe(enemy, state, tick);
            Assert.That(Read<string>(slots, "First"), Does.Contain(first));
            Assert.That(Read<string>(slots, "Second"), Does.Contain(second));
        }
    }
}
