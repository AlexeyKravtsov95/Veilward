using UnityEngine;
using UnityEngine.Rendering;

namespace Veilward.Presentation
{
    public static class ShadeOcclusionView
    {
        // Один cached renderer на pooled Shade, mesh/source/material slots не редактируются.
        public static Renderer Create(Transform enemy, Material material)
        {
            var source = enemy.GetComponentInChildren<MeshFilter>();
            var go = new GameObject("Occlusion presentation pass");
            go.transform.SetParent(source.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            var materials = new Material[source.sharedMesh.subMeshCount];
            for (var i = 0; i < materials.Length; i++) materials[i] = material;
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return renderer;
        }
    }
}
