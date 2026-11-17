using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Veilward.Application;
using Veilward.Domain;
using Veilward.Infrastructure;
using Veilward.Presentation;

namespace Veilward.Tests.EditMode
{
    public sealed class MainMenuControllerTests
    {
        private readonly List<UnityEngine.Object> _objects = new List<UnityEngine.Object>();
        private string _root;
        private WaveCheckpointStore _store;
        private BattleScenarioConfig _scenario;

        [SetUp]
        public void SetUp()
        {
            _root = Path.GetFullPath(Path.Combine("TestResults", "MainMenuControllerTests", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(_root);
            _store = new WaveCheckpointStore(_root);
            _scenario = BattleScenarioConfig.CreateDefault();
            _objects.Add(_scenario);
        }

        [TearDown]
        public void TearDown()
        {
            BattleSessionContext.Clear();
            for (var index = _objects.Count - 1; index >= 0; index--)
            {
                if (_objects[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_objects[index]);
                }
            }

            _objects.Clear();
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        [TestCase(BattlePhase.Preparation, true)]
        [TestCase(BattlePhase.WaveComplete, true)]
        [TestCase(BattlePhase.Victory, false)]
        [TestCase(BattlePhase.Defeat, false)]
        public void ContinueAvailability_DependsOnStableNonTerminalCheckpoint(
            BattlePhase phase,
            bool expected)
        {
            var menu = CreateMenu();
            Save(phase);

            menu.RefreshCheckpointStatus();

            Assert.That(menu.ContinueButton.interactable, Is.EqualTo(expected));
            if (!expected)
            {
                Assert.That(menu.StatusText.text, Does.Contain("finished"));
            }
        }

        [Test]
        public void DamagedPrimary_CompatibleBackup_EnablesContinueSession()
        {
            var menu = CreateMenu();
            Save(BattlePhase.WaveComplete);
            Save(BattlePhase.Preparation);
            File.WriteAllText(_store.PrimaryPath, "{truncated");

            menu.RefreshCheckpointStatus();

            Assert.That(menu.ContinueButton.interactable, Is.True);
            Assert.That(menu.StatusText.text, Does.Contain("backup"));
            Assert.That(menu.PrepareContinueSession(), Is.True);
            var session = BattleSessionContext.Consume();
            Assert.That(session.Store, Is.SameAs(_store));
            Assert.That(session.Checkpoint.state.phase, Is.EqualTo(BattlePhase.WaveComplete));
            Assert.That(BattleSessionContext.Consume(), Is.Null);
        }

        [Test]
        public void NewGame_DeletesSlotAndCreatesFreshSession()
        {
            var menu = CreateMenu();
            Save(BattlePhase.Preparation);
            File.WriteAllText(_store.TempPath, "stale");

            Assert.That(menu.PrepareNewGameSession(), Is.True);

            Assert.That(File.Exists(_store.PrimaryPath), Is.False);
            Assert.That(File.Exists(_store.BackupPath), Is.False);
            Assert.That(File.Exists(_store.TempPath), Is.False);
            var session = BattleSessionContext.Consume();
            Assert.That(session.Store, Is.SameAs(_store));
            Assert.That(session.Checkpoint, Is.Null);
            Assert.That(session.ScenarioContentId, Is.EqualTo(_scenario.ScenarioContentId));
        }

        [Test]
        public void NewGameResetFailure_PreservesCheckpointAndContinueSession()
        {
            var menu = CreateMenu();
            Save(BattlePhase.WaveComplete);
            Save(BattlePhase.Preparation);
            var primary = File.ReadAllBytes(_store.PrimaryPath);
            var backup = File.ReadAllBytes(_store.BackupPath);
            Directory.CreateDirectory(_store.TempPath);
            LogAssert.Expect(LogType.Error, new Regex("^Checkpoint delete failed:"));

            Assert.That(menu.PrepareNewGameSession(), Is.False);

            Assert.That(File.ReadAllBytes(_store.PrimaryPath), Is.EqualTo(primary));
            Assert.That(File.ReadAllBytes(_store.BackupPath), Is.EqualTo(backup));
            menu.RefreshCheckpointStatus();
            Assert.That(menu.ContinueButton.interactable, Is.True);
            Assert.That(menu.PrepareContinueSession(), Is.True);
            var session = BattleSessionContext.Consume();
            Assert.That(session.Store, Is.SameAs(_store));
            Assert.That(session.Checkpoint.state.phase, Is.EqualTo(BattlePhase.Preparation));
            Assert.That(BattleSessionContext.Consume(), Is.Null);
        }

        [Test]
        public void NewGameBackupDeleteFailure_RestoresPrimaryAndContinueSession()
        {
            var menu = CreateMenu();
            Save(BattlePhase.Preparation);
            var primary = File.ReadAllBytes(_store.PrimaryPath);
            Directory.CreateDirectory(_store.BackupPath);
            LogAssert.Expect(LogType.Error, new Regex("^Checkpoint delete failed:"));

            Assert.That(menu.PrepareNewGameSession(), Is.False);

            Assert.That(File.Exists(_store.PrimaryPath), Is.True, "Reset must retain a recoverable checkpoint.");
            Assert.That(File.ReadAllBytes(_store.PrimaryPath), Is.EqualTo(primary));
            menu.RefreshCheckpointStatus();
            Assert.That(menu.ContinueButton.interactable, Is.True);
            Assert.That(menu.PrepareContinueSession(), Is.True);
            var session = BattleSessionContext.Consume();
            Assert.That(session.Store, Is.SameAs(_store));
            Assert.That(session.Checkpoint.state.phase, Is.EqualTo(BattlePhase.Preparation));
            Assert.That(BattleSessionContext.Consume(), Is.Null);
        }

        [Test]
        public void MissingCheckpoint_DisablesContinueWithoutCreatingSession()
        {
            var menu = CreateMenu();

            menu.RefreshCheckpointStatus();

            Assert.That(menu.ContinueButton.interactable, Is.False);
            Assert.That(menu.PrepareContinueSession(), Is.False);
            Assert.That(BattleSessionContext.Consume(), Is.Null);
        }

        [TestCase(true)] [TestCase(false)]
        public void RegBattle182_183_NativeMenu_RoutesCompleteNativeOrLegacySession(bool native)
        {
            _scenario = UnityEngine.Object.Instantiate(ElementProgressionStorageTests.Config()); _objects.Add(_scenario);
            var serialized = new SerializedObject(_scenario);
            serialized.FindProperty("scenarioContentId").stringValue = "eight-wave-unlocks-v1";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var s = native ? _scenario.CreateSimulation(BattleElementUnlockTests.Rules()) : _scenario.CreateHistoricalAllOpenSimulation();
            if (native) s.ChooseElement(new ChooseElementCommand("start-resonance", "gale"));
            else s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId("storm"), new GridCoordinate(-5, -3)));
            var e = new WaveCheckpointEnvelope
            {
                schemaVersion = native ? 3 : 2, scenarioContentId = native ? "eight-wave-unlocks-v1" : "eight-wave-v1",
                state = s.ExportCheckpoint()
            };
            File.WriteAllText(_store.PrimaryPath, JsonUtility.ToJson(e)); var bytes = File.ReadAllBytes(_store.PrimaryPath);
            var menu = CreateMenu(); Assert.That(menu.PrepareContinueSession(), Is.True);
            var session = BattleSessionContext.Consume();
            Assert.That(session.ScenarioContentId, Is.EqualTo(e.scenarioContentId));
            var restored = _scenario.CreateSimulationForCheckpoint(session.Checkpoint);
            restored.RestoreCheckpoint(session.Checkpoint.state);
            Assert.That(restored.State.ElementProgression.RulesId, Is.EqualTo(native ? "wave-unlocks-v1" : "legacy-all-open-v1"));
            Assert.That(restored.State.Towers.Count, Is.EqualTo(native ? 0 : 1));
            Assert.That(File.ReadAllBytes(_store.PrimaryPath), Is.EqualTo(bytes));
        }

        private MainMenuController CreateMenu()
        {
            var menuObject = new GameObject("Main Menu");
            _objects.Add(menuObject);
            var menu = menuObject.AddComponent<MainMenuController>();
            var newGame = CreateButton("New Game Button");
            var continueButton = CreateButton("Continue Button");
            var statusObject = new GameObject("Status Text");
            _objects.Add(statusObject);
            var status = statusObject.AddComponent<UnityEngine.UI.Text>();
            var serialized = new SerializedObject(menu);
            serialized.FindProperty("scenario").objectReferenceValue = _scenario;
            serialized.FindProperty("newGameButton").objectReferenceValue = newGame;
            serialized.FindProperty("continueButton").objectReferenceValue = continueButton;
            serialized.FindProperty("statusText").objectReferenceValue = status;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            menu.ConfigureStore(_store);
            return menu;
        }

        private UnityEngine.UI.Button CreateButton(string name)
        {
            var buttonObject = new GameObject(name);
            _objects.Add(buttonObject);
            return buttonObject.AddComponent<UnityEngine.UI.Button>();
        }

        private void Save(BattlePhase phase)
        {
            var simulation = _scenario.CreateSimulation();
            var checkpoint = simulation.ExportCheckpoint();
            checkpoint.phase = phase;
            checkpoint.baseHealth = phase == BattlePhase.Defeat ? 0 : 20;
            var envelope = new WaveCheckpointEnvelope
            {
                schemaVersion = WaveCheckpointStore.CurrentSchemaVersion,
                scenarioContentId = _scenario.ScenarioContentId,
                state = checkpoint
            };
            Assert.That(_store.TrySave(envelope, simulation.TryValidateCheckpoint, out var error), Is.True, error);
        }
    }
}
