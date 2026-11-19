using System.IO;
using System.Linq;
using NUnit.Framework;
using Veilward.Domain;
using Veilward.Tests.Shared;
namespace Veilward.Tests.EditMode
{
    public sealed class AbilityBossReplayTests
    {
        public static Veilward.Presentation.BattleScenarioConfig Config() => UnityEditor.AssetDatabase.LoadAssetAtPath<Veilward.Presentation.BattleScenarioConfig>("Assets/_Game/Configs/EightWaveAbilityBossV2.asset");
        public static AbilityBossTrace.Run Replay(string start)
        { var run=new AbilityBossTrace.Run(Config().CreateSimulation(),AbilityBossTrace.Load(start));run.Play();return run; }
        [TestCase("flame")] [TestCase("gale")]
        public void RegBattle214_SameProductionRunMustWinWithAbilityBossAndFourPositiveCells(string start)
        {
            var run=Replay(start);AbilityBossTrace.Write(AbilityBossTrace.Output+"/natural/"+start,run);
                Assert.That(run.sim.State.Phase,Is.EqualTo(BattlePhase.Victory),"REG214 requires Victory in this same coverage run");
                Assert.That(run.report.bossKills,Is.EqualTo(1));Assert.That(run.report.abilityApplied,Is.GreaterThan(0));Assert.That(run.report.abilityKills,Is.GreaterThan(0));
                foreach(var c in run.report.cells)Assert.That(c.applied,Is.GreaterThan(0),c.kind+"/"+c.trigger+" zero coverage FAIL");
        }
        [TestCase("flame")] [TestCase("gale")]
        public void RegBattle214_TwoIndependentFullTracesMatch(string start)
        {
            var first=Replay(start);var second=Replay(start);
            AbilityBossTrace.Write(AbilityBossTrace.Output+"/double/"+start+".first",first);
            AbilityBossTrace.Write(AbilityBossTrace.Output+"/double/"+start+".second",second);
            Assert.That(second.trace,Is.EqualTo(first.trace));Assert.That(second.report.traceSha256,Is.EqualTo(first.report.traceSha256));
        }

        [TestCase("flame",false)] [TestCase("gale",false)] [TestCase("flame",true)]
        public void RegBattle216_R3StoneVictoryAndLegalBossLeakRemainSeparateControls(string start,bool leak)
        {
            var script=leak?AbilityBossTrace.Load(start+"-leak"):BossMissionScript.Load(start);
            var run=new AbilityBossTrace.Run(Config().CreateSimulation(),script);run.Play();
            AbilityBossTrace.Write(AbilityBossTrace.Output+"/controls/"+start+(leak?".leak":".stone-victory"),run);
            Assert.That(run.sim.State.Phase,Is.EqualTo(leak?BattlePhase.Defeat:BattlePhase.Victory));
            Assert.That(run.report.bossKills,Is.EqualTo(leak?0:1));Assert.That(run.report.bossLeaks,Is.EqualTo(leak?1:0));
            Assert.That(run.report.abilityKills,Is.GreaterThan(0));
            if(leak){Assert.That(run.report.waves[6].hp,Is.GreaterThan(0));Assert.That(run.report.bossHpBeforeLeak,Is.GreaterThan(0));}
            Assert.That(run.report.enemies.All(e=>e.applied+(e.leaked?e.leakedHp:e.remaining)==e.maxHp),Is.True);
        }
        [Test] public void RegBattle216_Historical1000HpLegalLeakIsPreserved()
        {
            var run=new AbilityBossTrace.Run(BossWaveTests.Config().CreateSimulation(),BossMissionScript.Original("flame",true));run.Play();
            AbilityBossTrace.Write(AbilityBossTrace.Output+"/revision-3/historical-leak",run);
            Assert.That(run.report.outcome,Is.EqualTo("Defeat"));Assert.That(run.report.bossLeaks,Is.EqualTo(1));Assert.That(run.report.bossHpBeforeLeak,Is.EqualTo(276));
        }
    }
}
