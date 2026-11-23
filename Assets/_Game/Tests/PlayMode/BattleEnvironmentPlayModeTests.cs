using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Veilward.Application;
using Veilward.Domain;
using Veilward.Presentation;
using Veilward.Tests.Shared;
using Object = UnityEngine.Object;

namespace Veilward.Tests.PlayMode
{
    public sealed class BattleEnvironmentPlayModeTests
    {
        [UnityTest]
        public IEnumerator Art404_AllCellsScreenRoutingBoundaryBlockedOccupiedAndProductionUpdate()
        {
            BattleSessionContext.Clear();
            yield return SceneManager.LoadSceneAsync("Battle"); yield return null;
            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            Assert.That(GameObject.Find("Sanctuary Environment ART4"), Is.Not.Null);
            Assert.That(battle.ChooseElement(new ChooseElementCommand("start-resonance", "flame")).Success, Is.True);
            var placement = battle.GetComponent<BattlePlacementController>();
            var route = typeof(BattlePlacementController).GetMethod("TryHandlePointer", BindingFlags.Instance | BindingFlags.NonPublic);
            var camera = Camera.main; var map = battle.Simulation.Grid.Map;
            var before = AbilityBossTrace.Snapshot(battle.Simulation);
            var checkedCells = 0;
            for (var y = map.Origin.Y; y < map.Origin.Y + map.Height; y++)
            for (var x = map.Origin.X; x < map.Origin.X + map.Width; x++)
            {
                placement.CancelSelection(); yield return null; yield return null;
                var cell = new GridCoordinate(x, y); var p = map.GetCellCenter(cell);
                var screen = camera.WorldToScreenPoint(new Vector3(p.X, 0, p.Y));
                route.Invoke(placement, new object[] { new Vector2(screen.x, screen.y), null });
                if (battle.Simulation.Grid.GetState(cell) == GridCellState.Available)
                {
                    Assert.That(placement.HasSelection, Is.True, cell.ToString());
                    Assert.That(placement.SelectedCell, Is.EqualTo(cell));
                }
                else Assert.That(placement.HasSelection, Is.False, cell.ToString());
                Assert.That(AbilityBossTrace.Snapshot(battle.Simulation), Is.EqualTo(before));
                checkedCells++;
            }
            Assert.That(checkedCells, Is.EqualTo(96)); placement.CancelSelection();
            foreach (var cell in new[] { new GridCoordinate(-7, -2), new GridCoordinate(6, 2),
                new GridCoordinate(-6, -5), new GridCoordinate(5, 4) })
            {
                Assert.That(placement.TrySelectCell(cell), Is.False);
                Assert.That(battle.PlaceTower(new TowerDefinitionId("flame"), cell).Success, Is.False);
            }
            foreach (var p in battle.Simulation.Path.Points)
                Assert.That(battle.PlaceTower(new TowerDefinitionId("flame"), p).Success, Is.False);
            Assert.That(AbilityBossTrace.Snapshot(battle.Simulation), Is.EqualTo(before));

            var chosen = new GridCoordinate(-4, -1);
            Assert.That(placement.TrySelectCell(chosen), Is.True);
            placement.SelectTowerType(new TowerDefinitionId("flame"));
            Assert.That(AbilityBossTrace.Snapshot(battle.Simulation), Is.EqualTo(before), "preview is free");
            placement.ConfirmSelection(); yield return null;
            Assert.That(battle.Simulation.State.Towers.Count, Is.EqualTo(1));
            var occupied = AbilityBossTrace.Snapshot(battle.Simulation);
            Assert.That(battle.PlaceTower(new TowerDefinitionId("flame"), chosen).Success, Is.False);
            Assert.That(AbilityBossTrace.Snapshot(battle.Simulation), Is.EqualTo(occupied));
            Assert.That(placement.TrySelectCell(chosen), Is.True);
            placement.RequestSellSelectedTower(); Assert.That(placement.IsSellConfirmationVisible, Is.True);
            Assert.That(placement.ConfirmSellSelectedTower().Success, Is.True);
            var sold = AbilityBossTrace.Snapshot(battle.Simulation);
            Assert.That(placement.ConfirmSellSelectedTower().Success, Is.False);
            Assert.That(AbilityBossTrace.Snapshot(battle.Simulation), Is.EqualTo(sold));

            Assert.That(battle.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
            var tick = battle.Simulation.State.Tick;
            yield return new WaitForSeconds(.25f);
            Assert.That(battle.enabled, Is.True);
            Assert.That(battle.Simulation.State.Tick, Is.GreaterThan(tick), "actual production Update advances combat");
            Assert.That(battle.Simulation.State.Enemies.Count, Is.GreaterThan(0));
            Assert.That(battle.Simulation.State.Enemies[0].DistanceAlongPath, Is.GreaterThan(0));
            BattleSessionContext.Clear();
        }
    }
}
