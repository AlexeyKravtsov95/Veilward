// Editor-only evidence: actual combat и synthetic dense probe явно разделены.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Veilward.Domain;
using Veilward.Editor;
using Veilward.Presentation;

public static class CaptureArt21Projectile
{
    const string Output = "Docs/review/ART.2a.1/projectile/";
    static ArtReferenceShowcase S => UnityEngine.Object.FindFirstObjectByType<ArtReferenceShowcase>();
    [Serializable] public sealed class Frame
    {
        public string file, kind, phase;
        public bool playMode, lethal, targetVisible;
        public int frame, attacks, targetId, projectileTargetId, liveHp, shownHp, kills, gold;
        public long tick, presentedTick;
        public Vector3 head, source, target, tailEnd, cameraPosition, cameraEuler;
        public float remaining, ortho;
    }
    [Serializable] sealed class VideoInfo
    { public int frames,firstFrame,lastFrame,attacks;public long firstTick,lastTick;public bool playMode; }
    static Frame Info(string file, string kind)
    {
        var s=S;var id=s.LastAttack.TargetEnemyId;
        FlameStrikeView v=s.ObservedAttacks>0?s.GetStrike((s.ObservedAttacks-1)%ArtReferenceShowcase.StrikeCapacity):null;
        var hp=-1;foreach(var e in s.Simulation.State.Enemies)if(e.Id==id)hp=e.Health;
        return new Frame{file=file,kind=kind,playMode=EditorApplication.isPlaying,frame=Time.frameCount,
            tick=s.Simulation.State.Tick,presentedTick=s.PresentedTick,attacks=s.ObservedAttacks,
            targetId=id.Value,projectileTargetId=v==null?0:v.TargetEnemyId.Value,lethal=s.LastAttack.WasLethal,liveHp=hp,shownHp=s.DisplayedHealth(id)??-1,
            targetVisible=s.FindEnemyView(id)!=null,kills=s.Simulation.State.Kills,gold=s.Simulation.State.Gold,
            phase=v==null||v.Remaining<=0?"inactive":v.IsFlying?"flight":"impact",
            head=v==null?Vector3.zero:v.HeadPosition,tailEnd=v==null?Vector3.zero:v.TailEnd,
            source=v==null?Vector3.zero:v.Source,target=v==null?Vector3.zero:v.Target,
            remaining=v==null?0:v.Remaining,cameraPosition=s.WorldCamera.transform.position,
            cameraEuler=s.WorldCamera.transform.eulerAngles,ortho=s.WorldCamera.orthographicSize};
    }
    static void Pixels(Camera c, string path, int width, int height)
    {
        var previous=c.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(width,height,24);Texture2D tex=null;
        try{c.targetTexture=rt;c.Render();RenderTexture.active=rt;tex=new Texture2D(width,height,TextureFormat.RGB24,false);
            tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());}
        finally{c.targetTexture=previous;RenderTexture.active=active;if(tex!=null)UnityEngine.Object.DestroyImmediate(tex);RenderTexture.ReleaseTemporary(rt);}
    }
    static void Capture(string name,string kind)
    {
        if(!EditorApplication.isPlaying)throw new Exception("Actual Play Mode required");
        Pixels(S.WorldCamera,Output+name+".png",1600,900);
        File.AppendAllText(Output+"phase-telemetry.jsonl",JsonUtility.ToJson(Info(name+".png",kind))+"\n");
    }
    static void Seek(bool lethal)
    {
        S.SetPaused(true);S.RestartShowcase();var previous=0;
        for(var i=0;i<500;i++)
        {
            S.AdvanceForReview(1);
            if(S.ObservedAttacks!=previous&&S.LastAttack.WasLethal==lethal)return;
            previous=S.ObservedAttacks;
        }
        throw new Exception("Required actual attack not observed");
    }
    public static string Phases()
    {
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"phase-telemetry.jsonl","");
        foreach(var lethal in new[]{false,true})
        {
            Seek(lethal);var prefix=lethal?"lethal":"nonlethal";
            Capture(prefix+"-00-onset","actual saved combat / launch");
            S.AdvanceForReview(3);Capture(prefix+"-01-mid-flight","actual saved combat / +3ticks");
            S.AdvanceForReview(2);Capture(prefix+"-02-pre-impact","actual saved combat / +5ticks; living target retained");
            S.AdvanceForReview(1);Capture(prefix+"-03-impact","actual saved combat / +6ticks; authoritative hit/death snapshot now visible");
            S.AdvanceForReview(2);Capture(prefix+"-04-decay","actual saved combat / +8ticks");
            S.AdvanceForReview(3);Capture(prefix+"-05-expired","actual saved combat / +11ticks");
        }
        S.RestartShowcase();return "12 real simulation phase frames; lethal/nonlethal separated";
    }
    public static string Dense()
    {
        Seek(true);var hit=S.GetStrike((S.ObservedAttacks-1)%ArtReferenceShowcase.StrikeCapacity).Target;
        var group=new GameObject("Synthetic 12 Shade projectile probe");var rects=new List<Rect>(12);
        var c=S.WorldCamera;var previous=c.targetTexture;var rt=RenderTexture.GetTemporary(1600,900,24);c.targetTexture=rt;
        try
        {
            for(var j=0;j<3;j++)for(var i=0;i<4;i++)
            {
                var e=UnityEngine.Object.Instantiate(ArtReferenceBuilder.Prefab("Enemy_Shade"),group.transform).transform;
                e.position=new Vector3(hit.x+(i-1.5f)*.48f,.14f,hit.z+(j-1)*.48f);e.rotation=Quaternion.LookRotation(Vector3.right);
                ShadeOcclusionView.Create(e,AssetDatabase.LoadAssetAtPath<Material>(ArtReferenceBuilder.MaterialFolder+"MAT_Art21_ShadeOcclusion.mat"));
                var h=e.GetComponent<EnemyHealthBarView>();S.ConfigureHealthView(h);h.SetNormalizedHealth(.5f+i*.1f);h.Face(c);h.Layout(c,rects);
            }
            for(var frame=0;frame<6;frame++)
            {
                if(frame>0)S.AdvanceForReview(frame==1?3:frame==2?2:frame==3?1:frame==4?2:3);
                foreach(var h in S.GetComponentsInChildren<EnemyHealthBarView>(true))h.gameObject.SetActive(false);
                Capture("dense-"+frame,"SYNTHETIC12/.48m / assigned HP; real event-driven flight/impact; NOT dense simulation");
            }
        }
        finally{UnityEngine.Object.DestroyImmediate(group);c.targetTexture=previous;RenderTexture.ReleaseTemporary(rt);S.RestartShowcase();}
        return "6 clearly synthetic dense phase captures";
    }
    public static string Video()
    {
        if(!EditorApplication.isPlaying||!Application.runInBackground)throw new Exception("Advancing Play Mode required");
        var folder="Temp/Art21ProjectileVideo/after";Directory.CreateDirectory(folder);
        foreach(var p in Directory.GetFiles(folder,"*.png"))File.Delete(p);
        var s=S;s.SetPaused(true);s.RestartShowcase();s.SendMessage("OnApplicationFocus",true);s.SetPaused(false);
        var old=Time.captureFramerate;Time.captureFramerate=30;var count=0;var last=-1;var start=Time.frameCount;var tick=s.Simulation.State.Tick;
        File.WriteAllText(Output+"video-frames.jsonl", "");
        EditorApplication.CallbackFunction callback=null;
        callback=()=>
        {
            if(Time.frameCount==last)return;last=Time.frameCount;
            try
            {
                Pixels(s.WorldCamera,folder+"/"+count.ToString("D4")+".png",1280,720);
                File.AppendAllText(Output+"video-frames.jsonl", JsonUtility.ToJson(Info(count.ToString("D4")+".png","actual advancing combat; no synthetic placements"))+"\n");
                if(++count<240)return;
                if(s.Simulation.State.Tick<=tick||s.ObservedAttacks==0)throw new Exception("Frozen recording");
                File.WriteAllText(Output+"after-video-capture.json",JsonUtility.ToJson(new VideoInfo{frames=count,firstFrame=start,lastFrame=Time.frameCount,
                    firstTick=tick,lastTick=s.Simulation.State.Tick,attacks=s.ObservedAttacks,playMode=true},true));
            }
            catch(Exception e){Debug.LogException(e);}
            EditorApplication.update-=callback;Time.captureFramerate=old;s.SetPaused(true);
        };
        EditorApplication.update+=callback;return "Recording 240 advancing frames with per-frame ID/head/HP/snapshot telemetry";
    }
}
