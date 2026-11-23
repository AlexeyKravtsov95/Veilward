// Временный in-memory Editor harness: script run, без runtime asset/сохранённых scene deltas.
// Replay побайтно неизменённого earned checkpoint, затем только публичные UI/API команды.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Veilward.Application;
using Veilward.Domain;
using Veilward.Infrastructure;
using Veilward.Presentation;

public sealed class Art3fEditorRecording : MonoBehaviour
{
    private const string Frames = "TestResults/ART3F/video-frames/";
    private const string Reports = "Docs/review/ART.3f/reports/";
    private readonly List<string> _rows = new List<string>();
    private readonly List<string> _console = new List<string>();
    private WaveCheckpointStore _store;
    private Text _label;
    private string _stage = "earned L1 checkpoint replay / menu Continue";
    private bool _recording;
    private double _started;
    private int _number;
    private TowerId _id;
    private GridCoordinate _cell;
    private System.Reflection.MethodInfo _encodeFrame, _prepareView;
    private BattleSceneController Battle => UnityEngine.Object.FindFirstObjectByType<BattleSceneController>();

    public static string BeginRecording()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode required.");
        if (UnityEngine.Object.FindFirstObjectByType<Art3fEditorRecording>() != null)
            throw new InvalidOperationException("Recording already present.");
        var root = new GameObject("ART3F Editor evidence recorder");
        DontDestroyOnLoad(root);
        root.AddComponent<Art3fEditorRecording>().Begin();
        return "Started continuous real-time Editor flow; inspect completion.json before encoding.";
    }

    private void Begin()
    {
        // ScreenCapture/Screen.width в unfocused Editor могут читать SceneView/backbuffer.
        // Используем тот же composited GameView RT и encoder, что установленная capture game_view.
        var captureType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Unity.Pipeline.Editor.Commands.Capture.CaptureCommands")).FirstOrDefault(t=>t!=null);
        Require(captureType!=null,"Installed Pipeline capture implementation required.");
        var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
        _encodeFrame=captureType.GetMethod("EncodeScreenToPng",flags);_prepareView=captureType.GetMethod("PrepareGameView",flags);
        var target=(RenderTexture)captureType.GetMethod("GetGameViewTargetTexture",flags).Invoke(null,new object[]{false});
        Require(target!=null&&target.width==1600&&target.height==900&&_encodeFrame!=null&&_prepareView!=null,"Actual composited GameView1600x900 required before flow.");
        Directory.CreateDirectory(Frames); Directory.CreateDirectory(Reports);
        if (Directory.GetFiles(Frames, "*.png").Length != 0)
            throw new InvalidOperationException("Use an empty task-owned video-frames directory.");
        var original = File.ReadAllBytes("TestResults/ART3F/natural-L1-checkpoint.json");
        _store = new WaveCheckpointStore(Path.GetFullPath("TestResults/ART3F/VideoStore"));
        Directory.CreateDirectory(Path.GetDirectoryName(_store.PrimaryPath));
        File.WriteAllBytes(_store.PrimaryPath, original);
        Require(File.ReadAllBytes(_store.PrimaryPath).SequenceEqual(original), "L1 checkpoint restore readback");
        File.WriteAllText(Reports+"video-checkpoint-origin.json", JsonUtility.ToJson(new Origin {
            origin="TestResults/ART3F/natural-L1-checkpoint.json; fresh earned public-command lifecycle test",
            restore="isolated TestResults/ART3F/VideoStore primary, before MainMenu Continue",
            sha256=Hash(original), readbackSha256=Hash(File.ReadAllBytes(_store.PrimaryPath)),
            freshRun=false, liveResourceMutation=false }, true)+"\n");
        MainMenuController.CheckpointStoreFactory = () => _store;
        BattleSessionContext.Clear();
        var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=1000;
        gameObject.AddComponent<CanvasScaler>().uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;
        var label = new GameObject("Editor proof label",typeof(RectTransform),typeof(Text));
        label.transform.SetParent(transform,false); _label=label.GetComponent<Text>();
        _label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); _label.fontSize=16;
        _label.alignment=TextAnchor.MiddleCenter; _label.color=Color.white; _label.raycastTarget=false;
        _label.rectTransform.anchorMin=new Vector2(0,0); _label.rectTransform.anchorMax=new Vector2(1,0);
        _label.rectTransform.pivot=new Vector2(.5f,0); _label.rectTransform.anchoredPosition=new Vector2(0,2);
        _label.rectTransform.sizeDelta=new Vector2(0,44);
        _recording=true; _started=Time.realtimeSinceStartupAsDouble;
        UnityEngine.Application.logMessageReceived+=Log;
        StartCoroutine(Record()); StartCoroutine(Flow());
    }

    private void Log(string message,string stack,LogType type)
    {
        if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert||type==LogType.Warning)
            _console.Add(type+"|"+message);
        if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert) Finish(false,message);
    }

    private IEnumerator Record()
    {
        double next=0;
        while(_recording)
        {
            yield return new WaitForEndOfFrame();
            var now=Time.realtimeSinceStartupAsDouble-_started;
            if(now<next) continue;
            var b=Battle; var s=b!=null?b.Simulation:null;
            var storm=s!=null?s.State.Enemies.Count(e=>e.LastStormTick.HasValue):0;
            var towers=s!=null?string.Join(";",s.State.Towers.Select(t=>t.Id.Value+":"+t.Definition.Id.Value+":L"+t.Level)):"";
            _rows.Add(string.Join("|",_number,now.ToString("R",System.Globalization.CultureInfo.InvariantCulture),Time.frameCount,
                _stage,s!=null?s.State.Tick.ToString():"",s!=null?s.State.Gold.ToString():"",s!=null?s.State.BaseHealth.ToString():"",
                s!=null?s.State.Kills.ToString():"",s!=null?s.State.Enemies.Count.ToString():"",storm,towers));
            _prepareView.Invoke(null,null);
            var pixels=(byte[])_encodeFrame.Invoke(null,new object[]{1600,900});
            File.WriteAllBytes(Frames+_number.ToString("D5")+".png",pixels);
            _number++;next=now+.1; if(_number%10==0)File.WriteAllText("TestResults/ART3F/video-progress.txt",_stage+" | frame="+Time.frameCount+" | tick="+(s!=null?s.State.Tick:0));
        }
    }

    private void Update()
    {
        var b=Battle;
        if(!_recording||b==null||b.enabled||b.Simulation==null)return;
        // Только test clock prefix. Штатный Update обновляет presentation lifetimes без Domain Step.
        typeof(BattleSceneController).GetField("_accumulator",Flags).SetValue(b,-1f);
        Call(b,"Update");Call(b,"LateUpdate");
    }
    private const System.Reflection.BindingFlags Flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
    private static void Call(object target,string name)=>target.GetType().GetMethod(name,Flags).Invoke(target,null);

    private IEnumerator Flow()
    {
        var script=JsonUtility.FromJson<Veilward.Tests.Shared.BossMissionScript>(File.ReadAllText("Docs/review/ART.3f/inputs/earned.commands.json"));
        yield return SceneManager.LoadSceneAsync("MainMenu");
        Stage("menu Continue / unchanged earned STORM L1 checkpoint");yield return new WaitForSecondsRealtime(1.5f);
        var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
        Require(menu.ContinueButton.interactable,"Continue available");menu.ContinueButton.onClick.Invoke();yield return null;yield return null;
        var b=Battle;b.enabled=false;
        var tower=b.Simulation.State.Towers.Single(t=>t.Definition.Id.Value=="storm");_id=tower.Id;_cell=tower.Coordinate;
        Require(tower.Level==1&&b.Simulation.State.WaveIndex==3,"earned STORM L1 / Preparation4");
        var p=b.GetComponent<BattlePlacementController>();Require(p.TrySelectCell(_cell),"select STORM L1");
        Stage("STORM L1 / Inspect + real Upgrade price");yield return new WaitForSecondsRealtime(2);p.CancelSelection();
        for(var wave=4;wave<=8;wave++)
        {
            b=Battle;p=b.GetComponent<BattlePlacementController>();
            foreach(var command in script.commands.Where(c=>c.wave==wave&&!(wave==4&&c.op=="place")))
            {
                var selected=b.Simulation.State.Towers.Single(t=>t.Id.Value==command.id);
                Require(p.TrySelectCell(selected.Coordinate),"select public operation");
                if(command.op=="sell")
                {
                    p.RequestSellSelectedTower();Require(p.IsSellConfirmationVisible,"prefix Sell confirmation");
                    Require(p.ConfirmSellSelectedTower().Success,"prefix public Sell");
                }
                else if(command.op=="upgrade")
                {
                    var previous=selected.Level;p.UpgradeButton.onClick.Invoke();Call(b,"LateUpdate");
                    Require(selected.Level==previous+1,"UI Upgrade");
                    if(selected.Definition.Id.Value=="storm")
                    {
                        var level=selected.Level;
                        Require(File.ReadAllBytes(_store.PrimaryPath).SequenceEqual(File.ReadAllBytes("TestResults/ART3F/natural-L"+level+"-checkpoint.json")),"exact earned L"+level+" replay readback");
                        Stage(level==2?"STORM L2 / same TowerId + cell / earned budget":"STORM L3 / same TowerId + cell / MAX + range");
                        yield return new WaitForSecondsRealtime(2);
                    }
                    p.CancelSelection();
                }
                else throw new InvalidOperationException("Unexpected prefix command");
                Call(b,"LateUpdate");
            }
            if(wave==8)break;
            p.CancelSelection();Require(b.StartWave()==WaveTransitionResult.Started,"earned prefix StartWave");
            var s=b.Simulation;var start=s.State.Tick;double accumulator=0;
            Stage("earned wave "+wave+" / test clock x4 / fixed30Hz / public commands / advancing combat");
            while(s.State.Phase==BattlePhase.Combat)
            {
                yield return null;accumulator+=Math.Min(Time.unscaledDeltaTime,.25f)*4;var steps=0;
                while(accumulator>=1.0/30&&steps++<32&&s.State.Phase==BattlePhase.Combat)
                {
                    s.Step();
                    foreach(var cast in script.casts.Where(c=>c.wave==wave&&c.relativeTick==s.State.Tick-start))
                        Require(s.CastAbility(new CastAbilityCommand(s.AbilityState.AbilityId,new PathPosition(cast.x,cast.y),s.State.WaveIndex,s.State.Tick,s.AbilityState.NextCastSequence)).Succeeded,"exact earned prefix cast");
                    Call(b,"ProcessAttackEvents");b.GetComponent<BattleCheckpointController>().ObservePhaseTransition();Call(b,"LateUpdate");
                    accumulator-=1.0/30;
                }
            }
            Require(s.State.Phase==BattlePhase.WaveComplete,"earned prefix completes");
            if(s.State.ElementProgression.PendingChoiceId!="none")
                Require(b.ChooseElement(new ChooseElementCommand(s.State.ElementProgression.PendingChoiceId,script.after6)).Success,"public after6 unlock");
            Require(b.ContinueToNextWave()==WaveTransitionResult.Continued,"earned prefix NextWave");Call(b,"LateUpdate");
        }
        b=Battle;p=b.GetComponent<BattlePlacementController>();p.CancelSelection();
        Require(b.Simulation.State.Gold==2&&b.Simulation.State.BaseHealth==15&&b.Simulation.State.Kills==51,"earned497 final ledger");
        var safe=File.ReadAllBytes(_store.PrimaryPath);var safeWire=SafeWire(b.Simulation);
        File.WriteAllText(Reports+"video-safe-wire.txt",safeWire);File.WriteAllBytes(Reports+"video-L3-before-combat.json",safe);
        b.enabled=true;Require(b.StartWave()==WaveTransitionResult.Started,"production Combat8");
        var firstTick=b.Simulation.State.Tick;var began=Time.realtimeSinceStartupAsDouble;
        Stage("advancing REAL-TIME Combat / HP + intrinsic Storm / generic beam #47");
        EnemyState enemy=null;ReactionExplanation? explanation=null;
        while(Time.realtimeSinceStartupAsDouble-began<15)
        {
            foreach(var e in b.Simulation.State.Enemies)
            {
                var memory=b.Presentation.GetMemory(e.Id)?.Storm;
                if(memory.HasValue&&memory.Value.Reaction.SourceTowerId==_id&&memory.Value.Reaction.TriggerKind==ReactionTriggerKind.Intrinsic&&memory.Value.HitCount>0&&memory.Value.AppliedHpLoss>0)
                {enemy=e;explanation=memory;break;}
            }
            if(enemy!=null)break;yield return null;
        }
        Require(enemy!=null&&b.Simulation.State.Tick>firstTick,"advancing actual intrinsic Storm chain");
        Require(b.GetComponent<EnemyInspectorController>().Select(enemy.Id),"actual Storm enemy Inspect");
        Stage("advancing combat / actual intrinsic Storm enemy Inspect + HP");
        File.WriteAllText(Reports+"video-inspector.txt",b.GetComponent<EnemyInspectorController>().Details+"\n");
        File.WriteAllText(Reports+"video-intrinsic.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{
            tick=b.Simulation.State.Tick,enemy=enemy.Id.Value,hp=enemy.Health,reaction=explanation.Value.Reaction,
            hitCount=explanation.Value.HitCount,appliedHpLoss=explanation.Value.AppliedHpLoss,source="read-only production ReactionPresentationState",synthetic=false},Newtonsoft.Json.Formatting.Indented));
        yield return new WaitForSecondsRealtime(.5f);
        b.GetComponent<EnemyInspectorController>().Close();b.ReturnToMenu();yield return null;yield return null;
        Stage("interrupted Combat / file-menu Continue rolls back to Preparation");yield return new WaitForSecondsRealtime(1.5f);
        menu=UnityEngine.Object.FindFirstObjectByType<MainMenuController>();Require(menu.ContinueButton.interactable,"rollback Continue");
        menu.ContinueButton.onClick.Invoke();yield return null;yield return null;b=Battle;
        Require(b.Simulation.State.Phase==BattlePhase.Preparation&&File.ReadAllBytes(_store.PrimaryPath).SequenceEqual(safe),"full safe rollback bytes");
        Require(SafeWire(b.Simulation)==safeWire,"full safe state including live Grid");File.WriteAllText(Reports+"video-rollback-wire.txt",SafeWire(b.Simulation));
        tower=b.Simulation.State.Towers.Single();Require(tower.Id==_id&&tower.Level==3&&tower.Coordinate==_cell,"rollback identity");
        p=b.GetComponent<BattlePlacementController>();Require(p.TrySelectCell(_cell),"rollback select");
        Stage("restored Preparation / STORM L3 / unchanged Gold-HP-kills");yield return new WaitForSecondsRealtime(2);
        p.RequestSellSelectedTower();Require(p.IsSellConfirmationVisible,"Sell confirmation");Stage("Sell confirmation / actual configured refund");yield return new WaitForSecondsRealtime(1.5f);
        Require(p.ConfirmSellSelectedTower().Success,"Confirm Sell");yield return null;
        Require(b.Simulation.State.Towers.Count==0&&UnityEngine.Object.FindObjectsByType<FlameModelBinding>(FindObjectsSortMode.None).Length==0,"Sell cleanup");
        File.WriteAllBytes(Reports+"video-sold.json",File.ReadAllBytes(_store.PrimaryPath));
        Stage("sold / no model roots / public-command Editor proof complete");yield return new WaitForSecondsRealtime(2);Finish(true,"");
    }

    private void Stage(string text)
    { _stage=text;_label.text="EDITOR Play Mode | earned replay | prefix test clock x4 / fixed30Hz / final production combat | Device Deferred QA53/54/55\n"+text; }

    private void Finish(bool success,string error)
    {
        if(!_recording)return;_recording=false;
        // Ошибка capture должна остановить и gameplay coroutine, прежде чем снять isolated store factory.
        StopAllCoroutines();
        UnityEngine.Application.logMessageReceived-=Log;MainMenuController.CheckpointStoreFactory=null;
        File.WriteAllLines(Reports+"video-frames.txt",_rows);File.WriteAllLines(Reports+"video-console.txt",_console);
        File.WriteAllText(Reports+"video-completion.json",JsonUtility.ToJson(new Completion {
            success=success,error=error,frames=_number,elapsed=Time.realtimeSinceStartupAsDouble-_started,
            editor=true,realTime=true,liveResourceMutation=false,deviceDeferred="QA53/54/55" },true)+"\n");
    }
    private void OnDestroy(){if(_recording)Finish(false,"Recorder destroyed before completion.");}
    private static void Require(bool value,string why){if(!value)throw new InvalidOperationException(why);}
    private static string SafeWire(BattleSimulation s) => Veilward.Tests.Shared.AbilityBossTrace.Snapshot(s);
    private static string Hash(byte[] bytes)
    {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    [Serializable] private sealed class Origin {public string origin,restore,sha256,readbackSha256;public bool freshRun,liveResourceMutation;}
    [Serializable] private sealed class Completion {public bool success,editor,realTime,liveResourceMutation;public string error,deviceDeferred;public int frames;public double elapsed;}
}
