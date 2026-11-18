using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Veilward.Application;
using Veilward.Presentation;

namespace Veilward.Tests.PlayMode
{
    public sealed class AbilityPlayModeTests
    {
        [UnityTest]
        public IEnumerator RegBattle208_HistoricalContentUsesWiredControllersButAbilityIsHidden()
        {
            BattleSessionContext.Clear();
            BattleSessionContext.SetNewGame(new Veilward.Infrastructure.WaveCheckpointStore(System.IO.Path.Combine("TestResults","HistoricalAbility")),"eight-wave-unlocks-v1");
            yield return SceneManager.LoadSceneAsync("Battle"); yield return null;
            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            Assert.That(battle.GetComponent("AbilityController"), Is.Not.Null, "REG208 saved scene ability refs");
            var button = Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(b => b.name == "Ability Button");
            Assert.That(button.gameObject.activeSelf, Is.False); button.onClick.Invoke();
            Assert.That(battle.Simulation.AbilityState.NextCastSequence, Is.EqualTo(1));
            Assert.That(battle.ActiveModalOwner, Is.EqualTo(BattleInputOwner.ElementChoice));
        }
        private BattleSceneController _battle;
        private BattlePlacementController _placement;
        private AbilityController _ability;
        private const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags | System.Reflection.BindingFlags.Public).Invoke(target, args);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        private static UnityEngine.UI.Button Button(string name) => Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(b => b.name == name);
        internal const string Content = "synthetic-p24-ability-v1";
        internal static Veilward.Domain.BattleSimulation Synthetic(BattleScenarioConfig config, int hp = 1000, float speed = .1f, int count = 2)
        {
            var p = config.CreateSimulation();
            var enemy = new Veilward.Domain.EnemyDefinition(new Veilward.Domain.EnemyDefinitionId("ability-ui-fixture"), hp, speed, 1, 5);
            var waves = Enumerable.Range(0, 8).Select(_ => new Veilward.Domain.WaveDefinition(enemy, count, 0, 60)).ToArray();
            var constructor = typeof(Veilward.Domain.BattleSimulation).GetConstructors(Flags)
                .Single(c => c.GetParameters().Last().Name == "abilityDefinition");
            return (Veilward.Domain.BattleSimulation)constructor.Invoke(new object[] { p.Grid.Map, p.Path, waves, p.TowerCatalog, 20, 150, 20, false,
                p.ReactionCatalog, new Veilward.Domain.ElementUnlockRules(new[] { Veilward.Domain.ElementKind.Flame, Veilward.Domain.ElementKind.Gale }, new[] { 3, 6 }), Veilward.Domain.AbilityDefinition.RiftSeal });
        }
        private IEnumerator Enabled(int hp = 1000, float speed = .1f, int count = 2)
        {
            BattleSessionContext.Clear(); yield return SceneManager.LoadSceneAsync("Battle"); yield return null;
            _battle = Object.FindFirstObjectByType<BattleSceneController>(); _battle.enabled = false;
            _placement = _battle.GetComponent<BattlePlacementController>(); _ability = _battle.GetComponent<AbilityController>();
            var config = (BattleScenarioConfig)_battle.GetType().GetField("scenario", Flags).GetValue(_battle);
            var s = Synthetic(config, hp, speed, count); s.ChooseElement(new Veilward.Domain.ChooseElementCommand("start-resonance", "flame"));
            Set(_battle, "_simulation", s);
            _battle.GetComponent<BattleCheckpointController>().Configure(null, s, Content); _battle.ResetPresentation();
            _battle.StartWave(); _ability.Refresh(); yield return null; yield return null;
        }
        private Veilward.Domain.PathPosition Center => _battle.Simulation.Path.GetPosition(0);
        private void Confirm() { Button("Confirm Ability Button").onClick.Invoke(); Call(_battle, "LateUpdate"); }
        [UnityTest]
        public IEnumerator RegBattle204_EnabledButtonsWorldMouseAndTouchDispatcherMoveWithoutCast()
        {
            yield return Enabled(); Button("Ability Button").onClick.Invoke(); yield return null; yield return null;
            var screen = (Vector2)Camera.main.WorldToScreenPoint(new Vector3(Center.X, 0, Center.Y));
            _placement.DispatchPointer(screen, -1, UnityEngine.InputSystem.TouchPhase.Began);
            Assert.That(_ability.HasTarget, Is.True); Assert.That(_battle.Simulation.AbilityState.NextCastSequence, Is.EqualTo(1));
            _placement.DispatchPointer(screen, -1, UnityEngine.InputSystem.TouchPhase.Ended);
            _placement.DispatchPointer(screen + new Vector2(5, 0), 11, UnityEngine.InputSystem.TouchPhase.Began);
            Assert.That(_ability.HasTarget, Is.True);
            _placement.DispatchPointer(screen, 11, UnityEngine.InputSystem.TouchPhase.Ended);
            Confirm(); Assert.That(_battle.Simulation.AbilityState.NextCastSequence, Is.EqualTo(2));
            Assert.That(_ability.LastCast.Value.TotalAppliedHpLoss, Is.EqualTo(120));
        }
        [UnityTest]
        public IEnumerator RegBattle205_TwoPointersAndHeldThroughPauseRequireReleaseNewDown()
        {
            yield return Enabled(); Button("Ability Button").onClick.Invoke(); yield return null; yield return null;
            var screen = (Vector2)Camera.main.WorldToScreenPoint(new Vector3(Center.X, 0, Center.Y));
            _placement.DispatchPointer(screen, 10, UnityEngine.InputSystem.TouchPhase.Began);
            var pause = _battle.GetComponent<BattlePauseController>(); pause.Pause(); pause.Resume();
            yield return null; yield return null;
            Button("Ability Button").onClick.Invoke(); yield return null; yield return null;
            _placement.DispatchPointer(screen, 10, UnityEngine.InputSystem.TouchPhase.Began);
            _placement.DispatchPointer(screen, 11, UnityEngine.InputSystem.TouchPhase.Began);
            Assert.That(_ability.HasTarget, Is.False);
            _placement.DispatchPointer(screen, 10, UnityEngine.InputSystem.TouchPhase.Ended);
            _placement.DispatchPointer(screen, 11, UnityEngine.InputSystem.TouchPhase.Canceled);
            _placement.DispatchPointer(screen, 12, UnityEngine.InputSystem.TouchPhase.Began);
            Assert.That(_ability.HasTarget, Is.True);
            Button("Confirm Ability Button").onClick.Invoke(); Button("Confirm Ability Button").onClick.Invoke();
            Call(_battle, "LateUpdate"); Assert.That(_battle.Simulation.AbilityState.NextCastSequence, Is.EqualTo(2));
        }
        [UnityTest]
        public IEnumerator RegBattle206_PauseFreezesAbilityEffectsAndCooldownNoAutoResume()
        {
            yield return Enabled(); _ability.BeginPreview(); _ability.SelectTarget(Center); Confirm();
            var effect = _battle.GetComponentsInChildren<AbilityEffectView>(true).First(e => e.RemainingSeconds > 0);
            var remaining = effect.RemainingSeconds; var tick = _battle.Simulation.State.Tick;
            var pause = _battle.GetComponent<BattlePauseController>(); pause.OnApplicationPause(true);
            Call(_battle, "Update"); yield return null; Call(_battle, "Update");
            Assert.That(effect.RemainingSeconds, Is.EqualTo(remaining)); Assert.That(_battle.Simulation.State.Tick, Is.EqualTo(tick));
            Assert.That(_battle.Simulation.AbilityState.CooldownRemainingTicks, Is.EqualTo(600));
            pause.OnApplicationPause(false); Assert.That(pause.IsPaused, Is.True); pause.Resume(); Call(_battle, "Update");
            Assert.That(_battle.Simulation.State.Tick, Is.EqualTo(tick)); Assert.That(_battle.Simulation.AbilityState.NextCastSequence, Is.EqualTo(2));
        }
        [UnityTest]
        public IEnumerator RegBattle207_LethalCompletionFailedSaveRetryAndDirtyFreshContinue()
        {
            yield return Enabled(hp:100, count:1);
            var s = _battle.Simulation; var checkpoint = _battle.GetComponent<BattleCheckpointController>();
            var directory = System.IO.Path.Combine("TestResults", "AbilityPlay", System.Guid.NewGuid().ToString("N"));
            var store = new Veilward.Infrastructure.WaveCheckpointStore(directory);
            checkpoint.Configure(store, s, Content);
            // Полный durable pre-Start snapshot отдельно от live Combat; synthetic test clock.
            var config = (BattleScenarioConfig)_battle.GetType().GetField("scenario", Flags).GetValue(_battle);
            var fresh = Synthetic(config, 100, .1f, 1); fresh.ChooseElement(new Veilward.Domain.ChooseElementCommand("start-resonance", "flame"));
            Assert.That(store.TrySave(new Veilward.Infrastructure.WaveCheckpointEnvelope { schemaVersion=4, scenarioContentId=Content, state=fresh.ExportCheckpoint() }, s.TryValidateCheckpoint, out _), Is.True);
            var durable = new Veilward.Infrastructure.WaveCheckpointEnvelope { schemaVersion=4, scenarioContentId=Content, state=fresh.ExportCheckpoint() };
            System.IO.Directory.CreateDirectory(store.TempPath);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Checkpoint save failed:"));
            _ability.BeginPreview(); _ability.SelectTarget(Center); Confirm();
            Assert.That(s.State.Phase, Is.EqualTo(Veilward.Domain.BattlePhase.WaveComplete)); Assert.That(s.State.Kills, Is.EqualTo(1));
            Assert.That(s.State.Gold, Is.EqualTo(175)); Assert.That(checkpoint.LastSaveSucceeded, Is.False);
            var sequence = s.AbilityState.NextCastSequence;
            System.IO.Directory.Delete(store.TempPath); Button("Retry Save Button").onClick.Invoke();
            Assert.That(checkpoint.LastSaveSucceeded, Is.True); Assert.That(s.AbilityState.NextCastSequence, Is.EqualTo(sequence));
            Assert.That(s.State.Gold, Is.EqualTo(175));
            checkpoint.RestoreCheckpoint(durable);
            Assert.That(_ability.HasInputOwnership, Is.False); Assert.That(_ability.Status, Is.Empty);
            Assert.That(_battle.GetComponentsInChildren<AbilityEffectView>(true).All(e => e.RemainingSeconds == 0), Is.True);
            Assert.That(s.AbilityState.NextCastSequence, Is.EqualTo(1)); Assert.That(s.State.Gold, Is.EqualTo(150));
            Set(_battle, "_simulation", fresh); checkpoint.Configure(store, fresh, Content); _battle.ResetPresentation(); checkpoint.RestoreCheckpoint(durable);
            Assert.That(fresh.State.Phase, Is.EqualTo(Veilward.Domain.BattlePhase.Preparation)); Assert.That(_ability.HasTarget, Is.False);
            System.IO.Directory.Delete(directory, true);
            yield return null;
        }
        [UnityTest]
        public IEnumerator RegBattle205_QueuedMouseAcceptsFreshDownAfterOwnerSwitch()
        {
            yield return Enabled();
            var settings=UnityEngine.InputSystem.InputSystem.settings;
            var background=settings.backgroundBehavior;
#if UNITY_EDITOR
            var editorInput=settings.editorInputBehaviorInPlayMode;
            settings.editorInputBehaviorInPlayMode=UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            settings.backgroundBehavior=UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            var mouse = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            UnityEngine.InputSystem.InputSystem.EnableDevice(mouse);
            try
            {
                Button("Ability Button").onClick.Invoke(); yield return null; yield return null; yield return null; yield return null;
                var screen = (Vector2)Camera.main.WorldToScreenPoint(new Vector3(Center.X,0,Center.Y));
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position=screen }.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));
                UnityEngine.InputSystem.InputSystem.Update(); Call(_placement,"Update"); yield return null; Assert.That(_ability.HasTarget,Is.True,"real Mouse Update -> dispatcher "+InputDiagnostic(mouse,screen));
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState { position=screen });
                yield return null; yield return null; Button("Cancel Ability Button").onClick.Invoke(); yield return null; yield return null;
                Button("Ability Button").onClick.Invoke(); yield return null; yield return null;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState { position=screen }.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));
                UnityEngine.InputSystem.InputSystem.Update(); Call(_placement,"Update"); yield return null; Assert.That(_ability.HasTarget,Is.True,"release then new Down must work");
            }
            finally { UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse); settings.backgroundBehavior=background;
