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
    public sealed class BossExpandedSearchTests
    {
        private const string Output="TestResults/P2.4e/revision-2";
        [Serializable] public sealed class Measured
        {
            public string start,after3,after6,origin,phase;
            public int hp,gold,kills,bossHp=1000,bossKills,bossAbilityDamage,simulations;
            public int bossDirect,bossBurn,bossReaction,steamDamage,stormDamage;
            public string preparationEight,armyProfile,rejectedCommand;
            public BossMissionScript script;
        }
        private static bool SeedPass=>Environment.GetEnvironmentVariable("P24E2_SEEDS_ONLY")=="1";
        private static bool Apply(BattleSimulation s,BossMissionCommand c)
        {
            if(c.op=="place")return s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId(c.type),new GridCoordinate(c.x,c.y))).Success;
            return c.op=="sell"?s.SellTower(new TowerId(c.id)).Success:s.UpgradeTower(new TowerId(c.id)).Success;
        }
        private static List<GridCoordinate> Cells(BattleSimulation s)
        {
            var result=new List<GridCoordinate>();var map=s.Grid.Map;
            for(var y=map.Origin.Y;y<map.Origin.Y+map.Height;y++)for(var x=map.Origin.X;x<map.Origin.X+map.Width;x++)
            {var c=new GridCoordinate(x,y);if(s.Grid.GetState(c)==GridCellState.Available)result.Add(c);}
            return result;
        }
        // Геометрия только задаёт порядок кандидатов; победа оценивается настоящим Domain.
        private static readonly Dictionary<string,double> Geometry=new Dictionary<string,double>();
        private static double Coverage(BattleSimulation s,GridCoordinate c,float range)
        {
            var key=c.X+","+c.Y+","+range; if(Geometry.TryGetValue(key,out var found))return found;
            var m=s.Grid.Map;var center=m.GetCellCenter(c);var x=center.X;var y=center.Y;double score=0;
            for(var d=0f;d<s.Path.TotalLength;d+=.125f){var p=s.Path.GetPosition(d);if((p.X-x)*(p.X-x)+(p.Y-y)*(p.Y-y)<=range*range)score+=.125;}
            Geometry[key]=score;return score;
        }
        private static void Clear(BattleSimulation s){s.ClearAttackEvents();s.ClearDamageEvents();s.ClearReactionEvents();s.ClearAbilityEvents();}
        private static void Fight(BattleSimulation s,BossMissionScript policy,List<BossMissionCast> casts,Measured m,bool noCast=false)
        {
            Assert.That(s.StartWave(),Is.EqualTo(WaveTransitionResult.Started));EnemyState boss=null;var guard=0;
            while(s.State.Phase==BattlePhase.Combat&&guard++<20000)
            {
                foreach(var e in s.State.Enemies)if(e.Definition.Id.Value=="rift-warden")boss=e;
                if(!noCast&&policy.TryCenter(s,out var p))
                {
                    var wave=s.State.WaveIndex+1;var relative=s.WaveElapsedTicks;
                    var r=s.CastAbility(BossWaveTests.Cast(s,p));Assert.That(r.Succeeded,Is.True);
                    casts.Add(new BossMissionCast{wave=wave,relativeTick=relative,x=p.X,y=p.Y});
                }
                Observe(s,boss,m);Clear(s);
                if(s.State.Phase==BattlePhase.Combat)s.Step();
                foreach(var e in s.State.Enemies)if(e.Definition.Id.Value=="rift-warden")boss=e;
                Observe(s,boss,m);Clear(s);
            }
            Assert.That(guard,Is.LessThan(20000));
            if(boss!=null){m.bossHp=boss.Health;if(!boss.IsAlive)m.bossKills=1;Assert.That(m.bossDirect+m.bossBurn+m.bossReaction+m.bossAbilityDamage+m.bossHp,Is.EqualTo(1000));}
        }
        private static void Observe(BattleSimulation s,EnemyState boss,Measured m)
        {
            foreach(var d in s.DamageEvents)
            {
                if(d.Origin==DamageOrigin.Reaction)
                {if(d.ReactionKind==ReactionKind.Steam)m.steamDamage+=d.AppliedHpLoss;else if(d.ReactionKind==ReactionKind.Storm)m.stormDamage+=d.AppliedHpLoss;}
                if(boss==null||d.TargetEnemyId!=boss.Id)continue;
                if(d.Origin==DamageOrigin.Ability)m.bossAbilityDamage+=d.AppliedHpLoss;
                else if(d.Origin==DamageOrigin.Direct)m.bossDirect+=d.AppliedHpLoss;
                else if(d.Origin==DamageOrigin.Burn)m.bossBurn+=d.AppliedHpLoss;
                else if(d.Origin==DamageOrigin.Reaction)m.bossReaction+=d.AppliedHpLoss;
            }
        }
        private static Measured Run(string start,string a,string b,GridCoordinate starter,int layout,int support,int upgrade,int threshold,int delay,bool sell,bool two,int role=0,int firstPurchase=4,int starterUpgrade=0)
        {
            var s=BossWaveTests.Config().CreateSimulation();var policy=new BossMissionScript{start=start,after3=a,after6=b,threshold=threshold,bossDelay=delay};
            var commands=new List<BossMissionCommand>();var casts=new List<BossMissionCast>();var m=new Measured{start=start,after3=a,after6=b,origin=$"grid:{starter.X},{starter.Y}/layout{layout}/support{support}/upgrade{upgrade}/sell{sell}/two{two}/threshold{threshold}/delay{delay}/role{role}/buyFrom{firstPurchase}/starterUpgrade{starterUpgrade}"};
            Assert.That(s.State.Gold,Is.EqualTo(150));Assert.That(s.State.BaseHealth,Is.EqualTo(20));Assert.That(s.ChooseElement(new ChooseElementCommand("start-resonance",start)).Success,Is.True);
            var cells=Cells(s);
            for(var wave=1;wave<=8&&s.State.Phase==BattlePhase.Preparation;wave++)
            {
                Action<BossMissionCommand> act=c=>{c.wave=wave;Assert.That(Apply(s,c),Is.True);commands.Add(c);};
                Action<string,GridCoordinate> place=(type,c)=>act(new BossMissionCommand{op="place",type=type,x=c.X,y=c.Y});
                if(wave==1)place(start,starter);
                if(two&&wave==1&&start=="flame")
                {var c=cells.Where(c=>s.Grid.GetState(c)==GridCellState.Available).OrderByDescending(c=>Coverage(s,c,3)).First();place(start,c);}
                if(two&&wave==2&&s.State.Gold>=s.TowerCatalog.Get(new TowerDefinitionId(start)).Levels[0].Price)
                {var c=cells.Where(c=>s.Grid.GetState(c)==GridCellState.Available).OrderByDescending(c=>Coverage(s,c,3)).First();place(start,c);}
                if(starterUpgrade>0&&wave==starterUpgrade)
                {
                    var tower=s.State.Towers.First(t=>t.Definition.Id.Value==start);
                    var archetype=s.TowerCatalog.Get(tower.Definition.Id);
                    if(s.State.Gold>=archetype.Levels[tower.Level].Price)act(new BossMissionCommand{op="upgrade",type=start,id=tower.Id.Value});
                }
                var types=s.TowerCatalog.Archetypes.Where(t=>t.ShowInCatalog&&t.RequiredElements.All(e=>s.State.ElementProgression.UnlockedElements.Contains(e))).ToArray();
                var available=new HashSet<string>(types.Select(t=>t.Id.Value));
                var priorities=new[]{"stone","steam","storm",start};
                var main=priorities.Skip(role).Concat(priorities.Take(role)).First(available.Contains);
                if(wave>=firstPurchase)
                {
                    if(wave==8&&sell)
                        foreach(var tower in s.State.Towers.Where(t=>t.Definition.Id.Value==start&&t.Definition.Id.Value!=main).ToArray())
                            act(new BossMissionCommand{op="sell",type=start,id=tower.Id.Value});
                    foreach(var type in support==0?new[]{"tide","gale"}:support==1?new[]{"gale","tide"}:new string[0])
                        if(available.Contains(type)&&type!=main&&!s.State.Towers.Any(t=>t.Definition.Id.Value==type)&&s.State.Gold>=types.Single(t=>t.Id.Value==type).Levels[0].Price)
                        {
                            var c=cells.Where(c=>s.Grid.GetState(c)==GridCellState.Available).OrderByDescending(c=>Coverage(s,c,types.Single(t=>t.Id.Value==type).Levels[0].Range)).First();place(type,c);
                        }
                    if(upgrade>0&&wave>=upgrade)
                    {
                        foreach(var t in s.State.Towers.OrderByDescending(t=>Coverage(s,t.Coordinate,t.Definition.Range)).ToArray())
                        {
                            var archetype=types.Single(t2=>t2.Id==t.Definition.Id);
                            if(t.Level<3&&s.State.Gold>=archetype.Levels[t.Level].Price)act(new BossMissionCommand{op="upgrade",type=t.Definition.Id.Value,id=t.Id.Value});
                        }
                    }
                    var price=types.Single(t=>t.Id.Value==main).Levels[0].Price;
                    var order=cells.OrderByDescending(c=>Coverage(s,c,types.Single(t=>t.Id.Value==main).Levels[0].Range)).ToArray();
                    if(layout%3==1)order=order.Skip(1).Concat(order.Take(1)).ToArray();
                    if(layout%3==2)order=order.OrderBy(c=>c.Y).ThenBy(c=>Math.Abs(c.X)).ToArray();
                    foreach(var c in order)if(s.State.Gold>=price&&s.Grid.GetState(c)==GridCellState.Available)place(main,c);
                }
                if(wave==8)
                {
                    m.preparationEight=Veilward.Infrastructure.WaveCheckpointJson.Serialize(BossNaturalSmokeTests.Envelope(s));
                    m.armyProfile=string.Join(";",s.State.Towers.GroupBy(t=>t.Definition.Id.Value+":"+t.Level).OrderBy(g=>g.Key).Select(g=>g.Key+"x"+g.Count()));
                }
                Fight(s,policy,casts,m);
                if(s.State.Phase==BattlePhase.WaveComplete)
                {if(s.State.ElementProgression.PendingChoiceId!="none")Assert.That(s.ChooseElement(new ChooseElementCommand(s.State.ElementProgression.PendingChoiceId,wave==3?a:b)).Success,Is.True);s.ContinueToNextWave();}
            }
            m.phase=s.State.Phase.ToString();m.hp=s.State.BaseHealth;m.gold=s.State.Gold;m.kills=s.State.Kills;
            policy.commands=commands.ToArray();policy.casts=casts.ToArray();m.script=policy;return m;
        }
        private static Measured ReplaySeed(string path,int threshold,int delay,bool early=false,bool hold=false)
        {
            var script=JsonUtility.FromJson<BossMissionScript>(File.ReadAllText(path));script.threshold=threshold;script.bossDelay=delay;script.earlyWave8=early;script.holdWave7=hold;script.casts=null;
            var s=BossWaveTests.Config().CreateSimulation();var casts=new List<BossMissionCast>();var m=new Measured{start=script.start,after3=script.after3,after6=script.after6,origin="seed:"+path+"/threshold"+threshold+"/delay"+delay+"/early"+early+"/hold7"+hold};
            s.ChooseElement(new ChooseElementCommand("start-resonance",script.start));
            while(s.State.Phase==BattlePhase.Preparation)
            {
                var wave=s.State.WaveIndex+1;
                foreach(var c in script.commands.Where(c=>c.wave==wave))if(!Apply(s,c)){m.phase="IllegalSeedSuffix";m.hp=s.State.BaseHealth;m.gold=s.State.Gold;m.kills=s.State.Kills;m.rejectedCommand="wave="+wave+"/"+JsonUtility.ToJson(c);script.casts=casts.ToArray();m.script=script;return m;}
                Fight(s,script,casts,m);
                if(s.State.Phase==BattlePhase.WaveComplete){if(s.State.ElementProgression.PendingChoiceId!="none")s.ChooseElement(new ChooseElementCommand(s.State.ElementProgression.PendingChoiceId,wave==3?script.after3:script.after6));s.ContinueToNextWave();}
            }
            m.phase=s.State.Phase.ToString();m.hp=s.State.BaseHealth;m.gold=s.State.Gold;m.kills=s.State.Kills;script.casts=casts.ToArray();m.script=script;return m;
        }
        [TestCase("flame")] [TestCase("gale")]
        public void RegBattle213_IndependentCastPoliciesOnReachedSeeds(string start)
        {
            var full=Environment.GetEnvironmentVariable("P24E2_TIMING")=="1";
            var output=Output+(full?(SeedPass?"/seed-timing":"/timing"):"/timing-smoke");Directory.CreateDirectory(output);var rows=new List<string>();var wins=0;var count=0;var best=new Dictionary<string,Measured>();
            var paths=full?ReachedProfiles(start).ToList():new List<string>();
            var inputs=new List<string>();
            foreach(var path in paths)
            {
                var m=JsonUtility.FromJson<Measured>(File.ReadAllText(path));var input=output+"/"+Path.GetFileName(path)+".commands.json";File.WriteAllText(input,JsonUtility.ToJson(m.script,true));inputs.Add(input);
            }
            foreach(var suffix in new[]{"survival","coverage"})inputs.Add("Docs/review/P2.3e.1/"+(start=="flame"?"A":"B")+"-"+suffix+".commands.json");
            foreach(var path in inputs)foreach(var threshold in full?new[]{100,200,300}:new[]{200})foreach(var delay in full?new[]{0,60,120,240}:new[]{0})foreach(var early in full?new[]{false,true}:new[]{false})foreach(var hold in full?new[]{false,true}:new[]{false})
            {
                var m=ReplaySeed(path,threshold,delay,early,hold);count++;var key=m.after3+"-"+m.after6;
                if(!best.ContainsKey(key)||Better(m,best[key]))best[key]=m;
                rows.Add($"{count}|{m.after3}/{m.after6}|{m.origin}|{m.phase}|hp={m.hp}|gold={m.gold}|kills={m.kills}|bossHP={m.bossHp}|bossAbility={m.bossAbilityDamage}");
                if(m.phase=="Victory")
                {
                    wins++;File.WriteAllText(output+"/"+start+".victory-"+wins+".json",JsonUtility.ToJson(m,true));
                    if(m.bossKills==1&&m.bossAbilityDamage>0)File.WriteAllText(output+"/"+start+".winner.commands.json",JsonUtility.ToJson(m.script,true));
                }
            }
            foreach(var kv in best)File.WriteAllText(output+"/"+start+"-"+kv.Key+".best.json",JsonUtility.ToJson(kv.Value,true));
            File.WriteAllLines(output+"/"+start+".candidates.txt",rows);
            File.WriteAllText(output+"/"+start+".limits.txt",$"simulations={count}; roots=per history best + best upgraded + best diverse profile; historical survival/coverage; thresholds100/200/300; delays0/60/120/240; early8/hold7 independent; victories={wins}; full={full}\n");Assert.That(count,Is.GreaterThan(0));
        }
        private static bool Upgraded(Measured m)=>m.armyProfile!=null&&(m.armyProfile.Contains(":2x")||m.armyProfile.Contains(":3x"));
        // Каждый ordered history получает собственные upgraded/diverse roots, без глобального Take.
        private static IEnumerable<string> ReachedProfiles(string start)
        {
            if(SeedPass)
            {
                foreach(var path in Directory.GetFiles("Docs/review/P2.4e/revision-2/research-seeds",start+"-*.json"))yield return path;
                yield break;
            }
            foreach(var path in Directory.GetFiles(Output+"/full",start+"-*.best.json"))yield return path;
            var profiles=Directory.GetFiles(Output+"/full",start+"-profile-*.json")
                .Select(path=>(path,m:JsonUtility.FromJson<Measured>(File.ReadAllText(path))));
            foreach(var history in profiles.GroupBy(p=>p.m.after3+"/"+p.m.after6))
            {
                var ranked=history.OrderBy(p=>p.m.bossHp).ToArray();
                var upgraded=ranked.Where(p=>Upgraded(p.m)).Take(1);
                var diverse=ranked.Where(p=>p.m.armyProfile!=ranked[0].m.armyProfile).Take(1);
                foreach(var p in upgraded.Concat(diverse).Distinct())yield return p.path;
            }
        }
        [Test]
        public void RegBattle213_LethalCastRecordsOriginalTickAndReplaysVictory()
        {
            var policy=new BossMissionScript{threshold=100};var casts=new List<BossMissionCast>();
            var s=BossWaveTests.Fixture(new WaveDefinition(BossWaveTests.Shade(),1,5,30));
            Fight(s,policy,casts,new Measured());
            Assert.That(s.State.Phase,Is.EqualTo(BattlePhase.Victory));Assert.That(casts.Count,Is.EqualTo(1));
            Assert.That(casts[0].relativeTick,Is.EqualTo(5));
            var replay=BossWaveTests.Fixture(new WaveDefinition(BossWaveTests.Shade(),1,5,30));
            Fight(replay,new BossMissionScript{casts=casts.ToArray()},new List<BossMissionCast>(),new Measured());
            Assert.That(replay.State.Phase,Is.EqualTo(BattlePhase.Victory));Assert.That(replay.State.Tick,Is.EqualTo(s.State.Tick));
        }
        [Test]
        public void RegBattle213_VictoryOutranksDefeatWithEqualBossHp()
        {
            Assert.That(Better(new Measured{phase="Victory",bossHp=0},new Measured{phase="Defeat",bossHp=0,hp=20,gold=999}),Is.True);
        }
        private sealed class RepairNode
        {
            public BattleCheckpoint prep;public List<BossMissionCommand> commands;public Measured measured;
        }
        private static BattleSimulation ReachEight(BossMissionScript script,List<BossMissionCast> casts)
        {
            var s=BossWaveTests.Config().CreateSimulation();s.ChooseElement(new ChooseElementCommand("start-resonance",script.start));
            while(s.State.Phase==BattlePhase.Preparation)
            {
                var wave=s.State.WaveIndex+1;foreach(var c in script.commands.Where(c=>c.wave==wave))if(!Apply(s,c))return null;
                if(wave==8)return s;
                Fight(s,script,casts,new Measured());
                if(s.State.Phase!=BattlePhase.WaveComplete)return null;
                if(s.State.ElementProgression.PendingChoiceId!="none")s.ChooseElement(new ChooseElementCommand(s.State.ElementProgression.PendingChoiceId,wave==3?script.after3:script.after6));
                s.ContinueToNextWave();
            }
            return null;
        }
        private static IEnumerable<BossMissionCommand[]> Repairs(BattleSimulation s)
        {
            yield return Array.Empty<BossMissionCommand>();
            var types=s.TowerCatalog.Archetypes.Where(t=>t.ShowInCatalog&&t.RequiredElements.All(e=>s.State.ElementProgression.UnlockedElements.Contains(e))).ToArray();
            foreach(var tower in s.State.Towers)
                if(tower.Level<3&&s.State.Gold>=types.Single(t=>t.Id==tower.Definition.Id).Levels[tower.Level].Price)
                    yield return new[]{new BossMissionCommand{wave=8,op="upgrade",id=tower.Id.Value,type=tower.Definition.Id.Value}};
            foreach(var type in types)if(s.State.Gold>=type.Levels[0].Price)
                foreach(var c in Cells(s))yield return new[]{new BossMissionCommand{wave=8,op="place",x=c.X,y=c.Y,type=type.Id.Value}};
            for(var a=0;a<s.State.Towers.Count;a++)
            {
                for(var b=-1;b<a;b++)
                {
                    var selling=b<0?new[]{s.State.Towers[a]}:new[]{s.State.Towers[a],s.State.Towers[b]};
                    var commands=selling.Select(t=>new BossMissionCommand{wave=8,op="sell",id=t.Id.Value,type=t.Definition.Id.Value}).ToArray();
                    var sold=BossWaveTests.Config().CreateSimulation();sold.RestoreCheckpoint(s.ExportCheckpoint());foreach(var c in commands)Assert.That(Apply(sold,c),Is.True);
                    yield return commands;
                    foreach(var type in types)if(sold.State.Gold>=type.Levels[0].Price)
                        foreach(var c in Cells(sold))yield return commands.Concat(new[]{new BossMissionCommand{wave=8,op="place",x=c.X,y=c.Y,type=type.Id.Value}}).ToArray();
                }
            }
        }
        private static string Profile(RepairNode n)=>string.Join(";",n.prep.towers.GroupBy(t=>t.towerTypeId+":"+t.level).OrderBy(g=>g.Key).Select(g=>g.Key+"x"+g.Count()));
        [TestCase("flame")] [TestCase("gale")]
        [Timeout(600000)]
        public void RegBattle213_PublicCheckpointPreparationEightRepair(string start)
        {
            var full=Environment.GetEnvironmentVariable("P24E2_REPAIR")=="1";var output=Output+(full?(SeedPass?"/seed-repair":"/repair"):"/repair-smoke");Directory.CreateDirectory(output);
            var paths=full?ReachedProfiles(start).ToArray():Array.Empty<string>();
            var rows=new List<string>();var prefixFailures=new List<string>();var attempted=0;var reachedCount=0;var wins=0;var eligibleWins=0;var total=0;var best=new Dictionary<string,Measured>();
            if(!full)
            {
                var result=ReplaySeed("Docs/review/P2.3e.1/"+(start=="flame"?"A":"B")+"-survival.commands.json",200,0);
                Assert.That(result.script.start,Is.EqualTo(start));Assert.That(result.phase,Is.Not.Null);return;
            }
            var roots=paths.Select(path=>(path,JsonUtility.FromJson<Measured>(File.ReadAllText(path)).script)).ToList();
            foreach(var suffix in new[]{"survival","coverage"})
            {
                var path="Docs/review/P2.3e.1/"+(start=="flame"?"A":"B")+"-"+suffix+".commands.json";
                roots.Add((path,JsonUtility.FromJson<BossMissionScript>(File.ReadAllText(path))));
            }
            foreach(var entry in roots)
            foreach(var hold in new[]{false,true})foreach(var early in new[]{false,true})
            {
                var path=entry.Item1;var policy=entry.Item2;policy.casts=null;policy.holdWave7=hold;policy.earlyWave8=early;
                attempted++;var prefixCasts=new List<BossMissionCast>();var reached=ReachEight(policy,prefixCasts);
                if(reached==null){prefixFailures.Add(path+"/hold7"+hold+"/early8"+early+"|Preparation8 not reached: rejected suffix or earlier terminal");continue;}
                reachedCount++;
                var root=new RepairNode{prep=reached.ExportCheckpoint(),commands=policy.commands.ToList()};var beam=new List<RepairNode>{root};var evaluated=0;
                var seen=new HashSet<string>();
                for(var depth=0;depth<4;depth++)
                {
                    var candidates=new List<RepairNode>();var streams=new List<IEnumerator<(RepairNode,BossMissionCommand[])>>();
                    foreach(var parent in beam)
                    {
                        var s=BossWaveTests.Config().CreateSimulation();s.RestoreCheckpoint(parent.prep);
                        streams.Add(Repairs(s).Select(m=>(parent,m)).GetEnumerator());
                    }
                    var depthLimit=(depth+1)*700;
                    while(streams.Count>0&&evaluated<depthLimit)
                    {
                        for(var i=0;i<streams.Count&&evaluated<depthLimit;)
                        {
                            var it=streams[i];if(!it.MoveNext()){it.Dispose();streams.RemoveAt(i);continue;}
                            var parent=it.Current.Item1;var moves=it.Current.Item2;var s=BossWaveTests.Config().CreateSimulation();s.RestoreCheckpoint(parent.prep);
                            foreach(var c in moves)Assert.That(Apply(s,c),Is.True);
                            var prep=s.ExportCheckpoint();var key=Veilward.Infrastructure.WaveCheckpointJson.Serialize(BossNaturalSmokeTests.Envelope(s));
                            if(!seen.Add(key)){i++;continue;}
                            var m=new Measured{start=start,after3=policy.after3,after6=policy.after6,origin=Path.GetFileName(path)+"/hold7"+hold+"/early8"+early+"/depth"+depth};var casts=new List<BossMissionCast>(prefixCasts);
                            Fight(s,policy,casts,m);evaluated++;total++;
                            m.phase=s.State.Phase.ToString();m.hp=s.State.BaseHealth;m.gold=s.State.Gold;m.kills=s.State.Kills;
                            m.preparationEight=key;m.armyProfile=Profile(new RepairNode{prep=prep});
                            var commands=parent.commands.Concat(moves).ToList();m.script=new BossMissionScript{start=start,after3=policy.after3,after6=policy.after6,commands=commands.ToArray(),casts=casts.ToArray(),threshold=policy.threshold,bossDelay=policy.bossDelay,earlyWave8=early,holdWave7=hold};
                            var history=m.after3+"-"+m.after6;if(!best.ContainsKey(history)||Better(m,best[history]))best[history]=m;
                            rows.Add($"{total}|{m.after3}/{m.after6}|{m.origin}|{m.phase}|hp={m.hp}|gold={m.gold}|kills={m.kills}|bossHP={m.bossHp}|bossAbility={m.bossAbilityDamage}");
                            if(m.phase=="Victory")
                            {
                                wins++;File.WriteAllText(output+"/"+start+".victory-"+wins+".json",JsonUtility.ToJson(m,true));
                                if(m.bossKills==1&&m.bossAbilityDamage>0){eligibleWins++;File.WriteAllText(output+"/"+start+".winner.commands.json",JsonUtility.ToJson(m.script,true));}
                            }
                            candidates.Add(new RepairNode{prep=prep,commands=commands,measured=m});i++;
                        }
                    }
                    foreach(var it in streams)it.Dispose();
                    var ranked=candidates.OrderByDescending(n=>n.measured.phase=="Victory").ThenBy(n=>n.measured.bossHp).ThenByDescending(n=>n.measured.hp).ThenByDescending(n=>n.measured.gold).ToList();
                    if(ranked.Count==0)break;
                    beam=ranked.Take(8).Concat(ranked.GroupBy(Profile).Select(g=>g.First())).Distinct().Take(20).ToList();
                    File.WriteAllLines(output+"/"+start+".candidates.txt",rows);
                    if(eligibleWins>0)break;
                }
                if(eligibleWins>0)break;
            }
            File.WriteAllLines(output+"/"+start+".candidates.txt",rows);
            foreach(var kv in best)File.WriteAllText(output+"/"+start+"-"+kv.Key+".best.json",JsonUtility.ToJson(kv.Value,true));
            File.WriteAllLines(output+"/"+start+".prefix-failures.txt",prefixFailures);
            File.WriteAllText(output+"/"+start+".limits.txt",$"simulations={total}; inputRoots={roots.Count}; attemptedVariants={attempted}; reachedVariants={reachedCount}; skippedVariants={prefixFailures.Count}; depth4; budget2800 per reached history/hold7/early8; beam20 incl8 best+level/type profiles; full-wire dedup; all public safe checkpoints; victories={wins}; eligibleVictories={eligibleWins}\n");
            Assert.That(total,Is.GreaterThan(0));
        }
        private static bool Better(Measured a,Measured b)
        {
            int Rank(string phase)=>phase=="Victory"?3:phase=="Defeat"?2:0;
            if(Rank(a.phase)!=Rank(b.phase))return Rank(a.phase)>Rank(b.phase);
            if(a.bossHp!=b.bossHp)return a.bossHp<b.bossHp;
            return a.hp>b.hp||a.hp==b.hp&&a.gold>b.gold;
        }
        private static IEnumerable<(GridCoordinate,int,int,int,int,bool,bool,int,int,int)> Plans(GridCoordinate[] cells)
        {
            var count=cells.Length*3*3*3*3*2*2*4*4*3;
            for(var i=0;i<count;i++)
            {
                var n=(i*7919)%count;var cell=cells[n%cells.Length];n/=cells.Length;
                var threshold=new[]{100,200,300}[n%3];n/=3;var layout=n%3;n/=3;
                var upgrade=new[]{0,4,6}[n%3];n/=3;var support=n%3;n/=3;
                var sell=n%2==1;n/=2;var two=n%2==1;n/=2;
                var role=n%4;n/=4;var firstPurchase=4+n%4;n/=4;
                yield return (cell,layout,support,upgrade,threshold,sell,two,role,firstPurchase,new[]{0,2,3}[n%3]);
            }
        }
        [TestCase("flame")] [TestCase("gale")]
        public void RegBattle213_ExpandedOrderedChoicesAndSeeds(string start)
        {
            var full=Environment.GetEnvironmentVariable("P24E2_SEARCH")=="1";var output=Output+(full?"/full":"/smoke");Directory.CreateDirectory(output);
            var candidates=new List<string>();var best=new Dictionary<string,Measured>();var profiles=new Dictionary<string,Measured>();var coverage=new Dictionary<string,long[]>();var victories=0;var simulations=0;
            Action<Measured> save=m=>{
                simulations++;m.simulations=simulations;var key=m.start+"/"+m.after3+"/"+m.after6;
                candidates.Add($"{simulations}|{key}|{m.origin}|{m.phase}|hp={m.hp}|gold={m.gold}|kills={m.kills}|bossHP={m.bossHp}|bossKills={m.bossKills}|bossAbility={m.bossAbilityDamage}");
                if(!coverage.ContainsKey(key))coverage[key]=new long[5];coverage[key][0]++;
                if(m.preparationEight!=null)
                {
                    coverage[key][1]++;var profile=key+"/"+m.armyProfile;
                    if(!profiles.ContainsKey(profile)||Better(m,profiles[profile]))profiles[profile]=m;
                }
                if(m.steamDamage>0)coverage[key][2]++;if(m.stormDamage>0)coverage[key][3]++;if(m.phase=="Victory")coverage[key][4]++;
                if(!best.ContainsKey(key)||Better(m,best[key]))best[key]=m;
                // Сохраняется до любого ranking/pruning, включая Victory с нулевым ability coverage.
                if(m.phase=="Victory") {victories++;File.WriteAllText(output+"/"+start+"-victory-"+victories+".json",JsonUtility.ToJson(m,true));if(m.bossKills==1&&m.bossAbilityDamage>0)File.WriteAllText(output+"/"+start+".winner.commands.json",JsonUtility.ToJson(m.script,true));}
            };
            foreach(var route in new[]{start=="flame"?"A":"B"})foreach(var suffix in new[]{"survival","coverage"})
                foreach(var threshold in full?new[]{100,200,300}:new[]{200})foreach(var delay in full?new[]{0,90}:new[]{0})save(ReplaySeed("Docs/review/P2.3e.1/"+route+"-"+suffix+".commands.json",threshold,delay));
            var cells=Cells(BossWaveTests.Config().CreateSimulation()).ToArray();
            var perHistory=full?4000:1;
            foreach(var choice in BossMissionScript.Choices(start))
            {
                var evaluated=0;
                foreach(var plan in Plans(cells))
                {
                    save(Run(start,choice[0],choice[1],plan.Item1,plan.Item2,plan.Item3,plan.Item4,plan.Item5,(plan.Item2==2?90:0),plan.Item6,plan.Item7,plan.Item8,plan.Item9,plan.Item10));
                    evaluated++;
                    if(simulations%1000==0)File.WriteAllLines(output+"/"+start+".candidates.txt",candidates);
                    if(evaluated>=perHistory)break;
                }
                File.WriteAllLines(output+"/"+start+".candidates.txt",candidates);
            }
            var index=0;
            foreach(var history in profiles.Values.GroupBy(m=>m.after3+"/"+m.after6))
                foreach(var profile in history.OrderByDescending(m=>m.phase=="Victory").ThenBy(m=>m.bossHp).Take(16).Concat(history.Where(Upgraded).OrderBy(m=>m.bossHp).Take(8)).Distinct())
                    File.WriteAllText(output+"/"+start+"-profile-"+(index++)+".json",JsonUtility.ToJson(profile,true));
            File.WriteAllLines(output+"/"+start+".coverage.txt",coverage.Select(kv=>kv.Key+"|attempts/prep8/steamPositive/stormPositive/victory="+string.Join(",",kv.Value)));
            foreach(var kv in best)File.WriteAllText(output+"/"+kv.Key.Replace('/','-')+".best.json",JsonUtility.ToJson(kv.Value,true));
            File.WriteAllLines(output+"/"+start+".candidates.txt",candidates);
            File.WriteAllText(output+"/"+start+".limits.txt",$"simulations={simulations}; orderedChoices=6; cells={cells.Length}; perHistoryBudget={perHistory}; dedup=none; ranking=actual bossHP per history; all Victory saved before ranking; victories={victories}; full={full}; no state/DTO edits\n");
            Assert.That(BossMissionScript.Choices(start).Count(),Is.EqualTo(6));Assert.That(simulations,Is.GreaterThan(0));
        }
    }
}
