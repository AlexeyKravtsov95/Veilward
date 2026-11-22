// Test-only bounded timing search. Restore только exact public-command earned snapshots; DTO не редактируется.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Veilward.Domain;
using Veilward.Presentation;
using Veilward.Tests.Shared;

public static class SearchSteamTiming
{
    private sealed class Node
    {
        public BattleCheckpoint checkpoint;
        public List<BossMissionCommand> commands=new List<BossMissionCommand>();
        public List<BossMissionCast> casts=new List<BossMissionCast>();
        public string history="";
        public int worth;
    }
    public static object Search()
    {
        var config=AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWaveAbilityBossV2.asset");
        var settings=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText("Docs/review/ART.3e/inputs/search-settings.json"));
        int V(string key)=>(int)settings[key];
        var initial=config.CreateSimulation();initial.ChooseElement(new ChooseElementCommand("start-resonance","flame"));
        var first=new BossMissionCommand{wave=1,op="place",type="flame",x=V("fx"),y=V("fy")};
        initial.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId("flame"),new GridCoordinate(first.x,first.y)));
        var second=new BossMissionCommand{wave=1,op="place",type="flame",x=V("bx"),y=V("by")};

        if(V("two")==1)initial.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId("flame"),new GridCoordinate(second.x,second.y)));
        var frontier=new List<Node>{new Node{checkpoint=initial.ExportCheckpoint(),commands=V("two")==1?new List<BossMissionCommand>{first,second}:new List<BossMissionCommand>{first}}};
        var counts=new List<object>();var outcomes=0;
        for(var wave=1;wave<=7;wave++)
        {
            var candidates=new List<Node>();
            foreach(var parent in frontier)
            foreach(var threshold in new[]{0,1,100,175,200,225,250,275,300,350})
            foreach(var policy in new[]{0,2,3})
            {
                var s=config.CreateSimulation();s.RestoreCheckpoint(parent.checkpoint);
                var n=new Node{commands=new List<BossMissionCommand>(parent.commands),casts=new List<BossMissionCast>(parent.casts),history=parent.history+","+threshold+":"+policy};
                if(wave==4)
                {
                    var r=s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId("steam"),new GridCoordinate(V("sx"),V("sy"))));
                    if(!r.Success)continue;
                    n.commands.Add(new BossMissionCommand{wave=wave,op="place",type="steam",x=V("sx"),y=V("sy")});
                }
                var steam=s.State.Towers.FirstOrDefault(t=>t.Definition.Id.Value=="steam");
                if(steam!=null&&steam.Level<2&&s.UpgradeTower(steam.Id).Success)
                    n.commands.Add(new BossMissionCommand{wave=wave,op="upgrade",type="steam",id=steam.Id.Value});
                s.StartWave();var start=s.State.Tick;
                while(s.State.Phase==BattlePhase.Combat)
                {
                    s.Step();
                    if(threshold!=0&&s.AbilityState.CooldownRemainingTicks==0&&s.State.Enemies.Count>0)
                    {
                        var chosen=default(PathPosition);var bestDamage=-1;var bestScore=-1;var urgent=false;
                        void Score(PathPosition point)
                        {
                            var applied=0;var kills=0;var progress=0.0;var firstIncluded=false;
                            foreach(var e in s.State.Enemies)
                            {var q=s.Path.GetPosition(e.DistanceAlongPath);var dx=(double)point.X-q.X;var dy=(double)point.Y-q.Y;if(dx*dx+dy*dy<=2.25){applied+=Math.Min(e.Health,120);if(e.Health<=120)kills++;progress+=e.DistanceAlongPath;firstIncluded|=e.Id==s.State.Enemies[0].Id;}}
                            if(policy==3&&!firstIncluded)return;var score=policy==0?applied:policy==1?applied+kills*1000:kills*1000+(int)(progress*10);if(score>bestScore){bestScore=score;bestDamage=applied;chosen=point;}
                        }
                        for(var i=0;i<s.State.Enemies.Count;i++)
                        {
                            var e=s.State.Enemies[i];var a=s.Path.GetPosition(e.DistanceAlongPath);Score(a);urgent|=e.DistanceAlongPath>=s.Path.TotalLength-1;
                            for(var j=i+1;j<s.State.Enemies.Count;j++)
                            {var b=s.Path.GetPosition(s.State.Enemies[j].DistanceAlongPath);var dx=a.X-b.X;var dy=a.Y-b.Y;if(dx*dx+dy*dy<=9.001)Score(new PathPosition((a.X+b.X)*.5f,(a.Y+b.Y)*.5f));}
                        }
                        if(bestDamage>0&&(bestDamage>=threshold||urgent))
                        {
                            var r=s.CastAbility(new CastAbilityCommand(s.AbilityState.AbilityId,chosen,s.State.WaveIndex,s.State.Tick,s.AbilityState.NextCastSequence));
                            if(!r.Succeeded)throw new InvalidOperationException("Cast rejected");
                            n.casts.Add(new BossMissionCast{wave=wave,relativeTick=(int)(s.State.Tick-start),x=chosen.X,y=chosen.Y});
                        }
                    }
                    AbilityBossTrace.Clear(s);
                }
                outcomes++;
                if(s.State.Phase!=BattlePhase.WaveComplete)continue;
                if(s.State.ElementProgression.PendingChoiceId!="none")s.ChooseElement(new ChooseElementCommand(s.State.ElementProgression.PendingChoiceId,wave==3?"tide":"gale"));
                s.ContinueToNextWave();n.checkpoint=s.ExportCheckpoint();
                n.worth=s.State.Gold+s.State.Towers.Sum(t=>t.Definition.Id.Value=="steam"?t.InvestedGold:t.InvestedGold*70/100);
                candidates.Add(n);
            }
            frontier=candidates.OrderByDescending(n=>n.worth).ThenBy(n=>n.checkpoint.ability.CooldownRemainingTicks)
                .GroupBy(n=>n.worth+":"+n.checkpoint.ability.CooldownRemainingTicks+":"+n.checkpoint.tick+":"+string.Join(",",n.checkpoint.towers.Select(t=>t.nextAttackTick)))
                .Select(g=>g.First()).Take(16).ToList();
            counts.Add(new{wave,outcomes,generated=candidates.Count,retained=frontier.Count,best=frontier.FirstOrDefault()?.worth});
            File.WriteAllText("TestResults/ART3E/timing-progress.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{counts,outcomes},Newtonsoft.Json.Formatting.Indented));
            if(frontier.Count==0)break;
        }
        var winner=frontier.FirstOrDefault(n=>n.worth>=495);
        if(winner!=null)
        {
            var s=config.CreateSimulation();s.RestoreCheckpoint(winner.checkpoint);
            foreach(var t in s.State.Towers.Where(t=>t.Definition.Id.Value!="steam").ToArray())
            {s.SellTower(t.Id);winner.commands.Add(new BossMissionCommand{wave=8,op="sell",type=t.Definition.Id.Value,id=t.Id.Value});}
            var tSteam=s.State.Towers.Single();
            while(tSteam.Level<3)
            {if(!s.UpgradeTower(tSteam.Id).Success)throw new InvalidOperationException("Earned budget");winner.commands.Add(new BossMissionCommand{wave=8,op="upgrade",type="steam",id=tSteam.Id.Value});}
            var script=new BossMissionScript{start="flame",after3="tide",after6="gale",commands=winner.commands.ToArray(),casts=winner.casts.ToArray()};
            Directory.CreateDirectory("TestResults/ART3E");File.WriteAllText("TestResults/ART3E/earned.commands.json",JsonUtility.ToJson(script,true)+"\n");
        }
        var report=new{counts,outcomes,winner=winner!=null?new{winner.worth,winner.history,checkpoint=winner.checkpoint}:null,
            best=frontier.Take(3).Select(n=>new{n.worth,n.history,hp=n.checkpoint.baseHealth,kills=n.checkpoint.kills,commands=n.commands,casts=n.casts}).ToArray()};
        File.WriteAllText("TestResults/ART3E/timing-final.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
        return report;
    }
}
