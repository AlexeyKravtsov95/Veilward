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
    public sealed class BattleReactionPlayModeTests : HistoricalAllOpenPlayModeFixture
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
                schemaVersion = WaveCheckpointStore.CurrentSchemaVersion, scenarioContentId = "eight-wave-v1", state = cp });
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
                schemaVersion = WaveCheckpointStore.CurrentSchemaVersion, scenarioContentId = "eight-wave-v1", state = checkpoint });
            yield return null;
            var restored = GameObject.Find("Tower 1").transform;
            Assert.That(restored.position, Is.EqualTo(battle.GetCellCenter(new GridCoordinate(-3, 0))));
            var binding = restored.GetComponent<FlameModelBinding>();
            Assert.That(binding.Model.name, Is.EqualTo("STORM L1"));
            Assert.That(binding.Model.GetComponentsInChildren<Renderer>().All(r => !r.HasPropertyBlock()), Is.True);
            Assert.That(restored.Find("Foundation").GetComponent<Renderer>().enabled, Is.False);
            Assert.That(restored.Find("Spirit").GetComponent<Renderer>().enabled, Is.False);
            Assert.That(original == null, Is.True, "Old view must not survive Restore.");
        }

        [UnityTest]
        public IEnumerator RegBattle160_ReactionKill_FileContinueAndRepeatedNextDoNotPay()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu"); yield return null;
            Object.FindFirstObjectByType<MainMenuController>().NewGameButton.onClick.Invoke(); yield return null; yield return null;
            var battle = Object.FindFirstObjectByType<BattleSceneController>(); battle.enabled = false;
            var store = new WaveCheckpointStore(_slot);
            BattleCheckpoint preparation = null;
            string expected = null;
            var prefix = new System.Collections.Generic.List<string>();
            var found = false;
            // Прежний basic script, настоящий бюджет и saved EightWave; без injection HP/Gold/status.
            for (var wave = 1; wave <= 8 && !found; wave++)
            {
                if (wave == 1)
                {
                    Assert.That(battle.PlaceTower(new TowerDefinitionId("flame"), new GridCoordinate(-2, -1)).Success, Is.True);
                    Assert.That(battle.PlaceTower(new TowerDefinitionId("gale"), new GridCoordinate(1, 0)).Success, Is.True);
                }
                if (wave == 3) Assert.That(battle.PlaceTower(new TowerDefinitionId("tide"), new GridCoordinate(-1, 0)).Success, Is.True);
                if (wave == 5) Assert.That(battle.PlaceTower(new TowerDefinitionId("stone"), new GridCoordinate(-3, -1)).Success, Is.True);
                preparation = battle.Simulation.ExportCheckpoint(); expected = FileState(battle.Simulation);
                Assert.That(battle.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
                var bytes = File.ReadAllBytes(store.PrimaryPath); prefix.Clear();
                prefix.Add(FileState(battle.Simulation));
                var sawTimestamp = false;
                for (var tick = 0; tick < 20000 && battle.Simulation.State.Phase == BattlePhase.Combat; tick++)
                {
                    battle.Simulation.Step(); prefix.Add(FileState(battle.Simulation));
                    sawTimestamp = battle.Simulation.State.Enemies.Any(e => e.LastSteamTick.HasValue || e.LastStormTick.HasValue);
                    found = battle.Simulation.DamageEvents.Any(d => d.Origin == DamageOrigin.Reaction && d.WasLethal) &&
                        sawTimestamp && battle.Simulation.State.Phase == BattlePhase.Combat;
                    if (found) break;
                    Drain(battle);
                }
                if (found)
                {
                    Assert.That(battle.Simulation.State.Gold, Is.GreaterThan(preparation.gold));
                    Assert.That(battle.Simulation.State.Kills, Is.GreaterThan(preparation.kills));
                    Assert.That(File.ReadAllBytes(store.PrimaryPath), Is.EqualTo(bytes));
                    Assert.That(battle.Simulation.ReactionEvents, Is.Not.Empty);
                    break;
                }
                Drain(battle);
                Assert.That(Object.FindFirstObjectByType<BattleCheckpointController>().ObservePhaseTransition(), Is.True);
                if (battle.Simulation.State.Phase != BattlePhase.WaveComplete) break;
                Assert.That(battle.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.Continued));
            }
            Assert.That(found, Is.True, "Требуется реальный reaction kill в saved матче");
            Assert.That(preparation.towers.Any(t => t.nextAttackTick > 0), Is.True);
            // Два настоящих menu Continue + полное повторение prefix, затем третий Continue для завершения волны.
            for (var repeat = 0; repeat < 3; repeat++)
            {
                yield return SceneManager.LoadSceneAsync("MainMenu"); yield return null;
                var menu = Object.FindFirstObjectByType<MainMenuController>();
                Assert.That(menu.ContinueButton.interactable, Is.True);
                menu.ContinueButton.onClick.Invoke(); yield return null; yield return null;
                battle = Object.FindFirstObjectByType<BattleSceneController>(); battle.enabled = false;
                Assert.That(FileState(battle.Simulation), Is.EqualTo(expected));
                Assert.That(JsonUtility.ToJson(battle.Simulation.ExportCheckpoint()), Is.EqualTo(JsonUtility.ToJson(preparation)));
                AssertClean(); Assert.That(battle.Simulation.State.Enemies, Is.Empty);
                Assert.That(battle.Simulation.AttackEvents, Is.Empty); Assert.That(battle.Simulation.ReactionEvents, Is.Empty); Assert.That(battle.Simulation.DamageEvents, Is.Empty);
                Assert.That(battle.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
                Assert.That(FileState(battle.Simulation), Is.EqualTo(prefix[0]));
                for (var i = 1; i < prefix.Count; i++)
                {
                    battle.Simulation.Step(); Assert.That(FileState(battle.Simulation), Is.EqualTo(prefix[i])); Drain(battle);
                }
            }
            for (var guard = 0; guard < 20000 && battle.Simulation.State.Phase == BattlePhase.Combat; guard++) { battle.Simulation.Step(); Drain(battle); }
            Assert.That(battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.WaveComplete));
            var checkpoint = Object.FindFirstObjectByType<BattleCheckpointController>();
            Assert.That(checkpoint.ObservePhaseTransition(), Is.True);
            var completed = JsonUtility.ToJson(battle.Simulation.ExportCheckpoint());
            var gold = battle.Simulation.State.Gold; var kills = battle.Simulation.State.Kills;
            for (var i = 0; i < 2; i++)
            {
                yield return SceneManager.LoadSceneAsync("MainMenu"); yield return null;
                Object.FindFirstObjectByType<MainMenuController>().ContinueButton.onClick.Invoke(); yield return null; yield return null;
                battle = Object.FindFirstObjectByType<BattleSceneController>(); battle.enabled = false;
                Assert.That(JsonUtility.ToJson(battle.Simulation.ExportCheckpoint()), Is.EqualTo(completed));
            }
            Assert.That(battle.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.Continued));
            var stable = FileState(battle.Simulation);
            Assert.That(battle.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.RejectedInvalidPhase));
            Assert.That(FileState(battle.Simulation), Is.EqualTo(stable));
            Assert.That(battle.Simulation.State.Gold, Is.EqualTo(gold)); Assert.That(battle.Simulation.State.Kills, Is.EqualTo(kills));
        }
        private static void Drain(BattleSceneController battle) => typeof(BattleSceneController).GetMethod("ProcessAttackEvents", Flags).Invoke(battle, null);
        private static string FileState(BattleSimulation s)
        {
            // JsonUtility не сериализует read-only properties событий, поэтому поля перечислены явно.
            var b = new System.Text.StringBuilder().Append(s.State.Phase).Append('|').Append(s.State.Tick).Append('|').Append(s.State.WaveIndex)
                .Append('|').Append(s.State.BaseHealth).Append('|').Append(s.State.Gold).Append('|').Append(s.State.Kills);
            if (s.State.Phase != BattlePhase.Combat) b.Append('|').Append(JsonUtility.ToJson(s.ExportCheckpoint()));
            foreach (var t in s.State.Towers) b.Append("|T:").Append(t.Id.Value).Append(',').Append(t.Definition.Id.Value).Append(',').Append(t.Level)
                .Append(',').Append(t.Coordinate.X).Append(',').Append(t.Coordinate.Y).Append(',').Append(t.InvestedGold).Append(',').Append(t.NextAttackTick);
            foreach (var e in s.State.Enemies)
            {
                b.Append("|E:").Append(e.Id.Value).Append(',').Append(e.Health).Append(',').Append(e.DistanceAlongPath.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
                    .Append(',').Append(e.LastSteamTick).Append(',').Append(e.LastStormTick);
                foreach (var st in e.Statuses) b.Append(';').Append(st.Kind).Append(',').Append(st.Power).Append(',').Append(st.RemainingTicks)
                    .Append(',').Append(st.NextDamageTick).Append(',').Append(st.DamageIntervalTicks).Append(',').Append(st.SourceTowerId.Value);
            }
            for (var y = s.Grid.Map.Origin.Y; y < s.Grid.Map.Origin.Y + s.Grid.Map.Height; y++)
                for (var x = s.Grid.Map.Origin.X; x < s.Grid.Map.Origin.X + s.Grid.Map.Width; x++) b.Append('|').Append(s.Grid.GetState(new GridCoordinate(x, y)));
            foreach (var a in s.AttackEvents) b.Append("|A:").Append(a.Tick).Append(',').Append(a.AttackerTowerId.Value).Append(',').Append(a.TargetEnemyId.Value)
                .Append(',').Append(a.Damage).Append(',').Append(a.WasLethal).Append(',').Append(a.TargetPosition.X).Append(',').Append(a.TargetPosition.Y);
            foreach (var e in s.ReactionEvents) b.Append("|R:").Append(e.Tick).Append(',').Append(e.SequenceInTick).Append(',').Append(e.Kind).Append(',').Append(e.TriggerKind)
                .Append(',').Append(e.SourceTowerId.Value).Append(',').Append(e.PrimaryEnemyId.Value).Append(',').Append(e.RadiusOrHopRange).Append(',').Append(e.TargetLimit);
            foreach (var d in s.DamageEvents) b.Append("|D:").Append(d.Tick).Append(',').Append(d.SequenceInTick).Append(',').Append(d.Origin).Append(',').Append(d.SourceTowerId.Value)
                .Append(',').Append(d.TargetEnemyId.Value).Append(',').Append(d.AppliedHpLoss).Append(',').Append(d.HealthAfter).Append(',').Append(d.WasLethal)
                .Append(',').Append(d.ReactionKind).Append(',').Append(d.ReactionSequenceInTick).Append(',').Append(d.HitIndex);
            return b.ToString();
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
