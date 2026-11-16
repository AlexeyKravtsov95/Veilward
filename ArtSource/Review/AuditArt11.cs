// Ephemeral Editor-only audit, вне Assets; результат CLI сохраняется в imported-models.json.
using System.Linq;
using UnityEngine;
using UnityEditor;
using Veilward.Editor;
public static class AuditArt11
{
    public static object Run()
    {
        var models=ArtReferenceBuilder.ModelNames.Select(name=>
        {
            var path=ArtReferenceBuilder.ModelFolder+name+".fbx";
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);var mesh=model.GetComponentInChildren<MeshFilter>().sharedMesh;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh,out string guid,out long localId);
            return new{name,triangles=Enumerable.Range(0,mesh.subMeshCount).Sum(i=>(long)mesh.GetIndexCount(i)/3),
                materialSlots=mesh.subMeshCount,vertices=mesh.vertexCount,bounds=mesh.bounds.ToString("F4"),guid,meshLocalId=localId,
                materials=model.GetComponentInChildren<MeshRenderer>().sharedMaterials.Select(m=>m.name).ToArray()};
        }).ToArray();
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Textures/ART1/Art1Palette_BaseColor_128.png");
        var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));var ios=importer.GetPlatformTextureSettings("iPhone");
        return new{models,texture=new{texture.width,texture.height,filter=texture.filterMode.ToString(),mipmaps=importer.mipmapEnabled,iosOverride=ios.overridden,iosFormat=ios.format.ToString()},unity=Application.unityVersion,platform=Application.platform.ToString()};
    }
}
