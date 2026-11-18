using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Veilward.Domain;
using Veilward.Infrastructure;

namespace Veilward.Tests.EditMode
{
    // Production150/20. Никаких edits checkpoint/HP/Gold/definitions; только команды и Step.
    public sealed class BossNaturalSmokeTests
    {
        [Serializable] public sealed class Receipt { public int wave; public long tick; public string command,result; public int hp,gold,kills,remaining; public long sequence; }
        [Serializable] public sealed class Result { public string start,phase; public int bossSpawns,bossKills,bossLeaks,bossHealthBeforeLeak,abilityHpLoss,bossAbilityHpLoss; public List<Receipt> receipts=new List<Receipt>(); public List<WaveCheckpointEnvelope> waves=new List<WaveCheckpointEnvelope>(); }
        internal static readonly GridCoordinate[] StoneCells = {new GridCoordinate(-1,0),new GridCoordinate(1,0),new GridCoordinate(-1,1),new GridCoordinate(1,1),new GridCoordinate(0,-1),new GridCoordinate(0,1)};
        internal static BattleSimulation Run(string start,bool leak,out Result report,int stopAtPreparation=0)
        {
            var s=BossWaveTests.Config().CreateSimulation();report=new Result{start=start};
            Assert.That(s.State.Gold,Is.EqualTo(150));Assert.That(s.State.BaseHealth,Is.EqualTo(20));
            Record(report,s,"Choose "+start,s.ChooseElement(new ChooseElementCommand("start-resonance",start)).FailureReason.ToString());
            var bossIds=new HashSet<int>();var deadBossIds=new HashSet<int>();
            while(s.State.Phase==BattlePhase.Preparation)
            {
                var wave=s.State.WaveIndex+1;
                Prepare(s,start,report);
                if(stopAtPreparation==wave)return s;

                Record(report,s,"Start",s.StartWave().ToString());
                var guard=0;
                while(s.State.Phase==BattlePhase.Combat&&guard++<20000)
                {
                    foreach(var enemy in s.State.Enemies)if(enemy.Definition.Id.Value=="rift-warden")bossIds.Add(enemy.Id.Value);
                    if(!leak||wave<8)TryCast(s,report);
                    Observe(s,report,bossIds,deadBossIds);Clear(s);
                    var tracked=s.State.Enemies.FirstOrDefault(e=>e.Definition.Id.Value=="rift-warden");
                    if(s.State.Phase==BattlePhase.Combat)s.Step();
                    if(tracked!=null&&tracked.IsAlive&&!s.State.Enemies.Contains(tracked)) {report.bossLeaks++;report.bossHealthBeforeLeak=tracked.Health;}

                    Observe(s,report,bossIds,deadBossIds);Clear(s);
                }
                Assert.That(guard,Is.LessThan(20000));
                Record(report,s,"Wave end",s.State.Phase.ToString());report.waves.Add(Envelope(s));
                if(s.State.ElementProgression.HasPendingChoice)
                {
                    var option=wave==3?"stone":"tide";
                    Record(report,s,"Choose "+option,s.ChooseElement(new ChooseElementCommand(s.State.ElementProgression.PendingChoiceId,option)).FailureReason.ToString());
                }
                if(s.State.Phase==BattlePhase.WaveComplete)Record(report,s,"Next",s.ContinueToNextWave().ToString());
            }
            report.phase=s.State.Phase.ToString();report.bossSpawns=bossIds.Count;report.bossKills=deadBossIds.Count;return s;
        }
        internal static WaveCheckpointEnvelope Envelope(BattleSimulation s) => new WaveCheckpointEnvelope {schemaVersion=4,scenarioContentId=WaveCheckpointCompatibility.AbilityBossContentId,state=s.ExportCheckpoint()};
        internal static void Prepare(BattleSimulation s,string start,Result r)
        {
            var wave=s.State.WaveIndex+1;
            var script=JsonUtility.FromJson<NativeEightWaveReplayTests.Script>(File.ReadAllText("Docs/review/P2.4e/"+start+".commands.json"));
            foreach(var c in script.commands.Where(c=>c.wave==wave))
            {
                if(c.op=="place")Place(s,r,c.type,c.x,c.y);
                else if(c.op=="upgrade"){var result=s.UpgradeTower(new TowerId(c.id));Record(r,s,"Upgrade "+c.id,result.Success.ToString());Assert.That(result.Success,Is.True);}
                else {var result=s.SellTower(new TowerId(c.id));Record(r,s,"Sell "+c.id,result.Success.ToString());Assert.That(result.Success,Is.True);}
            }
        }
        internal static void Place(BattleSimulation s,Result r,string type,int x,int y)
        {
            var result=s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId(type),new GridCoordinate(x,y)));
            Record(r,s,$"Place {type} {x},{y}",result.Success+"/"+result.FailureReason);
            Assert.That(result.Success,Is.True,$"natural {r.start} wave{s.State.WaveIndex+1} place{type} {x},{y}: {result.FailureReason}");
        }
        internal static void TryCast(BattleSimulation s,Result r)
        {
            if(s.AbilityState.CooldownRemainingTicks!=0||s.State.Enemies.Count==0)return;
            var boss=s.State.Enemies.FirstOrDefault(e=>e.Definition.Id.Value=="rift-warden");
            if(s.State.WaveIndex==7&&boss==null)return;
            EnemyState target=null;var best=0;
            foreach(var candidate in s.State.Enemies)
            {
                var p=s.Path.GetPosition(candidate.DistanceAlongPath);var score=0;
                foreach(var enemy in s.State.Enemies)
                {var q=s.Path.GetPosition(enemy.DistanceAlongPath);var dx=(double)p.X-q.X;var dy=(double)p.Y-q.Y;if(dx*dx+dy*dy<=2.25)score+=Math.Min(enemy.Health,120);}
                if(score>best){best=score;target=candidate;}
            }
            if(boss!=null)target=boss;
            if(target==null||(boss==null&&best<200&&target.DistanceAlongPath<s.Path.TotalLength-1))return;
            var center=s.Path.GetPosition(target.DistanceAlongPath);
            var result=s.CastAbility(BossWaveTests.Cast(s,center));
            Record(r,s,$"Cast center({center.X:R},{center.Y:R}) before next Step",result.Succeeded+"/"+result.FailureReason+" hpLoss="+result.TotalAppliedHpLoss);
            Assert.That(result.Succeeded,Is.True);
        }
        private static void Observe(BattleSimulation s,Result r,HashSet<int> bossIds,HashSet<int> dead)
        {
            foreach(var enemy in s.State.Enemies)if(enemy.Definition.Id.Value=="rift-warden")bossIds.Add(enemy.Id.Value);
            foreach(var e in s.DamageEvents)
            {
                if(e.Origin==DamageOrigin.Ability){r.abilityHpLoss+=e.AppliedHpLoss;if(bossIds.Contains(e.TargetEnemyId.Value))r.bossAbilityHpLoss+=e.AppliedHpLoss;}
                if(e.WasLethal&&bossIds.Contains(e.TargetEnemyId.Value))dead.Add(e.TargetEnemyId.Value);
            }
        }
        internal static void Clear(BattleSimulation s){s.ClearAttackEvents();s.ClearDamageEvents();s.ClearReactionEvents();s.ClearAbilityEvents();}
        internal static void Record(Result r,BattleSimulation s,string command,string result)=>r.receipts.Add(new Receipt{wave=s.State.WaveIndex+1,tick=s.State.Tick,command=command,result=result,hp=s.State.BaseHealth,gold=s.State.Gold,kills=s.State.Kills,remaining=s.AbilityState.CooldownRemainingTicks,sequence=s.AbilityState.NextCastSequence});
        [TestCase("flame",false)] [TestCase("gale",false)] [TestCase("flame",true)]
        public void RegBattle213_NaturalProductionVictoryAndDeliberateBossLeak(string start,bool leak)
        {
            var s=Run(start,leak,out var r);Directory.CreateDirectory("TestResults/P2.4e");
            File.WriteAllText("TestResults/P2.4e/"+start+(leak?"-leak":"-victory")+".json",JsonUtility.ToJson(r,true));
            File.WriteAllLines("TestResults/P2.4e/"+start+(leak?"-leak":"-victory")+".waves.jsonl",r.waves.Select(e=>WaveCheckpointJson.Serialize(e)));
            Assert.That(s.State.Phase,Is.EqualTo(leak?BattlePhase.Defeat:BattlePhase.Victory),JsonUtility.ToJson(r));
            Assert.That(r.bossLeaks,Is.EqualTo(leak?1:0));Assert.That(r.bossSpawns,Is.EqualTo(1));Assert.That(r.bossKills,Is.EqualTo(leak?0:1));Assert.That(r.abilityHpLoss,Is.GreaterThan(0));
            if(!leak)Assert.That(r.bossAbilityHpLoss,Is.GreaterThan(0));
        }
    }
}
