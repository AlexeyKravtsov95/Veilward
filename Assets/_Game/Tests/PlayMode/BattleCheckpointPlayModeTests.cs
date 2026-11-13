using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Veilward.Application;
using Veilward.Domain;
using Veilward.Infrastructure;
using Veilward.Presentation;
using Object = UnityEngine.Object;

namespace Veilward.Tests.PlayMode
{
    public sealed class BattleCheckpointPlayModeTests
    {
        private static readonly GridCoordinate TowerCell = new GridCoordinate(-4, 0);
        private string _root;
        private WaveCheckpointStore _store;

        [SetUp]
        public void SetUp()
        {
            _root = Path.GetFullPath(Path.Combine("TestResults", "CheckpointPlayModeTests", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(_root);
            _store = new WaveCheckpointStore(_root);
            MainMenuController.CheckpointStoreFactory = () => _store;
            BattleSessionContext.Clear();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            BattleSessionContext.Clear();
            yield return SceneManager.LoadSceneAsync("Battle", LoadSceneMode.Single);
            MainMenuController.CheckpointStoreFactory = null;
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        [UnityTest]
        public IEnumerator InterruptedCombat_MenuContinue_RestoresPreparationViewsAndClearsTransients()
        {
            yield return LoadMainMenu();
            var menu = Object.FindFirstObjectByType<MainMenuController>();
            Assert.That(menu.ContinueButton.interactable, Is.False);
            menu.NewGameButton.onClick.Invoke();
            yield return null;
            yield return null;

            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            var placement = Object.FindFirstObjectByType<BattlePlacementController>();
            Assert.That(battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.Preparation));
            Assert.That(File.Exists(_store.PrimaryPath), Is.True);
            Assert.That(placement.TrySelectCell(TowerCell), Is.True);
            FindButton("Confirm Button").onClick.Invoke();
            yield return null;
            Assert.That(placement.TrySelectCell(TowerCell), Is.True);
            FindButton("Upgrade Button").onClick.Invoke();
            yield return null;
            var expected = battle.Simulation.ExportCheckpoint();
            Assert.That(expected.gold, Is.EqualTo(40));
            Assert.That(expected.towers[0].level, Is.EqualTo(2));

            FindButton("Start Wave Button").onClick.Invoke();
            for (var index = 0; index < 31; index++)
            {
                battle.Simulation.Step();
                battle.Simulation.ClearAttackEvents();
            }

            Assert.That(battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.Combat));
            Assert.That(battle.Simulation.State.Gold, Is.GreaterThan(expected.gold));
            Assert.That(battle.Simulation.State.Kills, Is.GreaterThan(0));

            yield return LoadMainMenu();
            menu = Object.FindFirstObjectByType<MainMenuController>();
            Assert.That(menu.ContinueButton.interactable, Is.True);
            menu.ContinueButton.onClick.Invoke();
            yield return null;
            yield return null;

            battle = Object.FindFirstObjectByType<BattleSceneController>();
            placement = Object.FindFirstObjectByType<BattlePlacementController>();
            AssertCheckpoint(expected, battle.Simulation.ExportCheckpoint());
            Assert.That(battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.Preparation));
            Assert.That(battle.Simulation.State.Enemies, Is.Empty);
            Assert.That(GameObject.Find("Tower 1"), Is.Not.Null);
            Assert.That(placement.HasSelection, Is.False);
            Assert.That(placement.IsSellConfirmationVisible, Is.False);
            Assert.That(FindButton("Start Wave Button").interactable, Is.True);
        }

        [UnityTest]
        public IEnumerator WaveComplete_RepeatedMenuContinue_DoesNotDuplicateRewards()
        {
            yield return LoadMainMenu();
            Object.FindFirstObjectByType<MainMenuController>().NewGameButton.onClick.Invoke();
            yield return null;
            yield return null;
            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            var checkpoint = Object.FindFirstObjectByType<BattleCheckpointController>();
            Assert.That(battle.PlaceTower(new GridCoordinate(-1, 0)).Success, Is.True);
            FindButton("Start Wave Button").onClick.Invoke();
            var guard = 0;
            while (battle.Simulation.State.Phase == BattlePhase.Combat && guard++ < 20000)
            {
                battle.Simulation.Step();
                battle.Simulation.ClearAttackEvents();
                checkpoint.ObservePhaseTransition();
            }

            Assert.That(guard, Is.LessThan(20000));
            Assert.That(battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.WaveComplete));
            var completed = battle.Simulation.ExportCheckpoint();
            Assert.That(completed.gold, Is.EqualTo(135));
            Assert.That(completed.kills, Is.EqualTo(5));

            for (var index = 0; index < 2; index++)
            {
                yield return LoadMainMenu();
                var menu = Object.FindFirstObjectByType<MainMenuController>();
                Assert.That(menu.ContinueButton.interactable, Is.True);
                menu.ContinueButton.onClick.Invoke();
                yield return null;
                yield return null;
                battle = Object.FindFirstObjectByType<BattleSceneController>();
                AssertCheckpoint(completed, battle.Simulation.ExportCheckpoint());
                Assert.That(battle.Simulation.State.Gold, Is.EqualTo(135));
            }

            FindButton("Next Wave Button").onClick.Invoke();
            yield return null;
            Assert.That(battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.Preparation));
            Assert.That(battle.Simulation.State.WaveIndex, Is.EqualTo(1));
            Assert.That(battle.Simulation.State.Gold, Is.EqualTo(135));
        }

        [UnityTest]
        public IEnumerator DirectBattle_DoesNotReadOrWriteCheckpointStore()
        {
            File.WriteAllText(_store.PrimaryPath, "sentinel-not-json");
            yield return SceneManager.LoadSceneAsync("Battle", LoadSceneMode.Single);
            yield return null;

            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            Assert.That(battle.Simulation.State.Gold, Is.EqualTo(150));
            Assert.That(battle.PlaceTower(TowerCell).Success, Is.True);
            Assert.That(battle.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
            Assert.That(File.ReadAllText(_store.PrimaryPath), Is.EqualTo("sentinel-not-json"));
        }

        private static IEnumerator LoadMainMenu()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return null;
        }

        private static UnityEngine.UI.Button FindButton(string name)
        {
            return Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(button => button.name == name);
        }

        private static void AssertCheckpoint(BattleCheckpoint expected, BattleCheckpoint actual)
        {
            Assert.That(actual.phase, Is.EqualTo(expected.phase));
            Assert.That(actual.waveIndex, Is.EqualTo(expected.waveIndex));
            Assert.That(actual.tick, Is.EqualTo(expected.tick));
            Assert.That(actual.baseHealth, Is.EqualTo(expected.baseHealth));
            Assert.That(actual.gold, Is.EqualTo(expected.gold));
            Assert.That(actual.kills, Is.EqualTo(expected.kills));
            Assert.That(actual.nextEnemyId, Is.EqualTo(expected.nextEnemyId));
            Assert.That(actual.nextTowerId, Is.EqualTo(expected.nextTowerId));
            Assert.That(actual.towers.Length, Is.EqualTo(expected.towers.Length));
            for (var index = 0; index < expected.towers.Length; index++)
            {
                Assert.That(actual.towers[index].towerId, Is.EqualTo(expected.towers[index].towerId));
                Assert.That(actual.towers[index].level, Is.EqualTo(expected.towers[index].level));
                Assert.That(actual.towers[index].investedGold, Is.EqualTo(expected.towers[index].investedGold));
                Assert.That(actual.towers[index].nextAttackTick, Is.EqualTo(expected.towers[index].nextAttackTick));
            }
        }
    }
}
