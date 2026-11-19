// Static diagnostic masks, не combat и не доказательство читаемости на iPhone.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Veilward.Editor;
using Veilward.Presentation;
public static class MeasureArt21
{
    [Serializable]sealed class Row{public string stage;public float distance,coverage;public int fullBodyPixels,cueOrBodyPixels;}
    static int Pixels(Camera c)
    {
        var previous=c.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(1280,720,24);Texture2D t=null;
        try{c.targetTexture=rt;c.Render();RenderTexture.active=rt;t=new Texture2D(1280,720,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1280,720),0,0);t.Apply();return t.GetPixels32().Count(p=>p.r>100&&p.g>100&&p.b>100);}
        finally{c.targetTexture=previous;RenderTexture.active=active;if(t!=null)UnityEngine.Object.DestroyImmediate(t);RenderTexture.ReleaseTemporary(rt);}
    }
    public static string Run()
    {
        if(!EditorApplication.isPlaying)throw new Exception("Play Mode required");
        var s=UnityEngine.Object.FindFirstObjectByType<ArtReferenceShowcase>();s.SetPaused(true);s.RestartShowcase();
        var c=s.WorldCamera;var background=c.backgroundColor;var rs=s.GetComponentsInChildren<Renderer>(true);var enabled=rs.Select(r=>r.enabled).ToArray();
        var white=new Material(Shader.Find("Universal Render Pipeline/Unlit"));white.SetColor("_BaseColor",Color.white);
        var black=new Material(white);black.SetColor("_BaseColor",Color.black);
        var ghost=new Material(Shader.Find("Veilward/ShadeOcclusion"));ghost.SetColor("_Tint",new Color(1,1,1,.62f));
        var mask=AssetDatabase.LoadAssetAtPath<Material>(ArtReferenceBuilder.MaterialFolder+"MAT_Art21_TowerMask.mat");
        var tower=UnityEngine.Object.Instantiate(ArtReferenceBuilder.Prefab("Tower_FlameLantern"));tower.transform.position=s.TowerView.position;
        var enemy=UnityEngine.Object.Instantiate(ArtReferenceBuilder.Prefab("Enemy_Shade"));enemy.transform.rotation=Quaternion.LookRotation(Vector3.right);enemy.transform.Find("Health Bar").gameObject.SetActive(false);
        foreach(var r in tower.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(_=>black).ToArray();
        foreach(var r in enemy.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(_=>white).ToArray();
        ShadeOcclusionView.Create(tower.transform,mask);var cue=ShadeOcclusionView.Create(enemy.transform,ghost);
        var rows=new List<Row>();
        try
        {
            foreach(var r in rs)r.enabled=false;s.EnvironmentRoot.gameObject.SetActive(false);c.backgroundColor=Color.black;
            foreach(var stage in new[]{"before","after"})
                for(int i=0;i<=40;i++)
                {
                    var distance=1+i*.1f;var p=s.Simulation.Path.GetPosition(distance);enemy.transform.position=new Vector3(p.X,.14f,p.Y);
                    cue.enabled=false;tower.SetActive(false);var total=Pixels(c);
                    tower.SetActive(true);cue.enabled=stage=="after";var visible=Pixels(c);
                    if(total<100)throw new Exception("Empty body mask");
                    rows.Add(new Row{stage=stage,distance=distance,fullBodyPixels=total,cueOrBodyPixels=visible,coverage=(float)visible/total});
                }
            File.WriteAllText("Docs/review/ART.2a.1/occlusion-mask.json","{\"method\":\"Static diagnostic white body/black tower; after adds white-tinted cue alpha.62 via actual stencil shader; threshold100 both stages. Reports body OR occlusion-cue coverage, NOT opaque geometry visibility/contrast. Same source meshes/camera/path; 0.1m samples.\",\"rows\":["+string.Join(",",rows.Select(r=>JsonUtility.ToJson(r)))+"]}");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(tower);UnityEngine.Object.DestroyImmediate(enemy);UnityEngine.Object.DestroyImmediate(white);UnityEngine.Object.DestroyImmediate(black);UnityEngine.Object.DestroyImmediate(ghost);
            for(int i=0;i<rs.Length;i++)rs[i].enabled=enabled[i];s.EnvironmentRoot.gameObject.SetActive(true);c.backgroundColor=background;s.RestartShowcase();
        }
        return "82 comparable diagnostic samples; cue coverage is not opaque body visibility";
    }
}
