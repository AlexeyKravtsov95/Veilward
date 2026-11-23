// ART5 evidence only: actual production config/camera; no gameplay mutation.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Veilward.Presentation;
using Veilward.Domain;
using Veilward.Tests.Shared;
public static class CaptureArt5
{
    public const string Output="Docs/review/ART.5/";
    public static object ConfigureView()
    {
        // Только размер task GameView; вернуть previousIndex после evidence, PlayerSettings не меняются.
        var assembly=typeof(UnityEditor.Editor).Assembly;
        var type=assembly.GetType("UnityEditor.GameViewSizes");
        var sizes=typeof(ScriptableSingleton<>).MakeGenericType(type).GetProperty("instance").GetValue(null);
        var group=type.GetMethod("GetGroup").Invoke(sizes,new[]{type.GetProperty("currentGroupType").GetValue(sizes)});
        var window=EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
        var selected=window.GetType().GetProperty("selectedSizeIndex",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
        var previousIndex=(int)selected.GetValue(window);
        var count=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);
        var index=-1;
        for(var i=0;i<count;i++)
        {
            var candidate=group.GetType().GetMethod("GetGameViewSize").Invoke(group,new object[]{i});
            var c=candidate.GetType();
            if((string)c.GetProperty("baseText").GetValue(candidate)=="ART5 evidence" && (int)c.GetProperty("width").GetValue(candidate)==1600 && (int)c.GetProperty("height").GetValue(candidate)==900){index=i;break;}
        }
        if(index<0)
        {
            var kind=Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"),"FixedResolution");
            var size=Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),new object[]{kind,1600,900,"ART5 evidence"});
            group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});index=count;
        }
        selected.SetValue(window,index);window.Focus();return new{previousIndex,index,width=1600,height=900};
    }


    public static object Geometry()
    {
        var config=AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWaveAbilityBossV2.asset");
        var s=config.CreateSimulation();var map=s.Grid.Map;var cam=Camera.main;
        if(cam==null)throw new Exception("Production Battle camera required");
        cam.aspect=1600f/900f;
        var cells=new List<object>();
        for(var y=map.Origin.Y;y<map.Origin.Y+map.Height;y++)for(var x=map.Origin.X;x<map.Origin.X+map.Width;x++)
        {
            var cell=new GridCoordinate(x,y);var p=map.GetCellCenter(cell);var w=new Vector3(p.X,0,p.Y);
            var screen=cam.WorldToScreenPoint(w);var ray=cam.ScreenPointToRay(screen);
            var plane=new Plane(Vector3.up,Vector3.zero);if(!plane.Raycast(ray,out var distance))throw new Exception("Mapping ray failed");
            var hit=ray.GetPoint(distance);if(!map.TryGetCoordinate(new PathPosition(hit.x,hit.z),out var actual)||actual!=cell)throw new Exception("Mapping mismatch "+cell);
            cells.Add(new{x,y,state=s.Grid.GetState(cell).ToString(),center=new[]{p.X,p.Y},screen=new[]{screen.x,screen.y,screen.z}});
        }
        float[] V(Vector3 v)=>new[]{v.x,v.y,v.z};
        return new{map.Width,map.Height,origin=new[]{map.Origin.X,map.Origin.Y},map.CellSize,cells,
            path=s.Path.Points.Select(p=>new[]{p.X,p.Y}).ToArray(),distance=s.Path.TotalLength,
            samples=Enumerable.Range(0,57).Select(i=>s.Path.GetPosition(i*.25f)).Select(p=>new[]{p.X,p.Y}).ToArray(),
            camera=new{position=V(cam.transform.position),rotation=new[]{cam.transform.rotation.x,cam.transform.rotation.y,cam.transform.rotation.z,cam.transform.rotation.w},cam.orthographic,cam.orthographicSize,cam.nearClipPlane,cam.farClipPlane,cam.aspect,rect=new[]{cam.rect.x,cam.rect.y,cam.rect.width,cam.rect.height}},
            groundConvention="Domain X/Y -> Unity X/Z; input plane y=0; tower/preview y=.04; enemy y=.6"};
    }
    public static string Baseline()=>Snapshot("before");
    public static string After()=>Snapshot("after");
    private static string Snapshot(string phase)
    {
        Directory.CreateDirectory(Output+"reports");
        var geometry=Newtonsoft.Json.JsonConvert.SerializeObject(Geometry(),Newtonsoft.Json.Formatting.Indented)+"\n";
        File.WriteAllText(Output+"reports/"+phase+"-geometry.json",geometry);
        foreach(var start in new[]{"flame","gale"})
        {
            var c=AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWaveAbilityBossV2.asset");
            var run=new AbilityBossTrace.Run(c.CreateSimulation(),AbilityBossTrace.Load(start));run.Play();
            AbilityBossTrace.Write(Output+"reports/"+phase+"-"+start,run);
            if(run.sim.State.Phase!=BattlePhase.Victory)throw new Exception("Expected production Victory8");
        }
        if(phase=="after")
        {
            if(File.ReadAllText(Output+"reports/before-geometry.json")!=geometry)throw new Exception("Gameplay geometry changed");
            foreach(var start in new[]{"flame","gale"})
            {
                var b=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Output+"reports/before-"+start+".json"));
                var a=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Output+"reports/after-"+start+".json"));
                if(!Newtonsoft.Json.Linq.JToken.DeepEquals(a,b))throw new Exception("Replay changed "+start);
            }
        }
        return phase+": actual map/camera/mapping and two full production traces captured";
    }
}
