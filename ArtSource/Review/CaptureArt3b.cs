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
using Veilward.Domain;

public static class CaptureArt3b
{
    private const string Output = "Docs/review/ART.3b/playmode/";
    public static string LoadPreview()
    {
        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Play Mode required");
        var store=new WaveCheckpointStore(Path.GetFullPath("TestResults/ART3B/PreviewStore"));
        Directory.CreateDirectory(Path.GetDirectoryName(store.PrimaryPath));
        File.Copy("TestResults/ART3B/natural-L3-checkpoint.json",store.PrimaryPath,true);
        var config=AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWaveAbilityBossV2.asset");
        if(!store.TryLoad(config.ScenarioContentId,config.CreateSimulationForCheckpoint,out var loaded))throw new InvalidOperationException(loaded.Error);
        BattleSessionContext.SetContinue(store,loaded.Envelope);
        UnityEngine.SceneManagement.SceneManager.LoadScene("Battle");
        return "Loading unchanged earned L3 checkpoint into isolated PreviewStore";
    }
    public static object SelectPreview()
    {
        var battle=UnityEngine.Object.FindFirstObjectByType<BattleSceneController>();
        var gold=battle.Simulation.State.Gold;var towers=battle.Simulation.State.Towers.Count;
        var placement=battle.GetComponent<BattlePlacementController>();
        if(!placement.TrySelectCell(new GridCoordinate(4,1)))throw new InvalidOperationException("Preview cell rejected");
        placement.SelectTowerType(new TowerDefinitionId("tide"));
        if(battle.Simulation.State.Gold!=gold||battle.Simulation.State.Towers.Count!=towers)throw new InvalidOperationException("Preview mutated economy");
        var canvas=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).First(c=>c.renderMode==RenderMode.ScreenSpaceOverlay);
        var go=new GameObject("ART3B preview label",typeof(RectTransform),typeof(UnityEngine.UI.Text));go.transform.SetParent(canvas.transform,false);
        var text=go.GetComponent<UnityEngine.UI.Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=16;
        text.text="EDITOR | unchanged earned checkpoint replay | public TIDE catalog + preview | no purchase | Device Deferred";
        text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;var rect=text.rectTransform;
        rect.anchorMin=new Vector2(0,0);rect.anchorMax=new Vector2(1,0);rect.pivot=new Vector2(.5f,0);rect.anchoredPosition=new Vector2(0,2);rect.sizeDelta=new Vector2(0,30);
        return new{gold,towers,cell=placement.SelectedCell.ToString(),type=placement.SelectedTowerTypeId.Value,stats=placement.BuildStatsText.text};
    }
    public static string Capture()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Actual Play Mode required.");
        var battle = UnityEngine.Object.FindFirstObjectByType<BattleSceneController>();
        battle.GetComponent<BattlePauseController>().Pause();
        if (battle.Simulation.State.Towers.Count != 0) throw new InvalidOperationException("Use clean direct Battle.");
        var camera = Camera.main;
        var asyncCompilation = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
        var stage = new GameObject("ART3B Synthetic Model Probes");
        var probe = WetProbe();
        var wet = probe.State.Enemies.First(e=>e.Wet!=null);
        try
        {
            for (var level = 1; level <= 3; level++)
            {
                var model = Model(level, new Vector3(-1, .04f, -1), stage.transform);
                Text(stage.transform, camera, "SYNTHETIC MODEL PROBE  |  TIDE L"+level+"  |  same Battle camera / scale1 / lighting", new Vector3(0, 3.7f, 0), .15f);
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
            for (var level = 1; level <= 3; level++)
            {
                var point = new Vector3(-3+(level-1)*2, .04f, -2);
                Model(level, point, stage.transform);
                var flame = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Game/Prefabs/ART3/P_Tower_FlameLanternL"+level+".prefab"));
                flame.transform.SetParent(stage.transform, false); flame.transform.position = point+new Vector3(0,0,3);
            }
            Text(stage.transform,camera,"SYNTHETIC COMPARISON  |  TIDE L1-3 + FLAME L1-3  |  identical camera / scale1 / lighting",Vector3.zero,.15f);
            Pixels(camera,"tide-flame-comparison.png"); Clear(stage.transform);
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
                        enemy.GetComponent<EnemyStatusView>().Refresh(wet,probe.State.Tick,probe.ReactionCatalog,null,0,camera);
                        Text(stage.transform,camera,"SYNTHETIC OCCLUSION  |  TIDE L"+level+" / "+names[i]+" / assigned HP .6 + copied Wet snapshot  |  original camera",new Vector3(0,3.7f,0),.15f);
                        Pixels(camera,"occlusion-L"+level+"-"+names[i]+".png");
                        UnityEngine.Object.DestroyImmediate(enemy);
                        foreach(var label in stage.transform.Cast<Transform>().Where(t=>t.name.StartsWith("SYNTHETIC")).ToArray())UnityEngine.Object.DestroyImmediate(label.gameObject);
                    }
                    Clear(stage.transform);
                }
            }
            finally { ShaderUtil.allowAsyncCompilation=shaderAsync; }
            for(var level=1;level<=3;level++) Model(level,new Vector3(-3+(level-1)*2,.04f,-1),stage.transform);
            var occupied=new List<Rect>();var badges=new List<Rect>();
            for(var i=0;i<12;i++)
            {
                var enemy=(GameObject)PrefabUtility.InstantiatePrefab(prefab);enemy.transform.SetParent(stage.transform,false);
                enemy.transform.position=new Vector3(-3+(i%4)*1.3f,.6f,-2+(i/4)*.7f);
                enemy.GetComponent<EnemyHealthBarView>().SetNormalizedHealth(.2f+(i%4)*.2f);
                enemy.GetComponent<EnemyHealthBarView>().Face(camera);
                enemy.GetComponent<EnemyHealthBarView>().Layout(camera,occupied);
                var status=enemy.GetComponent<EnemyStatusView>();status.Refresh(wet,probe.State.Tick,probe.ReactionCatalog,null,0,camera);
                status.Layout(camera,badges);
            }
            Text(stage.transform,camera,"SYNTHETIC DENSE  |  TIDE L1-3 / 12 assigned enemies + HP / copied Wet  |  original camera",Vector3.zero,.15f);
            Pixels(camera,"occlusion-dense.png");Clear(stage.transform);
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output+"settings.json",JsonUtility.ToJson(new Settings { cameraPosition=camera.transform.position,
                cameraEuler=camera.transform.eulerAngles,ortho=camera.orthographicSize,width=1600,height=900,
                pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name,quality=QualitySettings.names[QualitySettings.GetQualityLevel()],
                ambient=RenderSettings.ambientLight,sunIntensity=RenderSettings.sun!=null?RenderSettings.sun.intensity:0,
                synthetic=true,frame=Time.frameCount,tick=battle.Simulation.State.Tick },true)+"\n");
        }
        finally { UnityEngine.Object.DestroyImmediate(stage); ShaderUtil.allowAsyncCompilation=asyncCompilation; }
        return "Synthetic singles/lineup/comparison/occlusion/dense captured in Play Mode; no live Domain mutation; Device Deferred.";
    }
    private static BattleSimulation WetProbe()
    {
        var store=new WaveCheckpointStore(Path.GetFullPath("TestResults/ART3B/ProbeStore"));
        Directory.CreateDirectory(Path.GetDirectoryName(store.PrimaryPath));
        File.Copy("TestResults/ART3B/natural-L3-checkpoint.json",store.PrimaryPath,true);
        var config=AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWaveAbilityBossV2.asset");
        if(!store.TryLoad(config.ScenarioContentId,config.CreateSimulationForCheckpoint,out var loaded))throw new InvalidOperationException(loaded.Error);
        var s=config.CreateSimulationForCheckpoint(loaded.Envelope);s.RestoreCheckpoint(loaded.Envelope.state);
        if(s.StartWave()!=WaveTransitionResult.Started)throw new InvalidOperationException("Probe StartWave");
        for(var i=0;i<1500&&!s.State.Enemies.Any(e=>e.Wet!=null);i++)
        {s.Step();s.ClearAttackEvents();s.ClearDamageEvents();s.ClearReactionEvents();s.ClearAbilityEvents();}
        if(!s.State.Enemies.Any(e=>e.Wet!=null))throw new InvalidOperationException("No actual Wet snapshot for synthetic badges");
        return s;
    }
    private static GameObject Model(int level,Vector3 position,Transform parent)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/ART3/TIDE/P_Tower_TideBellL"+level+".prefab");
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
