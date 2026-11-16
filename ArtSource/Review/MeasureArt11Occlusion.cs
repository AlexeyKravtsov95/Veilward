// Read-only геометрический render probe вне Assets: одинаковые saved camera/tower/path, не combat.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Veilward.Editor;
using Veilward.Presentation;
public static class MeasureArt11Occlusion
{
    [Serializable]sealed class Baseline {public Vector3[] vertices=null;public int[] indices=null;}
    [Serializable]sealed class Row {public string stage;public float distance,visibleFraction;public int totalPixels,visiblePixels;}
    static Mesh Original(string name)
    {
        var b=JsonUtility.FromJson<Baseline>(File.ReadAllText("Temp/Art11Baseline/"+name+".json"));
        var m=new Mesh{name="PR24 baseline temporary mesh"};m.vertices=b.vertices;m.triangles=b.indices;m.RecalculateNormals();m.RecalculateBounds();return m;
    }
    static int Pixels(Camera c)
    {
        var previous=c.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(1280,720,24);Texture2D t=null;
        try{c.targetTexture=rt;c.Render();RenderTexture.active=rt;t=new Texture2D(1280,720,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1280,720),0,0);t.Apply();return t.GetPixels32().Count(p=>p.r>220&&p.g>220&&p.b>220);}
        finally{c.targetTexture=previous;RenderTexture.active=active;if(t!=null)UnityEngine.Object.DestroyImmediate(t);RenderTexture.ReleaseTemporary(rt);}
    }
    public static string Run()
    {
        if(!EditorApplication.isPlaying)throw new Exception("Play Mode required");
        var s=UnityEngine.Object.FindFirstObjectByType<ArtReferenceShowcase>();s.SetPaused(true);s.RestartShowcase();
        var c=s.WorldCamera;var background=c.backgroundColor;
        var rs=s.GetComponentsInChildren<Renderer>(true);var enabled=rs.Select(r=>r.enabled).ToArray();
        var white=new Material(Shader.Find("Universal Render Pipeline/Unlit"));white.SetColor("_BaseColor",Color.white);
        var black=new Material(white);black.SetColor("_BaseColor",Color.black);
        var tower=UnityEngine.Object.Instantiate(ArtReferenceBuilder.Prefab("Tower_FlameLantern"));tower.transform.position=s.TowerView.position;
        var enemy=UnityEngine.Object.Instantiate(ArtReferenceBuilder.Prefab("Enemy_Shade"));enemy.transform.rotation=Quaternion.LookRotation(Vector3.right);enemy.transform.Find("Health Bar").gameObject.SetActive(false);
        var tf=tower.GetComponentInChildren<MeshFilter>();var ef=enemy.GetComponentInChildren<MeshFilter>();var currentTower=tf.sharedMesh;var currentEnemy=ef.sharedMesh;
        var beforeTower=Original("Tower_FlameLantern");var result=new List<Row>();
        try
        {
            foreach(var r in rs)r.enabled=false;s.EnvironmentRoot.gameObject.SetActive(false);c.backgroundColor=Color.black;
            foreach(var r in tower.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(_=>black).ToArray();
            foreach(var r in enemy.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(_=>white).ToArray();
            foreach(var stage in new[]{"before","after"})
            {
                tf.sharedMesh=stage=="before"?beforeTower:currentTower;ef.sharedMesh=currentEnemy;
                for(int i=0;i<=40;i++)
                {
                    var distance=1+i*.1f;var p=s.Simulation.Path.GetPosition(distance);enemy.transform.position=new Vector3(p.X,.14f,p.Y);
                    tower.SetActive(false);var total=Pixels(c);tower.SetActive(true);var visible=Pixels(c);
                    if(total<100)throw new Exception("Empty body mask");
                    result.Add(new Row{stage=stage,distance=distance,totalPixels=total,visiblePixels=visible,visibleFraction=(float)visible/total});
                }
            }
            File.WriteAllText("Docs/review/ART.1.1/occlusion-sweep.json","{\"method\":\"Original camera/tower/path/scale; current Shade held constant; opaque body pixel masks at 0.1m path intervals; static probe\",\"rows\":["+string.Join(",",result.Select(r=>JsonUtility.ToJson(r)))+"]}");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(tower);UnityEngine.Object.DestroyImmediate(enemy);UnityEngine.Object.DestroyImmediate(white);UnityEngine.Object.DestroyImmediate(black);
            UnityEngine.Object.DestroyImmediate(beforeTower);
            for(int i=0;i<rs.Length;i++)rs[i].enabled=enabled[i];s.EnvironmentRoot.gameObject.SetActive(true);c.backgroundColor=background;s.RestartShowcase();
        }
        return "82 body occlusion samples saved; original camera/tower/path/scale";
    }
}
