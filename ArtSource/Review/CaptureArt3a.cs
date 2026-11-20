// Подписанные synthetic model probes в настоящем Play Mode. Не меняет Domain/DTO.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Veilward.Presentation;
using Veilward.Application;
using Veilward.Infrastructure;

public static class CaptureArt3a
{
    private const string Output = "Docs/review/ART.3a/playmode/";
    public static string PrepareMotion()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode required.");
        var store=new WaveCheckpointStore(Path.GetFullPath("TestResults/ART3A/MotionStore"));
        Directory.CreateDirectory(Path.GetDirectoryName(store.PrimaryPath));
        File.Copy("TestResults/ART3A/natural-L3-checkpoint.json",store.PrimaryPath,true);
        var config=AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWaveAbilityBossV2.asset");
        if(!store.TryLoad(config.ScenarioContentId,config.CreateSimulationForCheckpoint,out var loaded))
            throw new InvalidOperationException(loaded.Error);
        BattleSessionContext.SetContinue(store,loaded.Envelope);
        UnityEngine.SceneManagement.SceneManager.LoadScene("Battle");
        return "Loading unmodified naturally earned L3 checkpoint into isolated store.";
    }
    public static string StartMotion()
    {
        var battle=UnityEngine.Object.FindFirstObjectByType<BattleSceneController>();
        if (!EditorApplication.isPlaying || battle.Simulation.State.Towers.Single().Level!=3 || battle.IsPaused)
            throw new InvalidOperationException("Ready unpaused natural L3 session required.");
        var stage=new GameObject("ART3A real-time label");
        Text(stage.transform,Camera.main,"REAL-TIME BATTLE  |  naturally earned L3 / original camera / generic beam #47  |  Editor, not device",Vector3.zero,.06f);
        if (battle.StartWave()!=Veilward.Domain.WaveTransitionResult.Started)throw new InvalidOperationException("Start rejected.");
        var rows=new List<string>();var started=Time.realtimeSinceStartupAsDouble;var next=started;var count=0;var frame=-1;
        EditorApplication.CallbackFunction callback=null;
        callback=()=>
        {
            if (!EditorApplication.isPlaying || battle==null) {EditorApplication.update-=callback;return;}
            var now=Time.realtimeSinceStartupAsDouble;
            if (now<next || Time.frameCount==frame)return;
            frame=Time.frameCount;
            try
            {
                rows.Add($"capture={count}|elapsed={now-started:R}|frame={frame}|tick={battle.Simulation.State.Tick}|enemies={battle.Simulation.State.Enemies.Count}|kills={battle.Simulation.State.Kills}|gold={battle.Simulation.State.Gold}|beams={battle.GetComponentsInChildren<AttackBeamView>().Length}");
                Pixels(Camera.main,"motion-"+count.ToString("00")+".png");count++;next=now+.7;
                if(count<12)return;
                File.WriteAllLines(Output+"motion-telemetry.txt",rows);
                UnityEngine.Object.Destroy(stage);EditorApplication.update-=callback;
            }
            catch(Exception error)
            {EditorApplication.update-=callback;UnityEngine.Object.Destroy(stage);Debug.LogException(error);}
        };
        EditorApplication.update+=callback;
        return "Capturing 12 advancing real-time frames over >=7.7s; capture overhead is not performance measurement.";
    }
    public static string Capture()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Actual Play Mode required.");
        var battle = UnityEngine.Object.FindFirstObjectByType<BattleSceneController>();
        battle.GetComponent<BattlePauseController>().Pause();
        if (battle.Simulation.State.Towers.Count != 0) throw new InvalidOperationException("Use clean direct Battle.");
        var camera = Camera.main;
        var asyncCompilation = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
        var stage = new GameObject("ART3A Synthetic Model Probes");
        try
        {
            for (var level = 1; level <= 3; level++)
            {
                var model = Model(level, new Vector3(-1, .04f, -1), stage.transform);
                Text(stage.transform, camera, "SYNTHETIC MODEL PROBE  |  FLAME L"+level+"  |  same Battle camera / scale1 / lighting", new Vector3(0, 3.7f, 0), .15f);
                Pixels(camera, "single-L"+level+".png");
                Clear(stage.transform);
            }
            for (var level = 1; level <= 3; level++)
            {
                var point = new Vector3(-3+(level-1)*2, .04f, -1);
                Model(level, point, stage.transform);
                Text(stage.transform, camera, "L"+level, point+new Vector3(0,-.05f,.65f), .2f);
            }
            Text(stage.transform, camera, "SYNTHETIC LINEUP  |  L1 / L2 / L3  |  same Battle camera / scale1 / lighting", new Vector3(0,3.7f,0), .15f);
            Pixels(camera, "lineup.png"); Clear(stage.transform);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Shade.prefab");
            var shaderAsync = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
            try
            {
                for (var level = 1; level <= 3; level++)
                {
                    var position = new Vector3(-1,.04f,-1);
                    Model(level,position,stage.transform);
                    var direction = camera.transform.position-position; direction.y=0; direction.Normalize();
                    var offsets = new[] { direction*.75f, camera.transform.right*.75f, -direction*.75f };
                    var names = new[] { "front", "side", "behind" };
                    for (var i=0;i<3;i++)
                    {
                        var enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                        enemy.name="Synthetic graybox enemy "+names[i]; enemy.transform.SetParent(stage.transform);
                        enemy.transform.position=position+offsets[i]+Vector3.up*.56f;
                        var hp=enemy.GetComponent<EnemyHealthBarView>();hp.SetNormalizedHealth(.6f);hp.Face(camera);
                        Text(stage.transform,camera,"SYNTHETIC OCCLUSION  |  FLAME L"+level+" / "+names[i]+" / graybox enemy / HP .6  |  original camera",new Vector3(0,3.7f,0),.15f);
                        Pixels(camera,"occlusion-L"+level+"-"+names[i]+".png");
                        UnityEngine.Object.DestroyImmediate(enemy);
                        foreach(var label in stage.transform.Cast<Transform>().Where(t=>t.name.StartsWith("SYNTHETIC")).ToArray())UnityEngine.Object.DestroyImmediate(label.gameObject);
                    }
                    Clear(stage.transform);
                }
            }
            finally { ShaderUtil.allowAsyncCompilation=shaderAsync; }
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output+"settings.json",JsonUtility.ToJson(new Settings { cameraPosition=camera.transform.position,
                cameraEuler=camera.transform.eulerAngles,ortho=camera.orthographicSize,width=1600,height=900,
                pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name,quality=QualitySettings.names[QualitySettings.GetQualityLevel()],
                ambient=RenderSettings.ambientLight,sunIntensity=RenderSettings.sun!=null?RenderSettings.sun.intensity:0,
                synthetic=true,frame=Time.frameCount,tick=battle.Simulation.State.Tick },true)+"\n");
        }
        finally { UnityEngine.Object.DestroyImmediate(stage); ShaderUtil.allowAsyncCompilation=asyncCompilation; }
        return "13 actual Play Mode synthetic camera captures; no Domain mutation; no device evidence.";
    }
    private static GameObject Model(int level,Vector3 position,Transform parent)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/ART3/P_Tower_FlameLanternL"+level+".prefab");
        var model=(GameObject)PrefabUtility.InstantiatePrefab(prefab);model.transform.SetParent(parent);model.transform.position=position;return model;
    }
    private static void Text(Transform parent,Camera camera,string value,Vector3 position,float size)
    {
        if(value.StartsWith("SYNTHETIC")||value.StartsWith("REAL-TIME")) {position=camera.ViewportToWorldPoint(new Vector3(.5f,.94f,10));size=.065f;}
        else size=.07f;
        var go=new GameObject(value);go.transform.SetParent(parent);go.transform.position=position;go.transform.rotation=camera.transform.rotation;
        var text=go.AddComponent<TextMesh>();text.text=value;text.fontSize=48;text.characterSize=size;text.anchor=TextAnchor.MiddleCenter;text.color=Color.white;
    }
    private static void Clear(Transform stage)
    { foreach(var child in stage.Cast<Transform>().ToArray())UnityEngine.Object.DestroyImmediate(child.gameObject); }
    private static void Pixels(Camera camera,string file)
    {
        Directory.CreateDirectory(Output);
        var target=camera.targetTexture;var active=RenderTexture.active;
        var rt=RenderTexture.GetTemporary(1600,900,24);Texture2D texture=null;
        try
        {
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            texture=new Texture2D(1600,900,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1600,900),0,0);texture.Apply();
            File.WriteAllBytes(Output+file,texture.EncodeToPNG());
        }
        finally
        {camera.targetTexture=target;RenderTexture.active=active;if(texture!=null)UnityEngine.Object.DestroyImmediate(texture);RenderTexture.ReleaseTemporary(rt);}
    }
    [Serializable] private sealed class Settings
    {public Vector3 cameraPosition,cameraEuler;public float ortho,sunIntensity;public int width,height,frame;public long tick;public string pipeline,quality;public Color ambient;public bool synthetic;}
}