#if UNITY_EDITOR
                settings.editorInputBehaviorInPlayMode=editorInput;
#endif
            }
        }
        [UnityTest]
        public IEnumerator RegBattle205_QueuedTouchUiDownCannotBecomeWorldTapOnUp()
        {
            yield return Enabled();
            var settings=UnityEngine.InputSystem.InputSystem.settings;
            var background=settings.backgroundBehavior;
#if UNITY_EDITOR
            var editorInput=settings.editorInputBehaviorInPlayMode;
            settings.editorInputBehaviorInPlayMode=UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            settings.backgroundBehavior=UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            var touch = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Touchscreen>();
            UnityEngine.InputSystem.InputSystem.EnableDevice(touch);
            try
            {
                Button("Ability Button").onClick.Invoke(); yield return null; yield return null; yield return null; yield return null;
                var screen = (Vector2)Camera.main.WorldToScreenPoint(new Vector3(Center.X,0,Center.Y));
                var ui = (Vector2)Button("Pause Button").transform.position;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(touch,new UnityEngine.InputSystem.LowLevel.TouchState { touchId=99, phase=UnityEngine.InputSystem.TouchPhase.Began, position=ui });
                yield return null; yield return null;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(touch,new UnityEngine.InputSystem.LowLevel.TouchState { touchId=99, phase=UnityEngine.InputSystem.TouchPhase.Ended, position=screen });
                UnityEngine.InputSystem.InputSystem.Update(); Call(_placement,"Update"); yield return null; Assert.That(_ability.HasTarget,Is.False);
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(touch,new UnityEngine.InputSystem.LowLevel.TouchState { touchId=100, phase=UnityEngine.InputSystem.TouchPhase.Began, position=screen });
                UnityEngine.InputSystem.InputSystem.Update(); Call(_placement,"Update"); yield return null; Assert.That(_ability.HasTarget,Is.True,"fresh touch Down reaches production dispatcher "+InputDiagnostic(touch,screen));
            }
            finally { UnityEngine.InputSystem.InputSystem.RemoveDevice(touch); settings.backgroundBehavior=background;
#if UNITY_EDITOR
                settings.editorInputBehaviorInPlayMode=editorInput;
#endif
            }
        }
        private string InputDiagnostic(UnityEngine.InputSystem.InputDevice device, Vector2 point)
        {
            return $"enabled={device.enabled} placement={_placement.enabled} owner={_battle.ActiveModalOwner} paused={_battle.IsPaused} status={_ability.Status} target={_ability.HasTarget} point={point} frame={Time.frameCount} suppressed={_placement.GetType().GetField("_pointerSuppressedUntilFrame",Flags).GetValue(_placement)} held="+
                string.Join(",",(System.Collections.Generic.HashSet<int>)_placement.GetType().GetField("_heldPointers",Flags).GetValue(_placement));
        }
        [UnityTest] public IEnumerator RegBattle208_VisualWideScenario() { yield return Visual(1600,900); }
        [UnityTest] public IEnumerator RegBattle208_VisualNarrowScenario() { yield return Visual(1280,960); }
        private bool _capture;
        private int _visualWidth, _visualHeight;
        private readonly System.Collections.Generic.List<string> _visualTrace = new System.Collections.Generic.List<string>();
        private IEnumerator Visual(int width, int height)
        {
            _capture = System.Environment.GetEnvironmentVariable("P24D_CAPTURE") == "1";
            _visualWidth=width; _visualHeight=height; _visualTrace.Clear();
            if (_capture) ConfigureGameView(width,height);
            var console = new System.Collections.Generic.List<string>(); var unexpected = 0; var expected = 0;
            UnityEngine.Application.LogCallback listener = (message,stack,type) => {
                if(type==LogType.Error && message.StartsWith("Checkpoint save failed:") && expected>0) { expected--; console.Add("EXPECTED|"+message); }
                else if(type==LogType.Error || type==LogType.Assert || type==LogType.Exception) { unexpected++; console.Add(type+"|"+message); }
                else if(type==LogType.Warning) console.Add(type+"|"+message);
            };
            UnityEngine.Application.logMessageReceived += listener;
            var root = System.IO.Path.Combine("TestResults","AbilityVisual",width+"x"+height);
            try
            {
                yield return Enabled(); SyntheticLabel();
                Assert.That(_ability.BeginPreview(),Is.True); _ability.CancelPreview();
                yield return Capture("01-ready");
                Button("Ability Button").onClick.Invoke(); _ability.SelectTarget(Center); yield return Capture("02-preview");
                _ability.SelectTarget(new Veilward.Domain.PathPosition(Center.X+.4f,Center.Y+.3f)); yield return Capture("03-moved-center");
                Confirm(); Assert.That(_ability.LastCast.Value.Succeeded,Is.True); yield return Capture("04-confirm-hits");
                Assert.That(_battle.Simulation.AbilityState.CooldownRemainingTicks,Is.EqualTo(600)); yield return Capture("05-cooldown");
                var pause=_battle.GetComponent<BattlePauseController>(); pause.Pause(); yield return Capture("06-pause");
                pause.Resume(); Call(_battle,"Update"); yield return Capture("07-resume");
                yield return Enabled(); SyntheticLabel(); _ability.BeginPreview(); _ability.SelectTarget(Center);
                Button("Cancel Ability Button").onClick.Invoke(); Assert.That(_battle.Simulation.AbilityState.NextCastSequence,Is.EqualTo(1));
                yield return Capture("08-cancel");
                // Отдельная lethal fixture для completion и IO; не natural replay.
                yield return Enabled(hp:100,count:1); SyntheticLabel();
                var cp=_battle.GetComponent<BattleCheckpointController>(); var s=_battle.Simulation;
                var store=new Veilward.Infrastructure.WaveCheckpointStore(root); cp.Configure(store,s,Content);
                System.IO.Directory.CreateDirectory(store.TempPath); expected++;
                LogAssert.Expect(LogType.Error,new System.Text.RegularExpressions.Regex("Checkpoint save failed:"));
                _ability.BeginPreview(); _ability.SelectTarget(Center); Confirm();
                Assert.That(cp.LastSaveSucceeded,Is.False); Assert.That(s.State.Kills,Is.EqualTo(1));
                yield return Capture("09-failed-save");
                System.IO.Directory.Delete(store.TempPath); Button("Retry Save Button").onClick.Invoke();
                Assert.That(cp.LastSaveSucceeded,Is.True); yield return Capture("10-retry-saved");
                Assert.That(store.TryLoad(Content,s.TryValidateCheckpoint,out var loaded),Is.True);
                // Fresh scene + exact-ID internal test path, production menu intentionally rejects synthetic.
                yield return Enabled(hp:100,count:1); SyntheticLabel();
                cp=_battle.GetComponent<BattleCheckpointController>(); cp.Configure(store,_battle.Simulation,Content);
                cp.RestoreCheckpoint(loaded.Envelope); _ability.Refresh();
                Assert.That(_ability.HasInputOwnership,Is.False); Assert.That(_ability.HasTarget,Is.False);
                Assert.That(_battle.GetComponentsInChildren<AbilityEffectView>(true).All(e=>e.RemainingSeconds==0),Is.True);
                yield return Capture("11-clean-continue");
                Assert.That(expected,Is.Zero); Assert.That(unexpected,Is.Zero,string.Join("\n",console));
            }
            finally
            {
                UnityEngine.Application.logMessageReceived -= listener;
                if(_capture)
                {
                    var path="TestResults/P2.4d/"+width+"x"+height; System.IO.Directory.CreateDirectory(path);
                    System.IO.File.WriteAllLines(path+"/console.txt",console);
                    System.IO.File.WriteAllLines(path+"/telemetry.txt",_visualTrace);
                }
                if(System.IO.Directory.Exists(root)) System.IO.Directory.Delete(root,true);
            }
        }
        private void SyntheticLabel()
        {
            var label=new GameObject("Synthetic Ability Evidence Label",typeof(RectTransform),typeof(UnityEngine.UI.Text));
            label.transform.SetParent(Object.FindFirstObjectByType<Canvas>().transform,false);
            var text=label.GetComponent<UnityEngine.UI.Text>(); text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize=24; text.alignment=TextAnchor.MiddleCenter; text.text="SYNTHETIC synthetic-p24-ability-v1 • test clock • Editor Play Mode"; text.raycastTarget=false;
            var rect=text.rectTransform; rect.anchorMin=rect.anchorMax=new Vector2(.5f,1); rect.pivot=new Vector2(.5f,1); rect.anchoredPosition=new Vector2(0,-8); rect.sizeDelta=new Vector2(1300,45);
        }
        private IEnumerator Capture(string label)
        {
            Call(_battle,"LateUpdate"); _ability.Refresh(); yield return null;
            var s=_battle.Simulation;
            _visualTrace.Add($"{label}|frame={Time.frameCount}|tick={s.State.Tick}|phase={s.State.Phase}|hp={s.State.BaseHealth}|gold={s.State.Gold}|kills={s.State.Kills}|sequence={s.AbilityState.NextCastSequence}|remaining={s.AbilityState.CooldownRemainingTicks}|owner={_battle.ActiveModalOwner}|status={_ability.Status}");
            if(!_capture) yield break;
            yield return new WaitForEndOfFrame();
            var texture=ScreenCapture.CaptureScreenshotAsTexture(); Assert.That(texture,Is.Not.Null);
            Assert.That(texture.width,Is.EqualTo(_visualWidth)); Assert.That(texture.height,Is.EqualTo(_visualHeight));
            var path="TestResults/P2.4d/"+_visualWidth+"x"+_visualHeight; System.IO.Directory.CreateDirectory(path);
            System.IO.File.WriteAllBytes(path+"/"+label+".png",texture.EncodeToPNG()); Object.Destroy(texture);
        }
        private static void ConfigureGameView(int width,int height)
        {
#if UNITY_EDITOR
            var a=typeof(UnityEditor.Editor).Assembly; var sizesType=a.GetType("UnityEditor.GameViewSizes");
            var singleton=typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes=singleton.GetProperty("instance").GetValue(null); var groupType=sizesType.GetProperty("currentGroupType").GetValue(sizes);
            var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new[]{groupType});
            var kind=System.Enum.Parse(a.GetType("UnityEditor.GameViewSizeType"),"FixedResolution");
            var size=System.Activator.CreateInstance(a.GetType("UnityEditor.GameViewSize"),new object[]{kind,width,height,"P2.4d evidence"});
            group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size}); var count=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);
            var view=UnityEditor.EditorWindow.GetWindow(a.GetType("UnityEditor.GameView"));
            view.GetType().GetProperty("selectedSizeIndex",Flags|System.Reflection.BindingFlags.Public).SetValue(view,count-1);view.Focus();
#endif
        }
    }
}
