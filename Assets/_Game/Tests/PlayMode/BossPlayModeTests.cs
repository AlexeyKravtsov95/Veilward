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
using Veilward.Application;
using Veilward.Domain;
using Veilward.Infrastructure;
using Veilward.Presentation;
using Veilward.Tests.Shared;
using Object=UnityEngine.Object;
namespace Veilward.Tests.PlayMode
{
    public sealed class BossPlayModeTests
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        private BattleSceneController Battle=>Object.FindFirstObjectByType<BattleSceneController>();
        private BossHudController Hud=>Battle.GetComponent<BossHudController>();
        private AbilityController Ability=>Battle.GetComponent<AbilityController>();
        private string root,output;private bool capture,expectedCompletionFault;private int width,height;
        private readonly List<string> telemetry=new List<string>();private readonly List<string> console=new List<string>();
        private static object Call(object o,string method)=>o.GetType().GetMethod(method,Flags|BindingFlags.Public).Invoke(o,null);
        private static UnityEngine.UI.Button Button(string name)=>Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(b=>b.name==name);
        [SetUp] public void Setup()
        {
            telemetry.Clear(); console.Clear(); output=null; capture=false; expectedCompletionFault=false;
            root=Path.GetFullPath(Path.Combine("TestResults","BossPlay",Guid.NewGuid().ToString("N")));Directory.CreateDirectory(root);
            MainMenuController.CheckpointStoreFactory=()=>new WaveCheckpointStore(root);BattleSessionContext.Clear();
            UnityEngine.Application.logMessageReceived+=Log;
        }
        private void Log(string message,string stack,LogType type)
        {
            if(expectedCompletionFault&&type==LogType.Error&&message.StartsWith("Checkpoint save failed:",StringComparison.Ordinal))
            {console.Add("ExpectedError|"+message);expectedCompletionFault=false;return;}
            if(type==LogType.Warning||type==LogType.Error||type==LogType.Exception||type==LogType.Assert)console.Add(type+"|"+message);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            UnityEngine.Application.logMessageReceived-=Log;
            if(output!=null){Directory.CreateDirectory(output);File.WriteAllLines(output+"/telemetry.txt",telemetry);File.WriteAllLines(output+"/console.txt",console);}
            MainMenuController.CheckpointStoreFactory=null;BattleSessionContext.Clear();yield return SceneManager.LoadSceneAsync("Battle");yield return null;
            if(Directory.Exists(root))Directory.Delete(root,true);
        }
        [UnityTest] public IEnumerator RegBattle213_ProductionHasEnabledAbilityAndBossHud()
        {
            yield return SceneManager.LoadSceneAsync("Battle");yield return null;
            Assert.That(Battle.Simulation.AbilityState.RulesId,Is.EqualTo("rift-seal-v1"));Assert.That(Hud,Is.Not.Null);
            Assert.That(Battle.ActiveModalOwner,Is.EqualTo(BattleInputOwner.ElementChoice));
        }
        [UnityTest] public IEnumerator RegBattle213_VisualWideNaturalFlame(){yield return Natural("flame",false,1600,900);}
        [UnityTest] public IEnumerator RegBattle213_VisualNarrowNaturalGale(){yield return Natural("gale",false,1280,960);}
        [UnityTest] public IEnumerator RegBattle213_VisualWideLegalBossLeak(){yield return Natural("flame",true,1600,900);}
        private IEnumerator Natural(string start,bool leak,int w,int h)
        {
            capture=Environment.GetEnvironmentVariable("P24E_CAPTURE")=="1";width=w;height=h;
            output="TestResults/P2.4f/revision-3/stone-play/"+start+(leak?"-leak":"-victory")+"-"+w+"x"+h;
            if(capture)ConfigureGameView(w,h);
            yield return SceneManager.LoadSceneAsync("MainMenu");yield return null;
            Object.FindFirstObjectByType<MainMenuController>().NewGameButton.onClick.Invoke();yield return null;yield return null;
            Battle.enabled=false;Label();Assert.That(Battle.Simulation.State.Gold,Is.EqualTo(150));Assert.That(Battle.Simulation.State.BaseHealth,Is.EqualTo(20));
            Assert.That((string)typeof(BattleScenarioConfig).GetProperty("ScenarioContentId").GetValue(typeof(BattleSceneController).GetField("scenario",Flags).GetValue(Battle)),Is.EqualTo(WaveCheckpointCompatibility.BalancedAbilityBossContentId));
            var script=leak?AbilityBossTrace.Load(start+"-leak"):BossMissionScript.Load(start);
            Choose(start);var bossKills=0;var bossLeaks=0;long bossLoss=0;var captured=new HashSet<string>();
            while(Battle.Simulation.State.Phase==BattlePhase.Preparation)
            {
                var s=Battle.Simulation;var wave=s.State.WaveIndex+1;
                foreach(var c in script.commands.Where(c=>c.wave==wave))
                {
                    if(c.op=="place")Assert.That(Battle.PlaceTower(new TowerDefinitionId(c.type),new GridCoordinate(c.x,c.y)).Success,Is.True);
                    else if(c.op=="upgrade")Assert.That(Battle.UpgradeTower(new TowerId(c.id)).Success,Is.True);
                    else Assert.That(Battle.SellTower(new TowerId(c.id)).Success,Is.True);
                    telemetry.Add($"{wave}|{s.State.Tick}|{c.op} {c.type} {c.x},{c.y} #{c.id}|gold={s.State.Gold}");
                }
                if(wave==8)
                {
                    yield return Shot("01-preparation8-notice");
                    var safe=Object.FindFirstObjectByType<BattleHudSafeArea>();
                    var scale=Object.FindFirstObjectByType<Canvas>().scaleFactor;
                    typeof(BattleHudSafeArea).GetMethod("ApplySafeArea",Flags).Invoke(safe,new object[]{new Rect(44,20,Screen.width-88,Screen.height-40),new Vector2(Screen.width,Screen.height),scale});
                    Hud.Refresh();Canvas.ForceUpdateCanvases();
                    var bossRect=((GameObject)typeof(BossHudController).GetField("panel",Flags).GetValue(Hud)).GetComponent<RectTransform>();
                    var stats=Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None).Single(r=>r.name=="Stats Panel");
                    var a=new Vector3[4];var b=new Vector3[4];bossRect.GetWorldCorners(a);stats.GetWorldCorners(b);
                    Assert.That(a[0].x,Is.GreaterThan(b[2].x),"narrow/wide with injected44px insets: boss must not overlap Stats");
                    yield return Shot("01b-injected-safe-area");
                    typeof(BattleHudSafeArea).GetMethod("ApplySafeArea",Flags).Invoke(safe,new object[]{Screen.safeArea,new Vector2(Screen.width,Screen.height),scale});
                    // Real disk MainMenu Continue, whole Preparation8; no DTO edits.
                    var expected=WaveCheckpointJson.Serialize(new WaveCheckpointEnvelope{schemaVersion=4,scenarioContentId=WaveCheckpointCompatibility.BalancedAbilityBossContentId,state=s.ExportCheckpoint()});
                    Button("Main Menu Button").onClick.Invoke();yield return null;yield return null;
                    Object.FindFirstObjectByType<MainMenuController>().ContinueButton.onClick.Invoke();yield return null;yield return null;
                    Battle.enabled=false;Label();s=Battle.Simulation;
                    Assert.That(WaveCheckpointJson.Serialize(new WaveCheckpointEnvelope{schemaVersion=4,scenarioContentId=WaveCheckpointCompatibility.BalancedAbilityBossContentId,state=s.ExportCheckpoint()}),Is.EqualTo(expected));
                    Assert.That(Ability.HasInputOwnership,Is.False);Assert.That(Battle.GetComponent<EnemyInspectorController>().SelectedEnemyId,Is.Null);yield return Shot("02-clean-continue8");
                }
                Button("Start Wave Button").onClick.Invoke();Assert.That(s.State.Phase,Is.EqualTo(BattlePhase.Combat));
                if(wave==8&&!leak)
                {
                    // Настоящий pre-Start checkpoint уже сохранён. Fault только completion IO, не gameplay.
                    Directory.CreateDirectory(new WaveCheckpointStore(root).TempPath);expectedCompletionFault=true;
                    LogAssert.Expect(LogType.Error,new System.Text.RegularExpressions.Regex("Checkpoint save failed:"));
                }
                var guard=0;
                while(s.State.Phase==BattlePhase.Combat&&guard++<20000)
                {
                    Hud.Refresh();var boss=s.State.Enemies.FirstOrDefault(e=>e.Definition.Id.Value=="rift-warden");
                    if(wave==8&&s.WaveElapsedTicks==329)yield return Shot("03-waiting-spawn330");
                    if(boss!=null&&captured.Add("spawn"))yield return Shot("04-boss-hp-armor");
                    if(!leak||wave<8)
                    {
                        var hasTarget=script.TryCenter(s,out var center);
                        if(hasTarget)
                        {
                            Button("Ability Button").onClick.Invoke();
                            Assert.That(Ability.HasInputOwnership,Is.True);
                            var beforeBoss=boss?.Health??0;
                            Ability.SelectTarget(center);Button("Confirm Ability Button").onClick.Invoke();Call(Battle,"LateUpdate");
                            telemetry.Add($"CAST|wave={wave}|tick={s.State.Tick}|sequence={s.AbilityState.NextCastSequence-1}|remaining={s.AbilityState.CooldownRemainingTicks}|{Ability.Status}");
                            if(boss!=null){if(!boss.IsAlive&&!s.State.Enemies.Contains(boss))bossKills++;bossLoss+=beforeBoss-boss.Health;if(captured.Add("hit"))yield return Shot("05-boss-ability-hit");}
                        }
                    }
                    if(boss!=null&&boss.IsAlive&&boss.Statuses.Count>0&&captured.Add("status"))
                    {
                        var inspector=Battle.GetComponent<EnemyInspectorController>();Assert.That(inspector.Select(boss.Id),Is.True);Ability.Refresh();
                        Assert.That(((GameObject)typeof(AbilityController).GetField("panel",Flags).GetValue(Ability)).activeInHierarchy,Is.False,"post-cast summary must not cover inspector");
                        yield return Shot("06-boss-status-inspector");inspector.Close();
                        var hp=Hud.Health;var tick=s.State.Tick;Battle.GetComponent<BattlePauseController>().Pause();Call(Battle,"Update");yield return Shot("07-boss-pause");Assert.That(s.State.Tick,Is.EqualTo(tick));Battle.GetComponent<BattlePauseController>().Resume();Hud.Refresh();Assert.That(Hud.Health,Is.EqualTo(hp));
                    }
                    var tracked=s.State.Enemies.FirstOrDefault(e=>e.Definition.Id.Value=="rift-warden");
                    if(s.State.Phase==BattlePhase.Combat)s.Step();
                    if(tracked!=null&&!s.State.Enemies.Contains(tracked)){if(tracked.IsAlive)bossLeaks++;else bossKills++;}
                    Call(Battle,"ProcessAttackEvents");Call(Battle,"LateUpdate");
                }
                telemetry.Add($"WAVE|{wave}|tick={s.State.Tick}|phase={s.State.Phase}|hp={s.State.BaseHealth}|gold={s.State.Gold}|kills={s.State.Kills}|remaining={s.AbilityState.CooldownRemainingTicks}|sequence={s.AbilityState.NextCastSequence}");
                if(wave==8)yield return Shot(s.State.Phase==BattlePhase.Victory?"08-boss-kill-victory":"08-defeat");
                if(s.State.ElementProgression.PendingChoiceId!="none")Choose(wave==3?script.after3:script.after6);
                if(s.State.Phase==BattlePhase.WaveComplete)Button("Next Wave Button").onClick.Invoke();
            }
            Assert.That(Battle.Simulation.State.Phase,Is.EqualTo(leak?BattlePhase.Defeat:BattlePhase.Victory));
            Assert.That(bossLeaks,Is.EqualTo(leak?1:0));if(!leak){Assert.That(bossKills,Is.EqualTo(1));Assert.That(bossLoss,Is.GreaterThan(0));}
            if(!leak)
            {
                var checkpoint=Battle.GetComponent<BattleCheckpointController>();var s=Battle.Simulation;var store=new WaveCheckpointStore(root);
                var config=(BattleScenarioConfig)typeof(BattleSceneController).GetField("scenario",Flags).GetValue(Battle);
                var envelope=new WaveCheckpointEnvelope{schemaVersion=4,scenarioContentId=WaveCheckpointCompatibility.BalancedAbilityBossContentId,state=s.ExportCheckpoint()};
                var expected=WaveCheckpointJson.Serialize(envelope);var gold=s.State.Gold;var kills=s.State.Kills;var sequence=s.AbilityState.NextCastSequence;
                Assert.That(checkpoint.LastSaveSucceeded,Is.False);Assert.That(expectedCompletionFault,Is.False);Assert.That(console.Count(c=>c.StartsWith("ExpectedError|")),Is.EqualTo(1));
                Assert.That(store.TryLoad(WaveCheckpointCompatibility.BalancedAbilityBossContentId,config.CreateSimulationForCheckpoint,out var previous),Is.True);
                Assert.That(previous.Envelope.state.phase,Is.EqualTo(BattlePhase.Preparation));Assert.That(previous.Envelope.state.waveIndex,Is.EqualTo(7));
                yield return Shot("09-victory-completion-save-failed");
                Directory.Delete(store.TempPath);Button("Retry Save Button").onClick.Invoke();
                Assert.That(checkpoint.LastSaveSucceeded,Is.True);Assert.That(s.State.Gold,Is.EqualTo(gold));Assert.That(s.State.Kills,Is.EqualTo(kills));Assert.That(s.AbilityState.NextCastSequence,Is.EqualTo(sequence));
                Assert.That(WaveCheckpointJson.Serialize(new WaveCheckpointEnvelope{schemaVersion=4,scenarioContentId=WaveCheckpointCompatibility.BalancedAbilityBossContentId,state=s.ExportCheckpoint()}),Is.EqualTo(expected));
                yield return Shot("10-victory-retry-saved");
                var primary=File.ReadAllBytes(store.PrimaryPath);var backup=File.ReadAllBytes(store.BackupPath);
                for(var i=0;i<2;i++)
                {Assert.That(store.TryLoad(WaveCheckpointCompatibility.BalancedAbilityBossContentId,config.CreateSimulationForCheckpoint,out var loaded),Is.True);Assert.That(WaveCheckpointJson.Serialize(loaded.Envelope),Is.EqualTo(expected));}
                Assert.That(File.ReadAllBytes(store.PrimaryPath),Is.EqualTo(primary));Assert.That(File.ReadAllBytes(store.BackupPath),Is.EqualTo(backup));
                checkpoint.RestoreCheckpoint(envelope);
                Assert.That(Ability.HasTarget,Is.False);Assert.That(Ability.HasInputOwnership,Is.False);Assert.That(Battle.GetComponent<EnemyInspectorController>().SelectedEnemyId,Is.Null);
                Assert.That(Battle.GetComponentsInChildren<AbilityEffectView>(true).All(e=>e.RemainingSeconds==0),Is.True);
                Assert.That(s.DamageEvents,Is.Empty);Assert.That(s.AbilityEvents,Is.Empty);Assert.That(s.ReactionEvents,Is.Empty);Assert.That(s.AttackEvents,Is.Empty);
                yield return Shot("11-clean-victory-restored");
                Button("Main Menu Button").onClick.Invoke();yield return null;yield return null;Label();
                var menu=Object.FindFirstObjectByType<MainMenuController>();Assert.That(menu.ContinueButton.interactable,Is.False);Assert.That(menu.PrepareContinueSession(),Is.False);
                Assert.That(File.ReadAllBytes(store.PrimaryPath),Is.EqualTo(primary));Assert.That(File.ReadAllBytes(store.BackupPath),Is.EqualTo(backup));
                yield return MenuShot("12-terminal-continue-off");
            }
            Assert.That(console.Any(c=>c.StartsWith("Error|")||c.StartsWith("Exception|")||c.StartsWith("Assert|")),Is.False,string.Join("\n",console));
        }
        private void Choose(string option){var choice=Battle.GetComponent<ElementChoiceController>();Assert.That(choice.SelectOption(option),Is.True);Button("Confirm Element Button").onClick.Invoke();}
        private void Label()
        {
            var go=new GameObject("Evidence Label",typeof(RectTransform),typeof(UnityEngine.UI.Text));go.transform.SetParent(Object.FindFirstObjectByType<Canvas>().transform,false);
            var text=go.GetComponent<UnityEngine.UI.Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=21;text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;text.text="PRODUCTION eight-wave-ability-boss-v2 • public-command test clock • Editor Play Mode";
            var r=text.rectTransform;r.anchorMin=r.anchorMax=new Vector2(.5f,0);r.pivot=new Vector2(.5f,0);r.anchoredPosition=new Vector2(0,8);r.sizeDelta=new Vector2(1450,32);
        }
        private IEnumerator Shot(string name)
        {
            Call(Battle,"LateUpdate");Hud.Refresh();yield return null;
            var summary=(UnityEngine.UI.Text)typeof(BattleSceneController).GetField("reactionSummaryText",Flags).GetValue(Battle);
            var bossPanel=((GameObject)typeof(BossHudController).GetField("panel",Flags).GetValue(Hud)).GetComponent<RectTransform>();
            if(bossPanel.gameObject.activeInHierarchy)
            {
                var panelCorners=new Vector3[4];var summaryCorners=new Vector3[4];bossPanel.GetWorldCorners(panelCorners);summary.rectTransform.GetWorldCorners(summaryCorners);
                Assert.That(summaryCorners[1].y,Is.LessThan(panelCorners[0].y),"reaction summary below boss notice/HP");
            }
            var s=Battle.Simulation;telemetry.Add($"SHOT {name}|frame={Time.frameCount}|wave={s.State.WaveIndex+1}|tick={s.State.Tick}|hp={s.State.BaseHealth}|gold={s.State.Gold}|kills={s.State.Kills}|boss={Hud.Health}|notice={Hud.Notice}");
            if(!capture)yield break;yield return new WaitForEndOfFrame();
            var texture=ScreenCapture.CaptureScreenshotAsTexture();Assert.That(texture.width,Is.EqualTo(width));Assert.That(texture.height,Is.EqualTo(height));Directory.CreateDirectory(output);File.WriteAllBytes(output+"/"+name+".png",texture.EncodeToPNG());Object.Destroy(texture);
        }
        private IEnumerator MenuShot(string name)
        {
            yield return null;telemetry.Add($"SHOT {name}|frame={Time.frameCount}|MainMenu|Continue=false");
            if(!capture)yield break;yield return new WaitForEndOfFrame();
            var texture=ScreenCapture.CaptureScreenshotAsTexture();Assert.That(texture.width,Is.EqualTo(width));Assert.That(texture.height,Is.EqualTo(height));
            Directory.CreateDirectory(output);File.WriteAllBytes(output+"/"+name+".png",texture.EncodeToPNG());Object.Destroy(texture);
        }
        private static void ConfigureGameView(int w,int h)
        {
#if UNITY_EDITOR
            var a=typeof(UnityEditor.Editor).Assembly;var type=a.GetType("UnityEditor.GameViewSizes");var sizes=typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(type).GetProperty("instance").GetValue(null);var group=type.GetMethod("GetGroup").Invoke(sizes,new[]{type.GetProperty("currentGroupType").GetValue(sizes)});
            var kind=Enum.Parse(a.GetType("UnityEditor.GameViewSizeType"),"FixedResolution");var size=Activator.CreateInstance(a.GetType("UnityEditor.GameViewSize"),new object[]{kind,w,h,"P2.4e evidence"});group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});var count=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);
            var view=UnityEditor.EditorWindow.GetWindow(a.GetType("UnityEditor.GameView"));view.GetType().GetProperty("selectedSizeIndex",Flags|BindingFlags.Public).SetValue(view,count-1);view.Focus();
#endif
        }
    }
}
