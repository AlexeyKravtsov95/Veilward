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
using Veilward.Tests.Shared;
using Object=UnityEngine.Object;
namespace Veilward.Tests.PlayMode
{
    public sealed class AbilityBossIntegrationPlayModeTests
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        private BattleSceneController Battle=>Object.FindFirstObjectByType<BattleSceneController>();
        private BattleCheckpointController Checkpoint=>Battle.GetComponent<BattleCheckpointController>();
        private AbilityController Ability=>Battle.GetComponent<AbilityController>();
        private ElementChoiceController Choice=>Battle.GetComponent<ElementChoiceController>();
        private BattlePlacementController Placement=>Battle.GetComponent<BattlePlacementController>();
        private BattleScenarioConfig Config=>(BattleScenarioConfig)typeof(BattleSceneController).GetField("scenario",Flags).GetValue(Battle);
        private string root,output;private WaveCheckpointStore store;private Writer writer;private bool capture;private int expectedErrors,width,height;
        private readonly List<string> console=new List<string>(),telemetry=new List<string>();
        private sealed class Writer:IWaveCheckpointWriter
        {
            private readonly FileWaveCheckpointWriter files=new FileWaveCheckpointWriter();public int writes,failAt=-1;
            public void WriteTemporary(string path,string json){writes++;if(writes==failAt){failAt=-1;throw new IOException("injected P24f IO failure");}files.WriteTemporary(path,json);}
            public void Commit(string temp,string primary,string backup)=>files.Commit(temp,primary,backup);
        }
        private static object Call(object o,string method)=>o.GetType().GetMethod(method,Flags|BindingFlags.Public).Invoke(o,null);
        private static Button Button(string name)=>Object.FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(b=>b.name==name);
        [SetUp] public void Setup()
        {
            root=Path.GetFullPath(Path.Combine("TestResults","P24fMenu",Guid.NewGuid().ToString("N")));Directory.CreateDirectory(root);
            writer=new Writer();store=new WaveCheckpointStore(root,writer);MainMenuController.CheckpointStoreFactory=()=>store;BattleSessionContext.Clear();
            console.Clear();telemetry.Clear();output=null;expectedErrors=0;capture=Environment.GetEnvironmentVariable("P24F_CAPTURE")=="1";UnityEngine.Application.logMessageReceived+=Log;
        }
        private void Log(string message,string stack,LogType type)
        {
            if(type==LogType.Error&&expectedErrors>0&&message.StartsWith("Checkpoint save failed:",StringComparison.Ordinal)){expectedErrors--;console.Add("ExpectedError|"+message);return;}
            if(type==LogType.Warning||type==LogType.Error||type==LogType.Exception||type==LogType.Assert)console.Add(type+"|"+message);
        }
        private void FailWrite(int offset=1){writer.failAt=writer.writes+offset;expectedErrors++;LogAssert.Expect(LogType.Error,new Regex("Checkpoint save failed:"));}
        [UnityTearDown] public IEnumerator Cleanup()
        {
            UnityEngine.Application.logMessageReceived-=Log;
            if(output!=null){Directory.CreateDirectory(output);File.WriteAllLines(output+"/console.txt",console);File.WriteAllLines(output+"/telemetry.txt",telemetry);}
            MainMenuController.CheckpointStoreFactory=null;BattleSessionContext.Clear();yield return SceneManager.LoadSceneAsync("Battle");yield return null;if(Directory.Exists(root))Directory.Delete(root,true);
        }
        [UnityTest] public IEnumerator RegBattle215_ProductionFlameMenuFilesAndCombat7And8(){yield return Route("flame",1600,900);}
        [UnityTest] public IEnumerator RegBattle215_ProductionGaleMenuFilesAndCombat7And8(){yield return Route("gale",1280,960);}
        private void Select(string option){Choice.Refresh();Choice.OptionButtons.Single(b=>b.name==option).onClick.Invoke();Button("Confirm Element Button").onClick.Invoke();}
        private void Clean()
        {
            var s=Battle.Simulation;Assert.That(s.State.Enemies,Is.Empty);Assert.That(s.AttackEvents,Is.Empty);Assert.That(s.DamageEvents,Is.Empty);Assert.That(s.ReactionEvents,Is.Empty);Assert.That(s.AbilityEvents,Is.Empty);
            Assert.That(Ability.HasInputOwnership,Is.False);Assert.That(Ability.HasTarget,Is.False);Assert.That(Ability.LastCast,Is.Null);Assert.That(Ability.Status,Is.Empty);
            Assert.That(Placement.HasSelection,Is.False);Assert.That(Placement.IsSellConfirmationVisible,Is.False);Assert.That(Choice.PreviewOptionId,Is.Null);Assert.That(Battle.GetComponent<EnemyInspectorController>().SelectedEnemyId,Is.Null);Assert.That(Battle.Presentation.LastSummary,Is.Empty);
            Assert.That(Object.FindObjectsByType<AttackBeamView>(FindObjectsSortMode.None),Is.Empty);
            Assert.That(Object.FindObjectsByType<ReactionEffectView>(FindObjectsInactive.Include,FindObjectsSortMode.None).All(e=>e.RemainingSeconds==0),Is.True);
            Assert.That(Object.FindObjectsByType<AbilityEffectView>(FindObjectsInactive.Include,FindObjectsSortMode.None).All(e=>e.RemainingSeconds==0),Is.True);
        }
        private IEnumerator Continue(string expected,string label)
        {
            var primary=File.ReadAllBytes(store.PrimaryPath);var backup=File.Exists(store.BackupPath)?File.ReadAllBytes(store.BackupPath):null;
            for(var repeat=0;repeat<2;repeat++)
            {
                Button("Main Menu Button").onClick.Invoke();yield return null;yield return null;
                var menu=Object.FindFirstObjectByType<MainMenuController>();Assert.That(menu.ContinueButton.interactable,Is.True);Assert.That(menu.PrepareContinueSession(),Is.True);
                menu.ContinueButton.onClick.Invoke();yield return null;yield return null;Battle.enabled=false;Label();Assert.That(AbilityBossTrace.Wire(Battle.Simulation),Is.EqualTo(expected));Clean();
                Assert.That(File.ReadAllBytes(store.PrimaryPath),Is.EqualTo(primary));if(backup!=null)Assert.That(File.ReadAllBytes(store.BackupPath),Is.EqualTo(backup));
            }
            yield return Shot(label);
        }
        private void CorruptRestoreProbe(WaveCheckpointEnvelope saved)
        {
            var bad=new WaveCheckpointEnvelope{schemaVersion=4,scenarioContentId=saved.scenarioContentId,state=saved.state.DeepCopy()};bad.state.ability=new AbilityCheckpoint("rift-seal-v1","rift-seal",601,2);
            var before=AbilityBossTrace.Snapshot(Battle.Simulation);var owner=Battle.ActiveModalOwner;var lease=Ability.Lease;
            Assert.Throws<ArgumentException>(()=>Checkpoint.RestoreCheckpoint(bad));Assert.That(AbilityBossTrace.Snapshot(Battle.Simulation),Is.EqualTo(before));Assert.That(Battle.ActiveModalOwner,Is.EqualTo(owner));Assert.That(Ability.Lease.Generation,Is.EqualTo(lease.Generation));
        }
        private IEnumerator Route(string start,int w,int h)
        {
            width=w;height=h;output=AbilityBossTrace.Output+"/play/"+start+"-"+w+"x"+h;if(capture)ConfigureGameView(w,h);
            yield return SceneManager.LoadSceneAsync("MainMenu");yield return null;Object.FindFirstObjectByType<MainMenuController>().NewGameButton.onClick.Invoke();yield return null;yield return null;Battle.enabled=false;Label();
            var script=AbilityBossTrace.Load(start);var control=new AbilityBossTrace.Run(Config.CreateSimulation(),script);control.Play();Assert.That(Battle.Simulation.State.Gold,Is.EqualTo(150));Assert.That(Battle.Simulation.State.BaseHealth,Is.EqualTo(20));
            yield return Continue(AbilityBossTrace.Wire(Battle.Simulation),"01-initial-pending");Select(start);yield return Continue(AbilityBossTrace.Wire(Battle.Simulation),"02-start-confirmed");
            var cells=new int[4];var abilityKills=0;var bossKills=0;var firstShots=new HashSet<string>();
            while(Battle.Simulation.State.Phase==BattlePhase.Preparation)
            {
                var s=Battle.Simulation;var wave=s.State.WaveIndex+1;
                foreach(var c in script.commands.Where(c=>c.wave==wave))
                {
                    if(c.op=="place"){Assert.That(Placement.TrySelectCell(new GridCoordinate(c.x,c.y)),Is.True);Placement.SelectTowerType(new TowerDefinitionId(c.type));Button("Confirm Button").onClick.Invoke();Placement.CancelSelection();}
                    else {Assert.That(Placement.TrySelectCell(s.State.Towers.Single(t=>t.Id.Value==c.id).Coordinate),Is.True);if(c.op=="sell"){Placement.RequestSellSelectedTower();Button("Confirm Sell Button").onClick.Invoke();}else Placement.UpgradeButton.onClick.Invoke();Placement.CancelSelection();}Record(c.op+" "+c.type+" #"+c.id);
                }
                if(wave>=7){yield return Continue(AbilityBossTrace.Wire(s),"preparation"+wave);s=Battle.Simulation;}
                var safe=AbilityBossTrace.Wire(s);var envelope=AbilityBossTrace.Envelope(s);var priorCells=(int[])cells.Clone();var priorKills=abilityKills;
                Button("Start Wave Button").onClick.Invoke();Assert.That(s.State.Phase,Is.EqualTo(BattlePhase.Combat));var rolled=false;var kill=false;var proc=false;var guard=0;
                while(s.State.Phase==BattlePhase.Combat&&guard++<20000)
                {
                    if(script.TryCenter(s,out var center))
                    {
                        var beforeKills=s.State.Kills;var sequence=s.AbilityState.NextCastSequence;Button("Ability Button").onClick.Invoke();Assert.That(Ability.HasInputOwnership,Is.True);yield return null;
                        if(firstShots.Add("world")){var screen=Camera.main.WorldToScreenPoint(Vector3.zero);Placement.DispatchPointer(screen,-1,UnityEngine.InputSystem.TouchPhase.Began);Placement.DispatchPointer(screen,-1,UnityEngine.InputSystem.TouchPhase.Ended);Assert.That(Ability.HasTarget,Is.True,"production world dispatcher");}
                        Assert.That(Ability.SelectTarget(center),Is.True);if(wave>=7)yield return Shot("wave"+wave+"-ability-preview-sequence"+sequence+(rolled?"-replay":""));
                        Button("Confirm Ability Button").onClick.Invoke();Button("Confirm Ability Button").onClick.Invoke();Assert.That(s.AbilityState.NextCastSequence,Is.EqualTo(sequence));if(wave>=7)CorruptRestoreProbe(envelope);
                        Call(Battle,"LateUpdate");Assert.That(Ability.LastCast.HasValue&&Ability.LastCast.Value.Succeeded,Is.True);Assert.That(s.AbilityState.NextCastSequence,Is.EqualTo(sequence+1));kill|=s.State.Kills>beforeKills;abilityKills+=s.State.Kills-beforeKills;
                        Record("Cast sequence="+sequence+" center="+center.X+","+center.Y+" applied="+Ability.LastCast.Value.TotalAppliedHpLoss);if(wave>=7)yield return Shot("wave"+wave+"-ability-hit-sequence"+sequence+(rolled?"-replay":""));
                        var pause=Battle.GetComponent<BattlePauseController>();var before=AbilityBossTrace.Snapshot(s);pause.Pause();Call(Battle,"Update");Assert.That(AbilityBossTrace.Snapshot(s),Is.EqualTo(before));pause.Resume();Call(Battle,"Update");Assert.That(AbilityBossTrace.Snapshot(s),Is.EqualTo(before));
                    }
                    if(s.State.Phase!=BattlePhase.Combat)break;s.Step();
                    foreach(var reaction in s.ReactionEvents){var applied=s.DamageEvents.Where(d=>d.Origin==DamageOrigin.Reaction&&d.Tick==reaction.Tick&&d.ReactionKind==reaction.Kind&&d.ReactionSequenceInTick==reaction.SequenceInTick).Sum(d=>d.AppliedHpLoss);if(applied>0){cells[(int)reaction.Kind*2+(int)reaction.TriggerKind]+=applied;proc=true;Record("positive "+reaction.Kind+"/"+reaction.TriggerKind+" applied="+applied);}}
                    bossKills+=s.DamageEvents.Count(d=>d.TargetEnemyId.Value==68&&d.WasLethal);
                    var keys=s.ReactionEvents.Where(e=>s.DamageEvents.Any(d=>d.Origin==DamageOrigin.Reaction&&d.ReactionSequenceInTick==e.SequenceInTick&&d.AppliedHpLoss>0)).Select(e=>e.Kind+"-"+e.TriggerKind).Distinct().ToArray();
                    var interrupt=wave>=7&&!rolled&&kill&&proc&&s.State.Phase==BattlePhase.Combat&&s.State.Enemies.Count>0&&s.DamageEvents.Count>0;
                    if(interrupt){Assert.That(s.DamageEvents,Is.Not.Empty);Assert.That(Battle.GetComponent<EnemyInspectorController>().Select(s.State.Enemies[0].Id),Is.True);CorruptRestoreProbe(envelope);}
                    Call(Battle,"ProcessAttackEvents");Call(Battle,"LateUpdate");foreach(var key in keys)if(firstShots.Add(key))yield return Shot("positive-"+key);
                    if(wave==8&&s.State.Enemies.Any(e=>e.Definition.Id.Value=="rift-warden")&&firstShots.Add("boss"))yield return Shot("boss1000-armor5");
                    if(interrupt){yield return Shot("interrupted-combat"+wave);var primary=File.ReadAllBytes(store.PrimaryPath);yield return Continue(safe,"clean-continue"+wave);Assert.That(File.ReadAllBytes(store.PrimaryPath),Is.EqualTo(primary));s=Battle.Simulation;rolled=true;kill=proc=false;cells=(int[])priorCells.Clone();abilityKills=priorKills;Button("Start Wave Button").onClick.Invoke();}
                    if(guard%120==0)yield return null;
                }
                Assert.That(guard,Is.LessThan(20000));if(wave>=7)Assert.That(rolled,Is.True,"actual ability kill + positive reaction Combat"+wave+" rollback required");Assert.That(AbilityBossTrace.Wire(s),Is.EqualTo(control.report.waves[wave-1].checkpoint));Record("wave complete");if(s.State.Phase!=BattlePhase.WaveComplete)break;
                if(s.State.ElementProgression.PendingChoiceId!="none")
                {
                    var before=AbilityBossTrace.Wire(s);yield return Continue(before,"pending"+wave);FailWrite();Select(wave==3?script.after3:script.after6);Assert.That(Checkpoint.LastSaveSucceeded,Is.False);var live=AbilityBossTrace.Wire(Battle.Simulation);yield return Shot("failed-confirm"+wave);
                    if(wave==3){yield return Continue(before,"failed-save-restart3");Select(script.after3);}else{Button("Retry Save Button").onClick.Invoke();Assert.That(AbilityBossTrace.Wire(Battle.Simulation),Is.EqualTo(live));yield return Shot("retry-confirm6");}
                    yield return Continue(AbilityBossTrace.Wire(Battle.Simulation),"confirmed"+wave);
                }
                if(wave==6||wave==7){FailWrite(2);Button("Next Wave Button").onClick.Invoke();Assert.That(Battle.Simulation.State.Phase,Is.EqualTo(BattlePhase.Preparation));Assert.That(Checkpoint.LastSaveSucceeded,Is.False);var live=AbilityBossTrace.Wire(Battle.Simulation);yield return Shot("failed-next-to-preparation"+(wave+1));Button("Retry Save Button").onClick.Invoke();Button("Retry Save Button").onClick.Invoke();Assert.That(Checkpoint.LastSaveSucceeded,Is.True);Assert.That(AbilityBossTrace.Wire(Battle.Simulation),Is.EqualTo(live));yield return Shot("retry-preparation"+(wave+1));}else Button("Next Wave Button").onClick.Invoke();
            }
            var final=Battle.Simulation;Assert.That(final.State.Phase.ToString(),Is.EqualTo(control.report.outcome));Assert.That(cells,Is.EqualTo(control.report.cells.Select(c=>c.applied).ToArray()));Assert.That(abilityKills,Is.EqualTo(control.report.abilityKills));Assert.That(bossKills,Is.EqualTo(control.report.bossKills));yield return Shot("natural-"+final.State.Phase+"-coverage-control");
            // Эта suite проверяет matrix при фактическом исходе. Обязательная Victory REG214 остаётся отдельным FAIL.
            Button("Main Menu Button").onClick.Invoke();yield return null;yield return null;Assert.That(Object.FindFirstObjectByType<MainMenuController>().ContinueButton.interactable,Is.False);Assert.That(Object.FindFirstObjectByType<MainMenuController>().PrepareContinueSession(),Is.False);yield return Shot("terminal-continue-off");Assert.That(expectedErrors,Is.Zero);Assert.That(console.Any(c=>c.StartsWith("Error|")||c.StartsWith("Exception|")||c.StartsWith("Assert|")),Is.False,string.Join("\n",console));
        }
        private void Record(string message){var s=Battle?.Simulation;telemetry.Add("frame="+Time.frameCount+"|"+message+(s==null?"":$"|wave={s.State.WaveIndex+1}|tick={s.State.Tick}|phase={s.State.Phase}|HP={s.State.BaseHealth}|Gold={s.State.Gold}|Kills={s.State.Kills}|cooldown={s.AbilityState.CooldownRemainingTicks}|sequence={s.AbilityState.NextCastSequence}"));}
        private void Label(){var go=new GameObject("P24f Evidence Label",typeof(RectTransform),typeof(Text));go.transform.SetParent(Object.FindFirstObjectByType<Canvas>().transform,false);var text=go.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=16;text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;text.text="P2.4f production coverage CONTROL | public-command test clock | Editor Play Mode | Victory NOT VERIFIED";var rect=text.rectTransform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,0);rect.pivot=new Vector2(.5f,0);rect.anchoredPosition=new Vector2(0,6);rect.sizeDelta=new Vector2(1260,32);}
        private IEnumerator Shot(string name){if(Battle!=null)Call(Battle,"LateUpdate");yield return null;Record("SHOT "+name);if(!capture)yield break;yield return new WaitForEndOfFrame();var texture=ScreenCapture.CaptureScreenshotAsTexture();Assert.That(texture.width,Is.EqualTo(width));Assert.That(texture.height,Is.EqualTo(height));Directory.CreateDirectory(output);File.WriteAllBytes(output+"/"+name+".png",texture.EncodeToPNG());Object.Destroy(texture);}
        private static void ConfigureGameView(int w,int h)
        {
#if UNITY_EDITOR
            var a=typeof(UnityEditor.Editor).Assembly;var type=a.GetType("UnityEditor.GameViewSizes");var sizes=typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(type).GetProperty("instance").GetValue(null);var group=type.GetMethod("GetGroup").Invoke(sizes,new[]{type.GetProperty("currentGroupType").GetValue(sizes)});var kind=Enum.Parse(a.GetType("UnityEditor.GameViewSizeType"),"FixedResolution");var size=Activator.CreateInstance(a.GetType("UnityEditor.GameViewSize"),new object[]{kind,w,h,"P2.4f evidence"});group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});var count=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);var view=UnityEditor.EditorWindow.GetWindow(a.GetType("UnityEditor.GameView"));view.GetType().GetProperty("selectedSizeIndex",Flags|BindingFlags.Public).SetValue(view,count-1);view.Focus();
#endif
        }
    }
}
