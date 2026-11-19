using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Veilward.Domain;
using Veilward.Infrastructure;
using Veilward.Presentation;
using Veilward.Tests.Shared;
namespace Veilward.Tests.EditMode
{
    public sealed class AbilityBossVersionTests
    {
        private const string V1=WaveCheckpointCompatibility.AbilityBossContentId,V2=WaveCheckpointCompatibility.BalancedAbilityBossContentId;
        private static WaveCheckpointEnvelope Initial(string id)
        {
            var factory=AbilityBossReplayTests.Config();
            return new WaveCheckpointEnvelope{schemaVersion=4,scenarioContentId=id,state=factory.CreateSessionSimulation(id).ExportCheckpoint()};
        }
        [Test] public void RegBattle214_V2ChangesOnlyBossHpAndKeepsV1Rules()
        {
            var a=BossWaveTests.Config().CreateSimulation();var b=AbilityBossReplayTests.Config().CreateSimulation();
            Assert.That(AbilityBossReplayTests.Config().ScenarioContentId,Is.EqualTo(V2));
            Assert.That(AbilityBossTrace.Wire(b),Is.EqualTo(AbilityBossTrace.Wire(a)));
            Assert.That(b.TowerCatalog.Archetypes.SelectMany(t=>t.Levels).Select(t=>$"{t.Id.Value}/{t.Price}/{t.Damage}/{t.Range}/{t.FireRate}"),Is.EqualTo(a.TowerCatalog.Archetypes.SelectMany(t=>t.Levels).Select(t=>$"{t.Id.Value}/{t.Price}/{t.Damage}/{t.Range}/{t.FireRate}")));
            Assert.That(b.AbilityDefinition,Is.SameAs(a.AbilityDefinition));
            for(var w=0;w<8;w++)for(var i=0;i<a.Waves[w].Count;i++)
            {
                var x=a.Waves[w].Schedule[i];var y=b.Waves[w].Schedule[i];
                Assert.That(y.RelativeTick,Is.EqualTo(x.RelativeTick));Assert.That(y.Enemy.Id,Is.EqualTo(x.Enemy.Id));
                Assert.That(y.Enemy.Speed,Is.EqualTo(x.Enemy.Speed));Assert.That(y.Enemy.Armor,Is.EqualTo(x.Enemy.Armor));
                Assert.That(y.Enemy.Reward,Is.EqualTo(x.Enemy.Reward));Assert.That(y.Enemy.LeakDamage,Is.EqualTo(x.Enemy.LeakDamage));
                Assert.That(y.Enemy.MaxHealth,Is.EqualTo(w==7&&i==11?600:x.Enemy.MaxHealth));
            }
            var old=AbilityBossReplayTests.Config().CreateSimulationForCheckpoint(Initial(V1));
            Assert.That(old.Waves[7].Schedule.Last().Enemy.MaxHealth,Is.EqualTo(1000));
            Assert.That(WaveCheckpointCompatibility.TryNormalize(Initial(V2),V1,out _,out _),Is.False);
            foreach(var schema in new[]{1,2,3}){var e=Initial(V2);e.schemaVersion=schema;Assert.That(WaveCheckpointCompatibility.TryNormalize(e,V2,out _,out _),Is.False);}
        }
        [TestCase(V1,V2)] [TestCase(V2,V1)]
        public void RegBattle215_CrossVersionDirtyRestoreRejectsBeforeMutation(string live,string other)
        {
            var config=AbilityBossReplayTests.Config();var s=config.CreateSessionSimulation(live);
            s.ChooseElement(new ChooseElementCommand("start-resonance","flame"));s.StartWave();s.Step();
            Assert.That(s.CastAbility(BossWaveTests.Cast(s,new PathPosition(-5,-2))).Succeeded,Is.True);s.Step();
            var before=AbilityBossTrace.Snapshot(s);var go=new GameObject("version restore");
            try
            {
                var controller=go.AddComponent<BattleCheckpointController>();controller.Configure(null,s,live,config.CreateSimulationForCheckpoint);
                var restored=0;controller.Restored+=()=>restored++;
                Assert.Throws<ArgumentException>(()=>controller.RestoreCheckpoint(Initial(other)));
                Assert.That(AbilityBossTrace.Snapshot(s),Is.EqualTo(before));Assert.That(restored,Is.Zero);Assert.That(controller.LastSaveSucceeded,Is.True);
            }
            finally {UnityEngine.Object.DestroyImmediate(go);}
        }
        [Test] public void RegBattle215_VersionedMixedSlotsKeepRulesAndBytesAcrossLoadSaveAndFallback()
        {
            var root=Path.Combine("TestResults","P24fVersions",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            try
            {
                var store=new WaveCheckpointStore(root);var config=AbilityBossReplayTests.Config();var audit=new System.Collections.Generic.List<string>();
                var ids=new[]{V2,V1,WaveCheckpointCompatibility.NativeContentId,WaveCheckpointCompatibility.LegacyEightWaveContentId};
                foreach(var primary in ids)foreach(var backup in ids.Where(id=>id!=primary))
                {
                    var a=Initial(primary);var b=Initial(backup);File.WriteAllText(store.PrimaryPath,WaveCheckpointJson.Serialize(a));File.WriteAllText(store.BackupPath,WaveCheckpointJson.Serialize(b));
                    var p=File.ReadAllBytes(store.PrimaryPath);var q=File.ReadAllBytes(store.BackupPath);
                    for(var repeat=0;repeat<2;repeat++)
                    {
                        Assert.That(store.TryLoad(V2,config.CreateSimulationForCheckpoint,out var result),Is.True,result.Error);
                        Assert.That(result.Envelope.scenarioContentId,Is.EqualTo(primary));var s=config.CreateSimulationForCheckpoint(result.Envelope);s.RestoreCheckpoint(result.Envelope.state);
                        Assert.That(s.Waves[7].Schedule.Last().Enemy.MaxHealth,Is.EqualTo(primary==V2?600:primary==V1?1000:100));
                    }
                    Assert.That(File.ReadAllBytes(store.PrimaryPath),Is.EqualTo(p));Assert.That(File.ReadAllBytes(store.BackupPath),Is.EqualTo(q));
                    var next=config.CreateSimulationForCheckpoint(b);
                    Assert.That(store.TrySave(b,next.TryValidateCheckpoint,out var error,config.CreateSimulationForCheckpoint),Is.True,error);
                    Assert.That(File.ReadAllBytes(store.BackupPath),Is.EqualTo(p),"previous primary validated by matching factory");
                    File.WriteAllText(store.PrimaryPath,"{corrupt}");
                    Assert.That(store.TryLoad(V2,config.CreateSimulationForCheckpoint,out var fallback),Is.True,fallback.Error);
                    Assert.That(fallback.Envelope.scenarioContentId,Is.EqualTo(primary));
                    Assert.That(store.TrySave(b,next.TryValidateCheckpoint,out error,config.CreateSimulationForCheckpoint),Is.True,error);
                    Assert.That(File.ReadAllBytes(store.BackupPath),Is.EqualTo(p),"corrupt primary cannot evict backup");
                    audit.Add(primary+"/"+backup+" matching rules, repeated no-write Load, Save/backup/corrupt fallback PASS");
                }
                Directory.CreateDirectory("TestResults/P2.4f/revision-3");File.WriteAllLines("TestResults/P2.4f/revision-3/version-slots.txt",audit);
            }
            finally {Directory.Delete(root,true);}
        }
        [TestCase(true)] [TestCase(false)] public void RegBattle214_V2RejectsWrongHpOrMissingHistoricalSource(bool hp)
        {
            var config=UnityEngine.Object.Instantiate(AbilityBossReplayTests.Config());
            try{var so=new SerializedObject(config);if(hp)so.FindProperty("boss").FindPropertyRelative("maxHealth").intValue=650;else so.FindProperty("historicalBossScenario").objectReferenceValue=null;so.ApplyModifiedPropertiesWithoutUndo();Assert.Throws<ArgumentException>(()=>config.CreateSimulation());}
            finally{UnityEngine.Object.DestroyImmediate(config);}
        }
    }
}
