using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Veilward.Domain;
using Command=Veilward.Tests.EditMode.NativeEightWaveReplayTests.Command;
using Script=Veilward.Tests.EditMode.NativeEightWaveReplayTests.Script;
namespace Veilward.Tests.EditMode
{
    public sealed class BossGreedySearchTests
    {
        [TestCase("flame")] [TestCase("gale")]
        public void RegBattle213_GreedyPublicCommandResearch(string start)
        {
            var full=Environment.GetEnvironmentVariable("P24E_SEARCH")=="1";
            var production=BossWaveTests.Config().CreateSimulation();
            var starters=BossStrategySearchTests.Cells(production).ToArray();
            var layouts=new[]{
                new[]{new GridCoordinate(-1,0),new GridCoordinate(1,0),new GridCoordinate(-1,-1),new GridCoordinate(1,1),new GridCoordinate(-1,1),new GridCoordinate(1,-1)},
                new[]{new GridCoordinate(1,0),new GridCoordinate(-1,0),new GridCoordinate(1,1),new GridCoordinate(-1,-1),new GridCoordinate(1,-1),new GridCoordinate(-1,1)},
                new[]{new GridCoordinate(-1,-1),new GridCoordinate(1,1),new GridCoordinate(1,0),new GridCoordinate(-1,0),new GridCoordinate(-1,1),new GridCoordinate(1,-1)} };
            var audit=new List<string>();var best=1001;var count=0;
            foreach(var starter in starters.Take(full?starters.Length:1))
            foreach(var layout in layouts.Take(full?layouts.Length:1))
            foreach(var tideCell in new[]{new GridCoordinate(-1,0),new GridCoordinate(1,0),new GridCoordinate(1,-1),new GridCoordinate(-1,1) }.Take(full?4:1))
            {
                var s=BossWaveTests.Config().CreateSimulation();s.ChooseElement(new ChooseElementCommand("start-resonance",start));
                var commands=new List<Command>();var scratch=new BossNaturalSmokeTests.Result();var bossHp=1000;var mask=0;var hp8=0;
                for(var wave=1;wave<=8&&s.State.Phase==BattlePhase.Preparation;wave++)
                {
                    Action<string,GridCoordinate> place=(type,c)=>{var command=new Command{wave=wave,op="place",type=type,x=c.X,y=c.Y};Assert.That(s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId(type),c)).Success,Is.True);commands.Add(command);};
                    if(wave==1)place(start,starter);
                    if(wave>=7&&!s.State.Towers.Any(t=>t.Definition.Id.Value=="tide")&&s.State.Gold>=60&&s.Grid.GetState(tideCell)==GridCellState.Available)place("tide",tideCell);
                    if(wave==8)
                    {
                        hp8=s.State.BaseHealth;
                        foreach(var t in s.State.Towers.Where(t=>t.Definition.Id.Value==start).ToArray())
                            if(s.State.Gold+t.InvestedGold*70/100>=90){commands.Add(new Command{wave=wave,op="sell",type=start,id=t.Id.Value});Assert.That(s.SellTower(t.Id).Success,Is.True);}
                    }
                    if(wave>=4)foreach(var c in layout)
                        if(s.State.Gold>=90&&s.Grid.GetState(c)==GridCellState.Available&&(wave<7||c!=tideCell)&&!(wave<7&&c==tideCell))place("stone",c);
                    s.StartWave();var ticks=0;
                    while(s.State.Phase==BattlePhase.Combat&&ticks++<20000)
                    {
                        BossNaturalSmokeTests.TryCast(s,scratch);
                        foreach(var e in s.State.Enemies)if(e.Definition.Id.Value=="rift-warden")bossHp=e.Health;
                        if(s.State.Phase==BattlePhase.Combat)s.Step();BossNaturalSmokeTests.Clear(s);
                    }
                    if(s.State.Phase==BattlePhase.Victory)bossHp=0;
                    if(s.State.ElementProgression.PendingChoiceId!="none")s.ChooseElement(new ChooseElementCommand(s.State.ElementProgression.PendingChoiceId,wave==3?"stone":"tide"));
                    if(s.State.Phase==BattlePhase.WaveComplete)s.ContinueToNextWave();
                }
                count++;if(s.State.WaveIndex==7&&bossHp<best)
                {
                    best=bossHp;var script=new Script{route=start,start=start,after3="stone",after6="tide",commands=commands.ToArray()};
                    Directory.CreateDirectory("TestResults/P2.4e/greedy");File.WriteAllText("TestResults/P2.4e/greedy/"+start+".commands.json",JsonUtility.ToJson(script,true));
                }
                audit.Add($"{count}|starter={starter.X},{starter.Y}|layout={Array.IndexOf(layouts,layout)}|tide={tideCell.X},{tideCell.Y}|phase={s.State.Phase}|wave={s.State.WaveIndex+1}|hp8={hp8}|bossHp={bossHp}|gold={s.State.Gold}|kills={s.State.Kills}");
                if(s.State.Phase==BattlePhase.Victory)break;
            }
            Directory.CreateDirectory("TestResults/P2.4e/greedy");File.WriteAllLines("TestResults/P2.4e/greedy/"+start+".txt",audit);
        }
    }
}
