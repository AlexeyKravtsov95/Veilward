using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Veilward.Domain;
using Veilward.Infrastructure;
using Veilward.Presentation;

namespace Veilward.Tests.EditMode
{
    public sealed class BattleCheckpointControllerTests
    {
        private const string ContentId = "legacy-single-wave-v1";
        private static readonly GridCoordinate TowerCell = new GridCoordinate(-4, 0);
        private readonly List<UnityEngine.Object> _objects = new List<UnityEngine.Object>();
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.GetFullPath(Path.Combine("TestResults", "CheckpointControllerTests", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
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

        [Test]
        public void SuccessfulCommands_SaveAndRejectedCommand_DoesNotRewriteCheckpoint()
        {
            var store = new WaveCheckpointStore(_root);
            var battle = CreateBattle(CreateSimulation(), store, out _);

            Assert.That(battle.PlaceTower(TowerCell).Success, Is.True);
            Assert.That(Load(store).state.gold, Is.EqualTo(90));
            var beforeRejected = File.ReadAllBytes(store.PrimaryPath);
            Assert.That(battle.PlaceTower(TowerCell).Success, Is.False);
            Assert.That(File.ReadAllBytes(store.PrimaryPath), Is.EqualTo(beforeRejected));

            Assert.That(battle.UpgradeTower(new TowerId(1)).Success, Is.True);
            Assert.That(Load(store).state.towers[0].level, Is.EqualTo(2));
            Assert.That(battle.SellTower(new TowerId(1)).Success, Is.True);
            Assert.That(Load(store).state.towers, Is.Empty);
            Assert.That(Load(store).state.nextTowerId, Is.EqualTo(2));
        }

        [Test]
        public void StartWaveWriteFailure_BlocksTransitionAndKeepsPreparation()
        {
            var store = new WaveCheckpointStore(_root);
            var simulation = CreateSimulation();
            var battle = CreateBattle(simulation, store, out var checkpoint);
            Directory.CreateDirectory(store.TempPath);
            var tick = simulation.State.Tick;
            LogAssert.Expect(LogType.Error, new Regex("Checkpoint save failed:.*"));

            Assert.That(battle.StartWave(), Is.EqualTo(WaveTransitionResult.RejectedInvalidPhase));
            Assert.That(simulation.State.Phase, Is.EqualTo(BattlePhase.Preparation));
            Assert.That(simulation.State.Tick, Is.EqualTo(tick));
            Assert.That(checkpoint.StatusText.text, Does.Contain("Start Wave requires"));
        }

        [Test]
        public void WaveCompleteAndNextWave_SaveRewardedStableStatesOnlyOnce()
        {
            var store = new WaveCheckpointStore(_root);
            var simulation = CreateSimulation(waveCount: 2, maxHealth: 10);
            var battle = CreateBattle(simulation, store, out var checkpoint);
            battle.PlaceTower(TowerCell);
            battle.StartWave();
            simulation.Step();

            Assert.That(simulation.State.Phase, Is.EqualTo(BattlePhase.WaveComplete));
            Assert.That(checkpoint.ObservePhaseTransition(), Is.True);
            var completed = Load(store);
            Assert.That(completed.state.gold, Is.EqualTo(115));
            Assert.That(completed.state.kills, Is.EqualTo(1));
            var completedBytes = File.ReadAllBytes(store.PrimaryPath);
            Assert.That(checkpoint.ObservePhaseTransition(), Is.True);
            Assert.That(File.ReadAllBytes(store.PrimaryPath), Is.EqualTo(completedBytes));

            Assert.That(battle.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.Continued));
            var next = Load(store);
            Assert.That(next.state.phase, Is.EqualTo(BattlePhase.Preparation));
            Assert.That(next.state.waveIndex, Is.EqualTo(1));
            Assert.That(next.state.gold, Is.EqualTo(completed.state.gold));
        }

        [Test]
        public void DirectBattleWithoutStore_StartsWaveInMemory()
        {
            var battle = CreateBattle(CreateSimulation(), store: null, out var checkpoint);

            Assert.That(battle.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
            Assert.That(battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.Combat));
            Assert.That(Directory.GetFiles(_root), Is.Empty);
            Assert.That(checkpoint.LastSaveSucceeded, Is.True);
        }

        private BattleSceneController CreateBattle(
            BattleSimulation simulation,
            WaveCheckpointStore store,
            out BattleCheckpointController checkpoint)
        {
            var checkpointObject = new GameObject("Checkpoint");
            _objects.Add(checkpointObject);
            checkpoint = checkpointObject.AddComponent<BattleCheckpointController>();
            var statusObject = new GameObject("Save Status");
            _objects.Add(statusObject);
            var status = statusObject.AddComponent<UnityEngine.UI.Text>();
            var serializedCheckpoint = new SerializedObject(checkpoint);
            serializedCheckpoint.FindProperty("saveStatusText").objectReferenceValue = status;
            serializedCheckpoint.ApplyModifiedPropertiesWithoutUndo();
            checkpoint.Configure(store, simulation, ContentId);

            var battleObject = new GameObject("Battle");
            _objects.Add(battleObject);
            var battle = battleObject.AddComponent<BattleSceneController>();
            typeof(BattleSceneController).GetField("_simulation", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(battle, simulation);
            var serializedBattle = new SerializedObject(battle);
            serializedBattle.FindProperty("checkpointController").objectReferenceValue = checkpoint;
            serializedBattle.ApplyModifiedPropertiesWithoutUndo();
            return battle;
        }

        private BattleSimulation CreateSimulation(int waveCount = 1, int maxHealth = 100)
        {
            var config = BattleScenarioConfig.CreateDefault();
            _objects.Add(config);
            var serialized = new SerializedObject(config);
            serialized.FindProperty("maxHealth").intValue = maxHealth;
            serialized.FindProperty("enemyReward").intValue = 5;
            serialized.FindProperty("waveCompletionBonus").intValue = 20;
            var waves = serialized.FindProperty("waves");
            waves.arraySize = waveCount;
            for (var index = 0; index < waveCount; index++)
            {
                var wave = waves.GetArrayElementAtIndex(index);
                wave.FindPropertyRelative("enemyCount").intValue = 1;
                wave.FindPropertyRelative("firstSpawnTick").intValue = 0;
                wave.FindPropertyRelative("spawnIntervalTicks").intValue = 30;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return config.CreateSimulation();
        }

        private WaveCheckpointEnvelope Load(WaveCheckpointStore store)
        {
            var simulation = CreateSimulation(waveCount: 2);
            Assert.That(store.TryLoad(ContentId, simulation.TryValidateCheckpoint, out var result), Is.True, result.Error);
            return result.Envelope;
        }
    }
}
