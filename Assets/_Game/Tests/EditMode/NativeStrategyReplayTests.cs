using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Veilward.Domain;
using Script = Veilward.Tests.EditMode.NativeEightWaveReplayTests.Script;

namespace Veilward.Tests.EditMode
{
    public sealed class NativeStrategyReplayTests
    {
        public const string Docs = "Docs/review/P2.3e.1/";
        public const string Output = "TestResults/P2.3e.1/";
        public static Script Load(string route) => JsonUtility.FromJson<Script>(File.ReadAllText(Docs+route+".commands.json"));
        [TestCase("A-survival")] [TestCase("B-survival")]
        [TestCase("A-coverage")] [TestCase("B-coverage")]
        public void RegBattle188_NewGameTwice_FullTraceAndPositiveDamage(string route)
        {
            var first=NativeEightWaveReplayTests.ReplayScript(Load(route));
            var second=NativeEightWaveReplayTests.ReplayScript(Load(route));
            Assert.That(second.trace,Is.EqualTo(first.trace));
            Assert.That(JsonUtility.ToJson(second.report),Is.EqualTo(JsonUtility.ToJson(first.report)));
            Assert.That(first.receipts.Any(r=>r.Contains("=False/")),Is.False,"Каждая запланированная economy command должна исполниться");
            Assert.That(first.sim.State.Phase,Is.EqualTo(BattlePhase.Victory));
            Assert.That(first.waves.Count,Is.EqualTo(8));
            Assert.That(first.waves[5].state.elementProgression.pendingChoiceId,Is.EqualTo("after-wave-6"));
            Assert.That(first.sim.State.ElementProgression.ChoicesSpent,Is.EqualTo(2));
            var hits=new Dictionary<string,int>(); var procs=new Dictionary<string,int>();
            var audit=new List<string>();
            // Проверяем correspondence event→damage по полному tick trace, не номинальный Damage башни.
            foreach(var line in first.trace.Where(l=>l.StartsWith("S|",StringComparison.Ordinal)))
            {
                var fields=line.Split('|'); var wave=int.Parse(fields[2])+1; var tick=long.Parse(fields[3]);
                foreach(var reaction in fields.Where(x=>x.StartsWith("R:",StringComparison.Ordinal)))
                {
                    var r=reaction.Substring(2).Split(','); var key=r[2]+"/"+r[3];
                    Assert.That(wave,Is.GreaterThanOrEqualTo(4));
                    var positive=fields.Where(x=>x.StartsWith("D:",StringComparison.Ordinal)).Select(x=>x.Substring(2).Split(','))
                        .Where(d=>d[2]=="Reaction" && d[0]==r[0] && d[3]==r[4] && d[15]==r[2] && d[16]==r[1] && int.Parse(d[12])>0).ToArray();
                    if(positive.Length==0)continue;
                    if(!hits.ContainsKey(key)){hits[key]=0;procs[key]=0;}
                    procs[key]++; hits[key]+=positive.Sum(d=>int.Parse(d[12]));
                    audit.Add("wave="+wave+" tick="+tick+" "+key+" positiveTargets="+positive.Length+" AppliedHpLoss="+positive.Sum(d=>int.Parse(d[12])));
                }
            }
            if(route.EndsWith("coverage",StringComparison.Ordinal))
                foreach(var key in new[]{"Steam/StatusPair","Steam/Intrinsic","Storm/StatusPair","Storm/Intrinsic"})
                    Assert.That(hits.ContainsKey(key)&&hits[key]>0,Is.True,key+" positive production coverage");
            Directory.CreateDirectory(Output);
            foreach(var pair in new[]{("first",first),("second",second)})
            {
                var bytes=Encoding.UTF8.GetBytes(string.Join("\n",pair.Item2.trace)+"\n");
                using(var file=File.Create(Output+route+"."+pair.Item1+".trace.gz"))
                using(var zip=new GZipStream(file,System.IO.Compression.CompressionLevel.Optimal))zip.Write(bytes,0,bytes.Length);
            }
            foreach(var key in hits.Keys.OrderBy(x=>x,StringComparer.Ordinal))audit.Add("TOTAL "+key+" positiveProcs="+procs[key]+" AppliedHpLoss="+hits[key]);
            File.WriteAllLines(Output+route+".reaction-audit.txt",audit);
            File.WriteAllText(Output+route+".summary.json",JsonUtility.ToJson(first.report,true)+"\n");
            // Golden создаётся отдельным исследовательским запуском и затем только читается тестом.
            var expected=JsonUtility.FromJson<NativeEightWaveReplayTests.Report>(File.ReadAllText(Docs+route+".summary.json"));
            Assert.That(JsonUtility.ToJson(first.report),Is.EqualTo(JsonUtility.ToJson(expected)));
        }
    }
}
