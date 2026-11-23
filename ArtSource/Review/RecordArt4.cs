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

public sealed class Art4EditorRecording : MonoBehaviour
{
    private const string Frames = "TestResults/art4/video-frames/";
    private const string Reports = "Docs/review/ART.4/reports/";
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
        if (UnityEngine.Object.FindFirstObjectByType<Art4EditorRecording>() != null)
            throw new InvalidOperationException("Recording already present.");
        var root = new GameObject("ART4 Editor evidence recorder");
        DontDestroyOnLoad(root);
        root.AddComponent<Art4EditorRecording>().Begin();
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
        _store = new WaveCheckpointStore(Path.GetFullPath("TestResults/art4/VideoStore"));
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
                s!=null?s.State.Kills.ToString():"",s!=null?s.State.Enemies.Count.ToString():"",storm,towers));
            _prepareView.Invoke(null,null);
            var pixels=(byte[])_encodeFrame.Invoke(null,new object[]{1600,900});
            File.WriteAllBytes(Frames+_number.ToString("D5")+".png",pixels);
            _number++;next=now+.1; if(_number%10==0)File.WriteAllText("TestResults/art4/video-progress.txt",_stage+" | frame="+Time.frameCount+" | tick="+(s!=null?s.State.Tick:0));
        }
    }

    private IEnumerator Flow()
    {
        yield return SceneManager.LoadSceneAsync("MainMenu");
        Stage("MainMenu / fresh NewGame");yield return new WaitForSecondsRealtime(1.5f);
        var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
        menu.NewGameButton.onClick.Invoke();yield return null;yield return null;
        var b=Battle;var s=b.Simulation;
        Require(s.State.Gold==150&&s.State.BaseHealth==20&&s.State.Towers.Count==0,"fresh production150/20");
        Stage("NewGame / production map / choose FLAME");yield return new WaitForSecondsRealtime(1.2f);
        var choice=b.GetComponent<ElementChoiceController>();Require(choice.SelectOption("flame"),"choose preview");
        yield return new WaitForSecondsRealtime(.8f);Require(choice.ConfirmChoice().Success,"choose confirm");
        var p=b.GetComponent<BattlePlacementController>();var cell=new GridCoordinate(-4,-1);
        Require(p.TrySelectCell(cell),"buildable socket");p.SelectTowerType(new TowerDefinitionId("flame"));
        Stage("Build preview / selected socket + range / no purchase");yield return new WaitForSecondsRealtime(1.7f);
        Require(s.State.Gold==150&&s.State.Towers.Count==0,"preview free");p.ConfirmSelection();yield return null;
        Require(s.State.Towers.Count==1&&s.State.Gold==80,"public place");var id=s.State.Towers.Single().Id;
        Require(p.TrySelectCell(cell),"occupied selects tower");
        Stage("Placed FLAME L1 / occupied socket selects tower");yield return new WaitForSecondsRealtime(1.2f);p.CancelSelection();
        Require(b.StartWave()==WaveTransitionResult.Started,"first wave");
        Stage("Combat1 / production Update / moving Shade + HP + attacks");
        var start=s.State.Tick;yield return new WaitForSecondsRealtime(1.5f);
        Require(b.enabled&&s.State.Tick>start&&s.State.Enemies.Count>0,"production Update advancing");
        var ability=b.GetComponent<AbilityController>();var target=s.Path.GetPosition(s.State.Enemies[0].DistanceAlongPath);
        Require(ability.BeginPreview()&&ability.SelectTarget(target),"ability preview");
        Stage("Ability preview / live combat / public Confirm");yield return new WaitForSecondsRealtime(.6f);
        Require(ability.ConfirmPreview(),"ability Confirm");
        File.WriteAllText(Reports+"video-ability.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{tick=s.State.Tick,cast=ability.LastCast,productionUpdate=b.enabled},Newtonsoft.Json.Formatting.Indented));
        Stage("Ability cast / production Combat1 continues");
        var began=Time.realtimeSinceStartupAsDouble;
        while(s.State.Phase==BattlePhase.Combat&&Time.realtimeSinceStartupAsDouble-began<30)yield return null;
        Require(s.State.Phase==BattlePhase.WaveComplete,"wave complete");
        Require(b.ContinueToNextWave()==WaveTransitionResult.Continued,"NextWave before economy");
        Stage("Preparation2 / earned gold / Upgrade FLAME L2");
        Require(p.TrySelectCell(cell),"select upgrade");yield return new WaitForSecondsRealtime(.8f);
        Require(p.UpgradeSelectedTower().Success,"earned Upgrade");
        yield return new WaitForSecondsRealtime(1.4f);p.CancelSelection();
        var safe=File.ReadAllBytes(_store.PrimaryPath);var safeWire=SafeWire(s);
        File.WriteAllBytes(Reports+"video-safe.json",safe);File.WriteAllText(Reports+"video-safe-wire.txt",safeWire);
        Require(b.StartWave()==WaveTransitionResult.Started,"Combat2");
        Stage("Combat2 / production Update / interruption before Continue");yield return new WaitForSecondsRealtime(3);
        Require(s.State.Tick>start&&s.State.Phase==BattlePhase.Combat,"live Combat2");
        b.ReturnToMenu();yield return null;yield return null;
        Stage("MainMenu / file Continue / interrupted Combat rollback");yield return new WaitForSecondsRealtime(1.3f);
        menu=UnityEngine.Object.FindFirstObjectByType<MainMenuController>();Require(menu.ContinueButton.interactable,"Continue available");
        menu.ContinueButton.onClick.Invoke();yield return null;yield return null;b=Battle;
        Require(SafeWire(b.Simulation)==safeWire,"whole snapshot + live Grid rollback");
        Require(File.ReadAllBytes(_store.PrimaryPath).SequenceEqual(safe),"unchanged file bytes");
        File.WriteAllText(Reports+"video-rollback-wire.txt",SafeWire(b.Simulation));
        Stage("Continue / restored Preparation2 + FLAME L2 + Grid");yield return new WaitForSecondsRealtime(1.4f);
        p=b.GetComponent<BattlePlacementController>();Require(p.TrySelectCell(cell),"select restored");
        p.RequestSellSelectedTower();Require(p.IsSellConfirmationVisible,"Sell confirmation");
        Stage("Sell confirmation / earned refund");yield return new WaitForSecondsRealtime(1.1f);
        Require(p.ConfirmSellSelectedTower().Success,"Sell Confirm");yield return null;
        Require(b.Simulation.State.Towers.Count==0,"Sold cleanup");
        File.WriteAllBytes(Reports+"video-sold.json",File.ReadAllBytes(_store.PrimaryPath));
        Stage("Sold / public flow complete / no resource substitution");yield return new WaitForSecondsRealtime(1.4f);Finish(true,"");
    }

    private void Stage(string text)
    { _stage=text;_label.text="EDITOR Play Mode | fresh NewGame | production Update throughout | Device Deferred QA53/54/55\n"+text; }

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
    [Serializable] private sealed class Completion {public bool success,editor,realTime,liveResourceMutation;public string error,deviceDeferred;public int frames;public double elapsed;}
}
