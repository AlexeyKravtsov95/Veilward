using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Veilward.Domain;
using Veilward.Presentation;

namespace Veilward.Tests.PlayMode
{
    public sealed class BattlePausePlayModeTests
    {
        private static readonly GridCoordinate TowerCell = new GridCoordinate(-4, 0);

        [UnityTest]
        public IEnumerator Battle_PauseFreezesSimulationAcrossFrames()
        {
            yield return SceneManager.LoadSceneAsync("Battle", LoadSceneMode.Single);
            yield return null;
            var battle = Object.FindFirstObjectByType<BattleSceneController>();

            var preparation = Snapshot(battle);
            FindButton("Pause Button").onClick.Invoke();
            yield return WaitPausedFrames(3);
            Assert.That(Snapshot(battle), Is.EqualTo(preparation));
            Assert.That(battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.Preparation));
            FindButton("Resume Button").onClick.Invoke();
            yield return null;

            Assert.That(battle.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
            yield return null;
            var combat = Snapshot(battle);
            FindButton("Pause Button").onClick.Invoke();
            yield return WaitPausedFrames(3);
            Assert.That(Snapshot(battle), Is.EqualTo(combat));
            Assert.That(battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.Combat));
            FindButton("Resume Button").onClick.Invoke();
            yield return null;

