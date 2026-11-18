using System;
using System.Collections.Generic;
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
    public sealed class AbilityBossFileTests
    {
        private string root; private WaveCheckpointStore store;private GameObject go;
        [SetUp] public void Setup(){root=Path.Combine(AbilityBossTrace.Output,"files",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);store=new WaveCheckpointStore(root);go=new GameObject("P24f checkpoint");}
        [TearDown] public void Cleanup(){UnityEngine.Object.DestroyImmediate(go);if(Directory.Exists(root))Directory.Delete(root,true);}
        private static void ExpectedFailure()=>LogAssert.Expect(LogType.Error,new System.Text.RegularExpressions.Regex("Checkpoint.*failed:"));
        private static void Clean(BattleSimulation s)
        {Assert.That(s.State.Enemies,Is.Empty);Assert.That(s.AttackEvents,Is.Empty);Assert.That(s.DamageEvents,Is.Empty);Assert.That(s.ReactionEvents,Is.Empty);Assert.That(s.AbilityEvents,Is.Empty);}
        private WaveCheckpointEnvelope LoadReadOnly()
        {
            var primary=File.ReadAllBytes(store.PrimaryPath);var backup=File.Exists(store.BackupPath)?File.ReadAllBytes(store.BackupPath):null;
            WaveCheckpointEnvelope result=null;
            for(var i=0;i<2;i++) {Assert.That(store.TryLoad(WaveCheckpointCompatibility.AbilityBossContentId,BossWaveTests.Config().CreateSimulationForCheckpoint,out var loaded),Is.True,loaded.Error);result=loaded.Envelope;}
            Assert.That(File.ReadAllBytes(store.PrimaryPath),Is.EqualTo(primary));
            if(backup!=null)Assert.That(File.ReadAllBytes(store.BackupPath),Is.EqualTo(backup));return result;
        }
        [TestCase("flame")] [TestCase("gale")]
        public void RegBattle215_WholeFileContinuedTraceMatchesUninterrupted(string start)
        {
            var control=AbilityBossReplayTests.Replay(start);var run=new AbilityBossTrace.Run(BossWaveTests.Config().CreateSimulation(),AbilityBossTrace.Load(start));
            var checkpoint=go.AddComponent<BattleCheckpointController>();var matrix=new List<string>();
            run.stable=(r,boundary)=>
            {
                checkpoint.Configure(store,r.sim,WaveCheckpointCompatibility.AbilityBossContentId,BossWaveTests.Config().CreateSimulationForCheckpoint);
                Assert.That(checkpoint.SaveCurrentState(),Is.True);var expected=AbilityBossTrace.Wire(r.sim);var loaded=LoadReadOnly();
                r.sim=BossWaveTests.Config().CreateSimulationForCheckpoint(loaded);r.sim.RestoreCheckpoint(loaded.state);Clean(r.sim);
                Assert.That(AbilityBossTrace.Wire(r.sim),Is.EqualTo(expected));matrix.Add(boundary+"|"+expected);
            };
            run.Play();AbilityBossTrace.Write(AbilityBossTrace.Output+"/files/"+start+".continued",run);
            File.WriteAllLines(AbilityBossTrace.Output+"/files/"+start+".matrix.txt",matrix);
            Assert.That(run.trace,Is.EqualTo(control.trace),"Every command/tick/state/event, including ability and cast sequence");
        }
        [TestCase("flame")] [TestCase("gale")]
        public void RegBattle215_NaturalSafeMatrixFailedChooseCompletionNextRestartAndRetry(string start)
        {
            var run=new AbilityBossTrace.Run(BossWaveTests.Config().CreateSimulation(),AbilityBossTrace.Load(start));var checkpoint=go.AddComponent<BattleCheckpointController>();
            checkpoint.Configure(store,run.sim,WaveCheckpointCompatibility.AbilityBossContentId,BossWaveTests.Config().CreateSimulationForCheckpoint);
            var matrix=new List<string>();
            run.stable=(r,boundary)=>
            {
                var s=r.sim;var wave=s.State.WaveIndex+1;
                var inject=boundary=="confirmed"||boundary=="completed"||boundary=="next"&&(wave==7||wave==8);
                if(!inject){Assert.That(checkpoint.SaveCurrentState(),Is.True);return;}
                var before=AbilityBossTrace.Snapshot(s);var previous=LoadReadOnly();var previousJson=WaveCheckpointJson.Serialize(previous);
                var primary=File.ReadAllBytes(store.PrimaryPath);var backup=File.Exists(store.BackupPath)?File.ReadAllBytes(store.BackupPath):null;
                Directory.CreateDirectory(store.TempPath);ExpectedFailure();Assert.That(checkpoint.SaveCurrentState(),Is.False);
                Assert.That(AbilityBossTrace.Snapshot(s),Is.EqualTo(before));Assert.That(File.ReadAllBytes(store.PrimaryPath),Is.EqualTo(primary));
                if(backup!=null)Assert.That(File.ReadAllBytes(store.BackupPath),Is.EqualTo(backup));
                var restarted=BossWaveTests.Config().CreateSimulationForCheckpoint(previous);restarted.RestoreCheckpoint(previous.state);Clean(restarted);
                Assert.That(WaveCheckpointJson.Serialize(LoadReadOnly()),Is.EqualTo(previousJson));
                if(s.CheckStartWave()==WaveTransitionResult.Started) {ExpectedFailure();Assert.That(checkpoint.SaveForStartWave(),Is.False);}
                if(s.CheckContinueToNextWave()==WaveTransitionResult.Continued) {ExpectedFailure();Assert.That(checkpoint.SaveForNextWave(),Is.False);}
                Assert.That(AbilityBossTrace.Snapshot(s),Is.EqualTo(before));Directory.Delete(store.TempPath);
                Assert.That(checkpoint.RetrySave(),Is.True);Assert.That(checkpoint.RetrySave(),Is.True);Assert.That(AbilityBossTrace.Snapshot(s),Is.EqualTo(before));
                Assert.That(WaveCheckpointJson.Serialize(LoadReadOnly()),Is.EqualTo(WaveCheckpointJson.Serialize(AbilityBossTrace.Envelope(s))));
                matrix.Add(boundary+"|wave="+wave+"|live="+AbilityBossTrace.Wire(s)+"|restart="+previousJson+"|Retry twice whole state unchanged");
            };
            run.Play();File.WriteAllLines(AbilityBossTrace.Output+"/files/"+start+".faults.txt",matrix);
            Assert.That(matrix.Count,Is.EqualTo(13),"3 Confirm +8 completion +Next7/8");
        }
        // Намеренно испорченный DTO — только rejection probe, не источник production армии.
        [TestCase("flame",7)] [TestCase("flame",8)] [TestCase("gale",7)] [TestCase("gale",8)]
        public void RegBattle215_NaturalCombatKillReactionDirtyFreshRollbackAndTwoPrefixes(string start,int wave)
        {
            var run=new AbilityBossTrace.Run(BossWaveTests.Config().CreateSimulation(),AbilityBossTrace.Load(start));
            var checkedPrefix=false;var attempts=new List<string>();
            run.stable=(r,boundary)=>
            {
                if(checkedPrefix||boundary!="preparation"||r.sim.State.WaveIndex+1!=wave)return;
                checkedPrefix=true;var snapshot=AbilityBossTrace.Envelope(r.sim);var expected=AbilityBossTrace.Wire(r.sim);
                var controller=go.AddComponent<BattleCheckpointController>();controller.Configure(store,r.sim,WaveCheckpointCompatibility.AbilityBossContentId,BossWaveTests.Config().CreateSimulationForCheckpoint);
                Assert.That(controller.SaveCurrentState(),Is.True);var primary=File.ReadAllBytes(store.PrimaryPath);
                for(var attempt=0;attempt<2;attempt++)
                {
                    var s=r.sim;Assert.That(s.StartWave(),Is.EqualTo(WaveTransitionResult.Started));AbilityBossTrace.Clear(s);
                    var prefix=new List<string>();var abilityKill=false;var reaction=false;var guard=0;
                    while(s.State.Phase==BattlePhase.Combat&&guard++<20000)
                    {
                        if(r.script.TryCenter(s,out var center)) {Assert.That(s.CastAbility(BossWaveTests.Cast(s,center)).Succeeded,Is.True);prefix.Add("Cast|"+AbilityBossTrace.Snapshot(s));}
                        abilityKill|=s.DamageEvents.Any(d=>d.Origin==DamageOrigin.Ability&&d.WasLethal);
                        reaction|=s.DamageEvents.Any(d=>d.Origin==DamageOrigin.Reaction&&d.AppliedHpLoss>0);
                        if(abilityKill&&reaction&&s.State.Phase==BattlePhase.Combat&&s.State.Enemies.Count>0)break;
                        if(s.State.Phase==BattlePhase.Combat){s.Step();prefix.Add("Step|"+AbilityBossTrace.Snapshot(s));}
                    }
                    Assert.That(s.State.Phase,Is.EqualTo(BattlePhase.Combat),"Required interrupt stays Combat after accepted ability kill + positive reaction");
                    Assert.That(abilityKill&&reaction,Is.True);Assert.That(s.State.Enemies,Is.Not.Empty);Assert.That(s.DamageEvents,Is.Not.Empty);Assert.That(s.AbilityEvents,Is.Not.Empty);Assert.That(s.ReactionEvents,Is.Not.Empty);
                    var dirty=AbilityBossTrace.Snapshot(s);var invalid=snapshot.state.DeepCopy();invalid.ability=new AbilityCheckpoint("rift-seal-v1","rift-seal",601,2);
                    Assert.Throws<ArgumentException>(()=>s.RestoreCheckpoint(invalid));Assert.That(AbilityBossTrace.Snapshot(s),Is.EqualTo(dirty));
                    Assert.That(File.ReadAllBytes(store.PrimaryPath),Is.EqualTo(primary),"Combat cast never becomes durable separately");
                    controller.RestoreCheckpoint(LoadReadOnly());Clean(s);Assert.That(AbilityBossTrace.Wire(s),Is.EqualTo(expected));
                    var fresh=BossWaveTests.Config().CreateSimulationForCheckpoint(snapshot);fresh.RestoreCheckpoint(snapshot.state.DeepCopy());Clean(fresh);Assert.That(AbilityBossTrace.Wire(fresh),Is.EqualTo(expected));
                    attempts.Add(string.Join("\n",prefix));
                }
                Assert.That(attempts[1],Is.EqualTo(attempts[0]));
                File.WriteAllLines(AbilityBossTrace.Output+"/files/"+start+".combat"+wave+".prefixes.txt",attempts);
            };
            run.Play();Assert.That(checkedPrefix,Is.True,"route must legally reach Preparation"+wave);
            var control=AbilityBossReplayTests.Replay(start);Assert.That(run.trace,Is.EqualTo(control.trace),"whole continued replay after two discarded attempts");
            AbilityBossTrace.Write(AbilityBossTrace.Output+"/files/"+start+".rollback"+wave,run);
        }

        [TestCase("flame")] [TestCase("gale")]
        public void RegBattle215_SyntheticOrderedOldNativeAllOpenNewNativeSlotsAndCorruptFallback(string start)
        {
            var natural=BossNaturalSmokeTests.Run(start,false,out _,8,missionOverride:AbilityBossTrace.Load(start));
            Assert.That(natural.State.Phase,Is.EqualTo(BattlePhase.Preparation));Assert.That(natural.State.WaveIndex,Is.EqualTo(7));
            var old=NativeEightWaveReplayTests.Config();
            var slots=new[]{AbilityBossTrace.Envelope(natural),
                new WaveCheckpointEnvelope{schemaVersion=4,scenarioContentId=WaveCheckpointCompatibility.NativeContentId,state=old.CreateSimulation().ExportCheckpoint()},
                new WaveCheckpointEnvelope{schemaVersion=4,scenarioContentId="eight-wave-v1",state=old.CreateHistoricalAllOpenSimulation().ExportCheckpoint()}};
            var audit=new List<string>();
            foreach(var primary in slots)foreach(var backup in slots.Where(e=>e.scenarioContentId!=primary.scenarioContentId))
            {
                File.WriteAllText(store.PrimaryPath,WaveCheckpointJson.Serialize(primary));File.WriteAllText(store.BackupPath,WaveCheckpointJson.Serialize(backup));
                Assert.That(WaveCheckpointJson.Serialize(LoadReadOnly()),Is.EqualTo(WaveCheckpointJson.Serialize(primary)));
                File.WriteAllText(store.PrimaryPath,"{corrupt primary}");
                Assert.That(WaveCheckpointJson.Serialize(LoadReadOnly()),Is.EqualTo(WaveCheckpointJson.Serialize(backup)));
                audit.Add("SYNTHETIC slots "+primary.scenarioContentId+" -> "+backup.scenarioContentId+" whole fallback/no-write PASS");
            }
            File.WriteAllLines(AbilityBossTrace.Output+"/files/"+start+".synthetic-slots.txt",audit);
        }
    }
}
