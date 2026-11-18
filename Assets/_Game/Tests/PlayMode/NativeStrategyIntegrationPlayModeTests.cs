using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
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
    public sealed class NativeStrategyIntegrationPlayModeTests
    {
        [Serializable] private sealed class Script { public string route, start, after3, after6; public Command[] commands; }
        [Serializable] private sealed class ExpectedReport { public ExpectedWave[] waves; }
        [Serializable] private sealed class ExpectedWave { public BattleCheckpoint state; }
        [Serializable] private sealed class Command { public int wave, x, y, id; public string op, type; }
        private string root;
        private WaveCheckpointStore store;
        private List<string> trace;
        private BattleSceneController Battle => Object.FindFirstObjectByType<BattleSceneController>();
        private BattleCheckpointController Checkpoint => Object.FindFirstObjectByType<BattleCheckpointController>();
        private ElementChoiceController Choice => Object.FindFirstObjectByType<ElementChoiceController>();
        private bool capture;
        private int expectedSaveErrors;
        private readonly List<string> console = new List<string>();
        private static void HistoricalMenu(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "MainMenu") return;
            var menu = Object.FindFirstObjectByType<MainMenuController>();
            var field=typeof(MainMenuController).GetField("scenario",BindingFlags.Instance|BindingFlags.NonPublic);
            var active=(BattleScenarioConfig)field.GetValue(menu);
            var source=(BattleScenarioConfig)typeof(BattleScenarioConfig).GetField("historicalScenario",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(active);
            if (source != null) field.SetValue(menu,source);
        }
        [SetUp] public void SetUp()
        {
            SceneManager.sceneLoaded += HistoricalMenu;
            root = Path.Combine(Path.GetTempPath(), "veilward-p23e1-menu-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            store = new WaveCheckpointStore(root); MainMenuController.CheckpointStoreFactory = () => store; BattleSessionContext.Clear();
            console.Clear(); expectedSaveErrors = 0; trace = new List<string>(); capture = Environment.GetEnvironmentVariable("P23E1_CAPTURE") == "1";
            if (capture) ConfigureGameView();
            UnityEngine.Application.logMessageReceived += ConsoleMessage;
        }
        private static void ConfigureGameView()
        {
#if UNITY_EDITOR
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes = singleton.GetProperty("instance").GetValue(null);
            var groupType = sizesType.GetProperty("currentGroupType").GetValue(sizes);
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { groupType });
            var kind = Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"), "FixedResolution");
            var size = Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"), new object[] { kind, 1920, 1080, "P2.3e 1920x1080" });
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            var count = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
            var view = UnityEditor.EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
            view.GetType().GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(view, count - 1);
            view.Focus();
#endif
        }
        private void ConsoleMessage(string message, string stack, LogType type)
        { if (type == LogType.Error && expectedSaveErrors > 0 && message.StartsWith("Checkpoint save failed:", StringComparison.Ordinal)) { expectedSaveErrors--; trace.Add("EXPECTED SAVE ERROR|" + message); return; }
          if (type == LogType.Warning || type == LogType.Error || type == LogType.Exception || type == LogType.Assert) console.Add(type + "|" + message); }
        [UnityTearDown] public IEnumerator TearDown()
        {
            UnityEngine.Application.logMessageReceived -= ConsoleMessage;
            Directory.CreateDirectory("TestResults/P2.3e.1");
            var path = "TestResults/P2.3e.1/" + TestContext.CurrentContext.Test.Name;
            File.WriteAllLines(path + ".menu-trace.txt", trace); File.WriteAllLines(path + ".console.txt", console);
            SceneManager.sceneLoaded -= HistoricalMenu;
            BattleSessionContext.Clear(); yield return SceneManager.LoadSceneAsync("Battle"); yield return null;
            MainMenuController.CheckpointStoreFactory = null; if (Directory.Exists(root)) Directory.Delete(root, true);
        }
        [UnityTest] public IEnumerator RegBattle189_ProductionMenuNaturalFlameContinue() { yield return Route("A-coverage"); }
        [UnityTest] public IEnumerator RegBattle189_ProductionMenuNaturalGaleContinue() { yield return Route("B-coverage"); }
        private IEnumerator Route(string route)
        {
            var earlyWave = route.StartsWith("A", StringComparison.Ordinal) ? 4 : 5;
            var script = JsonUtility.FromJson<Script>(File.ReadAllText("Docs/review/P2.3e.1/" + route + ".commands.json"));
            yield return SceneManager.LoadSceneAsync("MainMenu"); yield return null;
            Record("MainMenu NewGame"); Object.FindFirstObjectByType<MainMenuController>().NewGameButton.onClick.Invoke();
            yield return null; yield return null; Battle.enabled = false;
            Assert.That(Battle.Simulation.State.Gold, Is.EqualTo(150)); Assert.That(Battle.Simulation.State.BaseHealth, Is.EqualTo(20));
            Select(script.start); Record("Confirm " + script.start);
            var captured = new HashSet<string>();
            var expectedReport = JsonUtility.FromJson<ExpectedReport>(File.ReadAllText("Docs/review/P2.3e.1/" + route + ".summary.json"));
            while (Battle.Simulation.State.Phase == BattlePhase.Preparation)
            {
                var wave = Battle.Simulation.State.WaveIndex + 1;
                foreach (var c in script.commands.Where(c => c.wave == wave))
                {
                    var before = Battle.Simulation.State.Gold; var placement = Object.FindFirstObjectByType<BattlePlacementController>();
                    if (c.op == "place")
                    {
                        Assert.That(placement.TrySelectCell(new GridCoordinate(c.x, c.y)), Is.True);
                        placement.SelectTowerType(new TowerDefinitionId(c.type)); Button("Confirm Button").onClick.Invoke(); placement.CancelSelection();
                    }
                    else if (c.op == "sell")
                    {
                        var tower = Battle.Simulation.State.Towers.Single(t => t.Id.Value == c.id);
                        Assert.That(placement.TrySelectCell(tower.Coordinate), Is.True);
                        placement.RequestSellSelectedTower(); Assert.That(placement.ConfirmSellSelectedTower().Success, Is.True);
                    }
                    else Battle.UpgradeTower(new TowerId(c.id));
                    Record(c.op + " " + c.type + " gold=" + before + "->" + Battle.Simulation.State.Gold);
                }
                if (wave == 7) yield return Capture(route + "-preparation7");
                var safe = State(); var startKills = Battle.Simulation.State.Kills;
                Button("Start Wave Button").onClick.Invoke(); Assert.That(Battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.Combat));
                var sawProc = false; var rolledBack = false; var guard = 0;
                while (Battle.Simulation.State.Phase == BattlePhase.Combat && guard++ < 20000)
                {
                    var s = Battle.Simulation; s.Step();
                    foreach (var e in s.ReactionEvents)
                    {
                        sawProc |= s.DamageEvents.Any(d => d.Origin == DamageOrigin.Reaction && d.AppliedHpLoss > 0);
                        Record("PROC " + e.Kind + "/" + e.TriggerKind + " tower=" + e.SourceTowerId.Value + " tick=" + e.Tick);
                    }
                    var key = s.ReactionEvents.Count == 0 ? null : s.ReactionEvents[0].Kind + "-" + s.ReactionEvents[0].TriggerKind;
                    Invoke(Battle, "ProcessAttackEvents"); Checkpoint.ObservePhaseTransition();
                    if (key != null && captured.Add(key))
                    {
                        Invoke(Battle, "LateUpdate"); yield return Capture(route + "-production-" + key);
                    }
                    if ((wave == earlyWave || wave == 7) && !rolledBack && sawProc && s.State.Kills > startKills && s.State.Phase == BattlePhase.Combat)
                    {
                        Assert.That(Battle.Presentation.LastSummary, Is.Not.Empty);
                        var bytes = File.ReadAllBytes(store.PrimaryPath);
                        // Продолжение из настоящего меню отбрасывает всю боевую попытку.
                        yield return ContinueThroughMenu(safe, route + "-rollback" + wave);
                        Assert.That(File.ReadAllBytes(store.PrimaryPath), Is.EqualTo(bytes));
                        Assert.That(Battle.Presentation.LastSummary, Is.Empty);
                        Assert.That(Object.FindObjectsByType<ReactionEffectView>(FindObjectsInactive.Include, FindObjectsSortMode.None).All(v => v.RemainingSeconds == 0), Is.True);
                        Assert.That(Object.FindObjectsByType<AttackBeamView>(FindObjectsSortMode.None), Is.Empty);
                        Assert.That(Object.FindFirstObjectByType<EnemyInspectorController>().SelectedEnemyId, Is.Null);
                        Assert.That(Object.FindFirstObjectByType<BattlePlacementController>().HasSelection, Is.False);
                        Assert.That(Choice.PreviewOptionId, Is.Null);
                        Assert.That(Battle.Simulation.AttackEvents, Is.Empty); Assert.That(Battle.Simulation.DamageEvents, Is.Empty); Assert.That(Battle.Simulation.ReactionEvents, Is.Empty);
                        rolledBack = true; Record("PASS clean combat rollback " + wave);
                        Button("Start Wave Button").onClick.Invoke();
                    }
                    if (guard % 120 == 0) { Invoke(Battle, "LateUpdate"); yield return null; }
                }
                Assert.That(guard, Is.LessThan(20000));
                if (wave == earlyWave || wave == 7) Assert.That(rolledBack, Is.True, "Natural Combat kill+positive proc rollback required");
                Assert.That(State(), Is.EqualTo(JsonUtility.ToJson(expectedReport.waves[wave - 1].state)), "Menu/controller full snapshot equals Domain production replay");
                Record("natural wave terminal");
                if (Battle.Simulation.State.Phase != BattlePhase.WaveComplete) break;
                var pending = Battle.Simulation.State.ElementProgression.PendingChoiceId;
                if (pending != "none")
                {
                    var before = State(); Button("Next Wave Button").onClick.Invoke(); Assert.That(State(), Is.EqualTo(before));
                    yield return ContinueThroughMenu(before, route + "-pending" + wave);
                    Assert.That(Choice.PreviewOptionId, Is.Null);
                    if (wave == 3 || wave == 6)
                    {
                        Directory.CreateDirectory(store.TempPath); expectedSaveErrors++;
                        LogAssert.Expect(LogType.Error, new Regex("Checkpoint save failed:.*"));
                        Select(wave == 3 ? script.after3 : script.after6); Assert.That(Checkpoint.LastSaveSucceeded, Is.False);
                        var live = State(); Assert.That(live, Is.Not.EqualTo(before));
                        yield return ContinueThroughMenu(before, route + "-failed-confirm-restart" + wave);
                        Assert.That(Choice.PreviewOptionId, Is.Null);
                        Directory.Delete(store.TempPath); Record("PASS failed save restart restores whole pending");
                    }
                    Select(wave == 3 ? script.after3 : script.after6); var confirmed = State();
                    Button("Retry Save Button").onClick.Invoke(); Assert.That(State(), Is.EqualTo(confirmed));
                    yield return ContinueThroughMenu(confirmed, route + "-confirmed" + wave);
                    Button("Confirm Element Button").onClick.Invoke(); Assert.That(State(), Is.EqualTo(confirmed));
                }
                Button("Next Wave Button").onClick.Invoke(); var next = State();
                Button("Next Wave Button").onClick.Invoke(); Assert.That(State(), Is.EqualTo(next));
            }
            Assert.That(Battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.Victory));
            Assert.That(captured, Is.EquivalentTo(new[]{"Steam-Intrinsic","Steam-StatusPair","Storm-Intrinsic","Storm-StatusPair"}));
            Record("natural outcome"); yield return Capture(route + "-natural-outcome");
            Assert.That(console.Where(line => line != "Warning|Ignoring depth surface load action as it is memoryless" &&
                line != "Warning|Ignoring depth surface store action as it is memoryless"), Is.Empty, "Unexpected Console entries; known Editor rendering warnings remain in audit");
        }
        private void Select(string option)
        { Choice.Refresh(); Choice.OptionButtons.Single(b => b.name == option).onClick.Invoke(); Button("Confirm Element Button").onClick.Invoke(); }
        private IEnumerator ContinueThroughMenu(string expected, string label)
        {
            var bytes = File.ReadAllBytes(store.PrimaryPath); Button("Main Menu Button").onClick.Invoke(); yield return null; yield return null;
            Record("MainMenu Continue " + label);
            Assert.That(Object.FindFirstObjectByType<MainMenuController>().ContinueButton.interactable, Is.True);
            Object.FindFirstObjectByType<MainMenuController>().ContinueButton.onClick.Invoke(); yield return null; yield return null; Battle.enabled = false;
            Assert.That(State(), Is.EqualTo(expected)); Assert.That(File.ReadAllBytes(store.PrimaryPath), Is.EqualTo(bytes));
            Record("Battle restored " + label); yield return Capture(label);
        }
        private IEnumerator Capture(string label)
        {
            if (!capture) yield break;
            Invoke(Battle, "LateUpdate"); yield return null; yield return new WaitForEndOfFrame();
            var image = ScreenCapture.CaptureScreenshotAsTexture(); Assert.That(image, Is.Not.Null);
            Directory.CreateDirectory("TestResults/P2.3e.1/screenshots"); File.WriteAllBytes("TestResults/P2.3e.1/screenshots/" + label + ".png", image.EncodeToPNG());
            trace.Add("SCREENSHOT " + label + " frame=" + Time.frameCount + " size=" + image.width + "x" + image.height); Object.Destroy(image);
        }
        private string State() => JsonUtility.ToJson(Battle.Simulation.ExportCheckpoint());
        private void Record(string label)
        {
            var b = Battle;
            trace.Add("frame=" + Time.frameCount + "|" + label + (b == null ? "" : "|wave=" + (b.Simulation.State.WaveIndex + 1) + " tick=" + b.Simulation.State.Tick + " hp=" + b.Simulation.State.BaseHealth + " gold=" + b.Simulation.State.Gold + " kills=" + b.Simulation.State.Kills + (b.Simulation.State.Phase == BattlePhase.Combat ? "" : "|" + State())));
        }
        private static void Invoke(object target, string method) => target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);
        private static Button Button(string name) => Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(b => b.name == name);
    }
}
