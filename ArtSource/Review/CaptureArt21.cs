// Editor-only evidence harness вне Assets. Probe кадры не являются настоящим combat.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Veilward.Presentation;
using Veilward.Editor;

public static class CaptureArt21
{
    const string Output="Docs/review/ART.2a.1/";
    static ArtReferenceShowcase S=>UnityEngine.Object.FindFirstObjectByType<ArtReferenceShowcase>();
    static string Stage=>File.Exists("Temp/art21-after")?"after":"before";
    static void Capture(string name,string kind)
    {
        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Real Play Mode required");
        var c=S.WorldCamera;var old=c.targetTexture;var active=RenderTexture.active;
        var rt=RenderTexture.GetTemporary(1600,900,24,RenderTextureFormat.ARGB32);Texture2D tex=null;
        try
        {
            c.targetTexture=rt;c.Render();RenderTexture.active=rt;tex=new Texture2D(1600,900,TextureFormat.RGB24,false);
            tex.ReadPixels(new Rect(0,0,1600,900),0,0);tex.Apply();
            var file=Stage+"-"+name+".png";File.WriteAllBytes(Output+file,tex.EncodeToPNG());
            File.AppendAllText(Output+"capture-log.jsonl",JsonUtility.ToJson(new Info{file=file,kind=kind,playMode=true,
                frame=Time.frameCount,tick=S.Simulation.State.Tick,attacks=S.ObservedAttacks,kills=S.Simulation.State.Kills,
                cameraPosition=c.transform.position,cameraEuler=c.transform.eulerAngles,ortho=c.orthographicSize,
                towerPosition=S.TowerView.position,towerScale=S.TowerView.localScale})+"\n");
        }
        finally{c.targetTexture=old;RenderTexture.active=active;if(tex!=null)UnityEngine.Object.DestroyImmediate(tex);RenderTexture.ReleaseTemporary(rt);}
    }
    [Serializable]sealed class Info{public string file,kind;public bool playMode;public int frame;public long tick;public int attacks,kills;
        public Vector3 cameraPosition,cameraEuler,towerPosition,towerScale;public float ortho;}
    static void CombatTick()
    {
        S.SetPaused(true);S.RestartShowcase();int guard=0;
        while((S.ObservedAttacks<3||S.ActiveStrikeCount==0)&&guard++<300)S.AdvanceForReview(1);
        if(guard>300)throw new Exception("No actual combat attack");
    }
    public static string Stills()
    {
        Directory.CreateDirectory(Output);CombatTick();Capture("01-island-game-camera","actual saved combat");
        Capture("02-combat","actual saved combat; HP/impact from public simulation");
        var s=S;var c=s.WorldCamera;var pos=c.transform.position;var rot=c.transform.rotation;
        var size=c.orthographicSize;var bg=c.backgroundColor;
        var pool=s.GetComponentsInChildren<Renderer>(true);var enabled=pool.Select(r=>r.enabled).ToArray();
        Transform probe=null;
        try
        {
            foreach(var r in pool)r.enabled=false;
            foreach(var r in s.TowerView.GetComponentsInChildren<Renderer>())r.enabled=true;
            probe=UnityEngine.Object.Instantiate(ArtReferenceBuilder.Prefab("Enemy_Shade")).transform;
            AddOcclusion(probe);
            var near=new Vector3(pos.x,0,pos.z).normalized;var right=new Vector3(near.z,0,-near.x);
            foreach(var entry in new[]{("front",near*.8f),("side",right*.85f),("behind",-near*.8f)})
            {
                probe.position=s.TowerView.position+entry.Item2;probe.position=new Vector3(probe.position.x,.14f,probe.position.z);
                probe.rotation=Quaternion.LookRotation(Vector3.right);probe.GetComponent<EnemyHealthBarView>().SetNormalizedHealth(.65f);
                probe.GetComponent<EnemyHealthBarView>().Face(c);
                Capture("03-flame-"+entry.Item1,"static occlusion probe; original camera/tower; synthetic enemy placement");
                File.AppendAllText(Output+"probe-positions.jsonl",JsonUtility.ToJson(new ProbeInfo{stage=Stage,label=entry.Item1,enemy=probe.position,tower=s.TowerView.position})+"\n");
            }
            UnityEngine.Object.DestroyImmediate(probe.gameObject);probe=null;
            s.EnvironmentRoot.gameObject.SetActive(false);foreach(var r in pool)r.enabled=false;
            probe=UnityEngine.Object.Instantiate(ArtReferenceBuilder.Prefab("Enemy_Shade")).transform;
            AddOcclusion(probe);
            probe.position=new Vector3(0,.14f,0);probe.rotation=Quaternion.LookRotation(Vector3.right);
            probe.Find("Health Bar").gameObject.SetActive(false);
            c.backgroundColor=new Color(.86f,.88f,.84f);Capture("04-shade-light-game-scale","isolated static probe; original game camera/scale");
            c.backgroundColor=new Color(.035f,.045f,.08f);Capture("05-shade-dark-game-scale","isolated static probe; original game camera/scale");
            c.orthographicSize=1.25f;c.transform.position=pos+probe.position+Vector3.up*.72f;
            Capture("06-shade-dark-closeup","supplemental closeup; NOT game-scale evidence");
            c.backgroundColor=new Color(.86f,.88f,.84f);Capture("07-shade-light-closeup","supplemental closeup; NOT game-scale evidence");
            c.transform.SetPositionAndRotation(pos,rot);c.orthographicSize=size;c.backgroundColor=bg;
            UnityEngine.Object.DestroyImmediate(probe.gameObject);probe=null;
            s.EnvironmentRoot.gameObject.SetActive(true);
            var group=new GameObject("Synthetic dense Shade group");
            var previousTarget=c.targetTexture;var probeTarget=RenderTexture.GetTemporary(1600,900,24);c.targetTexture=probeTarget;
            try
            {
                for(int j=0;j<3;j++)for(int i=0;i<4;i++)
                {
                    var e=UnityEngine.Object.Instantiate(ArtReferenceBuilder.Prefab("Enemy_Shade"),group.transform).transform;
                    e.position=new Vector3(-.7f+i*.48f,.14f,-.45f+j*.48f);e.rotation=Quaternion.LookRotation(Vector3.right);
                    AddOcclusion(e);
                    e.GetComponent<EnemyHealthBarView>().SetNormalizedHealth(.5f+i*.1f);e.GetComponent<EnemyHealthBarView>().Face(c);
                }
                var views=group.GetComponentsInChildren<EnemyHealthBarView>();
                var rects=new List<Rect>(12);var beforeRects=new List<Rect>(12);
                foreach(var v in views)beforeRects.Add(v.ScreenBounds(c));
                if(Stage=="after")foreach(var v in views)v.Layout(c,rects);
                else rects.AddRange(beforeRects);
                var originalCollisions=0;for(var i=0;i<beforeRects.Count;i++)for(var j=0;j<i;j++)if(beforeRects[i].Overlaps(beforeRects[j]))originalCollisions++;
                File.WriteAllText(Output+"hp-baseline-measurement.json","{\"method\":\"Current unchanged body/fill geometry with layout disabled; synthetic12/.48m; 1600x900\",\"overlappingPairs\":"+originalCollisions+"}");
                var collisions=0;for(var i=0;i<rects.Count;i++)for(var j=0;j<i;j++)if(rects[i].Overlaps(rects[j]))collisions++;
                File.WriteAllText(Output+Stage+"-hp-layout.json", "{\"kind\":\"synthetic12/.48m\",\"overlappingPairs\":"+collisions+",\"rows\":["+
                    string.Join(",",views.Select(v=>JsonUtility.ToJson(new HpInfo{ratio=v.NormalizedHealth,anchor=v.AnchorPosition,bar=v.BarPosition,rect=v.ScreenBounds(c)})))+"]}");
                Capture("08-shade-dense-game-scale","synthetic 12-enemy group, .48m spacing; original camera/scale");
            }
            finally{c.targetTexture=previousTarget;RenderTexture.ReleaseTemporary(probeTarget);UnityEngine.Object.DestroyImmediate(group);}
        }
        finally
        {
            if(probe!=null)UnityEngine.Object.DestroyImmediate(probe.gameObject);
            for(int i=0;i<pool.Length;i++)pool[i].enabled=enabled[i];
            s.EnvironmentRoot.gameObject.SetActive(true);c.transform.SetPositionAndRotation(pos,rot);c.orthographicSize=size;c.backgroundColor=bg;
            s.RestartShowcase();
        }
        return Stage+": 10 real Play Mode captures; static probes explicitly separated from combat";
    }
    public static string DenseImpact()
    {
        CombatTick();
        var strikes=(FlameStrikeView[])typeof(ArtReferenceShowcase).GetField("_strikes",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(S);
        var hit=strikes.First(v=>v.Remaining>0).Target;
        var group=new GameObject("Synthetic dense impact probe");
        var rects=new List<Rect>(12);
        var c=S.WorldCamera;var previous=c.targetTexture;var rt=RenderTexture.GetTemporary(1600,900,24);c.targetTexture=rt;
        try
        {
            for(var j=0;j<3;j++)for(var i=0;i<4;i++)
            {
                var e=UnityEngine.Object.Instantiate(ArtReferenceBuilder.Prefab("Enemy_Shade"),group.transform).transform;
                e.position=new Vector3(hit.x+(i-1.5f)*.48f,.14f,hit.z+(j-1)*.48f);e.rotation=Quaternion.LookRotation(Vector3.right);
                AddOcclusion(e);var h=e.GetComponent<EnemyHealthBarView>();h.SetNormalizedHealth(.5f+i*.1f);h.Face(c);h.Layout(c,rects);
            }
            for(var frame=0;frame<4;frame++)
            {
                if(frame>0)S.AdvanceForReview(2);
                foreach(var h in S.GetComponentsInChildren<EnemyHealthBarView>(true))h.gameObject.SetActive(false);
                Capture("dense-impact-"+frame,"SYNTHETIC12/.48m group and assigned HP around captured real AttackEvent target; actual VFX advancing2ticks/frame; NOT dense combat");
            }
        }
        finally{UnityEngine.Object.DestroyImmediate(group);c.targetTexture=previous;RenderTexture.ReleaseTemporary(rt);S.RestartShowcase();}
        return "4 synthetic dense frames with genuine event-driven effect; not a dense combat claim";
    }
    public static string Phases()
    {
        CombatTick();Capture("strike-00-onset","actual saved combat; deterministic stepping from AttackEvent");
        S.AdvanceForReview(2);Capture("strike-01-impact","actual saved combat; +2ticks, trail expired, local impact");
        S.AdvanceForReview(3);Capture("strike-02-decay","actual saved combat; +5ticks, embers shrinking");
        S.AdvanceForReview(2);Capture("strike-03-expired","actual saved combat; +7ticks, no residual effect");
        S.RestartShowcase();return "4 real simulation phase captures";
    }
    static void AddOcclusion(Transform t)
    {
        if(Stage=="after")ShadeOcclusionView.Create(t,AssetDatabase.LoadAssetAtPath<Material>(ArtReferenceBuilder.MaterialFolder+"MAT_Art21_ShadeOcclusion.mat"));
    }
    [Serializable]sealed class HpInfo{public float ratio;public Vector3 anchor,bar;public Rect rect;}
    [Serializable]sealed class ProbeInfo{public string stage,label;public Vector3 enemy,tower;}
    public static string Video()
    {
        var s=S;if(!EditorApplication.isPlaying||!Application.runInBackground)throw new Exception("Advancing Play Mode/runInBackground required");
        File.Delete(Output+Stage+"-video-capture.json");
        Directory.CreateDirectory("Temp/Art21Video/"+Stage);foreach(var p in Directory.GetFiles("Temp/Art21Video/"+Stage,"*.png"))File.Delete(p);
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
                    tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();File.WriteAllBytes("Temp/Art21Video/"+Stage+"/"+count.ToString("D4")+".png",tex.EncodeToPNG());}
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
