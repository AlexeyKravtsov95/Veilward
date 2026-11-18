using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Veilward.Domain;
using Veilward.Infrastructure;
using Veilward.Presentation;
namespace Veilward.Tests.EditMode
{
    public sealed class BossPersistenceTests
    {
        private string _root; private WaveCheckpointStore _store;
        private const string Final=WaveCheckpointCompatibility.AbilityBossContentId;
        [SetUp] public void Setup(){_root=Path.Combine("TestResults","BossStorage",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_root);_store=new WaveCheckpointStore(_root);}
        [TearDown] public void Cleanup(){if(Directory.Exists(_root))Directory.Delete(_root,true);}
        private static WaveCheckpointEnvelope Initial(string id)
        {
            var old=UnityEditor.AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWave.asset");
            var s=id==Final?BossWaveTests.Config().CreateSimulation():id==WaveCheckpointCompatibility.NativeContentId?old.CreateSimulation():old.CreateHistoricalAllOpenSimulation();
            return new WaveCheckpointEnvelope{schemaVersion=4,scenarioContentId=id,state=s.ExportCheckpoint()};
        }
        [TestCase(Final,Final)] [TestCase(Final,"eight-wave-unlocks-v1")] [TestCase(Final,"eight-wave-v1")]
        [TestCase("eight-wave-unlocks-v1",Final)] [TestCase("eight-wave-v1",Final)]
        [TestCase("eight-wave-unlocks-v1","eight-wave-v1")] [TestCase("eight-wave-v1","eight-wave-unlocks-v1")]
        public void RegBattle212_MixedSlotsNormalizeRouteWholeNoWriteAndSavePreservesValidPrimary(string primary,string backup)
        {
            var a=Initial(primary);var b=Initial(backup);var factory=BossWaveTests.Config();
            File.WriteAllText(_store.PrimaryPath,WaveCheckpointJson.Serialize(a));File.WriteAllText(_store.BackupPath,WaveCheckpointJson.Serialize(b));
            var bytes=File.ReadAllBytes(_store.PrimaryPath);var backupBytes=File.ReadAllBytes(_store.BackupPath);
            Assert.That(_store.TryLoad(Final,factory.CreateSimulationForCheckpoint,out var result),Is.True,result.Error);
            Assert.That(result.Envelope.scenarioContentId,Is.EqualTo(primary));
            var live=factory.CreateSimulationForCheckpoint(result.Envelope);live.RestoreCheckpoint(result.Envelope.state);
            Assert.That(live.AbilityDefinition!=null,Is.EqualTo(primary==Final));
            Assert.That(live.Waves[7].Schedule.Last().Enemy.Id.Value,Is.EqualTo(primary==Final?"rift-warden":"shade"));
            Assert.That(File.ReadAllBytes(_store.PrimaryPath),Is.EqualTo(bytes));Assert.That(File.ReadAllBytes(_store.BackupPath),Is.EqualTo(backupBytes));
            Assert.That(WaveCheckpointCompatibility.TryNormalize(a,Final,out var normalized,out _),Is.True);
            Assert.That(WaveCheckpointJson.Serialize(normalized),Is.EqualTo(WaveCheckpointJson.Serialize(a)));
            var next=factory.CreateSimulationForCheckpoint(b);
            Assert.That(_store.TrySave(b,next.TryValidateCheckpoint,out var error,factory.CreateSimulationForCheckpoint),Is.True,error);
            Assert.That(File.ReadAllBytes(_store.BackupPath),Is.EqualTo(bytes),"valid previous primary must become backup using its own factory");
            File.WriteAllText(_store.PrimaryPath,"{}");backupBytes=File.ReadAllBytes(_store.BackupPath);
            Assert.That(_store.TryLoad(Final,factory.CreateSimulationForCheckpoint,out result),Is.True,result.Error);
            Assert.That(result.Status,Is.EqualTo(WaveCheckpointLoadStatus.LoadedBackup));
            Assert.That(_store.TrySave(b,next.TryValidateCheckpoint,out error,factory.CreateSimulationForCheckpoint),Is.True,error);
            Assert.That(File.ReadAllBytes(_store.BackupPath),Is.EqualTo(backupBytes),"invalid primary never evicts valid backup");
        }
        [TestCase(1)] [TestCase(2)] [TestCase(3)] public void RegBattle212_OldSchemaFinalIdRejected(int schema)
        {
            var e=Initial(Final);e.schemaVersion=schema;File.WriteAllText(_store.PrimaryPath,WaveCheckpointJson.Serialize(e));
            var bytes=File.ReadAllBytes(_store.PrimaryPath);Assert.That(_store.TryLoad(Final,BossWaveTests.Config().CreateSimulationForCheckpoint,out _),Is.False);
            Assert.That(File.ReadAllBytes(_store.PrimaryPath),Is.EqualTo(bytes));
        }
        [TestCaseSource(typeof(AbilityCheckpointStorageTests),nameof(AbilityCheckpointStorageTests.Malformed))]
        public void RegBattle212_FinalMalformedAbilityFallsBackWholeWithoutWrite(string field,string value)
        {
            var e=Initial(Final);var json=WaveCheckpointJson.Serialize(e);var good=json;
            var key="\""+field+"\":";
            if(value=="missing")json=json.Replace(key,"\"absent_"+field+"\":");
            else if(field=="extra")json=json.Replace("\"ability\":{","\"ability\":{\"targets\":1,");
            else if(field=="duplicate")json=json.Replace("\"nextCastSequence\":1","\"nextCastSequence\":1,\"nextCastSequence\":1");
            else if(field=="ability")json=json.Replace("\"ability\":{\"rulesId\":\"rift-seal-v1\",\"abilityId\":\"rift-seal\",\"cooldownRemainingTicks\":0,\"nextCastSequence\":1}","\"ability\":null");
            else{var old=field=="rulesId"?"\"rift-seal-v1\"":field=="abilityId"?"\"rift-seal\"":field=="nextCastSequence"?"1":"0";json=json.Replace(key+old,key+value);}
            Assert.That(json,Is.Not.EqualTo(good));File.WriteAllText(_store.PrimaryPath,json);
            Assert.That(_store.TryLoad(Final,BossWaveTests.Config().CreateSimulationForCheckpoint,out _),Is.False);
            File.WriteAllText(_store.BackupPath,good);Assert.That(_store.TryLoad(Final,BossWaveTests.Config().CreateSimulationForCheckpoint,out var load),Is.True,load.Error);
            Assert.That(load.Status,Is.EqualTo(WaveCheckpointLoadStatus.LoadedBackup));Assert.That(File.ReadAllText(_store.PrimaryPath),Is.EqualTo(json));Assert.That(File.ReadAllText(_store.BackupPath),Is.EqualTo(good));
        }
        [TestCase("legacy-all-open-v1",true)] [TestCase("wave-unlocks-v1",false)]
        public void RegBattle212_WrongRulesRejectBeforeRestore(string rules,bool enabled)
        {
            var s=BossWaveTests.Config().CreateSimulation();var e=Initial(Final);e.state.elementProgression.rulesId=rules;
            if(!enabled)e.state.ability=AbilityCheckpoint.Disabled();
            Assert.That(WaveCheckpointCompatibility.TryNormalize(e,Final,out _,out _),Is.False);
            var before=BattleAbilityTests.FullSnapshot(s);Assert.Throws<ArgumentException>(()=>s.RestoreCheckpoint(e.state));Assert.That(BattleAbilityTests.FullSnapshot(s),Is.EqualTo(before));
        }
        [Test] public void RegBattle212_Combat8WholeRollbackAndRejectedDirtyRestore()
        {
            var s=BossNaturalSmokeTests.Run("flame",false,out _,8);var prep=BossNaturalSmokeTests.Envelope(s);var json=WaveCheckpointJson.Serialize(prep);
            Assert.That(_store.TrySave(prep,s.TryValidateCheckpoint,out var error,BossWaveTests.Config().CreateSimulationForCheckpoint),Is.True,error);
            string first=null;
            for(var attempt=0;attempt<2;attempt++)
            {
                Assert.That(s.StartWave(),Is.EqualTo(WaveTransitionResult.Started));
                for(var i=0;i<400&&s.State.Phase==BattlePhase.Combat;i++){BossNaturalSmokeTests.TryCast(s,new BossNaturalSmokeTests.Result());s.Step();}
                Assert.That(s.State.Phase,Is.EqualTo(BattlePhase.Combat));Assert.That(s.State.Enemies.Any(e=>e.Definition.Id.Value=="rift-warden"),Is.True);
                Assert.That(s.AbilityEvents.Count,Is.GreaterThan(0));
                Assert.That(s.DamageEvents.Any(e=>e.Origin==DamageOrigin.Ability&&e.AppliedHpLoss>0),Is.True);
                Assert.That(s.State.Kills,Is.GreaterThan(prep.state.kills));
                Assert.That(s.ReactionEvents.Any(r=>s.DamageEvents.Any(d=>d.Origin==DamageOrigin.Reaction&&d.Tick==r.Tick&&d.ReactionSequenceInTick==r.SequenceInTick&&d.AppliedHpLoss>0)),Is.True,"rollback prefix must include positive reaction");
                var dirty=BattleAbilityTests.FullSnapshot(s);if(first==null)first=dirty;else Assert.That(dirty,Is.EqualTo(first));
                var invalid=prep.state.DeepCopy();invalid.ability=new AbilityCheckpoint("rift-seal-v1","rift-seal",601,2);
                Assert.Throws<ArgumentException>(()=>s.RestoreCheckpoint(invalid));Assert.That(BattleAbilityTests.FullSnapshot(s),Is.EqualTo(dirty));
                Assert.That(_store.TryLoad(Final,BossWaveTests.Config().CreateSimulationForCheckpoint,out var loaded),Is.True);
                s.RestoreCheckpoint(loaded.Envelope.state);Assert.That(WaveCheckpointJson.Serialize(BossNaturalSmokeTests.Envelope(s)),Is.EqualTo(json));
                Assert.That(s.State.Enemies,Is.Empty);Assert.That(s.DamageEvents,Is.Empty);Assert.That(s.AbilityEvents,Is.Empty);Assert.That(s.ReactionEvents,Is.Empty);Assert.That(s.AttackEvents,Is.Empty);
            }
            var fresh=BossWaveTests.Config().CreateSimulationForCheckpoint(prep);fresh.RestoreCheckpoint(prep.state);
            Assert.That(WaveCheckpointJson.Serialize(BossNaturalSmokeTests.Envelope(fresh)),Is.EqualTo(json));
        }
        private sealed class FaultWriter:IWaveCheckpointWriter
        {
            public int fault; private readonly FileWaveCheckpointWriter files=new FileWaveCheckpointWriter();
            public void WriteTemporary(string path,string json){if(fault==0)throw new IOException("injected");files.WriteTemporary(path,fault==1?"{}":json);}
            public void Commit(string tmp,string primary,string backup){if(fault==2)throw new IOException("injected");files.Commit(tmp,primary,backup);}
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] public void RegBattle212_FinalFailedSaveRetryAndResetRecovery(int fault)
        {
            var s=BossWaveTests.Config().CreateSimulation();var e=BossNaturalSmokeTests.Envelope(s);var factory=BossWaveTests.Config();
            Assert.That(_store.TrySave(e,s.TryValidateCheckpoint,out _,factory.CreateSimulationForCheckpoint),Is.True);
            File.Copy(_store.PrimaryPath,_store.BackupPath);var bytes=File.ReadAllBytes(_store.PrimaryPath);
            s.ChooseElement(new ChooseElementCommand("start-resonance","flame"));
            var writer=new FaultWriter{fault=fault};var faulty=new WaveCheckpointStore(_root,writer);
            var go=new GameObject("final persistence fixture");var controller=go.AddComponent<BattleCheckpointController>();
            try
            {
                controller.Configure(faulty,s,Final,factory.CreateSimulationForCheckpoint);LogAssert.Expect(LogType.Error,new System.Text.RegularExpressions.Regex("Checkpoint.*failed:"));
                Assert.That(controller.SaveCurrentState(),Is.False);Assert.That(File.ReadAllBytes(_store.PrimaryPath),Is.EqualTo(bytes));Assert.That(File.ReadAllBytes(_store.BackupPath),Is.EqualTo(bytes));
                var snapshot=WaveCheckpointJson.Serialize(BossNaturalSmokeTests.Envelope(s));writer.fault=-1;
                Assert.That(controller.RetrySave(),Is.True);Assert.That(WaveCheckpointJson.Serialize(BossNaturalSmokeTests.Envelope(s)),Is.EqualTo(snapshot));
                Assert.That(_store.TryLoad(Final,factory.CreateSimulationForCheckpoint,out var load),Is.True);Assert.That(WaveCheckpointJson.Serialize(load.Envelope),Is.EqualTo(snapshot));
                File.Delete(_store.BackupPath);Directory.CreateDirectory(_store.BackupPath);var durable=File.ReadAllBytes(_store.PrimaryPath);
                Assert.That(_store.Delete(out _),Is.False);Assert.That(File.ReadAllBytes(_store.PrimaryPath),Is.EqualTo(durable));Directory.Delete(_store.BackupPath);
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }

        [TestCase("flame")] [TestCase("gale")]
        public void RegBattle212_AllReachedNativeSafeBoundariesDeepRoundTrip(string start)
        {
            BossNaturalSmokeTests.Run(start,false,out var report);
            var initial=BossWaveTests.Config().CreateSimulation();RoundTrip(BossNaturalSmokeTests.Envelope(initial));
            initial.ChooseElement(new ChooseElementCommand("start-resonance",start));RoundTrip(BossNaturalSmokeTests.Envelope(initial));
            Assert.That(report.waves.Count,Is.EqualTo(8));
            foreach(var envelope in report.waves)
            {
                RoundTrip(envelope);var s=BossWaveTests.Config().CreateSimulationForCheckpoint(envelope);s.RestoreCheckpoint(envelope.state);
                if(s.State.ElementProgression.PendingChoiceId!="none")
                {s.ChooseElement(new ChooseElementCommand(s.State.ElementProgression.PendingChoiceId,s.State.WaveIndex==2?"stone":"tide"));RoundTrip(BossNaturalSmokeTests.Envelope(s));}
                if(s.State.Phase==BattlePhase.WaveComplete){s.ContinueToNextWave();RoundTrip(BossNaturalSmokeTests.Envelope(s));}
            }
        }
        private static void RoundTrip(WaveCheckpointEnvelope e)
        {
            var expected=WaveCheckpointJson.Serialize(e);var s=BossWaveTests.Config().CreateSimulationForCheckpoint(e);s.RestoreCheckpoint(e.state.DeepCopy());
            Assert.That(WaveCheckpointJson.Serialize(BossNaturalSmokeTests.Envelope(s)),Is.EqualTo(expected));Assert.That(s.State.Enemies,Is.Empty);
        }
    }
}
