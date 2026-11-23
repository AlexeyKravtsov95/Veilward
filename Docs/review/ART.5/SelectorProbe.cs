using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Veilward.Domain;
using Veilward.Presentation;
public static class Art5SelectorProbe
{
 public static string Capture()
 {
  if(!EditorApplication.isPlaying)throw new Exception("Actual Play Mode required");
  var production=AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWaveAbilityBossV2.asset").CreateSimulation();
  var historical=AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWaveAbilityBoss.asset").CreateSimulation();
  var oldBoss=historical.Waves[7].Schedule.Last().Enemy;var newBoss=production.Waves[7].Schedule.Last().Enemy;
  var groups=new[]{new WaveSpawnGroup(oldBoss,1,0,30),new WaveSpawnGroup(newBoss,1,0,30),new WaveSpawnGroup(new EnemyDefinition(new EnemyDefinitionId("unknown-art5"),600,1,20,50,5),1,0,30)};
  var sim=new BattleSimulation(production.Grid.Map,production.Path,new[]{new WaveDefinition(groups)},production.TowerCatalog,20,150,20,production.ReactionCatalog);
  sim.StartWave();var root=new GameObject("ART5 isolated synthetic selector probe");var b=root.AddComponent<BattleSceneController>();b.enabled=false;
  var flags=BindingFlags.Instance|BindingFlags.NonPublic;
  void Set(string name,object value)=>typeof(BattleSceneController).GetField(name,flags).SetValue(b,value);
  var shade=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Shade.prefab").transform;
  var boss=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/ART5/P_Enemy_RiftWarden.prefab").transform;
  Set("_simulation",sim);Set("enemyPrefab",shade);Set("bossPrefab",boss);Set("worldCamera",Camera.main);
  object[] Rows(bool fallback)
  {
   typeof(BattleSceneController).GetMethod("SynchronizeEnemyViews",flags).Invoke(b,null);
   var dict=(IDictionary)typeof(BattleSceneController).GetField("_enemyViews",flags).GetValue(b);var rows=new List<object>();
   foreach(DictionaryEntry pair in dict)
   {
    var id=(EnemyId)pair.Key;var transform=(Transform)pair.Value.GetType().GetProperty("Transform").GetValue(pair.Value);
    var e=sim.State.Enemies.Single(x=>x.Id==id);var expected=e.Definition.Id.Value=="rift-warden"?(fallback?.84f:1):.6f;
    if(Mathf.Abs(transform.localScale.x-expected)>.00001f||transform.GetComponentsInChildren<EnemyModelView>().Length!=1)throw new Exception("Scale/model ownership mismatch");
    rows.Add(new{id=id.Value,definition=e.Definition.Id.Value,hp=e.Definition.MaxHealth,e.Definition.Armor,fallback,rootScale=transform.localScale.ToString("R"),rootY=transform.position.y,modelOwners=transform.GetComponentsInChildren<EnemyModelView>().Length,model=AssetDatabase.GetAssetPath(transform.GetComponentInChildren<EnemyModelView>().Model.GetComponentInChildren<MeshFilter>().sharedMesh)});
   }
   return rows.ToArray();
  }
  try
  {
   var authored=Rows(false);Set("bossPrefab",null);b.ResetPresentation();var fallback=Rows(true);
   var report=new{synthetic=true,actualPlayMode=true,liveResourceMutation=false,production600Armor5=new[]{newBoss.MaxHealth,newBoss.Armor},historical1000Armor5=new[]{oldBoss.MaxHealth,oldBoss.Armor},authored,fallback,kills=sim.State.Kills,gold=sim.State.Gold};
   File.WriteAllText("Docs/review/ART.5/reports/selector-scale-probe.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented)+"\n");
  }
  finally{UnityEngine.Object.DestroyImmediate(root);}
  return "Actual ID selector: both600/1000boss scale1, missingref .84, unknown600HP shade .6, one owner; no gameplay mutation";
 }
}
