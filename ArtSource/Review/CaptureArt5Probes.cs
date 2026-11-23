// Synthetic assigned positions/HP + real copied Wet/Gust snapshot; production camera, no foreign renderer hiding.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Veilward.Domain;
using Veilward.Presentation;
using Veilward.Editor;
public static class CaptureArt5Probes
{
    const string Output="Docs/review/ART.5/";
    static BattleSimulation StatusProbe()
    {
        var s=AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/TestWave.asset").CreateSimulation();
        s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId("tide"),new GridCoordinate(-4,0)));
        s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId("gale"),new GridCoordinate(-3,0)));s.StartWave();
        for(var i=0;i<600&&!s.State.Enemies.Any(e=>e.Wet!=null&&e.Gust!=null);i++){s.Step();s.ClearAttackEvents();s.ClearDamageEvents();s.ClearReactionEvents();}
        if(!s.State.Enemies.Any(e=>e.Wet!=null&&e.Gust!=null))throw new Exception("Actual copied Wet/Gust snapshot required");return s;
    }
    public static string Capture()
    {
        if(!EditorApplication.isPlaying)throw new Exception("Actual Play Mode required");
        var battle=UnityEngine.Object.FindFirstObjectByType<BattleSceneController>();battle.GetComponent<BattlePauseController>().Pause();
        var previous=GameObject.Find("ART5 synthetic probes");if(previous!=null)UnityEngine.Object.DestroyImmediate(previous);
        var stage=new GameObject("ART5 synthetic probes");var camera=Camera.main;var probe=StatusProbe();var state=probe.State.Enemies.First(e=>e.Wet!=null&&e.Gust!=null);
        var async=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;var receipts=new List<object>();
        GameObject Enemy(bool boss,Vector3 position)
        {
            var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(boss?EnemyModelBuilder.BossPath:EnemyModelBuilder.ShadePath),stage.transform);
            go.transform.position=position;var visual=go.GetComponentInChildren<EnemyModelView>();visual.Synchronize(Vector3.right,true);visual.Advance(.23f);
            go.GetComponent<EnemyHealthBarView>().SetNormalizedHealth(.65f);go.GetComponent<EnemyHealthBarView>().Face(camera);
            go.GetComponent<EnemyStatusView>().Refresh(state,probe.State.Tick,probe.ReactionCatalog,null,0,camera);return go;
        }
        void Label(string text)
        {
            var go=new GameObject("Probe label");go.transform.SetParent(stage.transform,false);go.transform.position=camera.ViewportToWorldPoint(new Vector3(.5f,.94f,10));go.transform.rotation=camera.transform.rotation;
            var t=go.AddComponent<TextMesh>();t.text="SYNTHETIC ART5 | "+text+" | original camera | assigned HP/positions + copied Wet/Gust";t.fontSize=48;t.characterSize=.055f;t.anchor=TextAnchor.MiddleCenter;
        }
        void Clear(){foreach(Transform child in stage.transform.Cast<Transform>().ToArray())UnityEngine.Object.DestroyImmediate(child.gameObject);}
        void Shot(string file,string label){Label(label);Pixels(file);receipts.Add(new{file,synthetic=true,frame=Time.frameCount,tick=battle.Simulation.State.Tick,label});}
        try
        {
            foreach(var boss in new[]{false,true})
            {
                var type=boss?"boss":"shade";var go=Enemy(boss,new Vector3(-1,.6f,-1));Shot("single-"+type+".png",type+" single / authored meter dimensions");Clear();
                foreach(var pose in new[]{"move","turn","hit","death","leak"})
                {
                    go=Enemy(boss,new Vector3(-1,.6f,-1));var v=go.GetComponentInChildren<EnemyModelView>();
                    if(pose=="turn")v.Synchronize(Vector3.forward,true);
                    if(pose=="hit")v.Hit();
                    if(pose=="death"||pose=="leak")v.BeginTail(pose=="death",stage.transform);
                    v.Advance(.2f);Shot(type+"-"+pose+".png",type+" sampled "+pose+" / active time .2");Clear();
                }
            }
            Enemy(false,new Vector3(-1.5f,.6f,-1));Enemy(true,new Vector3(.2f,.6f,-1));Shot("pair.png","Shade + Rift Warden silhouette comparison");Clear();
            var families=new[]{"FLAME","TIDE","GALE","STONE","STEAM","STORM"};var names=new[]{"FlameLantern","TideBell","GaleTalisman","StoneSeal","SteamFox","StormCarp"};
            for(var i=0;i<6;i++)foreach(var boss in new[]{false,true})
            {
                var folder="Assets/_Game/Prefabs/ART3/"+(i==0?"":families[i]+"/");var tower=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(folder+"P_Tower_"+names[i]+"L3.prefab"),stage.transform);
                tower.transform.position=new Vector3(-1,.04f,-1);var direction=camera.transform.position-tower.transform.position;direction.y=0;direction.Normalize();
                var go=Enemy(boss,new Vector3(-1,.6f,-1)-direction*.62f);go.GetComponentInChildren<EnemyModelView>().Advance(.2f);
                Shot("behind-"+families[i]+"-"+(boss?"boss":"shade")+".png",(boss?"boss":"shade")+" behind "+families[i]+" L3 / animated multipart stencil");Clear();
            }
            foreach(var propName in new[]{"Spawn Entrance","Seal Heart"})foreach(var boss in new[]{false,true})
            {
                var prop=GameObject.Find(propName);var direction=camera.transform.position-prop.transform.position;direction.y=0;direction.Normalize();
                var position=prop.transform.position-direction*.55f;position.y=.6f;Enemy(boss,position);
                Shot("behind-"+propName.Replace(" ","-")+"-"+(boss?"boss":"shade")+".png",(boss?"boss":"shade")+" behind "+propName);Clear();
            }
            for(var i=0;i<6;i++)
            {
                var folder="Assets/_Game/Prefabs/ART3/"+(i==0?"":families[i]+"/");var tower=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(folder+"P_Tower_"+names[i]+"L3.prefab"),stage.transform);tower.transform.position=new Vector3(-3+i%3*2.5f,.04f,1+i/3*2);
            }
            var healthRects=new List<Rect>();var statusRects=new List<Rect>();
            for(var i=0;i<12;i++)
            {
                var go=Enemy(i==6,new Vector3(-3+(i%4)*1.1f,.6f,-2+(i/4)*.72f));
                go.GetComponent<EnemyStatusView>().Layout(camera,statusRects);go.GetComponent<EnemyHealthBarView>().Layout(camera,healthRects);
            }
            Shot("dense-six-families.png","12enemies / two status slots / all6tower families");
            var actual=new{synthetic=true,assignedPositionsAndHealth=true,statusSnapshot="actual separate TestWave publicTIDE+GALE Wet/Gust",camera=new{position=camera.transform.position.ToString("R"),rotation=camera.transform.rotation.ToString("R"),camera.orthographicSize,aspect=1600f/900},width=1600,height=900,foreignRendererHiding=false,receipts};
            File.WriteAllText(Output+"reports/synthetic-probes.json",Newtonsoft.Json.JsonConvert.SerializeObject(actual,Newtonsoft.Json.Formatting.Indented)+"\n");
        }
        finally{UnityEngine.Object.DestroyImmediate(stage);ShaderUtil.allowAsyncCompilation=async;}
        return "Captured30 original camera PlayMode probes: singles/pose/pair/6towers/gate/heart/dense; synthetic explicitly labelled";
    }
    public static void Pixels(string file)
    {
        Directory.CreateDirectory(Output+"playmode");var camera=Camera.main;var old=camera.targetTexture;var active=RenderTexture.active;
        var rt=RenderTexture.GetTemporary(1600,900,24);Texture2D texture=null;
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture=new Texture2D(1600,900,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1600,900),0,0);texture.Apply();File.WriteAllBytes(Output+"playmode/"+file,texture.EncodeToPNG());}
        finally{camera.targetTexture=old;RenderTexture.active=active;if(texture!=null)UnityEngine.Object.DestroyImmediate(texture);RenderTexture.ReleaseTemporary(rt);}
    }
}