            CompleteCurrentWave(battle);
            Assert.That(battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.WaveComplete));
            var waveComplete = Snapshot(battle);
            FindButton("Pause Button").onClick.Invoke();
            yield return WaitPausedFrames(3);
            Assert.That(Snapshot(battle), Is.EqualTo(waveComplete));
            Assert.That(battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.WaveComplete));
            FindButton("Resume Button").onClick.Invoke();
            Assert.That(Object.FindFirstObjectByType<BattlePauseController>().IsPaused, Is.False);
        }

        [UnityTest]
        public IEnumerator Battle_PausedCommandsAndButtons_CannotMutateMatch()
        {
            yield return SceneManager.LoadSceneAsync("Battle", LoadSceneMode.Single);
            yield return null;
            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            var placement = Object.FindFirstObjectByType<BattlePlacementController>();
            Assert.That(battle.PlaceTower(TowerCell).Success, Is.True);
            yield return null;
            var towerId = battle.Simulation.State.Towers[0].Id;
            Assert.That(placement.TrySelectCell(TowerCell), Is.True);
            var before = Snapshot(battle);

            FindButton("Pause Button").onClick.Invoke();
            yield return null;

            Assert.That(FindOverlay().activeInHierarchy, Is.True);
            foreach (var name in new[]
                     {
                         "Confirm Button", "Start Wave Button", "Next Wave Button",
                         "Upgrade Button", "Sell Button", "Confirm Sell Button"
                     })
            {
                var button = FindButton(name);
                Assert.That(button.interactable, Is.False, name);
                button.onClick.Invoke();
            }

            Assert.That(placement.TrySelectCell(new GridCoordinate(-1, 0)), Is.False);
            placement.ConfirmSelection();
            Assert.That(placement.UpgradeSelectedTower().FailureReason,
                Is.EqualTo(TowerOperationFailureReason.InvalidPhase));
            placement.RequestSellSelectedTower();
            Assert.That(placement.IsSellConfirmationVisible, Is.False);
            Assert.That(placement.ConfirmSellSelectedTower().FailureReason,
                Is.EqualTo(TowerOperationFailureReason.ConfirmationRequired));
            placement.StartWave();
            placement.ContinueToNextWave();
            Assert.That(battle.PlaceTower(new GridCoordinate(-1, 0)).FailureReason,
                Is.EqualTo(PlaceTowerFailureReason.InvalidPhase));
            Assert.That(battle.UpgradeTower(towerId).FailureReason,
                Is.EqualTo(TowerOperationFailureReason.InvalidPhase));
            Assert.That(battle.SellTower(towerId).FailureReason,
                Is.EqualTo(TowerOperationFailureReason.InvalidPhase));
            Assert.That(battle.StartWave(), Is.EqualTo(WaveTransitionResult.RejectedInvalidPhase));
            Assert.That(battle.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.RejectedInvalidPhase));
            Assert.That(Snapshot(battle), Is.EqualTo(before));
            Assert.That(battle.Simulation.Grid.GetState(TowerCell), Is.EqualTo(GridCellState.Occupied));
        }

        [UnityTest]
        public IEnumerator Battle_PauseClearsPendingActions()
        {
            yield return SceneManager.LoadSceneAsync("Battle", LoadSceneMode.Single);
            yield return null;
            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            var placement = Object.FindFirstObjectByType<BattlePlacementController>();
            var cell = new GridCoordinate(-1, 0);
            Assert.That(placement.TrySelectCell(cell), Is.True);
            var gold = battle.Simulation.State.Gold;

            FindButton("Pause Button").onClick.Invoke();
            Assert.That(placement.HasSelection, Is.False);
            Assert.That(battle.Simulation.Grid.GetState(cell), Is.EqualTo(GridCellState.Available));
            FindButton("Resume Button").onClick.Invoke();

            Assert.That(placement.TrySelectCell(cell), Is.True);
            placement.ConfirmSelection();
            yield return null;
            Assert.That(placement.TrySelectCell(cell), Is.True);
            placement.RequestSellSelectedTower();
            Assert.That(placement.IsSellConfirmationVisible, Is.True);
            gold = battle.Simulation.State.Gold;
            FindButton("Pause Button").onClick.Invoke();
            yield return null;

            Assert.That(placement.HasSelection, Is.False);
            Assert.That(placement.IsSellConfirmationVisible, Is.False);
            Assert.That(battle.Simulation.State.Gold, Is.EqualTo(gold));
            FindButton("Resume Button").onClick.Invoke();
            Assert.That(placement.ConfirmSellSelectedTower().FailureReason,
                Is.EqualTo(TowerOperationFailureReason.ConfirmationRequired));
            Assert.That(battle.Simulation.State.Gold, Is.EqualTo(gold));
            Assert.That(battle.Simulation.State.Towers.Count, Is.EqualTo(1));
            Assert.That(battle.Simulation.Grid.GetState(cell), Is.EqualTo(GridCellState.Occupied));
        }

        [UnityTest]
        public IEnumerator Battle_Resume_ContinuesWithoutCatchUp()
        {
            yield return SceneManager.LoadSceneAsync("Battle", LoadSceneMode.Single);
            yield return null;
            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            Assert.That(battle.PlaceTower(TowerCell).Success, Is.True);
            Assert.That(battle.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
            var expectedEvents = new List<string>();
            StepAndRecord(battle, 40, expectedEvents);
            var expectedSnapshot = Snapshot(battle);

            yield return SceneManager.LoadSceneAsync("Battle", LoadSceneMode.Single);
            yield return null;
            battle = Object.FindFirstObjectByType<BattleSceneController>();
            Assert.That(battle.PlaceTower(TowerCell).Success, Is.True);
            Assert.That(battle.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
            var beforePause = Snapshot(battle);
            FindButton("Pause Button").onClick.Invoke();
            yield return WaitPausedFrames(5);
            Assert.That(Snapshot(battle), Is.EqualTo(beforePause));

            FindButton("Resume Button").onClick.Invoke();
            yield return null;
            Assert.That(Snapshot(battle), Is.EqualTo(beforePause),
                "The first resumed frame must not consume accumulated background time.");
            var actualEvents = new List<string>();
            StepAndRecord(battle, 40, actualEvents);

            CollectionAssert.AreEqual(expectedEvents, actualEvents);
            Assert.That(Snapshot(battle), Is.EqualTo(expectedSnapshot));
        }

        [UnityTest]
        public IEnumerator Battle_ResumeClick_DoesNotPassThroughOverlay()
        {
            yield return SceneManager.LoadSceneAsync("Battle", LoadSceneMode.Single);
            yield return null;
            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            var placement = Object.FindFirstObjectByType<BattlePlacementController>();
            var pause = Object.FindFirstObjectByType<BattlePauseController>();
            var before = Snapshot(battle);
            FindButton("Pause Button").onClick.Invoke();
            yield return null;
            var resume = FindButton("Resume Button");
            var screenPosition = RectTransformUtility.WorldToScreenPoint(null, resume.transform.position);
            var raycastResults = new List<RaycastResult>();
            EventSystem.current.RaycastAll(
                new PointerEventData(EventSystem.current) { position = screenPosition },
                raycastResults);
            Assert.That(raycastResults.Any(result =>
                result.gameObject == resume.gameObject || result.gameObject.transform.IsChildOf(resume.transform)),
                Is.True, "Resume must remain reachable through the blocking overlay.");

            var pointer = new PointerEventData(EventSystem.current)
            {
                position = screenPosition,
                button = PointerEventData.InputButton.Left,
                clickCount = 1
            };
            var mouse = Mouse.current ?? InputSystem.AddDevice<Mouse>();
            InputSystem.QueueDeltaStateEvent(mouse.position, screenPosition);
            InputSystem.QueueDeltaStateEvent(mouse.leftButton, true);
            InputSystem.Update();
            ExecuteEvents.Execute(resume.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(resume.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(resume.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            yield return null;
            InputSystem.QueueDeltaStateEvent(mouse.leftButton, false);
            InputSystem.Update();
            yield return null;

            Assert.That(pause.IsPaused, Is.False);
            Assert.That(placement.HasSelection, Is.False);
            Assert.That(Snapshot(battle), Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator Battle_Reload_DoesNotDuplicatePauseListeners()
        {
            yield return SceneManager.LoadSceneAsync("Battle", LoadSceneMode.Single);
            yield return null;
            Assert.That(RuntimeListenerCount(FindButton("Pause Button").onClick), Is.EqualTo(1));
            Assert.That(RuntimeListenerCount(FindButton("Resume Button").onClick), Is.EqualTo(1));

            yield return SceneManager.LoadSceneAsync("Battle", LoadSceneMode.Single);
            yield return null;

            Assert.That(Object.FindObjectsByType<BattlePauseController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(1));
            var pauseButton = FindButton("Pause Button");
            var resumeButton = FindButton("Resume Button");
            Assert.That(RuntimeListenerCount(pauseButton.onClick), Is.EqualTo(1));
            Assert.That(RuntimeListenerCount(resumeButton.onClick), Is.EqualTo(1));
            var pause = Object.FindFirstObjectByType<BattlePauseController>();
            Assert.That(pause.IsPaused, Is.False);
            Assert.That(FindOverlay().activeInHierarchy, Is.False);
            pauseButton.onClick.Invoke();
            Assert.That(pause.IsPaused, Is.True);
            resumeButton.onClick.Invoke();
            Assert.That(pause.IsPaused, Is.False);
            Assert.That(RuntimeListenerCount(pauseButton.onClick), Is.EqualTo(1));
            Assert.That(RuntimeListenerCount(resumeButton.onClick), Is.EqualTo(1));
        }

        private static IEnumerator WaitPausedFrames(int count)
        {
            for (var index = 0; index < count; index++)
            {
                yield return null;
            }
        }

        private static void CompleteCurrentWave(BattleSceneController battle)
        {
            if (battle.Simulation.State.Phase == BattlePhase.Preparation)
            {
                battle.StartWave();
            }

            var guard = 0;
            while (battle.Simulation.State.Phase == BattlePhase.Combat && guard++ < 20000)
            {
                battle.Simulation.Step();
                battle.Simulation.ClearAttackEvents();
            }

            Assert.That(guard, Is.LessThan(20000), "Battle wave did not complete within the test guard.");
        }

        private static void StepAndRecord(BattleSceneController battle, int count, List<string> events)
        {
            for (var index = 0; index < count; index++)
            {
                battle.Simulation.Step();
                foreach (var attack in battle.Simulation.AttackEvents)
                {
                    events.Add(string.Join(":",
                        attack.Tick,
                        attack.AttackerTowerId.Value,
                        attack.TargetEnemyId.Value,
                        attack.Damage,
                        attack.WasLethal,
                        attack.TargetPosition.X.ToString("R", CultureInfo.InvariantCulture),
                        attack.TargetPosition.Y.ToString("R", CultureInfo.InvariantCulture)));
                }

                battle.Simulation.ClearAttackEvents();
            }
        }

        private static string Snapshot(BattleSceneController battle)
        {
            var state = battle.Simulation.State;
            var snapshot = new StringBuilder();
            snapshot.Append(state.Phase).Append('|')
                .Append(state.Tick).Append('|')
                .Append(state.WaveIndex).Append('|')
                .Append(state.BaseHealth).Append('|')
                .Append(state.Gold).Append('|')
                .Append(state.Kills);
            foreach (var enemy in state.Enemies)
            {
                snapshot.Append("|E").Append(enemy.Id.Value).Append(':')
                    .Append(enemy.Health).Append(':')
                    .Append(enemy.DistanceAlongPath.ToString("R", CultureInfo.InvariantCulture));
            }

            foreach (var tower in state.Towers)
            {
                snapshot.Append("|T").Append(tower.Id.Value).Append(':')
                    .Append(tower.Level).Append(':')
                    .Append(tower.NextAttackTick);
            }

            return snapshot.ToString();
        }

        private static Button FindButton(string name)
        {
            return Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(button => button.name == name);
        }

        private static GameObject FindOverlay()
        {
            return Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(rect => rect.name == "Pause Overlay").gameObject;
        }

        private static int RuntimeListenerCount(UnityEngine.Events.UnityEventBase unityEvent)
        {
            var callsField = typeof(UnityEngine.Events.UnityEventBase).GetField(
                "m_Calls", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(callsField, Is.Not.Null);
            var calls = callsField.GetValue(unityEvent);
            var runtimeField = calls.GetType().GetField(
                "m_RuntimeCalls", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(runtimeField, Is.Not.Null);
            return ((ICollection)runtimeField.GetValue(calls)).Count;
        }
    }
}
