using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Veilward.Application;
using Veilward.Domain;
using Veilward.Presentation;
using Object = UnityEngine.Object;

namespace Veilward.Tests.PlayMode
{
    public sealed class BattleEnemyInspectorWorldTapTests
    {
        private static readonly GridCoordinate Cell = new GridCoordinate(-4, 0);
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private BattleSceneController _battle;
        private BattlePlacementController _placement;
        private EnemyInspectorController _inspector;

        [SetUp] public void SetUp()
        {
            BattleSessionContext.Clear();
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            BattleSessionContext.Clear();
            yield return SceneManager.LoadSceneAsync("Battle");
            yield return null;
        }

        [UnityTest]
        public IEnumerator RegBattle173_WorldTapTransfersBuiltTowerSelectionToEnemyInspector()
        {
            yield return PrepareCombat();
            yield return WorldTap(_battle.GetCellCenter(Cell));
            Assert.That(_placement.SelectedTowerId, Is.EqualTo(new TowerId(1)));
            Assert.That(_battle.ActiveModalOwner, Is.EqualTo(BattleInputOwner.Placement));
            var before = Snapshot();
            var enemy = _battle.Simulation.State.Enemies[0];
            yield return TapEnemy(enemy);
            Assert.That(_inspector.SelectedEnemyId, Is.EqualTo(enemy.Id));
            Assert.That(_placement.HasSelection, Is.False);
            Assert.That(_placement.SelectedTowerId, Is.Null);
            Assert.That(_battle.ActiveModalOwner, Is.EqualTo(BattleInputOwner.Inspector));
            Assert.That(Snapshot(), Is.EqualTo(before), "Читающий world tap не меняет симуляцию/экономику");
        }

        [UnityTest]
        public IEnumerator RegBattle173_WorldTapSwitchesInspectedEnemyWithoutPlacementClickThrough()
        {
            yield return PrepareCombat();
            var first = _battle.Simulation.State.Enemies[0];
            var second = _battle.Simulation.State.Enemies[1];
            yield return TapEnemy(first);
            Assert.That(_inspector.SelectedEnemyId, Is.EqualTo(first.Id));
            var before = Snapshot();
            yield return TapEnemy(second);
            Assert.That(_inspector.SelectedEnemyId, Is.EqualTo(second.Id));
            Assert.That(_inspector.Details, Does.Contain("Враг #" + second.Id.Value));
            Assert.That(_placement.HasSelection, Is.False);
            Assert.That(_placement.SelectedTowerId, Is.Null);
            Assert.That(Snapshot(), Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator RegBattle173_WorldEnemyTapsKeepPauseSellAndElementChoiceBlocked()
        {
            foreach (var blocker in new[] { "pause", "sell", "choice" })
            {
                yield return LoadBattle();
                if (blocker != "choice")
                {
                    Assert.That(Object.FindFirstObjectByType<ElementChoiceController>().SelectOption("flame"), Is.True);
                    Assert.That(Object.FindFirstObjectByType<ElementChoiceController>().ConfirmChoice().Success, Is.True);
                    Assert.That(_placement.TrySelectCell(Cell), Is.True);
                    _placement.SelectTowerType(new TowerDefinitionId("flame")); _placement.ConfirmSelection();
                }
                if (blocker == "sell")
                {
                    Assert.That(_placement.TrySelectCell(Cell), Is.True);
                    _placement.RequestSellSelectedTower();
                    Assert.That(_placement.IsSellConfirmationVisible, Is.True);
                }
                if (blocker == "pause") Object.FindFirstObjectByType<BattlePauseController>().Pause();
                // Synthetic enemy в safe phase: изолирует input guard, не доказывает gameplay/replay.
                var enemy = new EnemyState(new EnemyId(99), new EnemyDefinition(new EnemyDefinitionId("input-fixture"), 100, 1f, 1, 0), 100, 2f);
                typeof(BattleState).GetMethod("AddEnemy", Flags).Invoke(_battle.Simulation.State, new object[] { enemy });
                var before = Snapshot();
                yield return TapEnemy(enemy, requireUncovered: false);
                Assert.That(_inspector.SelectedEnemyId, Is.Null, blocker);
                Assert.That(_inspector.TrySelectAt(EnemyScreen(enemy)), Is.False, blocker);
                Assert.That(Snapshot(), Is.EqualTo(before), blocker);
                if (blocker == "sell") Assert.That(_placement.IsSellConfirmationVisible, Is.True);
                if (blocker == "choice") Assert.That(_battle.Simulation.State.ElementProgression.PendingChoiceId, Is.EqualTo("start-resonance"));
            }
        }

        private IEnumerator PrepareCombat()
        {
            yield return LoadBattle();
            var choice = Object.FindFirstObjectByType<ElementChoiceController>();
            choice.SelectOption("flame"); Assert.That(choice.ConfirmChoice().Success, Is.True);
            _placement.TrySelectCell(Cell); _placement.SelectTowerType(new TowerDefinitionId("flame")); _placement.ConfirmSelection();
            _placement.StartWave();
            for (var i = 0; i < 55; i++) _battle.Simulation.Step();
            Assert.That(_battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.Combat));
            Assert.That(_battle.Simulation.State.Enemies.Count, Is.EqualTo(2));
            // Фиксируем реальный Domain tick55; Presentation продолжает обрабатывать Input System.
            typeof(BattleSceneController).GetMethod("LateUpdate", Flags).Invoke(_battle, null);
            yield return null;
        }
        private IEnumerator LoadBattle()
        {
            yield return SceneManager.LoadSceneAsync("Battle"); yield return null;
            _battle = Object.FindFirstObjectByType<BattleSceneController>(); _battle.enabled = false;
            _placement = Object.FindFirstObjectByType<BattlePlacementController>();
            _inspector = Object.FindFirstObjectByType<EnemyInspectorController>();
            yield return null;
        }
        private Vector2 EnemyScreen(EnemyState enemy)
        {
            var p = _battle.Simulation.Path.GetPosition(enemy.DistanceAlongPath);
            return Camera.main.WorldToScreenPoint(new Vector3(p.X, .6f, p.Y));
        }
        private IEnumerator TapEnemy(EnemyState enemy, bool requireUncovered = true) => PointerTap(EnemyScreen(enemy), requireUncovered);
        private IEnumerator WorldTap(Vector3 point) => PointerTap(Camera.main.WorldToScreenPoint(point), true);
        private IEnumerator PointerTap(Vector2 point, bool requireUncovered)
        {
            if (requireUncovered)
            {
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
                Assert.That(hits, Is.Empty, "Положительный regression должен тапать мир, не UI overlay");
            }
            // Тот же world-tap boundary, который вызывают production mouse/touch callbacks.
            typeof(BattlePlacementController).GetMethod("TryHandlePointer", Flags)
                .Invoke(_placement, new object[] { point, null });
            yield return null;
        }
        private string Snapshot()
        {
            var s = _battle.Simulation.State;
            return $"{s.Phase}|{s.Tick}|{s.Gold}|{s.BaseHealth}|{s.Kills}|{s.ElementProgression.PendingChoiceId}|" +
                string.Join(";", s.Towers.Select(t => $"{t.Id.Value},{t.Definition.Id.Value},{t.Level},{t.InvestedGold},{t.NextAttackTick}")) + "|" +
                string.Join(";", s.Enemies.Select(e => $"{e.Id.Value},{e.Health},{e.DistanceAlongPath:R}"));
        }
    }
}
