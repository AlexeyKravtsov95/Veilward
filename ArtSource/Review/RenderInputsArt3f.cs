// Read-only snapshot фактически загруженного Battle rendering. Не меняет настройки.
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
public static class RenderInputsArt3f
{
    public static string Capture()
    {
        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Actual Play Mode required");
        string Hash(string p){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
        float[] Rgba(Color c)=>new[]{c.r,c.g,c.b,c.a};
        var paths=Directory.GetFiles("Assets/Settings","*",SearchOption.AllDirectories).Where(File.Exists)
            .Concat(new[]{"ProjectSettings/QualitySettings.asset","ProjectSettings/GraphicsSettings.asset","Packages/manifest.json","Packages/packages-lock.json"})
            .Concat(Directory.GetFiles("Assets/_Game/Art/Materials","*",SearchOption.AllDirectories)).OrderBy(p=>p).ToArray();
        var pipeline=GraphicsSettings.currentRenderPipeline;
        var materials=UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct()
            .Select(m=>new {name=m.name,path=AssetDatabase.GetAssetPath(m),shader=m.shader.name,instanceId=m.GetInstanceID(),baseColor=Rgba(m.HasProperty("_BaseColor")?m.GetColor("_BaseColor"):Color.clear),legacyColor=Rgba(m.HasProperty("_Color")?m.GetColor("_Color"):Color.clear),keywords=m.shaderKeywords,renderQueue=m.renderQueue,passes=m.passCount}).ToArray();
        var report=new {frame=Time.frameCount,tick=UnityEngine.Object.FindFirstObjectByType<Veilward.Presentation.BattleSceneController>().Simulation.State.Tick,
            unity=UnityEngine.Application.unityVersion,platform=UnityEngine.Application.platform.ToString(),graphics=SystemInfo.graphicsDeviceType.ToString(),
            quality=QualitySettings.names[QualitySettings.GetQualityLevel()],pipelinePath=AssetDatabase.GetAssetPath(pipeline),pipelineName=pipeline.name,
            pipelineSerialized=Newtonsoft.Json.Linq.JObject.Parse(EditorJsonUtility.ToJson(pipeline)),materials,
            inputSha256=paths.ToDictionary(p=>p,Hash),
            packages=UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().Select(p=>new {p.name,p.version,p.source}).OrderBy(p=>p.name).ToArray(),
            actualPlayMode=true,newDeviceRun=false};
        Directory.CreateDirectory("Docs/review/ART.3f/reports");
        File.WriteAllText("Docs/review/ART.3f/reports/actual-render-inputs.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented)+"\n");
        return "Captured actual loaded pipeline/materials/quality/packages and disk SHA256 in Play Mode";
    }
}
