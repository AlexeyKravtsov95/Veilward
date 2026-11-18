using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Veilward.Domain;
using Veilward.Presentation;
namespace Veilward.Tests.EditMode
{
    public sealed class BossPresentationTests
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        private static object Call(object o,string method)=>o.GetType().GetMethod(method,Flags|BindingFlags.Public).Invoke(o,null);
        private static void Set(object o,string field,object value)=>o.GetType().GetField(field,Flags).SetValue(o,value);
        [TearDown] public void Close()=>EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        [Test] public void RegBattle213_SavedSceneRequiresBossHud()
        {
            EditorSceneManager.OpenScene("Assets/_Game/Scenes/Battle.unity");
            Assert.That(Object.FindFirstObjectByType<BossHudController>(),Is.Not.Null);
            Assert.That(Veilward.Editor.BattleAssetValidator.ValidateSavedBattle(),Is.Empty);
            Assert.That(Veilward.Editor.BattleAssetValidator.ValidateSavedMainMenu(),Is.Empty);
        }
        [Test] public void RegBattle213_NoticeSpawnArmorActualHealthGeometryAndRemoval()
        {
            EditorSceneManager.OpenScene("Assets/_Game/Scenes/Battle.unity");
            var battle=Object.FindFirstObjectByType<BattleSceneController>();Call(battle,"Start");
            var bossHud=battle.GetComponent<BossHudController>();Call(bossHud,"Start");
            var s=BossNaturalSmokeTests.Run("flame",false,out _,8,true);foreach(var tower in s.State.Towers.ToArray())s.SellTower(tower.Id);Set(battle,"_simulation",s);battle.ResetPresentation();bossHud.Refresh();
            Assert.That(bossHud.Notice,Does.Contain("11 Shade"));Assert.That(bossHud.Notice,Does.Contain("330"));Assert.That(bossHud.Notice,Does.Contain("1000"));Assert.That(bossHud.Notice,Does.Contain("20 урона"));Assert.That(bossHud.HealthVisible,Is.False);
            s.StartWave();for(var i=0;i<329;i++)s.Step();bossHud.Refresh();Assert.That(bossHud.HealthVisible,Is.False);
            s.Step();bossHud.Refresh();Assert.That(bossHud.Health,Does.Contain("1000/1000"));Assert.That(bossHud.Health,Does.Contain("Armor 10"));
            var boss=s.State.Enemies.Single(e=>e.Definition.Id.Value=="rift-warden");boss.ApplyDamage(110);bossHud.Refresh();
            Assert.That(bossHud.Health,Does.Contain("890/1000"));
            var fill=(UnityEngine.UI.Image)typeof(BossHudController).GetField("healthFill",Flags).GetValue(bossHud);
            Assert.That(fill.rectTransform.anchorMax.x,Is.EqualTo(.89f).Within(.0001f));
            // Existing status contract: two slots, no boss-only marker.
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Shade.prefab");
            var status=new SerializedObject(prefab.GetComponent<EnemyStatusView>());Assert.That(status.FindProperty("firstSlot").objectReferenceValue,Is.Not.Null);Assert.That(status.FindProperty("secondSlot").objectReferenceValue,Is.Not.Null);
            boss.ApplyDamage(890);s.Step();bossHud.Refresh();Assert.That(bossHud.HealthVisible,Is.False);Assert.That(bossHud.Notice,Does.Contain("+50 gold"));
            battle.GetComponent<BattlePauseController>().Pause();bossHud.Refresh();var panel=(GameObject)typeof(BossHudController).GetField("panel",Flags).GetValue(bossHud);Assert.That(panel.activeInHierarchy,Is.False);
        }
        [TestCase("id")] [TestCase("count")] [TestCase("firstSpawnTick")] [TestCase("spawnIntervalTicks")] [TestCase("missing")] [TestCase("boss")]
        public void RegBattle209_MalformedFinalConfigRejectsWithoutFallback(string field)
        {
            var clone=Object.Instantiate(BossWaveTests.Config());
            try
            {
                var data=new SerializedObject(clone);var groups=data.FindProperty("waves").GetArrayElementAtIndex(7).FindPropertyRelative("groups");
                if(field=="missing")groups.arraySize=0;
                else if(field=="boss")data.FindProperty("boss").FindPropertyRelative("maxHealth").intValue=999;
                else if(field=="id")groups.GetArrayElementAtIndex(1).FindPropertyRelative("enemyId").stringValue="unknown";
                else groups.GetArrayElementAtIndex(1).FindPropertyRelative(field).intValue=field=="count"?2:field=="firstSpawnTick"?329:0;
                data.ApplyModifiedPropertiesWithoutUndo();Assert.Throws<System.ArgumentException>(()=>clone.CreateSimulation());
            }
            finally{Object.DestroyImmediate(clone);}
        }
        [Test] public void RegBattle209_SharedEnemyCannotMasqueradeAsBoss()
        {
            var clone=Object.Instantiate(BossWaveTests.Config());var data=new SerializedObject(clone);
            var shared=Object.Instantiate((BattleScenarioConfig)data.FindProperty("sharedScenario").objectReferenceValue);
            var old=Object.Instantiate(AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWave.asset"));
            try
            {
                var sharedData=new SerializedObject(shared);sharedData.FindProperty("enemyId").stringValue="rift-warden";sharedData.ApplyModifiedPropertiesWithoutUndo();
                var oldData=new SerializedObject(old);oldData.FindProperty("sharedScenario").objectReferenceValue=shared;oldData.ApplyModifiedPropertiesWithoutUndo();
                data.FindProperty("historicalScenario").objectReferenceValue=old;data.FindProperty("sharedScenario").objectReferenceValue=shared;
                data.FindProperty("waves").GetArrayElementAtIndex(7).FindPropertyRelative("groups").GetArrayElementAtIndex(0).FindPropertyRelative("enemyId").stringValue="rift-warden";
                data.ApplyModifiedPropertiesWithoutUndo();Assert.Throws<System.ArgumentException>(()=>clone.CreateSimulation());
            }
            finally{Object.DestroyImmediate(clone);Object.DestroyImmediate(shared);Object.DestroyImmediate(old);}
        }
    }
}
