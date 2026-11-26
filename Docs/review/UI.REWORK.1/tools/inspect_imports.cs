using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;
using Veilward.Presentation;

const string art = "Assets/_Game/Art/UI/InkAndLanterns/";
var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(art + "InkUi.png");
var backdrop = AssetDatabase.LoadAssetAtPath<Texture2D>(art + "MenuBackdrop.jpg");
var font = AssetDatabase.LoadAssetAtPath<Font>(art + "CormorantGaramond-BoldItalic.ttf");
var sprites = AssetDatabase.LoadAllAssetsAtPath(art + "InkUi.png").OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray();
if (sprites.Length != 24 || sprites.Any(sprite => sprite.texture != atlas))
    throw new System.InvalidOperationException("Expected 24 sprites sharing one texture.");
if (sprites.Count(sprite => sprite.border != Vector4.zero) != 8)
    throw new System.InvalidOperationException("Expected eight nine-sliced surfaces.");

object Inventory(Texture2D texture) => new
{
    texture.name, texture.width, texture.height, format = texture.format.ToString(),
    texture.mipmapCount, texture.isReadable, runtimeBytes = Profiler.GetRuntimeMemorySizeLong(texture)
};

EditorSceneManager.OpenScene("Assets/_Game/Scenes/Battle.unity");
var glyph = UnityEngine.Object.FindObjectsByType<VeilwardUiGlyph>(FindObjectsInactive.Include, FindObjectsSortMode.None).First();
var method = typeof(VeilwardUiGlyph).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
    null, new[] { typeof(VertexHelper) }, null);
var vertices = new VertexHelper();
var populate = (System.Action<VertexHelper>)method.CreateDelegate(typeof(System.Action<VertexHelper>), glyph);
for (var i = 0; i < 20; i++)
{
    glyph.Glyph = i % 2 == 0 ? VeilwardGlyph.Lock : VeilwardGlyph.Tide;
    populate(vertices);
}
var start = System.GC.GetAllocatedBytesForCurrentThread();
for (var i = 0; i < 1000; i++)
{
    glyph.Glyph = i % 2 == 0 ? VeilwardGlyph.Lock : VeilwardGlyph.Tide;
    populate(vertices);
}
var delta = System.GC.GetAllocatedBytesForCurrentThread() - start;
vertices.Dispose();

Newtonsoft.Json.JsonConvert.SerializeObject(new
{
    unity = Application.unityVersion,
    platform = EditorUserBuildSettings.activeBuildTarget.ToString(),
    atlas = Inventory(atlas), backdrop = Inventory(backdrop),
    font = new { font.name, runtimeBytes = Profiler.GetRuntimeMemorySizeLong(font) },
    sprites = sprites.Select(sprite => new
    {
        sprite.name,
        border = new[] { sprite.border.x, sprite.border.y, sprite.border.z, sprite.border.w },
        rect = new[] { sprite.rect.x, sprite.rect.y, sprite.rect.width, sprite.rect.height }
    }).ToArray(),
    managedBytesFor1000GlyphSwitchesAndRebuilds = delta
})
