using System.Reflection;
using UnityEditor;
using UnityEngine;
using Veilward.Editor;
using Veilward.Presentation;

public static class ConfigureBasicVfx
{
    public static string Bind()
    {
        foreach (var path in new[] { EnemyModelBuilder.ShadePath, EnemyModelBuilder.BossPath })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var status = root.GetComponent<EnemyStatusView>();
                var data = new SerializedObject(status);
                foreach (var prefix in new[] { "first", "second" })
                {
                    var existing = data.FindProperty(prefix + "Cue").objectReferenceValue as StatusCueGraphic;
                    var slot = (UnityEngine.UI.Text)data.FindProperty(prefix + "Slot").objectReferenceValue;
                    if (existing != null) { existing.name = slot.name + " cue"; continue; }
                    data.FindProperty(prefix + "Cue").objectReferenceValue = (StatusCueGraphic)typeof(BattleSceneBuilder)
                        .GetMethod("AddStatusCue", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { slot });
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            EnemyModelBuilder.NormalizeSerializedWhitespace(path);
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        return "Added two prebuilt graphics per existing live owner; all existing fields/GUID/model refs preserved.";
    }
}
