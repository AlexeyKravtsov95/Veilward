using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Veilward.Domain;
using Veilward.Tests.Shared;
namespace Veilward.Tests.EditMode
{
    public sealed class AbilityBossRollbackRouteTests
    {
        // Отдельное построение rollback control, не новый balance-search или доказательство Victory.
        [TestCase("flame")] [TestCase("gale")]
        public void RegBattle215_ConstructLegalLateCastControl(string start)
        {
            var seed=JsonUtility.FromJson<BossMissionScript>(File.ReadAllText("Docs/review/P2.4e/revision-3/"+start+"-coverage.commands.json"));
            var script=JsonUtility.FromJson<BossMissionScript>(JsonUtility.ToJson(seed));
            var commands=script.commands.ToList();
            if(start=="gale")
            {
                commands.RemoveAll(c=>c.wave==8);
                commands.Insert(commands.FindIndex(c=>c.wave==7),new BossMissionCommand{wave=7,op="sell",type="storm",id=3});
                commands.Add(new BossMissionCommand{wave=8,op="place",type="steam",x=1,y=1});
            }
            script.holdWave7=false;script.commands=commands.ToArray();script.casts=seed.casts.Where(c=>c.wave<6).ToArray();
            var run=new AbilityBossTrace.Run(BossWaveTests.Config().CreateSimulation(),script);run.Choose(start);var casts=new List<BossMissionCast>();
            while(run.sim.State.Phase==BattlePhase.Preparation)
            {
                var wave=run.sim.State.WaveIndex+1;foreach(var c in script.commands.Where(c=>c.wave==wave))run.Execute(c);
                run.Start();var policy=new BossMissionScript{start=start,threshold=100,earlyWave8=true};
                while(run.sim.State.Phase==BattlePhase.Combat)
                {
                    var s=run.sim;PathPosition center;
                    var cast=wave<7?script.TryCenter(s,out center):policy.TryCenter(s,out center);
                    if(cast&&(wave<7||s.WaveElapsedTicks>0))
                    {casts.Add(new BossMissionCast{wave=wave,relativeTick=s.WaveElapsedTicks,x=center.X,y=center.Y});run.Cast(center);}
                    if(s.State.Phase==BattlePhase.Combat)run.Step();
                }
                run.FinishWave();if(run.sim.State.Phase!=BattlePhase.WaveComplete)break;
                if(run.sim.State.ElementProgression.HasPendingChoice)run.Choose(wave==3?script.after3:script.after6);
                run.sim.ContinueToNextWave();run.Capture("Next");
            }
            // Для поздних controls Cast выполняется после первого Step; исходные ранние boundaries сохранены.
            run.report.outcome=run.sim.State.Phase.ToString();script.casts=casts.ToArray();var output=AbilityBossTrace.Output+"/rollback-controls";Directory.CreateDirectory(output);
            File.WriteAllText(output+"/"+start+".commands.json",JsonUtility.ToJson(script,true));
            AbilityBossTrace.Write(output+"/"+start,run);
            Assert.That(run.report.waves.Count,Is.EqualTo(8));Assert.That(run.report.abilityKills,Is.GreaterThan(0));
        }
    }
}
