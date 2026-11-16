// Editor-only ephemeral utility, not imported into Assets or included in builds.
// unity command 'script run' -- --file ArtSource/Review/CaptureArt1.cs --entry CaptureArt1.Stills
using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using Veilward.Presentation;

public static class CaptureArt1
{
    private const string Output = "Docs/review/ART.1/";
    private static ArtReferenceShowcase Showcase => UnityEngine.Object.FindFirstObjectByType<ArtReferenceShowcase>();
    [Serializable] private sealed class FrameInfo
    {
        public string file; public bool playMode; public int frame; public long tick;
        public int attacks; public int kills; public int gold; public int activeEffects;
        public float orthographicSize; public Vector3 cameraPosition; public Vector3 cameraEuler;
    }
    private static void Capture(string path, Camera camera, int width, int height)
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Real Play Mode is required.");
        var old = camera.targetTexture; var active = RenderTexture.active;
        var rt = RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32);
        Texture2D image = null;
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            image = new Texture2D(width,height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply();
            File.WriteAllBytes(path,image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = old; RenderTexture.active = active;
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
            RenderTexture.ReleaseTemporary(rt);
        }
    }
    private static void Still(string name, int width=1600, int height=900)
    {
        var s=Showcase; var c=s.WorldCamera;
        Capture(Output+name,c,width,height);
        var info=new FrameInfo { file=name,playMode=EditorApplication.isPlaying,frame=Time.frameCount,
            tick=s.Simulation.State.Tick,attacks=s.ObservedAttacks,kills=s.Simulation.State.Kills,gold=s.Simulation.State.Gold,
            activeEffects=s.ActiveStrikeCount,orthographicSize=c.orthographicSize,cameraPosition=c.transform.position,cameraEuler=c.transform.eulerAngles };
        File.AppendAllText(Output+"capture-log.jsonl",JsonUtility.ToJson(info)+"\n");
    }
    public static string Stills()
    {
        var s=Showcase;
        if (s==null || !EditorApplication.isPlaying) throw new InvalidOperationException("Open ArtReference in Play Mode.");
        Directory.CreateDirectory(Output); File.WriteAllText(Output+"capture-log.jsonl","");
        var c=s.WorldCamera; var position=c.transform.position; var rotation=c.transform.rotation;
        var size=c.orthographicSize; var background=c.backgroundColor;
        s.SetPaused(true); s.RestartShowcase();
        var guard=0;
        // Three enemies and a real attack in the same frame, selected from actual event stream.
        while ((s.ObservedAttacks<3 || s.ActiveStrikeCount==0) && guard++<300) s.AdvanceForReview(1);
        if(guard>300) throw new InvalidOperationException("Saved combat produced no review attack within 300 ticks.");
        Still("01-game-camera-overview.png");
        s.RestartShowcase();
        guard=0;
        while(s.ObservedAttacks==0 && guard++<300) s.AdvanceForReview(1);
        if(s.ObservedAttacks==0) throw new InvalidOperationException("No attack to capture.");
        Still("04-flame-attack-playmode.png");
        c.orthographicSize=2.0f;
        c.transform.position=position+s.TowerView.position+Vector3.up*1.0f;
        Still("02-flame-closeup.png",1000,1000);
        c.orthographicSize=1.25f;
        var enemy=s.FirstEnemyView;
        c.transform.position=position+enemy.position+Vector3.up*.72f;
        Still("03-shade-closeup.png",1000,1000);
        // Supplemental isolated silhouettes, same camera tilt, same models at two values.
        s.EnvironmentRoot.gameObject.SetActive(false);
        var renderers=s.GetComponentsInChildren<Renderer>(true);
        foreach(var r in renderers) r.enabled=false;
        var towerRenderers=s.TowerView.GetComponentsInChildren<Renderer>();
        foreach(var r in towerRenderers)r.enabled=true;
        c.orthographicSize=1.6f; c.transform.position=position+s.TowerView.position+Vector3.up*1.1f;
        c.backgroundColor=new Color(.86f,.88f,.84f); Still("05-flame-light-background.png",900,900);
        c.backgroundColor=new Color(.035f,.045f,.08f); Still("06-flame-dark-background.png",900,900);
        foreach(var r in towerRenderers)r.enabled=false;
        foreach(var r in enemy.GetComponentsInChildren<MeshRenderer>())r.enabled=true;
        foreach(var r in enemy.GetComponentsInChildren<Renderer>()) if(r.name.StartsWith("Health"))r.enabled=false;
        c.orthographicSize=.95f; c.transform.position=position+enemy.position+Vector3.up*.72f;
        c.backgroundColor=new Color(.86f,.88f,.84f); Still("07-shade-light-background.png",900,900);
        c.backgroundColor=new Color(.035f,.045f,.08f); Still("08-shade-dark-background.png",900,900);
        foreach(var r in renderers)r.enabled=true;
        s.EnvironmentRoot.gameObject.SetActive(true);
        c.transform.SetPositionAndRotation(position,rotation); c.orthographicSize=size; c.backgroundColor=background;
        s.RestartShowcase();
        return "8 real Play Mode captures saved; original game camera restored; showcase paused.";
    }

    public static string Video()
    {
        var s=Showcase;
        if(s==null || !EditorApplication.isPlaying) throw new InvalidOperationException("ArtReference Play Mode required.");
        if(!UnityEngine.Application.runInBackground) throw new InvalidOperationException("Enable Editor-only run in background before capture.");
        var directory="Temp/Art1Video";Directory.CreateDirectory(directory);
        foreach(var old in Directory.GetFiles(directory,"*.png"))File.Delete(old);
        s.SetPaused(true);s.RestartShowcase();s.SendMessage("OnApplicationFocus",true);s.SetPaused(false);
        var oldCapture=Time.captureFramerate; Time.captureFramerate=30;
        var count=0;var lastFrame=-1;var startFrame=Time.frameCount;var startTick=s.Simulation.State.Tick;
        EditorApplication.CallbackFunction callback=null;
        callback=()=>
        {
            if(Time.frameCount==lastFrame)return;
            lastFrame=Time.frameCount;
            try
            {
                Capture(directory+"/"+count.ToString("D4")+".png",s.WorldCamera,1280,720);
                count++;
                if(count<180)return;
                File.WriteAllText(Output+"video-capture.json",JsonUtility.ToJson(new VideoInfo {
                    frames=count,firstUnityFrame=startFrame,lastUnityFrame=Time.frameCount,
                    firstTick=startTick,lastTick=s.Simulation.State.Tick,observedAttacks=s.ObservedAttacks,playMode=EditorApplication.isPlaying },true));
                Debug.Log("ART.1 recorded 180 actual advancing Play Mode frames.");
            }
            catch(Exception e){Debug.LogException(e);}
            EditorApplication.update-=callback;Time.captureFramerate=oldCapture;s.SetPaused(true);
        };
        EditorApplication.update+=callback;
        return "Recording 180 advancing Play Mode camera frames; check video-capture.json before encoding.";
    }
    [Serializable]private sealed class VideoInfo
    {
        public int frames,firstUnityFrame,lastUnityFrame; public long firstTick,lastTick;
        public int observedAttacks;public bool playMode;
    }
}
