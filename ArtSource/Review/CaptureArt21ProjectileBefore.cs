// Historical baseline ba87f0f; execute only on that runtime checkout.
using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using Veilward.Presentation;
public static class CaptureArt21ProjectileBefore
{
    const string Output="Docs/review/ART.2a.1/projectile/";
    static string Stage=>"before";
    static ArtReferenceShowcase S=>UnityEngine.Object.FindFirstObjectByType<ArtReferenceShowcase>();
    public static string Video()
    {
        var s=S;if(!EditorApplication.isPlaying||!Application.runInBackground)throw new Exception("Advancing Play Mode/runInBackground required");
        File.Delete(Output+Stage+"-video-capture.json");
        Directory.CreateDirectory("Temp/Art21ProjectileVideo/"+Stage);foreach(var p in Directory.GetFiles("Temp/Art21ProjectileVideo/"+Stage,"*.png"))File.Delete(p);
        s.SetPaused(true);s.RestartShowcase();s.SendMessage("OnApplicationFocus",true);s.SetPaused(false);
        var old=Time.captureFramerate;Time.captureFramerate=30;int count=0,last=-1,start=Time.frameCount;long tick=s.Simulation.State.Tick;
        EditorApplication.CallbackFunction callback=null;
        callback=()=>
        {
            if(Time.frameCount==last)return;last=Time.frameCount;
            try
            {
                // Capture общим методом с временным output нельзя: видео использует отдельные PNG без still metadata.
                var c=s.WorldCamera;var rt=RenderTexture.GetTemporary(1280,720,24);var previous=c.targetTexture;var active=RenderTexture.active;
                Texture2D tex=null;
                try{c.targetTexture=rt;c.Render();RenderTexture.active=rt;tex=new Texture2D(1280,720,TextureFormat.RGB24,false);
                    tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();File.WriteAllBytes("Temp/Art21ProjectileVideo/"+Stage+"/"+count.ToString("D4")+".png",tex.EncodeToPNG());}
                finally{c.targetTexture=previous;RenderTexture.active=active;if(tex!=null)UnityEngine.Object.DestroyImmediate(tex);RenderTexture.ReleaseTemporary(rt);}
                if(++count<240)return;
                if(s.Simulation.State.Tick<=tick||s.ObservedAttacks==0)throw new Exception("Frozen capture is not evidence");
                File.WriteAllText(Output+Stage+"-video-capture.json",JsonUtility.ToJson(new VideoInfo{frames=count,firstFrame=start,lastFrame=Time.frameCount,
                    firstTick=tick,lastTick=s.Simulation.State.Tick,attacks=s.ObservedAttacks,playMode=EditorApplication.isPlaying},true));
            }
            catch(Exception e){Debug.LogException(e);}
            EditorApplication.update-=callback;Time.captureFramerate=old;s.SetPaused(true);
        };
        EditorApplication.update+=callback;return "Recording 240 actual advancing frames";
    }
    [Serializable]sealed class VideoInfo{public int frames,firstFrame,lastFrame,attacks;public long firstTick,lastTick;public bool playMode;}
}
