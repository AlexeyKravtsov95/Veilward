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

public sealed class Art3bEditorRecording : MonoBehaviour
{
    private const string Frames = "TestResults/ART3B/R2/video-frames/";
    private const string Reports = "Docs/review/ART.3b/revision-2/reports/";
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
    private BattleSceneController Battle => UnityEngine.Object.FindFirstObjectByType<BattleSceneController>();

    public static string BeginRecording()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode required.");
        if (UnityEngine.Object.FindFirstObjectByType<Art3bEditorRecording>() != null)
            throw new InvalidOperationException("Recording already present.");
        var root = new GameObject("ART3B Editor evidence recorder");
        DontDestroyOnLoad(root);
        root.AddComponent<Art3bEditorRecording>().Begin();
        return "Started continuous real-time Editor flow; inspect completion.json before encoding.";
    }

    private void Begin()
    {
        Directory.CreateDirectory(Frames); Directory.CreateDirectory(Reports);
        if (Directory.GetFiles(Frames, "*.png").Length != 0)
            throw new InvalidOperationException("Use an empty task-owned video-frames directory.");
        var original = File.ReadAllBytes("TestResults/ART3B/natural-L1-checkpoint.json");
        _store = new WaveCheckpointStore(Path.GetFullPath("TestResults/ART3B/VideoStore"));
        Directory.CreateDirectory(Path.GetDirectoryName(_store.PrimaryPath));
        File.WriteAllBytes(_store.PrimaryPath, original);
        Require(File.ReadAllBytes(_store.PrimaryPath).SequenceEqual(original), "L1 checkpoint restore readback");
        File.WriteAllText(Reports+"video-checkpoint-origin.json", JsonUtility.ToJson(new Origin {
            origin="TestResults/ART3B/natural-L1-checkpoint.json; fresh earned public-command lifecycle test",
            restore="isolated TestResults/ART3B/VideoStore primary, before MainMenu Continue",
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
            var wet=s!=null?s.State.Enemies.Count(e=>e.Wet!=null):0;
            var towers=s!=null?string.Join(";",s.State.Towers.Select(t=>t.Id.Value+":"+t.Definition.Id.Value+":L"+t.Level)):"";
            _rows.Add(string.Join("|",_number,now.ToString("R",System.Globalization.CultureInfo.InvariantCulture),Time.frameCount,
                _stage,s!=null?s.State.Tick.ToString():"",s!=null?s.State.Gold.ToString():"",s!=null?s.State.BaseHealth.ToString():"",
                s!=null?s.State.Kills.ToString():"",s!=null?s.State.Enemies.Count.ToString():"",wet,towers));
            var pixels=ScreenCapture.CaptureScreenshotAsTexture();
            Require(pixels.width==1600&&pixels.height==900,"GameView1600x900 required");
            File.WriteAllBytes(Frames+_number.ToString("D5")+".png",pixels.EncodeToPNG());
            Destroy(pixels); _number++;next=now+.1;
        }
    }

    private IEnumerator Flow()
    {
        yield return SceneManager.LoadSceneAsync("MainMenu");
        Stage("menu Continue / unchanged earned TIDE L1 checkpoint"); yield return new WaitForSecondsRealtime(1.5f);
        var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
        Require(menu.ContinueButton.interactable,"Continue available");menu.ContinueButton.onClick.Invoke();
        yield return null;yield return null;
        var b=Battle;Require(b!=null&&!b.IsPaused,"unpaused Battle");
        var tower=b.Simulation.State.Towers.Single();_id=tower.Id;_cell=tower.Coordinate;
        Require(tower.Definition.Id.Value=="tide"&&tower.Level==1,"earned TIDE L1");
        var p=b.GetComponent<BattlePlacementController>();Require(p.TrySelectCell(_cell),"select tower");
        Stage("TIDE L1 / Inspect + real Upgrade price");yield return new WaitForSecondsRealtime(2);
        p.UpgradeButton.onClick.Invoke();yield return null;
        Require(b.Simulation.State.Towers.Single().Level==2,"UI Upgrade L2");
        Stage("TIDE L2 / same TowerId + cell / earned budget");yield return new WaitForSecondsRealtime(2);
        p.UpgradeButton.onClick.Invoke();yield return null;
        Require(b.Simulation.State.Towers.Single().Level==3,"UI Upgrade L3");
        Stage("TIDE L3 / same TowerId + cell / MAX + range");yield return new WaitForSecondsRealtime(2);
        var safe=File.ReadAllBytes(_store.PrimaryPath);
        File.WriteAllBytes(Reports+"video-L3-before-combat.json",safe);
        p.CancelSelection();Require(b.StartWave()==WaveTransitionResult.Started,"StartWave");
        var firstTick=b.Simulation.State.Tick;var start=Time.realtimeSinceStartupAsDouble;
        Stage("advancing REAL-TIME Combat / HP + Wet / generic beam #47");
        while(!b.Simulation.State.Enemies.Any(e=>e.Wet!=null)&&Time.realtimeSinceStartupAsDouble-start<15)
            yield return null;
        Require(b.Simulation.State.Tick>firstTick&&b.Simulation.State.Enemies.Any(e=>e.Wet!=null),"advancing combat + actual Wet");
        var enemy=b.Simulation.State.Enemies.First(e=>e.Wet!=null);
        Require(b.GetComponent<EnemyInspectorController>().Select(enemy.Id),"actual Wet enemy Inspect");
        Stage("advancing combat / actual Wet enemy Inspect + HP");yield return new WaitForSecondsRealtime(2);
        File.WriteAllText(Reports+"video-inspector.txt",b.GetComponent<EnemyInspectorController>().Details+"\n");
        b.GetComponent<EnemyInspectorController>().Close();
        b.ReturnToMenu();yield return null;yield return null;
        Stage("interrupted Combat / file-menu Continue rolls back to Preparation");yield return new WaitForSecondsRealtime(1.5f);
        menu=UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
        Require(menu.ContinueButton.interactable,"rollback Continue available");menu.ContinueButton.onClick.Invoke();yield return null;yield return null;
        b=Battle;Require(b.Simulation.State.Phase==BattlePhase.Preparation,"Combat rollback phase");
        Require(File.ReadAllBytes(_store.PrimaryPath).SequenceEqual(safe),"Combat rollback preserves saved bytes");
        tower=b.Simulation.State.Towers.Single();Require(tower.Id==_id&&tower.Coordinate==_cell&&tower.Level==3,"rollback identity/level");
        p=b.GetComponent<BattlePlacementController>();Require(p.TrySelectCell(_cell),"rollback select");
        Stage("restored Preparation / TIDE L3 / unchanged Gold-HP-kills");yield return new WaitForSecondsRealtime(2);
        p.RequestSellSelectedTower();Require(p.IsSellConfirmationVisible,"Sell confirmation");
        Stage("Sell confirmation / actual configured refund");yield return new WaitForSecondsRealtime(1.5f);
        Require(p.ConfirmSellSelectedTower().Success,"Confirm Sell");yield return null;
        Require(b.Simulation.State.Towers.Count==0&&UnityEngine.Object.FindObjectsByType<FlameModelBinding>(FindObjectsSortMode.None).Length==0,"Sell cleanup");
        File.WriteAllBytes(Reports+"video-sold.json",File.ReadAllBytes(_store.PrimaryPath));
        Stage("sold / no model roots / public-command Editor proof complete");yield return new WaitForSecondsRealtime(2);
        Finish(true,"");
    }

    private void Stage(string text)
    { _stage=text;_label.text="EDITOR Play Mode | earned checkpoint replay | real-time | Device Deferred QA53/54/55\n"+text; }

    private void Finish(bool success,string error)
    {
        if(!_recording)return;_recording=false;
        UnityEngine.Application.logMessageReceived-=Log;MainMenuController.CheckpointStoreFactory=null;
        File.WriteAllLines(Reports+"video-frames.txt",_rows);File.WriteAllLines(Reports+"video-console.txt",_console);
        File.WriteAllText(Reports+"video-completion.json",JsonUtility.ToJson(new Completion {
            success=success,error=error,frames=_number,elapsed=Time.realtimeSinceStartupAsDouble-_started,
            editor=true,realTime=true,liveResourceMutation=false,deviceDeferred="QA53/54/55" },true)+"\n");
    }
    private void OnDestroy(){if(_recording)Finish(false,"Recorder destroyed before completion.");}
    private static void Require(bool value,string why){if(!value)throw new InvalidOperationException(why);}
    private static string Hash(byte[] bytes)
    {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    [Serializable] private sealed class Origin {public string origin,restore,sha256,readbackSha256;public bool freshRun,liveResourceMutation;}
    [Serializable] private sealed class Completion {public bool success,editor,realTime,liveResourceMutation;public string error,deviceDeferred;public int frames;public double elapsed;}
}
