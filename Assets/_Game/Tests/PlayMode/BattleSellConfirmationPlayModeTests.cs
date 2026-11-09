using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Veilward.Domain;
using Veilward.Presentation;

namespace Veilward.Tests.PlayMode
{
    public sealed class BattleSellConfirmationPlayModeTests
    {
        [UnityTest]
        public IEnumerator Battle_SellConfirmation_EnforcesModalContract()
        {
            yield return SceneManager.LoadSceneAsync("Battle", LoadSceneMode.Single);
            yield return null;

            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            var placement = Object.FindFirstObjectByType<BattlePlacementController>();
            var firstCell = new GridCoordinate(-1, 0);
            var secondCell = new GridCoordinate(1, 0);
            PlaceTower(placement, firstCell);
            yield return null;
            var firstId = battle.Simulation.State.Towers[0].Id;
            Assert.That(placement.TrySelectCell(firstCell), Is.True);

            var bypass = placement.ConfirmSellSelectedTower();

            Assert.That(bypass.Success, Is.False);
            Assert.That(battle.Simulation.State.Gold, Is.EqualTo(90));
            Assert.That(battle.Simulation.State.Towers.Count, Is.EqualTo(1));
            Assert.That(battle.Simulation.Grid.GetState(firstCell), Is.EqualTo(GridCellState.Occupied));

            placement.RequestSellSelectedTower();

            Assert.That(placement.UpgradeButton.interactable, Is.False);
            Assert.That(FindButton("Sell Button").interactable, Is.False);
            Assert.That(FindButton("Start Wave Button").interactable, Is.False);
            Assert.That(FindButton("Next Wave Button").interactable, Is.False);
            Assert.That(FindButton("Confirm Sell Button").interactable, Is.True);
            Assert.That(FindButton("Cancel Sell Button").interactable, Is.True);
            Assert.That(placement.UpgradeSelectedTower().Success, Is.False);
            placement.StartWave();
            Assert.That(battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.Preparation));
            Assert.That(battle.Simulation.State.Gold, Is.EqualTo(90));

            FindButton("Cancel Sell Button").onClick.Invoke();

            Assert.That(placement.IsSellConfirmationVisible, Is.False);
            Assert.That(battle.Simulation.State.Gold, Is.EqualTo(90));
            Assert.That(battle.Simulation.State.Towers.Count, Is.EqualTo(1));
            Assert.That(placement.UpgradeButton.interactable, Is.True);
            Assert.That(FindButton("Sell Button").interactable, Is.True);
            Assert.That(FindButton("Start Wave Button").interactable, Is.True);
            Assert.That(placement.UpgradeSelectedTower().Success, Is.True);
            Assert.That(battle.Simulation.State.Gold, Is.EqualTo(40));

            CompleteCurrentWave(battle);
            battle.ContinueToNextWave();
            yield return null;
            PlaceTower(placement, secondCell);
            yield return null;
            var secondId = battle.Simulation.State.Towers.Single(tower => tower.Id != firstId).Id;
            Assert.That(battle.Simulation.State.Gold, Is.EqualTo(25));

            Assert.That(placement.TrySelectCell(firstCell), Is.True);
            placement.RequestSellSelectedTower();
            Assert.That(placement.TrySelectCell(secondCell), Is.True);
            var wrongTowerSale = placement.ConfirmSellSelectedTower();

            Assert.That(wrongTowerSale.Success, Is.False);
            Assert.That(placement.SelectedTowerId, Is.EqualTo(secondId));
            Assert.That(battle.Simulation.State.Towers.Count, Is.EqualTo(2));
            Assert.That(battle.Simulation.State.Gold, Is.EqualTo(25));

            Assert.That(placement.TrySelectCell(firstCell), Is.True);
            placement.RequestSellSelectedTower();
            var sale = placement.ConfirmSellSelectedTower();

            Assert.That(sale.Success, Is.True, sale.FailureReason.ToString());
            Assert.That(sale.Refund, Is.EqualTo(77));
            Assert.That(battle.Simulation.State.Gold, Is.EqualTo(102));
            Assert.That(battle.Simulation.State.Towers.Single().Id, Is.EqualTo(secondId));
            Assert.That(battle.Simulation.Grid.GetState(firstCell), Is.EqualTo(GridCellState.Available));
            Assert.That(battle.Simulation.Grid.GetState(secondCell), Is.EqualTo(GridCellState.Occupied));
            Assert.That(placement.HasSelection, Is.False);
            Assert.That(placement.IsSellConfirmationVisible, Is.False);
            Assert.That(FindButton("Confirm Sell Button").gameObject.activeInHierarchy, Is.False);
        }

        private static void PlaceTower(BattlePlacementController placement, GridCoordinate coordinate)
        {
            Assert.That(placement.TrySelectCell(coordinate), Is.True);
            placement.ConfirmSelection();
        }

        private static Button FindButton(string name)
        {
            return Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(button => button.name == name);
        }

        private static void CompleteCurrentWave(BattleSceneController battle)
        {
            battle.StartWave();
            var guard = 0;
            while (battle.Simulation.State.Phase == BattlePhase.Combat && guard++ < 20000)
            {
                battle.Simulation.Step();
                battle.Simulation.ClearAttackEvents();
            }

            Assert.That(guard, Is.LessThan(20000), "Battle wave did not complete within the test guard.");
        }
    }
}
