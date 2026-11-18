using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Veilward.Domain;
using Veilward.Infrastructure;
using Veilward.Presentation;
using Veilward.Tests.Shared;
namespace Veilward.Tests.EditMode
{
    public sealed class BossVictoryPersistenceTests
    {
        private const string Final=WaveCheckpointCompatibility.AbilityBossContentId;
        private string root;private WaveCheckpointStore store;
        [SetUp] public void Setup(){root=Path.Combine("TestResults","BossVictory",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);store=new WaveCheckpointStore(root);}
        [TearDown] public void Cleanup(){if(Directory.Exists(root))Directory.Delete(root,true);}
        private static string Wire(BattleSimulation s)=>WaveCheckpointJson.Serialize(BossNaturalSmokeTests.Envelope(s));
        private static void Frozen(BattleSimulation s)
        {
            var before=BattleAbilityTests.FullSnapshot(s);s.Step();
            Assert.That(s.StartWave(),Is.EqualTo(WaveTransitionResult.RejectedInvalidPhase));
            Assert.That(s.ContinueToNextWave(),Is.EqualTo(WaveTransitionResult.RejectedInvalidPhase));
            Assert.That(s.CastAbility(BossWaveTests.Cast(s)).FailureReason,Is.EqualTo(AbilityCastFailureReason.InvalidPhase));
            Assert.That(BattleAbilityTests.FullSnapshot(s),Is.EqualTo(before));
        }
        [TestCase("flame")] [TestCase("gale")]
        public void RegBattle212_R3NaturalVictoryWholeDirtyFreshRestoreNoWriteLoad(string start)
        {
            var s=BossNaturalSmokeTests.Run(start,false,out var report);Assert.That(s.State.Phase,Is.EqualTo(BattlePhase.Victory));
            Assert.That(report.bossKills,Is.EqualTo(1));Assert.That(report.bossAbilityHpLoss,Is.GreaterThan(0));
            var envelope=BossNaturalSmokeTests.Envelope(s);var expected=Wire(s);var factory=BossWaveTests.Config();
            Assert.That(store.TrySave(envelope,s.TryValidateCheckpoint,out var error,factory.CreateSimulationForCheckpoint),Is.True,error);
            File.Copy(store.PrimaryPath,store.BackupPath);var primary=File.ReadAllBytes(store.PrimaryPath);var backup=File.ReadAllBytes(store.BackupPath);
            for(var i=0;i<2;i++)
            {
                Assert.That(store.TryLoad(Final,factory.CreateSimulationForCheckpoint,out var loaded),Is.True,loaded.Error);
                Assert.That(WaveCheckpointJson.Serialize(loaded.Envelope),Is.EqualTo(expected));
                Assert.That(File.ReadAllBytes(store.PrimaryPath),Is.EqualTo(primary));Assert.That(File.ReadAllBytes(store.BackupPath),Is.EqualTo(backup));
            }
            var dirty=BossNaturalSmokeTests.Run(start,false,out _,8);dirty.StartWave();
            for(var i=0;i<400;i++){BossNaturalSmokeTests.TryCast(dirty,new BossNaturalSmokeTests.Result());dirty.Step();}
            Assert.That(dirty.State.Phase,Is.EqualTo(BattlePhase.Combat));Assert.That(dirty.State.Enemies,Is.Not.Empty);Assert.That(dirty.DamageEvents,Is.Not.Empty);
            dirty.RestoreCheckpoint(envelope.state.DeepCopy());Assert.That(Wire(dirty),Is.EqualTo(expected));
            Assert.That(dirty.State.Enemies,Is.Empty);Assert.That(dirty.DamageEvents,Is.Empty);Assert.That(dirty.AbilityEvents,Is.Empty);Assert.That(dirty.ReactionEvents,Is.Empty);Assert.That(dirty.AttackEvents,Is.Empty);Frozen(dirty);
            var fresh=factory.CreateSimulationForCheckpoint(envelope);fresh.RestoreCheckpoint(envelope.state.DeepCopy());Assert.That(Wire(fresh),Is.EqualTo(expected));Frozen(fresh);
            var path=BossCandidateMatrixTests.Output+"/persistence/"+start+"-victory";Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path+".json",expected);
        }
        private sealed class Writer:IWaveCheckpointWriter
        {
            public int fault=-1,writes,commits;private readonly FileWaveCheckpointWriter files=new FileWaveCheckpointWriter();
            public void WriteTemporary(string path,string json){writes++;if(fault==0)throw new IOException("injected natural boss completion temp fault");files.WriteTemporary(path,fault==1?"{}":json);}
            public void Commit(string tmp,string primary,string backup){commits++;if(fault==2)throw new IOException("injected natural boss completion commit fault");files.Commit(tmp,primary,backup);}
        }
        [TestCase("flame",0)] [TestCase("flame",1)] [TestCase("flame",2)]
        [TestCase("gale",0)] [TestCase("gale",1)] [TestCase("gale",2)]
        public void RegBattle212_R3NaturalBossKillCompletionFaultRetryNeverRepeatsCommandReward(string start,int fault)
        {
            var s=BossNaturalSmokeTests.Run(start,false,out _,8);var config=BossWaveTests.Config();var writer=new Writer();var faulty=new WaveCheckpointStore(root,writer);
            var go=new GameObject("natural Victory checkpoint");var controller=go.AddComponent<BattleCheckpointController>();
            try
            {
                controller.Configure(faulty,s,Final,config.CreateSimulationForCheckpoint);Assert.That(controller.SaveCurrentState(),Is.True);
                File.Copy(store.PrimaryPath,store.BackupPath);var primary=File.ReadAllBytes(store.PrimaryPath);var backup=File.ReadAllBytes(store.BackupPath);
                s.StartWave();Assert.That(controller.ObserveCommandCompletion(),Is.True);writer.fault=fault;
                var script=BossMissionScript.Load(start);var guard=0;var beforeGold=0;var beforeKills=0;var bossId=0;
                while(s.State.Phase==BattlePhase.Combat&&guard++<20000)
                {
                    beforeGold=s.State.Gold;beforeKills=s.State.Kills;
                    var boss=s.State.Enemies.FirstOrDefault(e=>e.Definition.Id.Value=="rift-warden");if(boss!=null)bossId=boss.Id.Value;
                    BossNaturalSmokeTests.Clear(s);BossNaturalSmokeTests.TryCast(s,new BossNaturalSmokeTests.Result(),script);
                    if(s.State.Phase==BattlePhase.Combat)s.Step();
                }
                Assert.That(guard,Is.LessThan(20000));Assert.That(s.State.Phase,Is.EqualTo(BattlePhase.Victory));
                Assert.That(bossId,Is.GreaterThan(0));Assert.That(s.DamageEvents.Any(d=>d.WasLethal&&d.TargetEnemyId.Value==bossId),Is.True);
                Assert.That(s.State.Gold-beforeGold,Is.EqualTo(50+20));Assert.That(s.State.Kills-beforeKills,Is.EqualTo(1));
                var whole=BattleAbilityTests.FullSnapshot(s);var expected=Wire(s);var writes=writer.writes;
                LogAssert.Expect(LogType.Error,new System.Text.RegularExpressions.Regex("Checkpoint.*failed:"));
                Assert.That(controller.ObserveCommandCompletion(),Is.False);Assert.That(controller.LastSaveSucceeded,Is.False);
                Assert.That(writer.writes,Is.EqualTo(writes+1));Assert.That(File.ReadAllBytes(store.PrimaryPath),Is.EqualTo(primary));Assert.That(File.ReadAllBytes(store.BackupPath),Is.EqualTo(backup));
                Assert.That(BattleAbilityTests.FullSnapshot(s),Is.EqualTo(whole));
                Assert.That(controller.ObserveCommandCompletion(),Is.True);Assert.That(writer.writes,Is.EqualTo(writes+1));Assert.That(controller.LastSaveSucceeded,Is.False);
                writer.fault=-1;Assert.That(controller.RetrySave(),Is.True);Assert.That(controller.RetrySave(),Is.True);
                Assert.That(BattleAbilityTests.FullSnapshot(s),Is.EqualTo(whole));Assert.That(Wire(s),Is.EqualTo(expected));Frozen(s);
                var durable=File.ReadAllBytes(store.PrimaryPath);var durableBackup=File.ReadAllBytes(store.BackupPath);
                Assert.That(store.TryLoad(Final,config.CreateSimulationForCheckpoint,out var loaded),Is.True,loaded.Error);
                Assert.That(WaveCheckpointJson.Serialize(loaded.Envelope),Is.EqualTo(expected));Assert.That(File.ReadAllBytes(store.PrimaryPath),Is.EqualTo(durable));Assert.That(File.ReadAllBytes(store.BackupPath),Is.EqualTo(durableBackup));
                var output=BossCandidateMatrixTests.Output+"/persistence/"+start+"-fault"+fault;Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output+".json",expected);
                File.WriteAllText(output+".audit.txt",$"natural NewGame150/20; boss kill reward delta70; kills delta1; fault={fault}; failed save live preserved; primary/backup unchanged; Retry twice whole snapshot unchanged; writes={writer.writes}; commits={writer.commits}; terminal commands frozen\n");
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
    }
}
