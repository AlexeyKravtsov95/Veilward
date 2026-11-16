using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Veilward.Application;
using Veilward.Domain;
using Veilward.Infrastructure;
using Veilward.Presentation;
using Object = UnityEngine.Object;

namespace Veilward.Tests.PlayMode
{
    public sealed class BattleReactionPlayModeTests
    {
        private string _slot;
        [SetUp] public void SetUp()
        {
            _slot = Path.Combine(UnityEngine.Application.temporaryCachePath, "p22e-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_slot);
            MainMenuController.CheckpointStoreFactory = () => new WaveCheckpointStore(_slot);
            BattleSessionContext.Clear();
        }
        [TearDown] public void TearDown()
        {
            MainMenuController.CheckpointStoreFactory = null;
            BattleSessionContext.Clear();
            if (Directory.Exists(_slot)) Directory.Delete(_slot, true);
        }

        [UnityTest]
        public IEnumerator RegBattle154_HybridPreviewAndOccupiedPanelStayCompact()
        {
            yield return Load();
            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            var placement = Object.FindFirstObjectByType<BattlePlacementController>();
            foreach (var id in new[] { "steam", "storm" })
            {
                placement.TrySelectCell(new GridCoordinate(-4, 0));
                placement.SelectTowerType(new TowerDefinitionId(id)); yield return null;
                Assert.That(placement.BuildStatsText.text, Does.Contain("встроенная"));
                Assert.That(placement.BuildStatsText.preferredHeight, Is.LessThanOrEqualTo(placement.BuildStatsText.rectTransform.rect.height));
                placement.ConfirmSelection(); yield return null;
                placement.TrySelectCell(new GridCoordinate(-4, 0)); yield return null;
                Assert.That(placement.TowerStatsText.text, Does.Contain("встроенная").And.Contain("Sell refund:"));
                Assert.That(placement.TowerStatsText.preferredHeight, Is.LessThanOrEqualTo(placement.TowerStatsText.rectTransform.rect.height));
                Assert.That(Button("Sell Button").GetComponent<RectTransform>().rect.height, Is.GreaterThanOrEqualTo(56));
                placement.RequestSellSelectedTower(); placement.ConfirmSellSelectedTower();
            }
        }

        [UnityTest]
        public IEnumerator RegBattle156_InspectorShowsEveryStatusCooldownAndClosesOnDeath()
        {
            yield return Load();
            var battle = Object.FindFirstObjectByType<BattleSceneController>(); battle.enabled = false;
            battle.StartWave();
            var enemy = battle.Simulation.State.Enemies[0];
            foreach (StatusKind k in Enum.GetValues(typeof(StatusKind)))
                typeof(EnemyState).GetMethod("ApplyStatus", Flags).Invoke(enemy, new object[] {
                    new StatusEffectDefinition(k, 7, 90, k == StatusKind.Burn ? 30 : 0), new TowerId(9), 0L });
            var inspector = Inspector(); Assert.That((bool)Call(inspector, "Select", enemy.Id), Is.True);
            Call(inspector, "Refresh");
            var text = (string)inspector.GetType().GetProperty("Details").GetValue(inspector);
            foreach (StatusKind k in Enum.GetValues(typeof(StatusKind))) Assert.That(text, Does.Contain(k.ToString()));
            Assert.That(text, Does.Contain("power 7").And.Contain("90 ticks").And.Contain("Steam CD: 0").And.Contain("Storm CD: 0"));
            Assert.That(enemy.Health, Is.EqualTo(enemy.Definition.MaxHealth));
            foreach (ReactionKind kind in Enum.GetValues(typeof(ReactionKind)))
                typeof(EnemyState).GetMethod("ReserveReaction", Flags).Invoke(enemy, new object[] { kind, 0L });
            var steam = new ReactionEvent(0, 1, ReactionKind.Steam, ReactionTriggerKind.StatusPair, new TowerId(9), enemy.Id,
                new PathPosition(0, 0), StatusKind.Burn, StatusKind.Wet, 1.25f, 4);
            var storm = new ReactionEvent(0, 5, ReactionKind.Storm, ReactionTriggerKind.Intrinsic, new TowerId(9), enemy.Id,
                new PathPosition(0, 0), StatusKind.Wet, StatusKind.Gust, 2, 2);
            var steamHit = new DamageResolvedEvent(0, 2, DamageOrigin.Reaction, new TowerId(9), enemy.Id, new PathPosition(0, 0),
                new PathPosition(0, 0), 999, 999, 999, 4, 996, false, ReactionKind.Steam, 1, 0);
            var stormHit = new DamageResolvedEvent(0, 6, DamageOrigin.Reaction, new TowerId(9), new EnemyId(99), new PathPosition(0, 0),
                new PathPosition(1, 0), 999, 999, 999, 6, 994, false, ReactionKind.Storm, 5, 1);
            // Синтетические captured events: inspector отображает applied, не configured 999.
            battle.Presentation.Consume(steam, new[] { steamHit, stormHit });
            battle.Presentation.Consume(storm, new[] { steamHit, stormHit });
            Call(inspector, "Select", enemy.Id);
            text = (string)inspector.GetType().GetProperty("Details").GetValue(inspector);
            Assert.That(text, Does.Contain("Steam CD: 60").And.Contain("Storm CD: 60").And.Contain("Пламя + Прилив → Пар").And.Contain("STORM: встроенная"));
            Assert.That(text, Does.Contain("AppliedHpLoss 4 HP").And.Contain("AppliedHpLoss 6 HP").And.Not.Contain("999"));
            Assert.That(enemy.Health, Is.EqualTo(enemy.Definition.MaxHealth), "Read-only inspector cannot apply captured damage.");
            typeof(EnemyState).GetMethod("ApplyDamage", Flags).Invoke(enemy, new object[] { enemy.Health });
            Call(inspector, "Refresh");
            Assert.That(inspector.GetType().GetProperty("SelectedEnemyId").GetValue(inspector), Is.Null);
        }

        [UnityTest]
        public IEnumerator RegBattle157_MultipleFixedStepsDrainEveryStreamAndRestoreClearsVisuals()
        {
            yield return Load();
            var battle = Object.FindFirstObjectByType<BattleSceneController>(); battle.enabled = false;
            var cp = battle.Simulation.ExportCheckpoint();
            battle.PlaceTower(new TowerDefinitionId("steam"), new GridCoordinate(-4, 0));
            battle.StartWave();
            var process = typeof(BattleSceneController).GetMethod("ProcessAttackEvents", Flags);
            var found = false;
            for (var i = 0; i < 200; i++)
            {
                battle.Simulation.Step(); found |= battle.Simulation.ReactionEvents.Count > 0;
                process.Invoke(battle, null);
                Assert.That(battle.Simulation.AttackEvents, Is.Empty);
                Assert.That(battle.Simulation.ReactionEvents, Is.Empty);
                Assert.That(battle.Simulation.DamageEvents, Is.Empty);
            }
            Assert.That(found, Is.True);
            var state = typeof(BattleSceneController).GetProperty("Presentation").GetValue(battle);
            Assert.That((string)state.GetType().GetProperty("LastSummary").GetValue(state), Does.Contain("HP"));
            Object.FindFirstObjectByType<BattleCheckpointController>().RestoreCheckpoint(new WaveCheckpointEnvelope {
                schemaVersion = 2, scenarioContentId = "eight-wave-v1", state = cp });
            Assert.That((string)state.GetType().GetProperty("LastSummary").GetValue(state), Is.Empty);
            Assert.That(battle.GetComponentsInChildren(Required("ReactionEffectView"), false), Is.Empty);
        }

        [UnityTest]
        public IEnumerator RegBattle158_PauseBackgroundResumeAndInspectorTapDoNotClickThrough()
        {
            yield return Load();
            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            var placement = Object.FindFirstObjectByType<BattlePlacementController>();
            var pause = Object.FindFirstObjectByType<BattlePauseController>();
            placement.StartWave(); yield return null;
            var enemy = battle.Simulation.State.Enemies[0];
            yield return null;
            var screen = Camera.main.WorldToScreenPoint(new Vector3(-5, .6f, -2));
            // Точная проекция живого врага, а не тап по строительной клетке.
            var p = battle.Simulation.Path.GetPosition(enemy.DistanceAlongPath);
            screen = Camera.main.WorldToScreenPoint(new Vector3(p.X, .6f, p.Y));
            typeof(BattlePlacementController).GetMethod("TryHandlePointer", Flags).Invoke(placement, new object[] { (Vector2)screen, null });
            Assert.That(Inspector().GetType().GetProperty("SelectedEnemyId").GetValue(Inspector()), Is.EqualTo(enemy.Id));
            Assert.That(placement.HasSelection, Is.False);
            pause.OnApplicationPause(true); yield return null;
            var tick = battle.Simulation.State.Tick;
            var state = typeof(BattleSceneController).GetProperty("Presentation").GetValue(battle);
            var clock = state.GetType().GetProperty("ActiveTime").GetValue(state);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(battle.Simulation.State.Tick, Is.EqualTo(tick));
            Assert.That(state.GetType().GetProperty("ActiveTime").GetValue(state), Is.EqualTo(clock));
            Assert.That((bool)Call(Inspector(), "Select", enemy.Id), Is.False);
            pause.OnApplicationPause(false); Assert.That(pause.Resume(), Is.True);
            var oldTick = battle.Simulation.State.Tick;
            typeof(BattleSceneController).GetMethod("Update", Flags).Invoke(battle, null);
            Assert.That(battle.Simulation.State.Tick, Is.EqualTo(oldTick));
            Assert.That(placement.HasSelection, Is.False);
        }

        [UnityTest]
        public IEnumerator RegBattle158_SyntheticCueAndVfxFreezeAcrossManualAndBackgroundPause()
        {
            yield return Load();
            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            var pause = Object.FindFirstObjectByType<BattlePauseController>();
            battle.StartWave();
            var enemy = battle.Simulation.State.Enemies[0];
            var p = battle.Simulation.Path.GetPosition(enemy.DistanceAlongPath);
            var reaction = new ReactionEvent(1, 1, ReactionKind.Steam, ReactionTriggerKind.StatusPair,
                new TowerId(99), enemy.Id, p, StatusKind.Burn, StatusKind.Wet, 1.25f, 4);
            // Синтетический event проверяет только presentation/lifecycle, без заявления о балансе.
            battle.Presentation.Consume(reaction, Array.Empty<DamageResolvedEvent>());
            var effect = battle.GetComponentsInChildren<ReactionEffectView>(true)[0];
            effect.Play(reaction, Array.Empty<DamageResolvedEvent>());
            pause.Pause(); yield return null;
            var clock = battle.Presentation.ActiveTime; var lifetime = effect.RemainingSeconds;
            var tick = battle.Simulation.State.Tick; var hp = enemy.Health;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(effect.RemainingSeconds, Is.EqualTo(lifetime));
            Assert.That(battle.Presentation.ActiveTime, Is.EqualTo(clock));
            Assert.That(enemy.Health, Is.EqualTo(hp)); Assert.That(battle.Simulation.State.Tick, Is.EqualTo(tick));
            Assert.That(pause.Resume(), Is.True);
            typeof(BattleSceneController).GetMethod("Update", Flags).Invoke(battle, null);
            Assert.That(effect.RemainingSeconds, Is.EqualTo(lifetime));
            pause.OnApplicationFocus(false); yield return new WaitForSecondsRealtime(.15f);
            Assert.That(effect.RemainingSeconds, Is.EqualTo(lifetime));
            pause.OnApplicationFocus(true); Assert.That(pause.Resume(), Is.True);
            yield return null; yield return null;
            Assert.That(effect.RemainingSeconds, Is.LessThan(lifetime));
        }

        [UnityTest]
        public IEnumerator RegBattle157_SyntheticBurstReusesBoundedPoolAndConsumerExceptionClearsStreams()
        {
            yield return Load();
            var battle = Object.FindFirstObjectByType<BattleSceneController>(); battle.enabled = false;
            var reactions = (System.Collections.Generic.List<ReactionEvent>)typeof(BattleSimulation).GetField("_reactionEvents", Flags).GetValue(battle.Simulation);
            for (var i = 0; i < 100; i++) reactions.Add(new ReactionEvent(i + 1, 1, ReactionKind.Steam,
                ReactionTriggerKind.Intrinsic, new TowerId(1), new EnemyId(1), new PathPosition(0, 0), StatusKind.Burn, StatusKind.Wet, 1.25f, 4));
            typeof(BattleSceneController).GetMethod("ProcessAttackEvents", Flags).Invoke(battle, null);
            Assert.That(battle.GetComponentsInChildren<ReactionEffectView>(true).Length, Is.EqualTo(24));
            Assert.That(battle.GetComponentsInChildren<ReactionEffectView>(false).Length, Is.EqualTo(24));
            Assert.That(battle.Simulation.ReactionEvents, Is.Empty);
            battle.ResetPresentation();
            Assert.That(battle.GetComponentsInChildren<ReactionEffectView>(false), Is.Empty);
            Assert.That(battle.PlaceTower(new TowerDefinitionId("steam"), new GridCoordinate(-4, 0)).Success, Is.True);
            battle.StartWave(); battle.Simulation.Step();
            var views = (System.Collections.Generic.Dictionary<TowerId, Transform>)typeof(BattleSceneController).GetField("_towerViews", Flags).GetValue(battle);
            views[new TowerId(1)] = null;
            Assert.Throws<TargetInvocationException>(() => typeof(BattleSceneController).GetMethod("ProcessAttackEvents", Flags).Invoke(battle, null));
            Assert.That(battle.Simulation.AttackEvents, Is.Empty);
            Assert.That(battle.Simulation.ReactionEvents, Is.Empty);
            Assert.That(battle.Simulation.DamageEvents, Is.Empty);
        }

        [UnityTest]
        public IEnumerator RegBattle159_ContinueAndNewGameDiscardOldSelectionCuesAndEffects()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu"); yield return null;
            Object.FindFirstObjectByType<MainMenuController>().NewGameButton.onClick.Invoke(); yield return null; yield return null;
            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            battle.StartWave(); yield return null;
            Assert.That((bool)Call(Inspector(), "Select", battle.Simulation.State.Enemies[0].Id), Is.True);
            yield return SceneManager.LoadSceneAsync("MainMenu"); yield return null;
            Object.FindFirstObjectByType<MainMenuController>().ContinueButton.onClick.Invoke(); yield return null; yield return null;
            AssertClean();
            yield return SceneManager.LoadSceneAsync("MainMenu"); yield return null;
            Object.FindFirstObjectByType<MainMenuController>().NewGameButton.onClick.Invoke(); yield return null; yield return null;
            AssertClean();
        }

