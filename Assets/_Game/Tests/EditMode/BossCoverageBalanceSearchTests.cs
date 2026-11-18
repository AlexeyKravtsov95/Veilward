using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Veilward.Domain;
using Veilward.Tests.Shared;
namespace Veilward.Tests.EditMode
{
    public sealed class BossCoverageBalanceSearchTests
    {
        private const string Output=BossCandidateMatrixTests.Output+"/coverage-search";
        private static readonly string[] Required={"flame","tide","gale","steam","storm"};
        private static readonly Dictionary<string,double> Geometry=new Dictionary<string,double>();
        private static double Coverage(BattleSimulation s,GridCoordinate c,float range)
        {
            var key=c.X+","+c.Y+","+range;
            if(Geometry.TryGetValue(key,out var score))return score;
            var p=s.Grid.Map.GetCellCenter(c);score=0;
            for(var d=0f;d<s.Path.TotalLength;d+=.125f){var q=s.Path.GetPosition(d);var x=p.X-q.X;var y=p.Y-q.Y;if(x*x+y*y<=range*range)score+=.125;}
            Geometry[key]=score;return score;
        }
        private static BossNaturalSmokeTests.Result Plan(int hp,int armor,string start,int index,out BossMissionScript script,int rebuild=0)
        {
            // Детерминированная перестановка ограниченного пространства; никакого изменения экономики.
            var n=(index*7919)%207360;var layout=n%12;n/=12;var two=n%2==1;n/=2;var order=n%2;n/=2;
            var threshold=new[]{100,200,300}[n%3];n/=3;var delay=new[]{0,60,120,240}[n%4];n/=4;
            var hold=n%2==1;n/=2;var early=n%2==1;n/=2;var upgrade=new[]{0,3,7}[n%3];n/=3;var flavor=n%5;n/=5;
            var sellExtra=n%2==1;var cellOffset=(n/2)%3;
            var others=Required.Take(3).Where(e=>e!=start).ToArray();var policy=new BossMissionScript{start=start,after3=others[order],after6=others[1-order],threshold=threshold,bossDelay=delay,holdWave7=hold,earlyWave8=early};
            var commands=new List<BossMissionCommand>();
            Action<BattleSimulation,BossNaturalSmokeTests.Result> prep=(s,r)=>
            {
                var wave=s.State.WaveIndex+1;
                Action<BossMissionCommand> act=c=>
                {
                    c.wave=wave;bool success;
                    if(c.op=="place")success=s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId(c.type),new GridCoordinate(c.x,c.y))).Success;
                    else if(c.op=="sell")success=s.SellTower(new TowerId(c.id)).Success;
                    else success=s.UpgradeTower(new TowerId(c.id)).Success;
                    Assert.That(success,Is.True);commands.Add(c);BossNaturalSmokeTests.Record(r,s,c.op+" "+c.type+" "+c.x+","+c.y+" #"+c.id,"True");
                };
                Func<string,bool> place=type=>
                {
                    var definition=s.TowerCatalog.Get(new TowerDefinitionId(type)).Levels[0];
                    if(s.State.Gold<definition.Price)return false;
                    var cells=NativeStrategySearchTests.Cells(s).OrderByDescending(c=>Coverage(s,c,definition.Range)).ThenBy(c=>c.Y).ThenBy(c=>c.X).ToArray();
                    if(cells.Length==0)return false;
                    var offset=(layout+Array.IndexOf(Required,type)*cellOffset)%Math.Min(12,cells.Length);var c=cells[offset];
                    act(new BossMissionCommand{op="place",type=type,x=c.X,y=c.Y});return true;
                };
                if(wave==1){place(start);if(two&&s.State.Gold>=s.TowerCatalog.Get(new TowerDefinitionId(start)).Levels[0].Price)place(start);}
                var available=Required.Where(t=>s.TowerCatalog.Get(new TowerDefinitionId(t)).RequiredElements.All(e=>s.State.ElementProgression.UnlockedElements.Contains(e))).ToArray();
                if(wave>=4)
                {
                    // Сначала обеспечивает полный набор, доступный к текущей волне.
                    var missing=available.Where(t=>!s.State.Towers.Any(tower=>tower.Definition.Id.Value==t)).ToArray();
                    if(sellExtra&&wave>=7)
                        foreach(var duplicate in s.State.Towers.GroupBy(t=>t.Definition.Id.Value).SelectMany(g=>g.Skip(1)).ToArray())
                            if(missing.Length>0)act(new BossMissionCommand{op="sell",type=duplicate.Definition.Id.Value,id=duplicate.Id.Value});
                    foreach(var type in missing)place(type);
                    if(upgrade==wave)
                    {
                        var t=s.State.Towers.FirstOrDefault(t=>t.Definition.Id.Value==Required[flavor]&&t.Level<3&&s.State.Gold>=s.TowerCatalog.Get(t.Definition.Id).Levels[t.Level].Price);
                        if(t!=null)act(new BossMissionCommand{op="upgrade",type=t.Definition.Id.Value,id=t.Id.Value});
                    }
                    var missingCost=Required.Where(t=>!s.State.Towers.Any(tower=>tower.Definition.Id.Value==t)).Sum(t=>s.TowerCatalog.Get(new TowerDefinitionId(t)).Levels[0].Price);
                    var future=0;for(var w=wave;w<7;w++)future+=(4+w)*5+20;
                    var reserve=wave<7?Math.Max(0,missingCost-future):missingCost;
                    if(available.Contains(Required[flavor]))
                    {
                        var price=s.TowerCatalog.Get(new TowerDefinitionId(Required[flavor])).Levels[0].Price;
                        while(s.State.Gold-price>=reserve)if(!place(Required[flavor]))break;
                    }
                }
                if(wave==8&&rebuild>0&&BossCandidateMatrixTests.Coverage(r))
                {
                    // Coverage уже реально получен в1–7. Продажа/покупка разрешены только в Preparation8.
                    var selling=s.State.Towers.Where(t=>t.Definition.Id.Value=="storm"||(rebuild!=2&&t.Definition.Id.Value=="flame")).ToArray();
                    foreach(var t in selling)act(new BossMissionCommand{op="sell",type=t.Definition.Id.Value,id=t.Id.Value});
                    if(rebuild==3)
                    {
                        var t=s.State.Towers.FirstOrDefault(t=>t.Definition.Id.Value=="steam"&&t.Level<3&&s.State.Gold>=s.TowerCatalog.Get(t.Definition.Id).Levels[t.Level].Price);
                        if(t!=null)act(new BossMissionCommand{op="upgrade",type="steam",id=t.Id.Value});
                    }
                    while(place("steam")){}
                }
                if(wave==3&&upgrade==3)
                {
                    var t=s.State.Towers[0];var next=s.TowerCatalog.Get(t.Definition.Id).Levels[t.Level];
                    if(s.State.Gold>=next.Price)act(new BossMissionCommand{op="upgrade",type=t.Definition.Id.Value,id=t.Id.Value});
                }
            };
            BossNaturalSmokeTests.Run(start,false,out var report,0,false,()=>BossCandidateMatrixTests.Candidate(hp,armor),policy,prep);
            policy.commands=commands.ToArray();policy.casts=report.casts.ToArray();script=policy;return report;
        }
        [TestCase(1000,10,"flame")] [TestCase(1000,10,"gale")]
        [TestCase(900,10,"flame")] [TestCase(900,10,"gale")]
        [TestCase(800,10,"flame")] [TestCase(800,10,"gale")]
        [TestCase(1000,8,"flame")] [TestCase(1000,8,"gale")]
        [TestCase(1000,5,"flame")] [TestCase(1000,5,"gale")]
        [Timeout(600000)]
        public void RegBattle213_R3CoverageBalancedLayouts(int hp,int armor,string start)
        {
            var full=Environment.GetEnvironmentVariable("P24E3_SEARCH")=="1";var budget=full?3000:2;
            var rebuildPass=Environment.GetEnvironmentVariable("P24E3_REBUILD")=="1";
            var output=(rebuildPass?BossCandidateMatrixTests.Output+"/coverage-rebuild":Output)+(full?"/full":"/smoke")+"/"+hp+"-"+armor+"/"+start;Directory.CreateDirectory(output);
            var rows=new List<string>();BossNaturalSmokeTests.Result best=null;BossMissionScript bestScript=null;var wins=0;
            for(var i=0;i<budget;i++)
            {
                var r=Plan(hp,armor,start,i,out var script,rebuildPass?1+i%3:0);var mask=r.reactions.Count(c=>c.appliedHpLoss>0);var end=r.receipts.Last();
                rows.Add(i+"|"+r.phase+"|spawns="+r.bossSpawns+"|bossHP="+(r.bossSpawns==0?hp:r.bossHealthBeforeLeak)+"|kill="+r.bossKills+"|ability="+r.bossAbilityHpLoss+"|coverage="+mask+"|hp="+end.hp+"|gold="+end.gold+"|kills="+end.kills);
                var eligible=r.phase=="Victory"&&r.bossKills==1&&r.bossAbilityHpLoss>0&&BossCandidateMatrixTests.Coverage(r);
                if(eligible){wins++;BossCandidateMatrixTests.Write(output+"/winner-"+i,script,r);}
                int Rank(BossNaturalSmokeTests.Result value)=>value.phase=="Victory"?100:0;
                if(best==null||Rank(r)>Rank(best)||Rank(r)==Rank(best)&&(mask>best.reactions.Count||mask==best.reactions.Count&&r.bossHealthBeforeLeak<best.bossHealthBeforeLeak))
                {best=r;bestScript=script;}
                if(i%500==0)File.WriteAllLines(output+"/candidates.txt",rows);
            }
            BossCandidateMatrixTests.Write(output+"/best",bestScript,best);File.WriteAllLines(output+"/candidates.txt",rows);
            File.WriteAllText(output+"/limits.txt",$"attempts={budget}; eligibleWins={wins}; space207360; stride7919; layout12; starter1/2; orderedChoices2; thresholds100/200/300; delays0/60/120/240; hold7/early8; upgrade0/3/7; flavor5; sellExtra; cellOffset3; rebuildPass={rebuildPass}; modes sellStormFlame+Steam/sellStorm+Steam/sellStormFlame+UpgradeSteamThenPlace; only if4coverage before8; no dedup/pruning; all eligible Victory saved before ranking; full={full}\n");
            Assert.That(rows.Count,Is.EqualTo(budget));
        }
    }
}
