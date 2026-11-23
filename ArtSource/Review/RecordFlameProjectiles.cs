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

public sealed class FlameEditorRecording : MonoBehaviour
{
    private const string Frames = "TestResults/vfx1a/video-frames/";
    private const string Reports = "Docs/review/VFX.1a/reports/";
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
        if (UnityEngine.Object.FindFirstObjectByType<FlameEditorRecording>() != null)
            throw new InvalidOperationException("Recording already present.");
        var root = new GameObject("VFX1a Editor evidence recorder");
        DontDestroyOnLoad(root);
        root.AddComponent<FlameEditorRecording>().Begin();
        return "Started continuous fresh NewGame production Update flow; inspect completion.json before encoding.";
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
        _store = new WaveCheckpointStore(Path.GetFullPath("TestResults/vfx1a/VideoStore"));
        Directory.CreateDirectory(Path.GetDirectoryName(_store.PrimaryPath));
        File.WriteAllText(Reports+"video-origin.json", "{\"freshNewGame\":true,\"liveResourceMutation\":false,\"testClock\":false,\"productionUpdate\":true}\n");
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
                shots=b!=null?Enumerable.Range(0,FlameProjectilePool.Capacity).Select(i=>new {slot=i, remaining=b.FlameProjectiles.GetView(i).Remaining, head=b.FlameProjectiles.GetView(i).HeadPosition.ToString("R"), source=b.FlameProjectiles.GetView(i).Source.ToString("R"), target=b.FlameProjectiles.GetView(i).Target.ToString("R"), flying=b.FlameProjectiles.GetView(i).IsFlying, impact=b.FlameProjectiles.GetView(i).ImpactVisible, tower=b.FlameProjectiles.GetEvent(i).AttackerTowerId.Value, enemy=b.FlameProjectiles.GetEvent(i).TargetEnemyId.Value, attackTick=b.FlameProjectiles.GetEvent(i).Tick, level=b.FlameProjectiles.GetLevel(i)}).ToArray():null });
            _number++;next=now+1.0/30; if(_number%10==0)File.WriteAllText("TestResults/vfx1a/video-progress.txt",_stage+" | frame="+Time.frameCount+" | tick="+(s!=null?s.State.Tick:0));
        }
    }

    private IEnumerator Flow()
    {
        yield return SceneManager.LoadSceneAsync("MainMenu");
        Stage("MainMenu / fresh NewGame"); yield return new WaitForSecondsRealtime(1);
        var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
        menu.NewGameButton.onClick.Invoke(); yield return null; yield return null;
        var b=Battle; var s=b.Simulation;
        Require(s.State.Gold==150&&s.State.BaseHealth==20&&s.State.Towers.Count==0,"fresh150/20");
        var choice=b.GetComponent<ElementChoiceController>(); Require(choice.SelectOption("flame")&&choice.ConfirmChoice().Success,"choose FLAME");
        var p=b.GetComponent<BattlePlacementController>(); var cell=new GridCoordinate(-4,-1);
        Require(p.TrySelectCell(cell),"socket"); p.SelectTowerType(new TowerDefinitionId("flame"));
        Stage("Build preview / public placement"); yield return new WaitForSecondsRealtime(.7f);
        p.ConfirmSelection(); yield return null;
        Require(s.State.Towers.Count==1&&s.State.Gold==80,"public placement70");
        p.CancelSelection();
        for(var level=1;level<=3;level++)
        {
            Require(b.StartWave()==WaveTransitionResult.Started,"StartWave L"+level);
            Stage("FLAME L"+level+" / production Update combat");
            var deadline=Time.realtimeSinceStartupAsDouble+5;
            while(!b.FlameProjectiles.GetView(0).IsFlying&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Require(b.FlameProjectiles.GetView(0).IsFlying,"actual Update flight L"+level);
            if(level==1)
            {
                var pause=b.GetComponent<BattlePauseController>();pause.Pause();
                var frozen=ShotGeometry(b); var tick=s.State.Tick;
                Stage("Pause / frozen FLAME flight");yield return new WaitForSecondsRealtime(1.2f);
                Require(ShotGeometry(b)==frozen&&s.State.Tick==tick,"pause geometry/tick frozen");
                pause.OnApplicationFocus(true);Require(pause.Resume(),"Resume");
                Stage("Resume / production Update flight without catch-up");
            }
            if(level==1)
            {
                yield return new WaitForSecondsRealtime(1.5f);
                var ability=b.GetComponent<AbilityController>();
                var point=s.Path.GetPosition(s.State.Enemies[0].DistanceAlongPath);
                Require(ability.BeginPreview()&&ability.SelectTarget(point)&&ability.ConfirmPreview(),"public ability");
                Stage("FLAME L1 / ability + real Update damage and death");
            }
            if(level<3)
            {
                deadline=Time.realtimeSinceStartupAsDouble+35;
                while(s.State.Phase==BattlePhase.Combat&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                Require(s.State.Phase==BattlePhase.WaveComplete,"earned complete"+level);
                Require(b.ContinueToNextWave()==WaveTransitionResult.Continued,"NextWave");
                Require(p.TrySelectCell(cell),"select Upgrade");
                Require(p.UpgradeSelectedTower().Success,"earned Upgrade L"+(level+1));
                Stage("Earned FLAME L"+(level+1)+" / Preparation");yield return new WaitForSecondsRealtime(.7f);p.CancelSelection();
            }
        }
        yield return new WaitForSecondsRealtime(.7f);
        var checkpoint=b.GetComponent<BattleCheckpointController>();
        Require(_store.TryLoad("eight-wave-ability-boss-v2",((BattleScenarioConfig)new UnityEditor.SerializedObject(b).FindProperty("scenario").objectReferenceValue).CreateSimulationForCheckpoint,out var loaded),"load safe");
        var config=(BattleScenarioConfig)new UnityEditor.SerializedObject(b).FindProperty("scenario").objectReferenceValue;
        var control=config.CreateSimulationForCheckpoint(loaded.Envelope);control.RestoreCheckpoint(loaded.Envelope.state);
        var safeWire=SafeWire(control);var safe=File.ReadAllBytes(_store.PrimaryPath);
        File.WriteAllBytes(Reports+"video-safe.json",safe);File.WriteAllText(Reports+"video-safe-wire.txt",safeWire);
        Stage("FLAME L3 / active flight before menu interruption");yield return new WaitForSecondsRealtime(.7f);
        b.ReturnToMenu();yield return null;yield return null;
        Stage("MainMenu / file Continue / pool unloaded");yield return new WaitForSecondsRealtime(.7f);
        menu=UnityEngine.Object.FindFirstObjectByType<MainMenuController>();Require(menu.ContinueButton.interactable,"Continue available");
        menu.ContinueButton.onClick.Invoke();yield return null;yield return null;b=Battle;
        Require(SafeWire(b.Simulation)==safeWire,"whole snapshot + live Grid rollback");
        Require(File.ReadAllBytes(_store.PrimaryPath).SequenceEqual(safe),"unchanged file");
        Require(Enumerable.Range(0,8).All(i=>b.FlameProjectiles.GetView(i).Remaining==0),"Continue clean pool");
        File.WriteAllText(Reports+"video-rollback-wire.txt",SafeWire(b.Simulation));
        Stage("Continue / Preparation3 FLAME L3 / clean pool + Grid");yield return new WaitForSecondsRealtime(1);
        p=b.GetComponent<BattlePlacementController>();Require(p.TrySelectCell(cell),"select restored L3");
        p.RequestSellSelectedTower();Require(p.IsSellConfirmationVisible,"Sell confirmation");yield return new WaitForSecondsRealtime(.5f);
        Require(p.ConfirmSellSelectedTower().Success,"public Sell");yield return null;
        Require(b.Simulation.State.Towers.Count==0,"sold");
        File.WriteAllBytes(Reports+"video-sold.json",File.ReadAllBytes(_store.PrimaryPath));
        Stage("Sold / public flow complete / no resource substitution");yield return new WaitForSecondsRealtime(.8f);Finish(true,"");
    }

    private static string ShotGeometry(BattleSceneController b) => string.Join(";",b.GetComponentsInChildren<LineRenderer>(true).Where(l=>l.transform.parent.name.StartsWith("Flame Projectile Pool")).Select(l=>l.enabled+":"+l.GetPosition(0).ToString("R")+":"+l.GetPosition(1).ToString("R")+":"+l.GetPosition(2).ToString("R")+":"+l.widthMultiplier.ToString("R")));

    private void Stage(string text)
    { _stage=text;_label.text="EDITOR Play Mode | fresh NewGame | production Update throughout | Device Deferred QA53/54/55\n"+text; }

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
