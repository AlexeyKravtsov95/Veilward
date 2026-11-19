using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Veilward.Domain;
using Veilward.Tests.Shared;
namespace Veilward.Tests.EditMode
{
    public sealed class AbilityBossBalanceTests
    {
        internal static BattleSimulation Candidate(int hp)
        {
            // Definition до нового150/20; ни live state, ни checkpoint не редактируются.
            var source=BossWaveTests.Config().CreateSimulation();
            var waves=source.Waves.ToArray();
            waves[7]=new WaveDefinition(source.Waves[7].Schedule.Select(spawn=>new WaveSpawnGroup(
                spawn.Enemy.Id.Value=="rift-warden" ? new EnemyDefinition(spawn.Enemy.Id,hp,1,20,50,5) : spawn.Enemy,
                1,(int)spawn.RelativeTick,30)).ToArray());
            return new BattleSimulation(source.Grid.Map,source.Path,waves,source.TowerCatalog,20,150,20,
                source.ReactionCatalog,new ElementUnlockRules(new[]{ElementKind.Flame,ElementKind.Gale},new[]{3,6}),AbilityDefinition.RiftSeal);
        }
        internal static BossMissionScript Input(string history) => JsonUtility.FromJson<BossMissionScript>(
            File.ReadAllText("Docs/review/P2.4f/revision-2/inputs/"+history+".commands.json"));
        [TestCase("flame-tide-gale")] [TestCase("flame-gale-tide")]
        [TestCase("gale-tide-flame")] [TestCase("gale-flame-tide")]
        public void RegBattle214_BoundedHpMatrixBeforeNewGame(string history)
        {
            foreach(var hp in new[]{1000,700,650,600})
            {
                var run=new AbilityBossTrace.Run(Candidate(hp),Input(history));run.Play();
                AbilityBossTrace.Write("TestResults/P2.4f/revision-3/matrix/"+hp+"/"+history,run);
                Assert.That(run.report.abilityKills,Is.GreaterThan(0));Assert.That(run.report.cells.All(c=>c.applied>0),Is.True);
                Assert.That(run.report.outcome,Is.EqualTo(hp==600&&history!="flame-gale-tide"?"Victory":"Defeat"));
            }
        }
        [TestCase("flame-tide-gale")] [TestCase("flame-gale-tide")]
        [TestCase("gale-tide-flame")] [TestCase("gale-flame-tide")]
        public void RegBattle214_BossCastTimingSensitivity(string history)
        {
            foreach(var shift in new[]{-30,-15,15,30})
            {
                var script=Input(history);
                foreach(var c in script.casts.Where(c=>c.wave==8))c.relativeTick+=shift;
                var run=new AbilityBossTrace.Run(Candidate(600),script);run.Play();
                AbilityBossTrace.Write("TestResults/P2.4f/revision-3/timing/"+shift+"/"+history,run);
                Assert.That(run.report.abilityKills,Is.GreaterThan(0));Assert.That(run.report.cells.All(c=>c.applied>0),Is.True);
            }
            var noCast=Input(history);noCast.casts=noCast.casts.Where(c=>c.wave!=8).ToArray();
            var control=new AbilityBossTrace.Run(Candidate(600),noCast);control.Play();
            AbilityBossTrace.Write("TestResults/P2.4f/revision-3/no-cast/"+history,control);
            Assert.That(control.report.outcome,Is.EqualTo("Defeat"));Assert.That(control.report.bossLeaks,Is.EqualTo(1));
        }

