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
using Veilward.Tests.Shared;

public sealed class BasicEditorRecording : MonoBehaviour
{
    private const string Frames = "TestResults/vfx1b/video-frames/";
    private const string Reports = "Docs/review/VFX.1b/reports/";
    private readonly List<object> _shots = new List<object>();
    private readonly List<string> _rows = new List<string>();
    private readonly List<string> _console = new List<string>();
    private WaveCheckpointStore _store;
    private Text _label;
    private string _stage = "fresh MainMenu / NewGame";
    private bool _recording;
    private double _started;
    private int _number;
    private System.Reflection.MethodInfo _encodeFrame, _prepareView;
    private BattleSceneController Battle => UnityEngine.Object.FindFirstObjectByType<BattleSceneController>();

    public static string BeginRecording()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode required.");
        if (UnityEngine.Object.FindFirstObjectByType<BasicEditorRecording>() != null)
            throw new InvalidOperationException("Recording already present.");
        // script run может загрузить новую assembly того же recorder: убрать только завершённые task-owned labels.
        foreach(var old in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Where(c=>c.GetType().Name==nameof(BasicEditorRecording)))
        {
            var field=old.GetType().GetField("_recording",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            if(field!=null && field.GetValue(old) is bool running && !running) UnityEngine.Object.DestroyImmediate(old.gameObject);
        }
        var root = new GameObject("VFX1b Editor evidence recorder");
        DontDestroyOnLoad(root);
        root.AddComponent<BasicEditorRecording>().Begin();
        return "Started continuous earned Continue8 production Update flow; inspect completion.json before encoding.";
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
        _store = new WaveCheckpointStore(Path.GetFullPath("TestResults/vfx1b/VideoStore"));
        Directory.CreateDirectory(Path.GetDirectoryName(_store.PrimaryPath));
        File.WriteAllText(Reports+"video-origin.json", "{\"freshNewGame\":false,\"liveResourceMutation\":false,\"testClock\":false,\"productionUpdate\":true}\n");
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
                s!=null?s.State.Kills.ToString():"",s!=null?s.State.Enemies.Count.ToString():"",storm,towers, b!=null?string.Join(";",b.GetComponentsInChildren<EnemyModelView>().Select(v=>v.Pose)):""));
            _prepareView.Invoke(null,null);
            var pixels=(byte[])_encodeFrame.Invoke(null,new object[]{1600,900});
            File.WriteAllBytes(Frames+_number.ToString("D5")+".png",pixels);
            _shots.Add(new { index=_number, elapsed=now, frame=Time.frameCount, stage=_stage, paused=b!=null&&b.IsPaused, tick=s!=null?s.State.Tick:0,
                timeScale=Time.timeScale,captureFramerate=Time.captureFramerate,productionUpdate=b!=null&&b.enabled,
                shots=b!=null?Enumerable.Range(0,BasicProjectilePool.Capacity).Select(i=>new {slot=i,kind=b.BasicProjectiles.GetView(i).Kind.ToString(),remaining=b.BasicProjectiles.GetView(i).Remaining,head=b.BasicProjectiles.GetView(i).HeadPosition.ToString("R"),source=b.BasicProjectiles.GetView(i).Source.ToString("R"),target=b.BasicProjectiles.GetView(i).Target.ToString("R"),flying=b.BasicProjectiles.GetView(i).IsFlying,impact=b.BasicProjectiles.GetView(i).ImpactVisible,tower=b.BasicProjectiles.GetEvent(i).AttackerTowerId.Value,enemy=b.BasicProjectiles.GetEvent(i).TargetEnemyId.Value,attackTick=b.BasicProjectiles.GetEvent(i).Tick,level=b.BasicProjectiles.GetLevel(i)}).ToArray():null,
                cues=b!=null?b.GetComponentsInChildren<EnemyStatusView>().Select(v=>new {enemy=v.name,first=v.FirstCue.Kind?.ToString(),second=v.SecondCue.Kind?.ToString(),slots=v.VisibleSlotCount}).ToArray():null });
            _number++;next=now+1.0/30; if(_number%10==0)File.WriteAllText("TestResults/vfx1b/video-progress.txt",_stage+" | frame="+Time.frameCount+" | tick="+(s!=null?s.State.Tick:0));
        }
    }

    public static string Prepare()
    {
        Directory.CreateDirectory(Reports);
        var config=AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWaveAbilityBossV2.asset");
        var run=new AbilityBossTrace.Run(config.CreateSimulation(),BossMissionScript.Load("gale"));
        WaveCheckpointEnvelope earned=null;
        run.stable=(r,stage)=>{if(stage=="preparation"&&r.sim.State.WaveIndex==7)earned=AbilityBossTrace.Envelope(r.sim);};
        run.Play();Require(earned!=null,"earned Preparation8");
        AbilityBossTrace.Write(Reports+"earned-stone-route",run);
        var origin=new WaveCheckpointStore(Path.GetFullPath("TestResults/vfx1b/OriginStore"));
        Require(origin.TrySave(earned,run.sim.TryValidateCheckpoint,out var error,config.CreateSimulationForCheckpoint),error);
        File.WriteAllBytes(Reports+"earned-preparation8.json",File.ReadAllBytes(origin.PrimaryPath));
        return "Fresh public-command GALE→STONE→TIDE full replay; unchanged earned Preparation8 captured before realtime recording.";
    }
    private IEnumerator Flow()
    {
        var config=AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWaveAbilityBossV2.asset");
        Directory.CreateDirectory(_store.RootDirectory);
        File.WriteAllBytes(_store.PrimaryPath,File.ReadAllBytes(Reports+"earned-preparation8.json"));
        Require(_store.TryLoad("eight-wave-ability-boss-v2",config.CreateSimulationForCheckpoint,out var loaded),"read actual production wire");
        var earned=loaded.Envelope;
        var control=config.CreateSimulationForCheckpoint(earned);control.RestoreCheckpoint(earned.state);
        File.WriteAllText(Reports+"video-origin.json","{\"freshNewGame\":false,\"earnedCheckpoint\":\"earned-preparation8.json\",\"preparationClock\":\"explicit public-command replay before recording\",\"testClock\":false,\"productionUpdate\":true,\"liveResourceMutation\":false}\n");
        yield return SceneManager.LoadSceneAsync("MainMenu");
        Stage("MainMenu / earned file Continue8");yield return new WaitForSecondsRealtime(.7f);
        var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuController>();Require(menu.ContinueButton.interactable,"earned Continue available");
        menu.ContinueButton.onClick.Invoke();yield return null;yield return null;
        var b=Battle;var s=b.Simulation;
        Require(SafeWire(s)==SafeWire(control),"whole earned state+Grid loaded");
        var safe=File.ReadAllBytes(_store.PrimaryPath);var safeWire=SafeWire(s);
        File.WriteAllText(Reports+"video-safe-wire.txt",safeWire);File.WriteAllBytes(Reports+"video-safe.json",safe);
        Stage("Earned GALE STONE TIDE / Preparation8 / clean owners");yield return new WaitForSecondsRealtime(.7f);
        Require(b.StartWave()==WaveTransitionResult.Started,"StartWave8");
        Stage("GALE STONE TIDE / production Update / live status snapshots");
        var deadline=Time.realtimeSinceStartupAsDouble+5;
        while(!Enumerable.Range(0,12).Any(i=>b.BasicProjectiles.GetView(i).Remaining>0)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        Require(Enumerable.Range(0,12).Any(i=>b.BasicProjectiles.GetView(i).Remaining>0),"actual Update shot");
        yield return new WaitForSecondsRealtime(.08f);
        var pause=b.GetComponent<BattlePauseController>();pause.Pause();
        var frozen=ShotGeometry(b);var tick=s.State.Tick;
        Stage("Pause / frozen basic shot and status geometry");yield return new WaitForSecondsRealtime(.8f);
        Require(ShotGeometry(b)==frozen&&s.State.Tick==tick,"actual pause geometry");
        pause.OnApplicationFocus(true);Require(pause.Resume(),"Resume");
        Stage("GALE STONE TIDE / production Update / cadence and status transitions");
        yield return new WaitForSecondsRealtime(15);
        Require(s.State.Phase==BattlePhase.Combat,"interrupted Combat8 before terminal");
        b.ReturnToMenu();yield return null;yield return null;
        Stage("MainMenu / interrupted Combat8 / Continue");yield return new WaitForSecondsRealtime(.6f);
        menu=UnityEngine.Object.FindFirstObjectByType<MainMenuController>();Require(menu.ContinueButton.interactable,"rollback Continue");
        menu.ContinueButton.onClick.Invoke();yield return null;yield return null;b=Battle;
        Require(SafeWire(b.Simulation)==safeWire,"whole state and Grid rollback");
        Require(File.ReadAllBytes(_store.PrimaryPath).SequenceEqual(safe),"file bytes unchanged");
        Require(Enumerable.Range(0,12).All(i=>b.BasicProjectiles.GetView(i).Remaining==0),"Continue clears shots");
        Require(b.GetComponentsInChildren<EnemyStatusView>().Length==0,"Continue clears status owners");
        File.WriteAllText(Reports+"video-rollback-wire.txt",SafeWire(b.Simulation));
        Stage("Continue8 / clean shots and status owners / earned state");yield return new WaitForSecondsRealtime(.8f);
        var p=b.GetComponent<BattlePlacementController>();var tower=b.Simulation.State.Towers.First(t=>t.Definition.Id.Value=="gale");
        Require(p.TrySelectCell(tower.Coordinate),"select restored Gale");
        p.RequestSellSelectedTower();Require(p.IsSellConfirmationVisible,"Sell confirmation");yield return new WaitForSecondsRealtime(.4f);
        Require(p.ConfirmSellSelectedTower().Success,"public Sell");yield return null;
        File.WriteAllBytes(Reports+"video-sold.json",File.ReadAllBytes(_store.PrimaryPath));
        Stage("Sold GALE / public command / no resource substitution");yield return new WaitForSecondsRealtime(.6f);
        Finish(true,"");
    }
    private static string ShotGeometry(BattleSceneController b) => string.Join(";",b.GetComponentsInChildren<LineRenderer>(true).Where(l=>l.transform.parent.name.StartsWith("Basic Projectile Pool")).Select(l=>l.enabled+":"+string.Join(",",Enumerable.Range(0,l.positionCount).Select(i=>l.GetPosition(i).ToString("R")))+":"+l.widthMultiplier.ToString("R")));

    private void Stage(string text)
    { _stage=text;_label.text="EDITOR Play Mode | earned public-command checkpoint | production Update throughout | Device Deferred QA53/54/55\n"+text; }

    private void Finish(bool success,string error)
    {
        if(!_recording)return;_recording=false;
        // Ошибка capture должна остановить и gameplay coroutine, прежде чем снять isolated store factory.
        StopAllCoroutines();
        UnityEngine.Application.logMessageReceived-=Log;MainMenuController.CheckpointStoreFactory=null;
        File.WriteAllText(Reports+"video-shots.json",Newtonsoft.Json.JsonConvert.SerializeObject(_shots,Newtonsoft.Json.Formatting.Indented));
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
    [Serializable] private sealed class Completion {public bool success,editor,realTime,liveResourceMutation;public string error,deviceDeferred;public int frames;public double elapsed;}
}
