using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Veilward.Domain;
using Veilward.Tests.Shared;

namespace Veilward.Tests.EditMode
{
    public sealed class AbilityBossSearchTests
    {
        private sealed class Counts { public int attempts, illegal, duplicate, defeated, victories, eligible; public long ticks; }
        private readonly Dictionary<string,GridCoordinate[]> geometry=new Dictionary<string,GridCoordinate[]>();
        private readonly HashSet<string> seen=new HashSet<string>();
        private static double Exposure(BattleSimulation s,GridCoordinate c,float range)
        {
            var p=s.Grid.Map.GetCellCenter(c);var sum=0d;
            for(var d=0f;d<s.Path.TotalLength;d+=.1f){var q=s.Path.GetPosition(d);var x=p.X-q.X;var y=p.Y-q.Y;if(x*x+y*y<=range*range)sum+=.1;}
            return sum;
        }
        private GridCoordinate[] Cells(BattleSimulation s,string type,int level)
        {
            var key=type+level;
            if(!geometry.TryGetValue(key,out var cells))
            {
                var range=s.TowerCatalog.Get(new TowerDefinitionId(type)).Levels[level-1].Range;
                // Геометрический порядок статичен; занятость фильтруется отдельно после каждого command.
                cells=NativeStrategySearchTests.Cells(BossWaveTests.Config().CreateSimulation())
                    .OrderByDescending(c=>Exposure(s,c,range)).ThenBy(c=>c.Y).ThenBy(c=>c.X).ToArray();geometry.Add(key,cells);
            }
            return cells;
        }
        private BossMissionScript Candidate(string start,int index,Counts counts,out string row)
        {
            counts.attempts++;
            var random=new System.Random(4011+index*7919+(start=="flame"?0:1));
            var script=new BossMissionScript{start=start,after3="tide",after6=start=="flame"?"gale":"flame",threshold=new[]{100,200,300}[random.Next(3)],earlyWave8=random.Next(2)==1,bossDelay=random.Next(5)*60};
            var commands=new List<BossMissionCommand>();var casts=new List<BossMissionCast>();
            var s=BossWaveTests.Config().CreateSimulation();var cells=new int[4];var abilityKills=0;var bossKills=0;var bossHp=1000;
            var layout=random.Next(16);var offset=random.Next(5);var two=random.Next(2)==1;var extraWave=2+random.Next(2);var earlyUpgrade=random.Next(5)==0;
            var sellExtra=random.Next(2)==1;var rebuild=random.Next(4);var finalFlavor=random.Next(5);var hold7=random.Next(3)==0;
            bool Act(BossMissionCommand c)
            {
                c.wave=s.State.WaveIndex+1;bool ok;
                if(c.op=="place")ok=s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId(c.type),new GridCoordinate(c.x,c.y))).Success;
                else if(c.op=="sell")ok=s.SellTower(new TowerId(c.id)).Success;
                else ok=s.UpgradeTower(new TowerId(c.id)).Success;
                if(ok)commands.Add(c);return ok;
            }
            bool Place(string type)
            {
                var price=s.TowerCatalog.Get(new TowerDefinitionId(type)).Levels[0].Price;if(s.State.Gold<price)return false;
                var available=Cells(s,type,1).Where(c=>s.Grid.GetState(c)==GridCellState.Available).ToArray();if(available.Length==0)return false;
                var cell=available[(layout+Array.IndexOf(new[]{"flame","tide","gale","steam","storm"},type)*offset)%Math.Min(16,available.Length)];
                return Act(new BossMissionCommand{op="place",type=type,x=cell.X,y=cell.Y});
            }
            bool Upgrade(TowerState t)=>t.Level<3&&s.State.Gold>=s.TowerCatalog.Get(t.Definition.Id).Levels[t.Level].Price&&Act(new BossMissionCommand{op="upgrade",id=t.Id.Value,type=t.Definition.Id.Value});
            void Sell(TowerState t)=>Act(new BossMissionCommand{op="sell",id=t.Id.Value,type=t.Definition.Id.Value});
            bool Has(string type)=>s.State.Towers.Any(t=>t.Definition.Id.Value==type);
            void Observe()
            {
                foreach(var e in s.ReactionEvents)
                    if(s.DamageEvents.Any(d=>d.Origin==DamageOrigin.Reaction&&d.Tick==e.Tick&&d.ReactionSequenceInTick==e.SequenceInTick&&d.ReactionKind==e.Kind&&d.AppliedHpLoss>0))cells[(int)e.Kind*2+(int)e.TriggerKind]++;
                foreach(var d in s.DamageEvents)
                {if(d.Origin==DamageOrigin.Ability&&d.WasLethal)abilityKills++;if(d.TargetEnemyId.Value==68){bossHp=d.HealthAfter;if(d.WasLethal)bossKills++;}}
                BossNaturalSmokeTests.Clear(s);
            }
            s.ChooseElement(new ChooseElementCommand("start-resonance",start));
            while(s.State.Phase==BattlePhase.Preparation)
            {
                var wave=s.State.WaveIndex+1;
                if(wave==1){Place(start);if(two)Place(start);}
                if(wave==extraWave&&!two)Place(start);
                if(wave==3&&earlyUpgrade)Upgrade(s.State.Towers[0]);
                if(wave>=4&&wave<7)
                {if(!Has("tide"))Place("tide");var hybrid=start=="flame"?"steam":"storm";if(!Has(hybrid))Place(hybrid);}
                if(wave==7)
                {
                    var other=script.after6;var hybrid=start=="flame"?"storm":"steam";
                    if(sellExtra)foreach(var t in s.State.Towers.Where(t=>t.Definition.Id.Value==start).Skip(1).ToArray())Sell(t);
                    if(!Has(other))Place(other);if(!Has(hybrid))Place(hybrid);
                }
                if(wave==8)
                {
                    foreach(var type in new[]{"flame","tide","gale","steam","storm"})if(!Has(type))Place(type);
                    if(cells.All(c=>c>0)&&rebuild>0)
                    {
                        foreach(var t in s.State.Towers.Where(t=>t.Definition.Id.Value=="storm"||(rebuild>=2&&t.Definition.Id.Value=="flame")||(rebuild==3&&t.Definition.Id.Value=="gale")).ToArray())Sell(t);
                    }
                    var flavor=new[]{"steam","steam","gale","tide","flame"}[finalFlavor];
                    if(finalFlavor>=2)foreach(var t in s.State.Towers.Where(t=>t.Definition.Id.Value==flavor).ToArray())Upgrade(t);
                    while(Place("steam")){}
                }
                s.StartWave();
                while(s.State.Phase==BattlePhase.Combat)
                {
                    var boss=s.State.Enemies.FirstOrDefault(e=>e.Definition.Id.Value=="rift-warden");if(boss!=null)bossHp=boss.Health;
                    if(!(hold7&&wave==7)&&script.TryCenter(s,out var center))
                    {
                        var result=s.CastAbility(BossWaveTests.Cast(s,center));
                        if(!result.Succeeded){counts.illegal++;row="illegal Cast";return null;}
                        casts.Add(new BossMissionCast{wave=wave,relativeTick=s.WaveElapsedTicks,x=center.X,y=center.Y});Observe();
                    }
                    if(s.State.Phase==BattlePhase.Combat){s.Step();counts.ticks++;Observe();}
                }
                if(s.State.Phase!=BattlePhase.WaveComplete)break;
                if(s.State.ElementProgression.HasPendingChoice)s.ChooseElement(new ChooseElementCommand(s.State.ElementProgression.PendingChoiceId,wave==3?script.after3:script.after6));
                s.ContinueToNextWave();
            }
            script.commands=commands.ToArray();script.casts=casts.ToArray();
            var victory=s.State.Phase==BattlePhase.Victory;if(victory)counts.victories++;else counts.defeated++;
            var eligible=victory&&bossKills==1&&abilityKills>0&&cells.All(c=>c>0);
            if(eligible)counts.eligible++;
            row=$"{index}|{s.State.Phase}|hp={s.State.BaseHealth}|gold={s.State.Gold}|kills={s.State.Kills}|bossHP={bossHp}|bossKills={bossKills}|abilityKills={abilityKills}|cells={string.Join(",",cells)}";
            var key=JsonUtility.ToJson(script);if(!seen.Add(key))counts.duplicate++;
            return script;
        }
        [TestCase("flame")] [TestCase("gale")] [Timeout(650000)]
        public void RegBattle214_BoundedOptInSearch(string start)
        {
            var full=Environment.GetEnvironmentVariable("P24F_SEARCH")=="1";var budget=full?20000:2;var timer=Stopwatch.StartNew();var counts=new Counts();
            var output=AbilityBossTrace.Output+"/search/"+(full?"full":"smoke")+"/"+start;Directory.CreateDirectory(output);
            var rows=new List<string>();var bestHp=1001;BossMissionScript best=null;
            for(var i=0;i<budget&&timer.Elapsed.TotalSeconds<600;i++)
            {
                var before=counts.eligible;var script=Candidate(start,i,counts,out var row);rows.Add(row);
                if(script!=null)
                {
                    var hp=int.Parse(row.Split(new[]{"bossHP="},StringSplitOptions.None)[1].Split('|')[0]);
                    if(hp<bestHp){bestHp=hp;best=script;File.WriteAllText(output+"/best.commands.json",JsonUtility.ToJson(best,true));}
                    // Каждая Victory сохраняется до любого ranking/dedup/pruning.
                    if(row.Contains("|Victory|"))File.WriteAllText(output+"/victory-"+i+".commands.json",JsonUtility.ToJson(script,true));
                    if(counts.eligible>before){File.WriteAllText(output+"/winner.commands.json",JsonUtility.ToJson(script,true));break;}
                }
                if(i%250==0){File.WriteAllLines(output+"/candidates.txt",rows);File.WriteAllText(output+"/progress.txt",$"attempts={counts.attempts};ticks={counts.ticks};elapsed={timer.Elapsed.TotalSeconds};bestBossHP={bestHp}");}
            }
            File.WriteAllLines(output+"/candidates.txt",rows);
            File.WriteAllText(output+"/limits.txt",$"budget={budget};wallCapSeconds=600;attempts={counts.attempts};ticks={counts.ticks};illegal={counts.illegal};duplicate={counts.duplicate};defeated={counts.defeated};victories={counts.victories};eligible={counts.eligible};bestBossHP={bestHp};elapsedSeconds={timer.Elapsed.TotalSeconds};no shared/DTO/live edits; dedup measurement only, no pruning; geometry16, offset5, starter1/2, second2/3, starterUpgrade3, threshold100/200/300, delay0/60/120/180/240, early8,hold7,sellExtra7,rebuild4,finalFlavor5; full={full}\n");
            Assert.That(counts.attempts,Is.GreaterThan(0));
        }
    }
}