        [TestCase("flame-tide-gale")] [TestCase("gale-tide-flame")]
        public void RegBattle215_BoundedWinningRollbackRoute(string history)
        {
            for(var skip=0;skip<2;skip++)for(var early=0;early<2;early++)foreach(var threshold in new[]{100,200})
                Route(history,skip==1?6:0,early==1,threshold,false,"routes/"+history+"-"+skip+"-"+early+"-"+threshold);
        }
        [TestCase("gale-tide-flame")]
        public void RegBattle215_BoundedGaleCast5Correction(string history)
        {
            for(var skip=0;skip<2;skip++)for(var early=0;early<2;early++)foreach(var threshold in new[]{100,200})
                Route(history,skip==1?5:0,early==1,threshold,false,"routes-gale-v2/"+history+"-"+skip+"-"+early+"-"+threshold);
        }
        [TestCase("flame-tide-gale")] [TestCase("gale-tide-flame")]
        public void RegBattle215_FinalWinningRoutesFromNewGame(string history) =>
            Route(history,history.StartsWith("gale")?5:6,true,100,true,"routes-final/"+history.Split('-')[0]);
        private static void Route(string history,int skipWave,bool early7,int threshold,bool afterStep,string name)
        {
            var script=Input(history);var recorded=new System.Collections.Generic.List<BossMissionCast>();
            var run=new AbilityBossTrace.Run(Candidate(600),script);run.Choose(script.start);var interrupts=new bool[8];
            while(run.sim.State.Phase==BattlePhase.Preparation)
            {
                var wave=run.sim.State.WaveIndex+1;
                foreach(var c in script.commands.Where(c=>c.wave==wave))run.Execute(c);
                run.Start();var kill=false;var reaction=false;
                while(run.sim.State.Phase==BattlePhase.Combat)
                {
                    var sim=run.sim;PathPosition center;
                    var policy=new BossMissionScript{threshold=wave==7?100:threshold,earlyWave8=true};
                    var cast=wave==8||wave==7&&early7 ? policy.TryCenter(sim,out center) : script.TryCenter(sim,out center);
                    if(wave==skipWave||afterStep&&wave>=7&&sim.WaveElapsedTicks==0)cast=false;
                    if(cast)
                    {
                        recorded.Add(new BossMissionCast{wave=wave,relativeTick=sim.WaveElapsedTicks,x=center.X,y=center.Y});
                        var before=run.report.abilityKills;run.Cast(center);kill|=run.report.abilityKills>before;
                    }
                    if(sim.State.Phase==BattlePhase.Combat)
                    {
                        var before=run.report.cells.Sum(c=>c.applied);run.Step();reaction|=run.report.cells.Sum(c=>c.applied)>before;
                        if(sim.State.Phase==BattlePhase.Combat&&sim.State.Enemies.Count>0&&kill&&reaction)interrupts[wave-1]=true;
                    }
                }
                run.FinishWave();if(run.sim.State.Phase!=BattlePhase.WaveComplete)break;
                if(run.sim.State.ElementProgression.HasPendingChoice)run.Choose(wave==3?script.after3:script.after6);
                run.sim.ContinueToNextWave();run.Capture("Next");
            }
            script.casts=recorded.ToArray();script.holdWave7=false;run.report.outcome=run.sim.State.Phase.ToString();
            var output="TestResults/P2.4f/revision-3/"+name;
            AbilityBossTrace.Write(output,run);File.WriteAllText(output+".commands.json",JsonUtility.ToJson(script,true)+"\n");
            File.WriteAllText(output+".interrupts.json",JsonUtility.ToJson(new Flags{waves=interrupts})+"\n");
            if(afterStep){Assert.That(interrupts[6]&&interrupts[7],Is.True);Assert.That(run.report.outcome,Is.EqualTo("Victory"));}
        }
        [TestCase("flame")] [TestCase("gale")]
        public void RegBattle214_WinningRouteTimingAndCenterSensitivity(string start)
        {
            foreach(var shift in new[]{-30,-15,15,30})foreach(var boss in new[]{false,true})
            {
                var script=AbilityBossTrace.Load(start);var casts=script.casts.Where(c=>c.wave==8).ToArray();
                casts[boss?casts.Length-1:0].relativeTick+=shift;
                var run=new AbilityBossTrace.Run(AbilityBossReplayTests.Config().CreateSimulation(),script);run.Play();
                AbilityBossTrace.Write("TestResults/P2.4f/revision-3/winning-timing/"+start+"-"+(boss?"boss":"first")+"-"+shift,run);
                // Ранний запланированный cast может попасть в cooldown и не исполниться; исход не подменяется PASS.
                Assert.That(run.report.cells.All(c=>c.applied>0),Is.True);
            }
            foreach(var dx in new[]{-.5f,.5f})foreach(var axis in new[]{0,1})
            {
                var script=AbilityBossTrace.Load(start);var cast=script.casts.Last();if(axis==0)cast.x+=dx;else cast.y+=dx;
                var run=new AbilityBossTrace.Run(AbilityBossReplayTests.Config().CreateSimulation(),script);run.Play();
                AbilityBossTrace.Write("TestResults/P2.4f/revision-3/winning-center/"+start+"-"+axis+"-"+dx.ToString(System.Globalization.CultureInfo.InvariantCulture),run);
                Assert.That(run.report.outcome,Is.EqualTo("Victory"));
            }
        }
        [System.Serializable] private sealed class Flags {public bool[] waves;}
    }
}
