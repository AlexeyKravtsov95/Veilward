using System;
using System.Collections;
using System.Collections.Generic;
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
using Veilward.Tests.Shared;
using Object = UnityEngine.Object;

namespace Veilward.Tests.PlayMode
{
    public sealed class StoneModelPlayModeTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private string _root;
        private WaveCheckpointStore _store;
        private readonly List<string> _trace = new List<string>();
        private readonly List<string> _console = new List<string>();
        private bool Capture => Environment.GetEnvironmentVariable("ART3D_CAPTURE") == "1";
        private const string Output = "TestResults/ART3D";
        private BattleSceneController Battle => Object.FindFirstObjectByType<BattleSceneController>();
        private static void Call(object target, string method) => target.GetType().GetMethod(method, Flags).Invoke(target, null);

        private object _gameView;
        private PropertyInfo _gameViewSizeProperty;
        private int _previousGameViewSize = -1;

        [OneTimeSetUp] public void BeginConsoleReport()
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/console.txt", string.Empty);
        }

        [SetUp] public void Setup()
        {
            _root = Path.GetFullPath(Path.Combine("TestResults", "Art3dSaves", Guid.NewGuid().ToString("N")));
            _store = new WaveCheckpointStore(_root);
            MainMenuController.CheckpointStoreFactory = () => _store;
            BattleSessionContext.Clear(); _trace.Clear(); _console.Clear();
            UnityEngine.Application.logMessageReceived += Log;
            if (Capture) ConfigureGameView();
        }
        private void Log(string message, string stack, LogType kind)
        {
            if (kind == LogType.Warning || kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert)
                _console.Add(kind + "|" + message);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            UnityEngine.Application.logMessageReceived -= Log;
            Directory.CreateDirectory(Output);
            File.WriteAllLines(Output + "/" + (TestContext.CurrentContext.Test.Name.StartsWith("Art3D04") ? "natural-trace" : "binding-trace") + ".txt", _trace);
            var testName = TestContext.CurrentContext.Test.Name;
            File.WriteAllLines(Output + "/" + testName + ".console.txt", _console);
            File.AppendAllLines(Output + "/console.txt", new[] { "BEGIN|" + testName }
                .Concat(_console).Concat(new[] { "END|" + testName }));
            RestoreGameView();
            MainMenuController.CheckpointStoreFactory = null; BattleSessionContext.Clear();
            yield return SceneManager.LoadSceneAsync("Battle"); yield return null;
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        private TowerId _stoneId;

        [UnityTest] public IEnumerator Art3D04_EarnedStoneLevelsFileContinueCombatRollbackSellAndRestart()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu"); yield return null;
            Object.FindFirstObjectByType<MainMenuController>().NewGameButton.onClick.Invoke();
            yield return null; yield return null; Battle.enabled = false; Label();
            Assert.That(Battle.Simulation.State.Gold, Is.EqualTo(150));
            Assert.That(Battle.ChooseElement(new ChooseElementCommand("start-resonance", "flame")).Success, Is.True);
            var script = AbilityBossTrace.Load("flame");
            for (var wave = 1; wave <= 7; wave++)
            {
                var simulation = Battle.Simulation;
                foreach (var command in script.commands.Where(c => c.wave == wave && wave <= 6))
                {
                    Assert.That(command.op, Is.EqualTo("place"));
                    Assert.That(Battle.PlaceTower(new TowerDefinitionId(command.type), new GridCoordinate(command.x, command.y)).Success, Is.True);
                }
                Call(Battle, "LateUpdate");
                Assert.That(Battle.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
                var startTick = simulation.State.Tick; var guard = 0;
                while (simulation.State.Phase == BattlePhase.Combat && guard++ < 20000)
                {
                    simulation.Step();
                    foreach (var cast in script.casts.Where(c => c.wave == wave && c.relativeTick == simulation.State.Tick - startTick))
                        Assert.That(simulation.CastAbility(new CastAbilityCommand(simulation.AbilityState.AbilityId,
                            new PathPosition(cast.x, cast.y), simulation.State.WaveIndex, simulation.State.Tick,
                            simulation.AbilityState.NextCastSequence)).Succeeded, Is.True);
                    Call(Battle, "ProcessAttackEvents"); Call(Battle, "LateUpdate");
                    if (guard % 120 == 0) yield return null;
                }
                Assert.That(guard, Is.LessThan(20000));
                Assert.That(simulation.State.Phase, Is.EqualTo(BattlePhase.WaveComplete)); Record("earned wave complete");
                if (simulation.State.ElementProgression.PendingChoiceId != "none")
                    Assert.That(Battle.ChooseElement(new ChooseElementCommand(simulation.State.ElementProgression.PendingChoiceId,
                        wave == 3 ? script.after3 : "stone")).Success, Is.True);
                Assert.That(Battle.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.Continued));
            }
            // Законные Sell семи волн оплачивают STONE proof; Gold/HP/checkpoint DTO не подменяются.
            foreach (var tower in Battle.Simulation.State.Towers.ToArray())
                Assert.That(Battle.SellTower(tower.Id).Success, Is.True);
            Battle.ResetPresentation();
            var coordinate = new GridCoordinate(2, 1);
            Assert.That(Battle.PlaceTower(new TowerDefinitionId("stone"), coordinate).Success, Is.True);
            _stoneId = Battle.Simulation.State.Towers.Single().Id;
            var beforeDuplicate = AbilityBossTrace.Wire(Battle.Simulation);
            Assert.That(Battle.PlaceTower(new TowerDefinitionId("stone"), coordinate).Success, Is.False);
            Assert.That(AbilityBossTrace.Wire(Battle.Simulation), Is.EqualTo(beforeDuplicate));
            Call(Battle, "LateUpdate"); CheckLevel(_stoneId, 1);
            SaveEarnedCheckpoint(1);
            yield return Shot("01-natural-STONE-L1"); yield return ContinueAndCheck(1);
            Assert.That(Battle.UpgradeTower(_stoneId).Success, Is.True);
            Call(Battle, "LateUpdate"); CheckLevel(_stoneId, 2); SaveEarnedCheckpoint(2);
            yield return Shot("02-natural-STONE-L2"); yield return ContinueAndCheck(2);
            Assert.That(Battle.UpgradeTower(_stoneId).Success, Is.True);
            Call(Battle, "LateUpdate"); CheckLevel(_stoneId, 3); SaveEarnedCheckpoint(3);
            yield return Shot("03-natural-STONE-L3"); yield return ContinueAndCheck(3);
            var saved = AbilityBossTrace.Wire(Battle.Simulation);
            Assert.That(Battle.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
            var start = Battle.Simulation.State.Tick; var crack = false;
            for (var tick = 0; tick < 180 && Battle.Simulation.State.Phase == BattlePhase.Combat; tick++)
            {
                Battle.Simulation.Step();
                crack |= Battle.Simulation.State.Enemies.Any(e => e.Statuses.Any(s => s.Kind == StatusKind.Crack));
                Call(Battle, "ProcessAttackEvents"); Call(Battle, "LateUpdate");
                if (tick % 15 == 0) yield return null;
            }
            Assert.That(Battle.Simulation.State.Tick, Is.GreaterThan(start));
            Assert.That(crack, Is.True, "earned STONE army actually applies Crack in combat");
            yield return Shot("04-natural-STONE-interrupted-combat");
            yield return ContinueAndCheck(3);
            Assert.That(AbilityBossTrace.Wire(Battle.Simulation), Is.EqualTo(saved), "full safe snapshot Combat rollback");
            var beforeMax = AbilityBossTrace.Wire(Battle.Simulation);
            Assert.That(Battle.UpgradeTower(_stoneId).Success, Is.False);
            Assert.That(AbilityBossTrace.Wire(Battle.Simulation), Is.EqualTo(beforeMax)); CheckLevel(_stoneId, 3);
            var old = GameObject.Find("Tower " + _stoneId.Value);
            Battle.ResetPresentation();
            Assert.That(old.activeSelf, Is.False); CheckLevel(_stoneId, 3);
            yield return null; CheckLevel(_stoneId, 3);
            Assert.That(Battle.SellTower(_stoneId).Success, Is.True);
            var afterSell = AbilityBossTrace.Wire(Battle.Simulation);
            Assert.That(Battle.SellTower(_stoneId).Success, Is.False);
            Assert.That(AbilityBossTrace.Wire(Battle.Simulation), Is.EqualTo(afterSell));
            yield return null;
            Assert.That(Object.FindObjectsByType<FlameModelBinding>(FindObjectsSortMode.None), Is.Empty);
            yield return Shot("05-natural-sold-no-model-roots");
            Battle.ReturnToMenu(); yield return null; yield return null;
            Object.FindFirstObjectByType<MainMenuController>().NewGameButton.onClick.Invoke();
            yield return null; yield return null;
            Assert.That(Battle.Simulation.State.Towers, Is.Empty);
            Assert.That(Object.FindObjectsByType<FlameModelBinding>(FindObjectsSortMode.None), Is.Empty);
            Assert.That(_console.Any(c => c.StartsWith("Error|") || c.StartsWith("Exception|") || c.StartsWith("Assert|")), Is.False, string.Join("\n", _console));
        }

        [UnityTest] public IEnumerator Art3D02_DeferredFamilyReplacementHidesOldRootsSynchronously()
        {
            yield return SceneManager.LoadSceneAsync("Battle"); yield return null;
            Battle.enabled = false;
            var source = (Transform)typeof(BattleSceneController).GetField("towerPrefab", Flags).GetValue(Battle);
            var root = Object.Instantiate(source.gameObject);
            try
            {
                var binding = root.GetComponent<FlameModelBinding>();
                var types = new[] { "flame", "tide", "gale", "stone", "stone", "gale", "tide", "flame", "flame", "tide", "gale", "stone" };
                var levels = new[] { 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3 };
                Transform previous = null;
                for (var i = 0; i < types.Length; i++)
                {
                    Assert.That(binding.Apply(new TowerDefinitionId(types[i]), levels[i]), Is.True);
                    if (previous != null) Assert.That(previous.gameObject.activeSelf, Is.False, "deferred old root must stop rendering in the same frame");
                    previous = binding.Model;
                    Assert.That(binding.Apply(new TowerDefinitionId(types[i]), levels[i]), Is.True);
                    Assert.That(binding.Model, Is.SameAs(previous));
                    Assert.That(root.transform.Cast<Transform>().Count(t => t.gameObject.activeSelf && (t.name.StartsWith("FLAME L") || t.name.StartsWith("TIDE L") || t.name.StartsWith("STONE L") || t.name.StartsWith("GALE L"))), Is.EqualTo(1));
                }
                Assert.That(binding.Apply(new TowerDefinitionId("unknown-storm"), 1), Is.False);
                Assert.That(previous.gameObject.activeSelf, Is.False);
                Assert.That(root.transform.Find("Foundation").GetComponent<Renderer>().enabled, Is.True);
                Assert.That(binding.Apply(new TowerDefinitionId("stone"), 3), Is.True);
                yield return null;
                Assert.That(root.transform.Cast<Transform>().Count(t => t.name.StartsWith("FLAME L") || t.name.StartsWith("TIDE L") || t.name.StartsWith("STONE L") || t.name.StartsWith("GALE L")), Is.EqualTo(1));
                Assert.That(root.GetComponentsInChildren<MeshFilter>().Length, Is.EqualTo(4));
            }
            finally { Object.Destroy(root); }
        }

        private void SaveEarnedCheckpoint(int level)
        {
            Directory.CreateDirectory(Output);
            File.Copy(_store.PrimaryPath, Output + "/natural-L" + level + "-checkpoint.json", true);
            Record("unmodified earned checkpoint " + Output + "/natural-L" + level + "-checkpoint.json");
        }

        private IEnumerator ContinueAndCheck(int level)
        {
            var combat = Battle.Simulation.State.Phase == BattlePhase.Combat;
            var state = combat ? null : AbilityBossTrace.Wire(Battle.Simulation);
            var bytes = File.ReadAllBytes(_store.PrimaryPath);
            for (var repeat = 0; repeat < 2; repeat++)
            {
                Battle.ReturnToMenu(); yield return null; yield return null;
                var menu = Object.FindFirstObjectByType<MainMenuController>();
                Assert.That(menu.ContinueButton.interactable, Is.True);
                menu.ContinueButton.onClick.Invoke(); yield return null; yield return null;
                Battle.enabled = false; Label(); CheckLevel(_stoneId, level);
                if (!combat) Assert.That(AbilityBossTrace.Wire(Battle.Simulation), Is.EqualTo(state));
                Assert.That(File.ReadAllBytes(_store.PrimaryPath), Is.EqualTo(bytes));
            }
            Record("repeated Continue L" + level);
        }
        private void CheckLevel(TowerId id, int level)
        {
            var s = Battle.Simulation; var tower = s.State.Towers.Single(t => t.Id == id);
            var root = GameObject.Find("Tower " + id.Value).transform;
            Assert.That(root.position, Is.EqualTo(Battle.GetCellCenter(tower.Coordinate)));
            Assert.That(root.localScale, Is.EqualTo(Vector3.one * s.Grid.Map.CellSize));
            var source = (Transform)typeof(BattleSceneController).GetField("towerPrefab", Flags).GetValue(Battle);
            Assert.That(root.GetComponentsInChildren<Collider>().Length, Is.EqualTo(source.GetComponentsInChildren<Collider>().Length));
            var placement = Object.FindFirstObjectByType<BattlePlacementController>();
            Assert.That(placement.TrySelectCell(tower.Coordinate), Is.True);
            Assert.That(placement.SelectedTowerId, Is.EqualTo(id));
            var range = (LineRenderer)typeof(BattlePlacementController).GetField("rangePreview", Flags).GetValue(placement);
            Assert.That(range.gameObject.activeSelf, Is.True);
            Assert.That(range.GetPosition(0).magnitude, Is.EqualTo(tower.Definition.Range).Within(.0001f));
            placement.CancelSelection();
            var binding = root.GetComponent<FlameModelBinding>();
            Assert.That(binding.Level, Is.EqualTo(level)); Assert.That(tower.Level, Is.EqualTo(level));
            Assert.That(binding.Model.name, Is.EqualTo("STONE L" + level));
            Assert.That(root.Cast<Transform>().Count(t => t.gameObject.activeSelf && t.name.StartsWith("STONE L")), Is.EqualTo(1));
            Assert.That(binding.Model.GetComponentsInChildren<Renderer>().All(r => !r.HasPropertyBlock()), Is.True);
            Assert.That(binding.ShotSource, Is.Not.Null);
            Record("verified ID=" + id.Value + " L" + level);
        }
        private void Record(string message)
        {
            var s = Battle.Simulation;
            _trace.Add($"frame={Time.frameCount}|{message}|wave={s.State.WaveIndex+1}|tick={s.State.Tick}|phase={s.State.Phase}|gold={s.State.Gold}|hp={s.State.BaseHealth}|kills={s.State.Kills}");
        }
        private IEnumerator Shot(string name)
        {
            Call(Battle, "LateUpdate"); yield return null; Record("SHOT " + name);
            if (!Capture) yield break;
#if UNITY_EDITOR
            // Cyan async-shader placeholders не являются visual evidence модели.
            for (var frame = 0; frame < 10; frame++) yield return null;
            var guard = 0;
            while (UnityEditor.ShaderUtil.anythingCompiling && guard++ < 600) yield return null;
            Assert.That(UnityEditor.ShaderUtil.anythingCompiling, Is.False);
#endif
            yield return new WaitForEndOfFrame();
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            Assert.That(image.width, Is.EqualTo(1600)); Assert.That(image.height, Is.EqualTo(900));
            Directory.CreateDirectory(Output); File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG()); Object.Destroy(image);
        }
        private static void Label()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>();
            var go = new GameObject("ART3D evidence label", typeof(RectTransform), typeof(Text)); go.transform.SetParent(canvas.transform, false);
            var text = go.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 16;
            text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            text.text = "ART3d | natural public commands / test clock | Editor Play Mode | generic beam: final VFX #47";
            var rect = text.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0); rect.pivot = new Vector2(.5f, 0);
            rect.anchoredPosition = new Vector2(0, 4); rect.sizeDelta = new Vector2(1500, 30);
        }
        private void ConfigureGameView()
        {
#if UNITY_EDITOR
            var assembly = typeof(UnityEditor.Editor).Assembly; var type = assembly.GetType("UnityEditor.GameViewSizes");
            var sizes = typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(type).GetProperty("instance").GetValue(null);
            var group = type.GetMethod("GetGroup").Invoke(sizes, new[] { type.GetProperty("currentGroupType").GetValue(sizes) });
            _gameView = UnityEditor.EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
            _gameViewSizeProperty = _gameView.GetType().GetProperty("selectedSizeIndex", Flags | BindingFlags.Public);
            _previousGameViewSize = (int)_gameViewSizeProperty.GetValue(_gameView);
            var count = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
            var selected = -1;
            for (var i = 0; i < count; i++)
            {
                var candidate = group.GetType().GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                var candidateType = candidate.GetType();
                if ((string)candidateType.GetProperty("baseText").GetValue(candidate) == "ART3d evidence" &&
                    (int)candidateType.GetProperty("width").GetValue(candidate) == 1600 &&
                    (int)candidateType.GetProperty("height").GetValue(candidate) == 900)
                { selected = i; break; }
            }
            if (selected < 0)
            {
                var kind = Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"), "FixedResolution");
                var size = Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"), new object[] { kind, 1600, 900, "ART3d evidence" });
                group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
                selected = count;
            }
            _gameViewSizeProperty.SetValue(_gameView, selected);
            ((UnityEditor.EditorWindow)_gameView).Focus();
#endif
        }
        private void RestoreGameView()
        {
#if UNITY_EDITOR
            if (_gameView != null && _previousGameViewSize >= 0)
                _gameViewSizeProperty.SetValue(_gameView, _previousGameViewSize);
            _gameView = null;
            _previousGameViewSize = -1;
#endif
        }

    }
}
