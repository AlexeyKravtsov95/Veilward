using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Veilward.Tests.Shared;
namespace Veilward.Tests.EditMode
{
    public sealed class AbilityBossTargetedReplayTests
    {
        [TestCase("flame-tide-gale",376)] [TestCase("flame-gale-tide",434)]
        [TestCase("gale-tide-flame",376)] [TestCase("gale-flame-tide",369)]
        public void RegBattle214_TargetedBestLosingControlsReplayFromPublicNewGame(string history,int expectedBossHp)
        {
            var path="Docs/review/P2.4f/revision-2/inputs/"+history+".commands.json";
            var script=JsonUtility.FromJson<BossMissionScript>(File.ReadAllText(path));
            var run=new AbilityBossTrace.Run(BossWaveTests.Config().CreateSimulation(),script);run.Play();
            AbilityBossTrace.Write("TestResults/P2.4f/revision-2/fixed-replays/"+history,run);
            // Контроли сохраняют фактический Defeat. Обязательная same-run Victory REG214 не меняется.
            Assert.That(run.report.outcome,Is.EqualTo("Defeat"));Assert.That(run.report.bossKills,Is.Zero);
            Assert.That(run.report.bossHpBeforeLeak,Is.EqualTo(expectedBossHp));Assert.That(run.report.abilityKills,Is.GreaterThan(0));
            Assert.That(run.report.abilityApplied,Is.GreaterThan(0));Assert.That(run.report.cells.All(c=>c.applied>0),Is.True);
            var recorded=JsonUtility.FromJson<AbilityBossTrace.Report>(File.ReadAllText("Docs/review/P2.4f/revision-2/full-replays/best-"+history+".full.json"));
            Assert.That(JsonUtility.ToJson(run.report),Is.EqualTo(JsonUtility.ToJson(recorded)),"Whole captured run including prefix/state/accounting, not a late checkpoint grant");
        }
    }
}
