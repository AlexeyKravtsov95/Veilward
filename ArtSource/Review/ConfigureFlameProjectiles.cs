using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Veilward.Presentation;

public static class ConfigureFlameProjectiles
{
    public static string Bind()
    {
        var scene = EditorSceneManager.OpenScene("Assets/_Game/Scenes/Battle.unity");
        var data = new SerializedObject(Object.FindFirstObjectByType<BattleSceneController>());
        data.FindProperty("flameProjectileMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/ART1/MAT_Art1_EmberStroke.mat");
        data.FindProperty("flameProjectileCoreMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/ART1/MAT_Art1_EmberCore.mat");
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene);
        EditorSceneManager.OpenScene("Assets/_Game/Scenes/MainMenu.unity");
        return "Bound existing shared fireball materials; saved Battle only.";
    }
}
