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
    public sealed class FlameModelPlayModeTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private string _root;
        private WaveCheckpointStore _store;
        private readonly List<string> _trace = new List<string>();
        private readonly List<string> _console = new List<string>();
        private bool Capture => Environment.GetEnvironmentVariable("ART3A_CAPTURE") == "1";
        private const string Output = "TestResults/ART3A";
        private BattleSceneController Battle => Object.FindFirstObjectByType<BattleSceneController>();
        private static void Call(object target, string method) => target.GetType().GetMethod(method, Flags).Invoke(target, null);

        [SetUp] public void Setup()
        {
            _root = Path.GetFullPath(Path.Combine("TestResults", "Art3aSaves", Guid.NewGuid().ToString("N")));
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
            File.WriteAllLines(Output + "/natural-trace.txt", _trace);
            File.WriteAllLines(Output + "/console.txt", _console);
            MainMenuController.CheckpointStoreFactory = null; BattleSessionContext.Clear();
            yield return SceneManager.LoadSceneAsync("Battle"); yield return null;
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [UnityTest] public IEnumerator Art3A03_NaturalBattleLevelsContinueRollbackSellAndRestart()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu"); yield return null;
            Object.FindFirstObjectByType<MainMenuController>().NewGameButton.onClick.Invoke();
            yield return null; yield return null; Battle.enabled = false; Label();
            Assert.That(Battle.Simulation.State.Gold, Is.EqualTo(150));
            Assert.That(Battle.ChooseElement(new ChooseElementCommand("start-resonance", "flame")).Success, Is.True);
            var script = AbilityBossTrace.Load("flame");
            for (var wave = 1; wave <= 6; wave++)
            {
                var s = Battle.Simulation;
                foreach (var command in script.commands.Where(c => c.wave == wave))
                {
                    Assert.That(command.op, Is.EqualTo("place"));
                    Assert.That(Battle.PlaceTower(new TowerDefinitionId(command.type), new GridCoordinate(command.x, command.y)).Success, Is.True);
                }
                Call(Battle, "LateUpdate");
                if (wave == 1)
                {
                    CheckLevel(new TowerId(1), 1);
                    yield return Shot("01-natural-L1");
                    yield return ContinueAndCheck(1);
                    s = Battle.Simulation;
                }
                Assert.That(Battle.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
                var startTick = s.State.Tick; var guard = 0; var combatShot = false;
                while (s.State.Phase == BattlePhase.Combat && guard++ < 20000)
                {
                    s.Step();
                    foreach (var cast in script.casts.Where(c => c.wave == wave && c.relativeTick == s.State.Tick - startTick))
                    {
                        Assert.That(s.CastAbility(new CastAbilityCommand(s.AbilityState.AbilityId,
                            new PathPosition(cast.x, cast.y), s.State.WaveIndex, s.State.Tick,
                            s.AbilityState.NextCastSequence)).Succeeded, Is.True);
                    }
                    var attacking = s.AttackEvents.Count > 0;
                    Call(Battle, "ProcessAttackEvents"); Call(Battle, "LateUpdate");
                    if (wave == 1 && attacking && !combatShot)
                    { combatShot = true; yield return Shot("02-natural-combat-generic-beam"); }
                    if (guard % 120 == 0) yield return null;
                }
                Assert.That(guard, Is.LessThan(20000));
                Assert.That(s.State.Phase, Is.EqualTo(BattlePhase.WaveComplete));
                Record("wave complete");
                if (s.State.ElementProgression.PendingChoiceId != "none")
                    Assert.That(Battle.ChooseElement(new ChooseElementCommand(s.State.ElementProgression.PendingChoiceId,
                        wave == 3 ? script.after3 : script.after6)).Success, Is.True);
                Assert.That(Battle.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.Continued));
            }
            // Законная ликвидация части обороны: она оплачивает модельный proof, не обещает победу.
            foreach (var tower in Battle.Simulation.State.Towers.Where(t => t.Id.Value != 1).ToArray())
                Assert.That(Battle.SellTower(tower.Id).Success, Is.True);
            // Test clock сжимает шесть волн: явно чистим presentation pools предыдущих волн.
            Battle.ResetPresentation();
            var id = new TowerId(1);
            Assert.That(Battle.UpgradeTower(id).Success, Is.True);
            Call(Battle, "LateUpdate"); CheckLevel(id, 2);
            yield return Shot("03-natural-L2"); yield return ContinueAndCheck(2);
            Assert.That(Battle.UpgradeTower(id).Success, Is.True);
            Call(Battle, "LateUpdate"); CheckLevel(id, 3);
            yield return Shot("04-natural-L3"); yield return ContinueAndCheck(3);
            Directory.CreateDirectory(Output);
            File.Copy(_store.PrimaryPath, Output + "/natural-L3-checkpoint.json", true);
            var saved = AbilityBossTrace.Wire(Battle.Simulation);
            Assert.That(Battle.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
            for (var tick = 0; tick < 60; tick++)
            { Battle.Simulation.Step(); Call(Battle, "ProcessAttackEvents"); Call(Battle, "LateUpdate"); if (tick % 15 == 0) yield return null; }
            yield return Shot("05-natural-L3-interrupted-combat");
            yield return ContinueAndCheck(3);
            Assert.That(AbilityBossTrace.Wire(Battle.Simulation), Is.EqualTo(saved), "Combat rollback preserves full Preparation");
            var before = Battle.Simulation.State.Gold;
            Assert.That(Battle.UpgradeTower(id).Success, Is.False);
            Assert.That(Battle.Simulation.State.Gold, Is.EqualTo(before)); CheckLevel(id, 3);
            var old = GameObject.Find("Tower 1");
            Battle.ResetPresentation();
            Assert.That(old.activeSelf, Is.False, "old root is hidden before deferred Destroy"); CheckLevel(id, 3);
            yield return null;
            CheckLevel(id, 3);
            Assert.That(Battle.SellTower(id).Success, Is.True);
            var afterSell = Battle.Simulation.State.Gold;
            Assert.That(Battle.SellTower(id).Success, Is.False);
            Assert.That(Battle.Simulation.State.Gold, Is.EqualTo(afterSell));
            yield return null;
            Assert.That(Object.FindObjectsByType<FlameModelBinding>(FindObjectsSortMode.None), Is.Empty);
            yield return Shot("06-natural-sold-no-roots");
            Battle.ReturnToMenu(); yield return null; yield return null;
            Object.FindFirstObjectByType<MainMenuController>().NewGameButton.onClick.Invoke();
            yield return null; yield return null;
            Assert.That(Battle.Simulation.State.Towers, Is.Empty);
            Assert.That(Object.FindObjectsByType<FlameModelBinding>(FindObjectsSortMode.None), Is.Empty);
            Assert.That(_console.Any(c => c.StartsWith("Error|") || c.StartsWith("Exception|") || c.StartsWith("Assert|")), Is.False, string.Join("\n", _console));
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
                Battle.enabled = false; Label(); CheckLevel(new TowerId(1), level);
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
            var binding = root.GetComponent<FlameModelBinding>();
            Assert.That(binding.Level, Is.EqualTo(level)); Assert.That(tower.Level, Is.EqualTo(level));
            Assert.That(binding.Model.name, Is.EqualTo("FLAME L" + level));
            Assert.That(root.Cast<Transform>().Count(t => t.gameObject.activeSelf && t.name.StartsWith("FLAME L")), Is.EqualTo(1));
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
            var go = new GameObject("ART3A evidence label", typeof(RectTransform), typeof(Text)); go.transform.SetParent(canvas.transform, false);
            var text = go.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 16;
            text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            text.text = "ART3a | natural public commands / test clock | Editor Play Mode | generic beam: final VFX #47";
            var rect = text.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0); rect.pivot = new Vector2(.5f, 0);
            rect.anchoredPosition = new Vector2(0, 4); rect.sizeDelta = new Vector2(1500, 30);
        }
        private static void ConfigureGameView()
        {
#if UNITY_EDITOR
            var assembly = typeof(UnityEditor.Editor).Assembly; var type = assembly.GetType("UnityEditor.GameViewSizes");
            var sizes = typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(type).GetProperty("instance").GetValue(null);
            var group = type.GetMethod("GetGroup").Invoke(sizes, new[] { type.GetProperty("currentGroupType").GetValue(sizes) });
            var kind = Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"), "FixedResolution");
            var size = Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"), new object[] { kind, 1600, 900, "ART3a evidence" });
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            var view = UnityEditor.EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
            view.GetType().GetProperty("selectedSizeIndex", Flags | BindingFlags.Public).SetValue(view, (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null)-1); view.Focus();
#endif
        }
    }
}
