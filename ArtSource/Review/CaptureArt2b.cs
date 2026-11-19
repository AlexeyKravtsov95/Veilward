// Actual Editor Play Mode evidence; device captures собираются отдельно.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Veilward.Presentation;

public static class CaptureArt2b
{
    public static string Capture()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Actual Play Mode required.");
        var s = UnityEngine.Object.FindFirstObjectByType<ArtReferenceShowcase>();
        var probe = s.GetComponent<ArtDeviceProbe>();
        if (probe == null) { probe = s.gameObject.AddComponent<ArtDeviceProbe>(); probe.Initialize(s); }
        probe.SelectScenario("normal"); s.SetPaused(true); s.RestartShowcase();
        while (s.ObservedAttacks == 0) s.AdvanceForReview(1);
        s.AdvanceForReview(3); Pixels(s.WorldCamera, "playmode-normal.png");
        probe.SelectScenario("dense"); probe.SendMessage("LateUpdate"); Pixels(s.WorldCamera, "playmode-dense.png");
        return "2 actual Play Mode camera captures; dense is synthetic, not device evidence.";
    }
    private static void Pixels(Camera camera, string file)
    {
        const string output = "Docs/review/ART.2b/";
        Directory.CreateDirectory(output);
        var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
        var rt = RenderTexture.GetTemporary(1600, 900, 24); Texture2D texture = null;
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); texture.Apply();
            File.WriteAllBytes(output + file, texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
            if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            RenderTexture.ReleaseTemporary(rt);
        }
    }
}