        [UnityTest]
        public IEnumerator RegBattle157_RestoreSameIdAndLevelRebuildsChangedTowerTypeAndCell()
        {
            yield return Load();
            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            Assert.That(battle.PlaceTower(new TowerDefinitionId("steam"), new GridCoordinate(-4, 0)).Success, Is.True);
            yield return null;
            var original = GameObject.Find("Tower 1").transform;
            var checkpoint = battle.Simulation.ExportCheckpoint();
            checkpoint.towers[0].towerTypeId = "storm"; checkpoint.towers[0].x = -3;
            Object.FindFirstObjectByType<BattleCheckpointController>().RestoreCheckpoint(new WaveCheckpointEnvelope {
                schemaVersion = 2, scenarioContentId = "eight-wave-v1", state = checkpoint });
            yield return null;
            var restored = GameObject.Find("Tower 1").transform;
            Assert.That(restored.position, Is.EqualTo(battle.GetCellCenter(new GridCoordinate(-3, 0))));
            var block = new MaterialPropertyBlock(); restored.GetComponentInChildren<Renderer>().GetPropertyBlock(block);
            Assert.That(Vector4.Distance(block.GetColor("_BaseColor"), EnemyStatusView.StormColor), Is.LessThan(.0001f));
            Assert.That(original == null, Is.True, "Old view must not survive Restore.");
        }

        private static void AssertClean()
        {
            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            var state = typeof(BattleSceneController).GetProperty("Presentation").GetValue(battle);
            Assert.That((string)state.GetType().GetProperty("LastSummary").GetValue(state), Is.Empty);
            Assert.That(Inspector().GetType().GetProperty("SelectedEnemyId").GetValue(Inspector()), Is.Null);
            Assert.That(battle.GetComponentsInChildren(Required("ReactionEffectView"), false), Is.Empty);
        }
        private static IEnumerator Load() { yield return SceneManager.LoadSceneAsync("Battle"); yield return null; }
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Type Required(string name)
        {
            var type = typeof(TowerDisplay).Assembly.GetType("Veilward.Presentation." + name);
            Assert.That(type, Is.Not.Null, "Missing " + name); return type;
        }
        private static Component Inspector() { var result = Object.FindFirstObjectByType(Required("EnemyInspectorController")); Assert.That(result, Is.Not.Null); return (Component)result; }
        private static object Call(object o, string method, params object[] args) => o.GetType().GetMethod(method).Invoke(o, args);
        private static Button Button(string name) => Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(b => b.name == name);
    }
}
