using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Veilward.Domain;
using Veilward.Presentation;

namespace Veilward.Tests.PlayMode
{
    public sealed class BattleSmokeTests
    {
        [UnityTest]
        public IEnumerator Battle_LoadsIntoPreparation()
        {
            yield return SceneManager.LoadSceneAsync("Battle", LoadSceneMode.Single);
            yield return null;

            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            Assert.That(battle, Is.Not.Null);
            Assert.That(battle.Simulation, Is.Not.Null);
            Assert.That(battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.Preparation));
            Assert.That(battle.Simulation.State.WaveIndex, Is.Zero);
            Assert.That(battle.Simulation.State.WaveCount, Is.EqualTo(8));
            Assert.That(battle.Simulation.State.Gold, Is.EqualTo(150));
            Assert.That(battle.Simulation.State.BaseHealth, Is.EqualTo(20));
            Assert.That(battle.Simulation.State.Towers, Is.Empty);
            Assert.That(battle.Simulation.State.Enemies, Is.Empty);
        }

        [UnityTest]
        public IEnumerator Battle_TowerEconomyFlow_UpgradesSellsAndRebuilds()
        {
            yield return SceneManager.LoadSceneAsync("Battle", LoadSceneMode.Single);
            yield return null;

            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            var placement = Object.FindFirstObjectByType<BattlePlacementController>();
            var cell = new GridCoordinate(-1, 0);
            Assert.That(placement.TrySelectCell(cell), Is.True);
            placement.ConfirmSelection();
            yield return null;
            var soldId = battle.Simulation.State.Towers[0].Id;

            Assert.That(placement.TrySelectCell(cell), Is.True);
            Assert.That(placement.SelectedTowerId, Is.EqualTo(soldId));
            Assert.That(placement.TowerStatsText.text, Does.Contain("Level: 1"));
            Assert.That(placement.TowerStatsText.text, Does.Contain("Damage: 25"));
            Assert.That(placement.TowerStatsText.text, Does.Contain("Upgrade: 50"));
            Assert.That(placement.TowerStatsText.text, Does.Contain("Sell refund: 42"));
            Assert.That(placement.UpgradeButton.interactable, Is.True);

            CompleteCurrentWave(battle);
            Assert.That(battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.WaveComplete));
            Assert.That(battle.Simulation.State.Kills, Is.EqualTo(5));
            battle.ContinueToNextWave();
            yield return null;
            Assert.That(placement.TrySelectCell(cell), Is.True);

            var levelTwo = placement.UpgradeSelectedTower();
            Assert.That(levelTwo.Success, Is.True, levelTwo.FailureReason.ToString());
            yield return null;
            Assert.That(placement.TowerStatsText.text, Does.Contain("Level: 2"));
            Assert.That(placement.TowerStatsText.text, Does.Contain("Damage: 35"));
            Assert.That(placement.TowerStatsText.text, Does.Contain("Sell refund: 77"));

            var levelThree = placement.UpgradeSelectedTower();
            Assert.That(levelThree.Success, Is.True, levelThree.FailureReason.ToString());
            yield return null;
            Assert.That(placement.TowerStatsText.text, Does.Contain("Level: 3"));
            Assert.That(placement.TowerStatsText.text, Does.Contain("Damage: 50"));
            Assert.That(placement.TowerStatsText.text, Does.Contain("Upgrade: MAX"));
            Assert.That(placement.TowerStatsText.text, Does.Contain("Sell refund: 133"));
            Assert.That(placement.UpgradeButton.interactable, Is.False);

            placement.RequestSellSelectedTower();
            Assert.That(placement.IsSellConfirmationVisible, Is.True);
            Assert.That(battle.Simulation.State.Towers.Count, Is.EqualTo(1));
            placement.CancelSellConfirmation();
            Assert.That(placement.IsSellConfirmationVisible, Is.False);
            Assert.That(battle.Simulation.State.Towers.Count, Is.EqualTo(1));
            placement.RequestSellSelectedTower();
            var sale = placement.ConfirmSellSelectedTower();
            Assert.That(sale.Success, Is.True, sale.FailureReason.ToString());
            Assert.That(sale.Refund, Is.EqualTo(133));
            yield return null;
            Assert.That(battle.Simulation.State.Gold, Is.EqualTo(138));
            Assert.That(battle.Simulation.State.Towers, Is.Empty);
            Assert.That(battle.Simulation.Grid.GetState(cell), Is.EqualTo(GridCellState.Available));
            Assert.That(placement.HasSelection, Is.False);
            Assert.That(GameObject.Find($"Tower {soldId.Value}"), Is.Null);

            Assert.That(placement.TrySelectCell(cell), Is.True);
            placement.ConfirmSelection();
            yield return null;
            Assert.That(battle.Simulation.State.Towers.Count, Is.EqualTo(1));
            Assert.That(battle.Simulation.State.Towers[0].Id.Value, Is.GreaterThan(soldId.Value));
            Assert.That(battle.Simulation.State.Towers[0].Level, Is.EqualTo(1));
            Assert.That(battle.Simulation.State.Gold, Is.EqualTo(78));
            Assert.That(GameObject.Find($"Tower {battle.Simulation.State.Towers[0].Id.Value}"), Is.Not.Null);
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
