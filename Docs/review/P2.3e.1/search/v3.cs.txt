using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Veilward.Domain;
using Script = Veilward.Tests.EditMode.NativeEightWaveReplayTests.Script;
using Command = Veilward.Tests.EditMode.NativeEightWaveReplayTests.Command;

namespace Veilward.Tests.EditMode
{
    // Изолированная test-only эвристика; checkpoints получены только настоящими командами.
    public sealed class NativeStrategySearchTests
    {
        public const string Output = "TestResults/P2.3e.1/search";
        private const int Width = 24;
        private sealed class Node
        {
            public BattleCheckpoint preparation, end;
            public List<Command> commands;
            public int mask, priorMask;
            public long order;
        }
        private sealed class Counters
        {
            public long simulated, ticks, generated, duplicate, illegal, defeated, capPruned, beamPruned;
            public readonly Dictionary<string,long> evaluated = new Dictionary<string,long>();
        }
        private Counters counts = new Counters();
        private long serial;
        private readonly List<string> log = new List<string>();
        [TestCase("A", false)] [TestCase("B", false)]
        [TestCase("A", true)] [TestCase("B", true)]
        public void RegBattle188_BoundedLegalSearch(string route, bool coverage)
        {
            var full = Environment.GetEnvironmentVariable("P23E1_SEARCH") == "1";
            Search(route, coverage, full ? 16000 : 128, full ? 8 : 1);
        }
        private static BattleSimulation Fresh(BattleCheckpoint checkpoint = null)
        {
            var s = NativeEightWaveReplayTests.Config().CreateSimulation();
            if (checkpoint != null) s.RestoreCheckpoint(checkpoint);
            return s;
        }
        public static IEnumerable<GridCoordinate> Cells(BattleSimulation s)
        {
            var m = s.Grid.Map;
            for (var y = m.Origin.Y; y < m.Origin.Y + m.Height; y++)
                for (var x = m.Origin.X; x < m.Origin.X + m.Width; x++)
                {
                    var c = new GridCoordinate(x, y);
                    if (s.Grid.GetState(c) == GridCellState.Available) yield return c;
                }
        }
        private static bool Apply(BattleSimulation s, Command c)
        {
            if (c.op == "place") return s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId(c.type), new GridCoordinate(c.x,c.y))).Success;
            if (c.op == "upgrade") return s.UpgradeTower(new TowerId(c.id)).Success;
            return s.SellTower(new TowerId(c.id)).Success;
        }
        private static IEnumerable<Command[]> Moves(BattleSimulation s, int wave, bool relocation)
        {
            var types = s.TowerCatalog.Archetypes.Where(t => t.ShowInCatalog && t.RequiredElements.All(e => s.State.ElementProgression.UnlockedElements.Contains(e))).OrderBy(t => t.Id.Value, StringComparer.Ordinal).ToArray();
            yield return Array.Empty<Command>();
            foreach (var t in s.State.Towers)
                if (t.Level < 3 && types.Single(a => a.Id == t.Definition.Id).Levels[t.Level].Price <= s.State.Gold)
                    yield return new[] { new Command { wave=wave, op="upgrade", id=t.Id.Value, type=t.Definition.Id.Value } };
            foreach (var type in types)
                if (type.Levels[0].Price <= s.State.Gold)
                    foreach (var c in Cells(s)) yield return new[] { new Command { wave=wave, op="place", type=type.Id.Value, x=c.X, y=c.Y } };
            foreach (var t in s.State.Towers)
            {
                var sell = new Command {wave=wave, op="sell", id=t.Id.Value, type=t.Definition.Id.Value};
                yield return new[] { sell };
                if (!relocation) continue;
                var sold = Fresh(s.ExportCheckpoint()); Assert.That(Apply(sold,sell), Is.True);
                foreach (var type in types)
                    if (type.Levels[0].Price <= sold.State.Gold)
                        foreach (var c in Cells(sold))
                            yield return new[] { sell, new Command {wave=wave, op="place", type=type.Id.Value, x=c.X,y=c.Y} };
            }
        }
        private List<Node> Rank(IEnumerable<Node> nodes, bool coverage)
        {
            // Экономика учитывает Gold и сохранённый investment, не количество reaction events.
            return nodes.OrderByDescending(n => n.end.phase != BattlePhase.Defeat)
                .ThenByDescending(n => coverage ? Bits(n.mask) : 0)
                .ThenByDescending(n => n.end.baseHealth)
                .ThenByDescending(n => n.end.gold + n.end.towers.Sum(t => t.investedGold))
                .ThenByDescending(n => n.end.kills).ThenBy(n => n.order).ToList();
        }
        private List<Node> Retain(IEnumerable<Node> nodes, bool coverage, int size)
        {
            var all=nodes.ToList(); var survival=Rank(all,false); var reactions=Rank(all,true);
            // Сохраняем также иные размеры/уровни армии: single L2 не должен исчезнуть из-за two L1 на предыдущей волне.
            var profiles=survival.GroupBy(n=>n.end.towers.Length+":"+n.end.towers.Sum(t=>t.level)+":"+string.Join(",",n.end.towers.Select(t=>t.towerTypeId).Distinct().OrderBy(x=>x,StringComparer.Ordinal)))
                .Select(g=>g.First()).ToList();
            var selected=survival.Take(coverage?size/3:size/2).ToList();
            if(coverage)selected.AddRange(reactions.Where(n=>!selected.Contains(n)).Take(size/3));
            selected.AddRange(profiles.Where(n=>!selected.Contains(n)).Take(size-selected.Count));
            return selected.Concat(coverage?reactions:survival).Distinct().Take(size).ToList();
        }
        private static int Bits(int mask) { var n=0; while(mask!=0) { n+=mask&1; mask>>=1; } return n; }
        private void Search(string route, bool coverage, int cap, int waves)
        {
            Directory.CreateDirectory(Output); counts = new Counters(); serial=0; log.Clear();
            var clock=Stopwatch.StartNew(); var s=Fresh(); var start=route=="A"?"flame":"gale";
            Assert.That(s.State.Gold,Is.EqualTo(150)); Assert.That(s.State.BaseHealth,Is.EqualTo(20));
            Assert.That(s.ChooseElement(new ChooseElementCommand("start-resonance",start)).Success,Is.True);
            log.Add("AvailableCells="+string.Join(";",Cells(s).Select(c=>c.X+","+c.Y)));
            var beam=new List<Node>{new Node{end=s.ExportCheckpoint(),commands=new List<Command>()}};
            Node best=null;
            for(var wave=1;wave<=waves && beam.Count>0;wave++)
            {
                var candidates=new List<Node>(); var seen=new HashSet<string>(); var before=counts.simulated;
                for(var depth=0;depth<2;depth++)
                {
                    var parents=depth==0?beam:Retain(candidates.Where(n=>n.end.phase!=BattlePhase.Defeat),coverage,8);
                    var streams=new List<IEnumerator<(Node,Command[])>>();
                    foreach(var parent in parents)
                    {
                        var prep=Fresh(depth==0?parent.end:parent.preparation);
                        if(depth==0 && prep.State.Phase==BattlePhase.WaveComplete)
                        {
                            if(prep.State.ElementProgression.PendingChoiceId!="none") Assert.That(prep.ChooseElement(new ChooseElementCommand(prep.State.ElementProgression.PendingChoiceId,wave==4?"tide":route=="A"?"gale":"flame")).Success,Is.True);
                            Assert.That(prep.ContinueToNextWave(),Is.EqualTo(WaveTransitionResult.Continued));
                        }
                        var parentPrep=new Node{preparation=prep.ExportCheckpoint(),commands=parent.commands,mask=depth==0?parent.mask:parent.priorMask};
                        streams.Add(Moves(prep,wave,depth==0).Select(m=>(parentPrep,m)).GetEnumerator());
                    }
                    var depthLimit=depth==0?cap*3/4:cap;
                    while(streams.Count>0 && counts.simulated-before<depthLimit)
                    {
                        for(var i=0;i<streams.Count && counts.simulated-before<depthLimit;)
                        {
                            var iterator=streams[i]; if(!iterator.MoveNext()){iterator.Dispose(); streams.RemoveAt(i); continue;}
                            var parent=iterator.Current.Item1; var moves=iterator.Current.Item2; counts.generated++;
                            var branch=Fresh(parent.preparation); bool legal=true;
                            foreach(var move in moves) if(!Apply(branch,move)){legal=false; break;}
                            if(!legal){counts.illegal++; i++; continue;}
                            var checkpoint=branch.ExportCheckpoint();
                            // Учитывается полный checkpoint, включая IDs/cooldown, и уже полученное coverage.
                            var key=JsonUtility.ToJson(checkpoint)+"|"+parent.mask;
                            if(!seen.Add(key)){counts.duplicate++; i++; continue;}
                            var mask=parent.mask;
                            Assert.That(branch.StartWave(),Is.EqualTo(WaveTransitionResult.Started));
                            var ticks=0;
                            while(branch.State.Phase==BattlePhase.Combat && ticks++<20000)
                            {
                                branch.Step();
                                foreach(var e in branch.ReactionEvents)
                                    if(branch.DamageEvents.Any(d=>d.ReactionSequenceInTick==e.SequenceInTick && d.ReactionKind==e.Kind && d.AppliedHpLoss>0))
                                        mask |= 1 << ((int)e.Kind*2+(e.TriggerKind==ReactionTriggerKind.Intrinsic?1:0));
                                NativeEightWaveReplayTests.Clear(branch);
                            }
                            Assert.That(ticks,Is.LessThan(20000)); counts.ticks+=ticks; counts.simulated++;
                            var category=moves.Length==0?"hold":string.Join("+",moves.Select(m=>m.op));
                            if(!counts.evaluated.ContainsKey(category))counts.evaluated[category]=0;
                            counts.evaluated[category]++;
                            if(branch.State.Phase==BattlePhase.Defeat)counts.defeated++;
                            var node=new Node{preparation=checkpoint,end=branch.ExportCheckpoint(),commands=parent.commands.Concat(moves).ToList(),mask=mask,priorMask=parent.mask,order=serial++};
                            candidates.Add(node); i++;
                        }
                    }
                    if(streams.Count>0)counts.capPruned+=streams.Count;
                    foreach(var iterator in streams)iterator.Dispose();
                }
                var ranked=Rank(candidates,coverage); if(ranked.Count==0)break;
                best=ranked[0];
                var survivors=ranked.Where(n=>n.end.phase==BattlePhase.WaveComplete).ToList();
                beam=Retain(survivors,coverage,Width); counts.beamPruned+=Math.Max(0,survivors.Count-Width);
                log.Add("wave="+wave+" simulated="+(counts.simulated-before)+" candidates="+candidates.Count+" survivors="+survivors.Count+" best="+JsonUtility.ToJson(best.end)+" positiveMask="+best.mask);
                File.WriteAllLines(Output+"/"+route+"-"+coverage+".progress.txt",log);
            }
            Assert.That(best,Is.Not.Null); clock.Stop();
            var script=new Script{route=route,start=start,after3="tide",after6=route=="A"?"gale":"flame",commands=best.commands.ToArray()};
            var label=route+(coverage?"-coverage":"-survival");
            File.WriteAllText(Output+"/"+label+".commands.json",JsonUtility.ToJson(script,true)+"\n");
            log.Add("FINAL label="+label+" seconds="+clock.Elapsed.TotalSeconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" simulated="+counts.simulated+" ticks="+counts.ticks+" generated="+counts.generated+" duplicate="+counts.duplicate+" illegal="+counts.illegal+" defeated="+counts.defeated+" truncatedStreams="+counts.capPruned+" beamPruned="+counts.beamPruned);
            log.Add("EVALUATED "+string.Join(" ",counts.evaluated.OrderBy(x=>x.Key,StringComparer.Ordinal).Select(x=>x.Key+"="+x.Value)));
            File.WriteAllLines(Output+"/"+label+".txt",log);
            TestContext.WriteLine(log.Last());
        }
    }
}
