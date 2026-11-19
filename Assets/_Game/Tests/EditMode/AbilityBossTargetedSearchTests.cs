using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Veilward.Domain;
using Veilward.Tests.Shared;

namespace Veilward.Tests.EditMode
{
    // Только исследовательский harness; live/DTO state никогда не подменяется планировщиком.
    public sealed class AbilityBossTargetedSearchTests
    {
        private static readonly string[] Types = { "flame", "tide", "gale", "steam", "storm" };
        public const string Output = "TestResults/P2.4f/revision-2/targeted";
        [Serializable] public sealed class Counters
        {
            public int waveOutcomes, fullNewGameOutcomes, generatedMaterials, unaffordable, duplicates,
                invalidSuffixes, defeated, victories, eligible, retained, beamPruned, materialPruned,
                armyCapPruned, lockedPackPruned, materialDuplicates;
            public long ticks; public double seconds; public string stop;
            public int Total => waveOutcomes + fullNewGameOutcomes;
        }
        [Serializable] public sealed class Receipt
        { public string op,type,result; public int wave,id,x,y,before,after,price,refund; }
        [Serializable] public sealed class Row
        {
            public int id,parent,wave,hp,gold,kills,remaining,abilityKills,bossKills,bossHp=1000;
            public long tick,sequence; public string history,phase,army,policy;
            public int[] cells,bossApplied=new int[4];
            public BossMissionCommand[] commands; public BossMissionCast[] casts; public Receipt[] receipts;
        }
        private sealed class Node
        {
            public int id; public string history,start,after3,after6;
            public BattleCheckpoint preparation; public BossMissionCommand[] commands=Array.Empty<BossMissionCommand>();
            public BossMissionCast[] casts=Array.Empty<BossMissionCast>(); public Row row;
            public List<string> waves=new List<string>();
        }
        private sealed class Unit
        {
            public int type,level,id;public GridCoordinate cell;
            public Unit(int type,int level,int id=0,GridCoordinate cell=default)
            {this.type=type;this.level=level;this.id=id;this.cell=cell;}
        }
        private sealed class Pack { public Unit[] units;public int cost;public string key; }
        private sealed class Material
        {public Unit[] kept;public Pack pack;public int gold;public string key,profile;public double score;}
        private sealed class Policy
        {
            public int threshold,bossDelay,centerMode;public bool hold7,early8;
            public string Key => threshold+":"+bossDelay+":"+hold7+":"+early8+":"+centerMode;
        }
        private sealed class Engine : IDisposable
        {
            private readonly string start,output;private readonly bool full;private readonly int cap;
            private readonly Stopwatch watch=Stopwatch.StartNew();private readonly StreamWriter rows,parents;
            public readonly Counters counts=new Counters();
            private readonly TowerDefinition[][] definitions=new TowerDefinition[5][];
            private readonly Dictionary<string,double> exposures=new Dictionary<string,double>();
            private readonly Dictionary<float,GridCoordinate[]> ranked=new Dictionary<float,GridCoordinate[]>();
            private readonly GridCoordinate[] buildable;private readonly BattleSimulation geometry;
            private readonly HashSet<string> evaluated=new HashSet<string>();
            private readonly List<Pack> packs=new List<Pack>();private int serial;
            private readonly Dictionary<string,Node> best=new Dictionary<string,Node>();
            private readonly List<Row> allVictories=new List<Row>();
            public Engine(string start,bool full)
            {
                this.start=start;this.full=full;cap=full?50000:96;
                output=Output+"/"+(full?"full":"smoke")+"/"+start;Directory.CreateDirectory(output);
                rows=new StreamWriter(output+"/outcomes.jsonl");parents=new StreamWriter(output+"/parents.jsonl");
                geometry=BossWaveTests.Config().CreateSimulation();buildable=NativeStrategySearchTests.Cells(geometry).ToArray();
                for(var t=0;t<Types.Length;t++)definitions[t]=geometry.TowerCatalog.Get(new TowerDefinitionId(Types[t])).Levels.ToArray();
                BuildPacks();
            }
            public void Dispose(){rows.Dispose();parents.Dispose();}
            private bool Available => counts.Total < cap-4 && watch.Elapsed.TotalSeconds < (full?590:20);
            private static int Bits(int[] cells)=>cells.Count(c=>c>0);
            private TowerDefinition Def(Unit u)=>definitions[u.type][u.level-1];
            private int Price(int type,int level)
            {var cost=0;for(var l=0;l<level;l++)cost+=definitions[type][l].Price;return cost;}
            private double Exposure(GridCoordinate c,float range)
            {
                var key=c.X+","+c.Y+","+range.ToString("R",CultureInfo.InvariantCulture);
                if(exposures.TryGetValue(key,out var value))return value;
                var p=geometry.Grid.Map.GetCellCenter(c);value=0;
                for(var d=0f;d<geometry.Path.TotalLength;d+=.1f)
                {var q=geometry.Path.GetPosition(d);var x=p.X-q.X;var y=p.Y-q.Y;if(x*x+y*y<=range*range)value+=.1;}
                exposures.Add(key,value);return value;
            }
            private GridCoordinate[] Ranked(float range)
            {
                if(!ranked.TryGetValue(range,out var result))
                {result=buildable.OrderByDescending(c=>Exposure(c,range)).ThenBy(c=>c.Y).ThenBy(c=>c.X).ToArray();ranked.Add(range,result);}
                return result;
            }
            private double Potential(IEnumerable<Unit> army,int wave)
            {
                var units=army.ToArray();var armor=wave==8?5:0;var gust=0;var wet=0;
                foreach(var u in units)
                {var st=Def(u).StatusEffect;if(st?.Kind==StatusKind.Gust)gust=Math.Max(gust,st.Power);if(st?.Kind==StatusKind.Wet)wet=Math.Max(wet,st.Power);}
                var score=0d;var burn=0d;var steam=0d;
                foreach(var u in units)
                {
                    var d=Def(u);var exposure=u.id==0?Exposure(Ranked(d.Range)[0],d.Range):Exposure(u.cell,d.Range);
                    var seconds=exposure/(wave==8?1:1.5)/(1-wet*.01);
                    var hit=(long)Math.Max(1,d.Damage-Math.Max(0,armor-d.ArmorPenetration))*(100+gust)/100;
                    score+=hit*30d/d.AttackIntervalTicks*seconds;
                    if(d.StatusEffect?.Kind==StatusKind.Burn)
                        burn=Math.Max(burn,Math.Max(1,d.StatusEffect.Power-armor)*30d/d.StatusEffect.DamageIntervalTicks*(seconds+d.StatusEffect.DurationTicks/30d));
                    if(u.type==3)
                        steam=Math.Max(steam,Math.Max(1,d.Damage*d.IntrinsicReaction.DamagePercent/100-armor)*seconds/2);
                }
                return score+burn+steam;
            }
            private void BuildPacks()
            {
                var seen=new HashSet<string>();
                void Add(IEnumerable<Unit> list)
                {
                    var a=list.OrderBy(u=>u.type).ThenBy(u=>u.level).ToArray();var key=string.Join(",",a.Select(u=>u.type+"L"+u.level));
                    if(seen.Add(key))packs.Add(new Pack{units=a,key=key,cost=a.Sum(u=>Price(u.type,u.level))});
                }
                Add(Array.Empty<Unit>());
                var options=new List<Unit>();for(var t=0;t<5;t++)for(var l=1;l<=3;l++)options.Add(new Unit(t,l));
                foreach(var a in options)Add(new[]{a});
                for(var i=0;i<options.Count;i++)for(var j=i;j<options.Count;j++)Add(new[]{options[i],options[j]});
                for(var i=0;i<5;i++)for(var j=i;j<5;j++)for(var k=j;k<5;k++)
                {
                    Add(new[]{new Unit(i,1),new Unit(j,1),new Unit(k,1)});
                    for(var l=2;l<=3;l++)for(var t=0;t<5;t++)Add(new[]{new Unit(t,l),new Unit(i,1),new Unit(j,1)});
                    for(var q=k;q<5;q++)Add(new[]{new Unit(i,1),new Unit(j,1),new Unit(k,1),new Unit(q,1)});
                }
                packs.Sort((a,b)=>a.cost!=b.cost?a.cost.CompareTo(b.cost):string.CompareOrdinal(a.key,b.key));
            }
            private List<Material> Materials(Node parent,int wave)
            {
                var cp=parent.preparation;var source=cp.towers.Select(t=>new Unit(Array.IndexOf(Types,t.towerTypeId),t.level,t.towerId,new GridCoordinate(t.x,t.y))).ToArray();
                var allowed=new bool[5];for(var t=0;t<5;t++)allowed[t]=geometry.TowerCatalog.Get(new TowerDefinitionId(Types[t])).RequiredElements.All(e=>cp.elementProgression.unlockedElementIds.Contains(e.ToString().ToLowerInvariant()));
                var affordable=packs.Where(p=>p.units.All(u=>allowed[u.type])).ToArray();counts.lockedPackPruned+=packs.Count-affordable.Length;var all=new List<Material>();var keys=new HashSet<string>();
                var n=source.Length;var masks=wave>=7?Enumerable.Range(0,1<<n):new[]{(1<<n)-1}.Concat(Enumerable.Range(0,n).Select(i=>((1<<n)-1)^(1<<i)));
                foreach(var mask in masks)
                {
                    var kept=source.Where((u,i)=>(mask&(1<<i))!=0).ToArray();var gold=cp.gold+source.Where((u,i)=>(mask&(1<<i))==0).Sum(u=>Price(u.type,u.level)*70/100);
                    var variants=new List<int[]>{kept.Select(u=>u.level).ToArray()};
                    for(var i=0;i<kept.Length;i++)for(var l=kept[i].level+1;l<=3;l++)
                    {var v=kept.Select(u=>u.level).ToArray();v[i]=l;variants.Add(v);}
                    variants.Add(kept.Select(u=>Math.Max(2,u.level)).ToArray());variants.Add(kept.Select(u=>3).ToArray());
                    foreach(var levels in variants)
                    {
                        var upgrades=0;for(var i=0;i<kept.Length;i++)upgrades+=Price(kept[i].type,levels[i])-Price(kept[i].type,kept[i].level);
                        foreach(var pack in affordable)
                        {
                            counts.generatedMaterials++;
                            if(kept.Length+pack.units.Length>8){counts.armyCapPruned++;continue;}
                            if(pack.cost+upgrades>gold){counts.unaffordable++;continue;}
                            var key=mask+":"+string.Join(",",levels)+":"+pack.key;if(!keys.Add(key)){counts.materialDuplicates++;continue;}
                            var changed=kept.Select((u,i)=>new Unit(u.type,levels[i],u.id,u.cell)).ToArray();var army=changed.Concat(pack.units).ToArray();
                            var profile=string.Join(",",army.Select(u=>u.type+"L"+u.level).OrderBy(x=>x,StringComparer.Ordinal));
                            var score=Potential(army,wave);var present=army.Select(u=>u.type).ToArray();
                            var possible=new[]{present.Contains(0)&&present.Contains(1),present.Contains(3),present.Contains(1)&&present.Contains(2),present.Contains(4)};
                            for(var c=0;c<4;c++)if(parent.row.cells[c]==0&&possible[c])score+=wave>=7?100:15;
                            all.Add(new Material{kept=changed,pack=pack,gold=gold-upgrades-pack.cost,key=key,profile=profile,score=score});
                        }
                    }
                }
                var ordered=all.OrderByDescending(m=>m.score).ThenByDescending(m=>m.gold).ThenBy(m=>m.key,StringComparer.Ordinal).ToList();
                var result=new List<Material>();var used=new HashSet<string>();
                var top=ordered.Take(80).ToArray();var diversity=ordered.GroupBy(m=>m.profile).Select(g=>g.First()).Take(64).ToArray();
                var reserve=ordered.OrderByDescending(m=>m.gold).Take(12).ToArray();
                void Add(Material m){if(used.Add(m.key))result.Add(m);}
                // При малой квоте profile champions не должны оказаться за недостижимым хвостом top80.
                for(var i=0;i<Math.Max(top.Length,diversity.Length);i++)
                {if(i<top.Length)Add(top[i]);if(i<diversity.Length)Add(diversity[i]);if(i%4==0&&i/4<reserve.Length)Add(reserve[i/4]);}
                counts.materialPruned+=all.Count-result.Count;
                return result;
            }
            private BossMissionCommand[] Commands(Node parent,Material material,int layout,int wave)
            {
                var commands=new List<BossMissionCommand>();var occupied=new HashSet<GridCoordinate>(material.kept.Select(u=>u.cell));
                foreach(var old in parent.preparation.towers)
                {
                    var keep=material.kept.FirstOrDefault(u=>u.id==old.towerId);
                    if(keep==null)commands.Add(new BossMissionCommand{wave=wave,op="sell",type=old.towerTypeId,id=old.towerId});
                }
                foreach(var u in material.kept)
                {
                    var old=parent.preparation.towers.Single(t=>t.towerId==u.id);
                    for(var l=old.level;l<u.level;l++)commands.Add(new BossMissionCommand{wave=wave,op="upgrade",type=Types[u.type],id=u.id});
                }
                var next=parent.preparation.nextTowerId;
                var buying=layout>=6?material.pack.units.Reverse():material.pack.units.AsEnumerable();
                foreach(var u in buying)
                {
                    var available=Ranked(Def(u).Range).Where(c=>!occupied.Contains(c)).Take(12).ToArray();if(available.Length==0)return null;
                    var cell=available[(layout+u.type*(layout/3))%available.Length];occupied.Add(cell);
                    commands.Add(new BossMissionCommand{wave=wave,op="place",type=Types[u.type],x=cell.X,y=cell.Y});
                    for(var l=1;l<u.level;l++)commands.Add(new BossMissionCommand{wave=wave,op="upgrade",type=Types[u.type],id=next});next++;
                }
                return commands.ToArray();
            }
            private static Policy CastPolicy(int wave,int variation)
            {
                var p=new Policy{threshold=new[]{200,300,400,100}[variation%4],centerMode=(variation/4)%2};
                if(wave==7)p.hold7=(variation/8)%2==1;
                if(wave==8){p.centerMode=(variation/12)%2;p.early8=(variation/6)%2==0;p.bossDelay=new[]{0,30,90,180,270,360}[variation%6];}
                return p;
            }
            private static bool Center(BattleSimulation s,Policy p,out PathPosition center)
            {
                center=default;if(s.AbilityState.CooldownRemainingTicks!=0||s.State.Phase!=BattlePhase.Combat)return false;
                var wave=s.State.WaveIndex+1;if(wave==7&&p.hold7)return false;
                if(wave==8)
                {
                    var boss=s.State.Enemies.FirstOrDefault(e=>e.Definition.Id.Value=="rift-warden");
                    if(boss!=null)
                    {
                        if(s.WaveElapsedTicks<330+p.bossDelay)return false;
                        var bossPosition=s.Path.GetPosition(boss.DistanceAlongPath);center=bossPosition;
                        if(p.centerMode==1)
                        {
                            var bestScore=-1;
                            foreach(var e in s.State.Enemies)
                            {
                                var q=s.Path.GetPosition(e.DistanceAlongPath);var point=new PathPosition((bossPosition.X+q.X)*.5f,(bossPosition.Y+q.Y)*.5f);
                                var dx=(double)point.X-bossPosition.X;var dy=(double)point.Y-bossPosition.Y;if(dx*dx+dy*dy>2.25)continue;
                                var score=0;
                                foreach(var target in s.State.Enemies)
                                {var pos=s.Path.GetPosition(target.DistanceAlongPath);dx=(double)point.X-pos.X;dy=(double)point.Y-pos.Y;
                                    if(dx*dx+dy*dy<=2.25){var damage=Math.Max(1,120-target.Definition.Armor);score+=Math.Min(target.Health,damage)+(target.Health<=damage?1000:0);}}
                                if(score>bestScore){bestScore=score;center=point;}
                            }
                        }
                        return true;
                    }
                    if(!p.early8)return false;
                }
                var best=-1;var bestApplied=0;var chosen=default(PathPosition);var urgent=false;
                void Score(PathPosition point)
                {
                    var applied=0;var kills=0;
                    foreach(var e in s.State.Enemies)
                    {var q=s.Path.GetPosition(e.DistanceAlongPath);var dx=(double)point.X-q.X;var dy=(double)point.Y-q.Y;
                        if(dx*dx+dy*dy<=2.25){var damage=Math.Max(1,120-Math.Max(0,e.Definition.Armor-(e.Crack?.Power??0)));applied+=Math.Min(e.Health,damage);if(e.Health<=damage)kills++;}}
                    var score=applied+(p.centerMode==1?kills*1000:0);if(score>best){best=score;bestApplied=applied;chosen=point;}
                }
                for(var i=0;i<s.State.Enemies.Count;i++)
                {var e=s.State.Enemies[i];var a=s.Path.GetPosition(e.DistanceAlongPath);Score(a);urgent|=e.DistanceAlongPath>=s.Path.TotalLength-1;
                    for(var j=i+1;j<s.State.Enemies.Count;j++) {var b=s.Path.GetPosition(s.State.Enemies[j].DistanceAlongPath);var dx=a.X-b.X;var dy=a.Y-b.Y;if(dx*dx+dy*dy<=9.001)Score(new PathPosition((a.X+b.X)*.5f,(a.Y+b.Y)*.5f));}}
                center=chosen;return bestApplied>0&&(bestApplied>=p.threshold||urgent);
            }
            private Node Evaluate(Node parent,BossMissionCommand[] commands,Policy policy,int wave)
            {
                var planKey=parent.history+"|"+JsonUtility.ToJson(parent.preparation)+"|ability="+parent.preparation.ability.CooldownRemainingTicks+","+parent.preparation.ability.NextCastSequence+"|"+
                    string.Join(";",commands.Select(c=>JsonUtility.ToJson(c)))+"|"+policy.Key;
                if(!evaluated.Add(planKey)){counts.duplicates++;return null;}
                if(!Available)return null;
                var s=BossWaveTests.Config().CreateSimulation();s.RestoreCheckpoint(parent.preparation.DeepCopy());
                var receipts=new List<Receipt>();
                foreach(var c in commands)
                {
                    var receipt=new Receipt{wave=wave,op=c.op,type=c.type,id=c.id,x=c.x,y=c.y,before=s.State.Gold};bool ok;
                    if(c.op=="place") {receipt.price=s.TowerCatalog.Get(new TowerDefinitionId(c.type)).Levels[0].Price;var result=s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId(c.type),new GridCoordinate(c.x,c.y)));ok=result.Success;receipt.result=result.FailureReason.ToString();if(ok)receipt.id=result.Tower.Id.Value;}
                    else
                    {
                        var tower=s.State.Towers.SingleOrDefault(t=>t.Id.Value==c.id);
                        if(c.op=="sell"){receipt.refund=tower==null?0:tower.InvestedGold*70/100;var result=s.SellTower(new TowerId(c.id));ok=result.Success;receipt.result=result.FailureReason.ToString();}
                        else {receipt.price=tower==null?0:s.TowerCatalog.Get(tower.Definition.Id).Levels[tower.Level].Price;var result=s.UpgradeTower(new TowerId(c.id));ok=result.Success;receipt.result=result.FailureReason.ToString();}
                    }
                    receipt.after=s.State.Gold;receipts.Add(receipt);
                    if(!ok){counts.invalidSuffixes++;rows.WriteLine(JsonUtility.ToJson(new Row{parent=parent.id,wave=wave,history=parent.history,phase="INVALID_SUFFIX",commands=commands,receipts=receipts.ToArray()}));return null;}
                    Assert.That(receipt.after,Is.EqualTo(receipt.before-receipt.price+receipt.refund));
                }
                counts.waveOutcomes++;var row=new Row{id=++serial,parent=parent.id,wave=wave,history=parent.history,policy=policy.Key,
                    cells=(int[])parent.row.cells.Clone(),abilityKills=parent.row.abilityKills,commands=commands,receipts=receipts.ToArray()};
                row.army=string.Join(";",s.State.Towers.Select(t=>$"{t.Id.Value}:{t.Definition.Id.Value}L{t.Level}@{t.Coordinate.X},{t.Coordinate.Y}={t.InvestedGold}"));
                var casts=new List<BossMissionCast>();var bossId=wave==8?s.ExportCheckpoint().nextEnemyId+s.Waves[7].Schedule.Count-1:-1;
                void Observe()
                {
                    foreach(var r in s.ReactionEvents)
                    {var applied=0;foreach(var d in s.DamageEvents)if(d.Origin==DamageOrigin.Reaction&&d.Tick==r.Tick&&d.ReactionKind==r.Kind&&d.ReactionSequenceInTick==r.SequenceInTick)applied+=d.AppliedHpLoss;
                        if(applied>0)row.cells[(int)r.Kind*2+(int)r.TriggerKind]+=applied;}
                    foreach(var d in s.DamageEvents)
                    {if(d.Origin==DamageOrigin.Ability&&d.WasLethal)row.abilityKills++;if(d.TargetEnemyId.Value==bossId){row.bossApplied[(int)d.Origin]+=d.AppliedHpLoss;row.bossHp=d.HealthAfter;if(d.WasLethal)row.bossKills++;}}
                    AbilityBossTrace.Clear(s);
                }
                Assert.That(s.StartWave(),Is.EqualTo(WaveTransitionResult.Started));var guard=0;
                while(s.State.Phase==BattlePhase.Combat&&guard++<20000)
                {
                    if(Center(s,policy,out var center))
                    {var relative=s.WaveElapsedTicks;var result=s.CastAbility(BossWaveTests.Cast(s,center));Assert.That(result.Succeeded,Is.True);
                        casts.Add(new BossMissionCast{wave=wave,relativeTick=relative,x=center.X,y=center.Y});Observe();}
                    if(s.State.Phase==BattlePhase.Combat){s.Step();counts.ticks++;Observe();}
                }
                Assert.That(guard,Is.LessThan(20000));
                row.casts=casts.ToArray();row.phase=s.State.Phase.ToString();row.hp=s.State.BaseHealth;row.gold=s.State.Gold;row.kills=s.State.Kills;row.tick=s.State.Tick;
                row.remaining=s.AbilityState.CooldownRemainingTicks;row.sequence=s.AbilityState.NextCastSequence;
                var node=new Node{id=row.id,history=parent.history,start=start,after3=parent.after3,after6=parent.after6,row=row,
                    commands=parent.commands.Concat(commands).ToArray(),casts=parent.casts.Concat(casts).ToArray(),waves=new List<string>(parent.waves)};
                node.waves.Add(Veilward.Infrastructure.WaveCheckpointJson.Serialize(BossNaturalSmokeTests.Envelope(s)));
                rows.WriteLine(JsonUtility.ToJson(row));
                // Каждая Victory сохраняется до любого ranking/pruning.
                if(s.State.Phase==BattlePhase.Victory)
                {counts.victories++;allVictories.Add(row);Save(node,"victory-"+row.id);if(row.bossKills==1&&row.abilityKills>0&&Bits(row.cells)==4){counts.eligible++;Save(node,"winner");}}
                if(s.State.Phase==BattlePhase.Defeat)counts.defeated++;
                if(wave==8&&Bits(row.cells)==4&&(!best.TryGetValue(parent.history,out var old)||FinalRank(node)<FinalRank(old)))
                {best[parent.history]=node;Save(node,"best-"+parent.history);}
                if(s.State.Phase==BattlePhase.WaveComplete)
                {
                    if(s.State.ElementProgression.HasPendingChoice)Assert.That(s.ChooseElement(new ChooseElementCommand(s.State.ElementProgression.PendingChoiceId,wave==3?parent.after3:parent.after6)).Success,Is.True);
                    Assert.That(s.ContinueToNextWave(),Is.EqualTo(WaveTransitionResult.Continued));node.preparation=s.ExportCheckpoint();
                }
                if(counts.waveOutcomes%250==0)Progress(wave,parent.history);
                return node;
            }
            private static int FinalRank(Node n)=>(n.row.phase=="Victory"?0:100000)+n.row.bossHp;
            private BossMissionScript Script(Node node)=>new BossMissionScript{start=start,after3=node.after3,after6=node.after6,commands=node.commands,casts=node.casts};
            private void Save(Node node,string name)
            {
                File.WriteAllText(output+"/"+name+".commands.json",JsonUtility.ToJson(Script(node),true));
                File.WriteAllText(output+"/"+name+".outcome.json",JsonUtility.ToJson(node.row,true));File.WriteAllLines(output+"/"+name+".waves.jsonl",node.waves);
            }
            private List<Node> Retain(List<Node> outcomes,int wave)
            {
                var alive=outcomes.Where(n=>n.preparation!=null&&n.row.hp>0).ToList();if(alive.Count==0)return new List<Node>();
                var selected=new List<Node>();var ids=new HashSet<int>();var width=full?32:2;
                int Wealth(Node n)=>n.preparation.gold+n.preparation.towers.Sum(t=>t.investedGold);
                string Profile(Node n)=>Bits(n.row.cells)+"|"+string.Join(",",n.preparation.towers.Select(t=>t.towerTypeId+"L"+t.level).OrderBy(x=>x,StringComparer.Ordinal))+"|hp="+(n.row.hp/4)+"|cd="+(n.row.remaining/150);
                void Take(IEnumerable<Node> source,int count)
                {foreach(var n in source.Take(count))if(ids.Add(n.id))selected.Add(n);}
                var health=alive.OrderByDescending(n=>n.row.hp).ThenByDescending(Wealth).ThenBy(n=>n.id);
                Take(health,width/4);Take(alive.OrderByDescending(n=>Bits(n.row.cells)).ThenByDescending(n=>n.row.hp).ThenByDescending(Wealth),width/4);
                Take(alive.OrderByDescending(Wealth).ThenByDescending(n=>n.row.hp),width/4);
                var diversity=health.GroupBy(Profile).Select(g=>g.First()).ToArray();Take(diversity,width-selected.Count);
                Take(health,width-selected.Count);selected=selected.Take(width).ToList();counts.retained+=selected.Count;counts.beamPruned+=alive.Count-selected.Count;
                foreach(var n in selected)parents.WriteLine(JsonUtility.ToJson(new Parent{ id=n.id,history=n.history,state=n.preparation,abilityRemaining=n.preparation.ability.CooldownRemainingTicks,abilitySequence=n.preparation.ability.NextCastSequence,commands=n.commands,casts=n.casts}));
                return selected;
            }
            [Serializable] private sealed class Parent
            {public int id,abilityRemaining;public long abilitySequence;public string history;public BattleCheckpoint state;public BossMissionCommand[] commands;public BossMissionCast[] casts;}
            private void Progress(int wave,string history)
            {
                rows.Flush();parents.Flush();counts.seconds=watch.Elapsed.TotalSeconds;
                File.WriteAllText(output+"/progress.json",JsonUtility.ToJson(new ProgressData{counts=counts,wave=wave,history=history},true));
            }
            [Serializable] private sealed class ProgressData {public Counters counts;public int wave;public string history;}
            public void Run()
            {
                var other=start=="flame"?"gale":"flame";
                var histories=new[]{new[]{"tide",other},new[]{other,"tide"}};
                var beams=new List<Node>[2];
                for(var h=0;h<2;h++)
                {
                    var s=BossWaveTests.Config().CreateSimulation();Assert.That(s.State.Gold,Is.EqualTo(150));Assert.That(s.State.BaseHealth,Is.EqualTo(20));
                    Assert.That(s.ChooseElement(new ChooseElementCommand("start-resonance",start)).Success,Is.True);
                    beams[h]=new List<Node>{new Node{id=--serial,history=start+"-"+histories[h][0]+"-"+histories[h][1],start=start,after3=histories[h][0],after6=histories[h][1],preparation=s.ExportCheckpoint(),row=new Row{cells=new int[4],hp=20,gold=150}}};
                }
                // Положительные IDs outcomes не пересекаются с отрицательными roots.
                serial=0;var quotas=new[]{300,1000,1800,2600,2400,2400,4500};
                for(var wave=1;wave<=7&&Available;wave++)
                {
                    for(var h=0;h<2&&Available;h++)
                    {
                        var outcomes=new List<Node>();var current=beams[h];if(current.Count==0)continue;
                        var materials=current.Select(n=>Materials(n,wave)).ToArray();var quota=full?quotas[wave-1]:4;
                        var done=0;var iteration=0;var exhausted=new bool[current.Count];
                        while(done<quota&&Available&&exhausted.Any(x=>!x))
                        {
                            var pi=iteration%current.Count;var variation=iteration/current.Count;iteration++;
                            if(exhausted[pi])continue;var list=materials[pi];
                            if(list.Count==0||variation>=list.Count*24){exhausted[pi]=true;continue;}
                            var mi=(variation/2)%list.Count;var epoch=variation/(list.Count*2);
                            var mat=list[mi];var layout=(mi+epoch)%12;
                            var commands=Commands(current[pi],mat,layout,wave);if(commands==null)continue;
                            // Две policies на том же layout до перехода к следующему material.
                            var policy=CastPolicy(wave,(mi*5+variation%2+epoch*2)%16);
                            var before=counts.waveOutcomes;var node=Evaluate(current[pi],commands,policy,wave);done+=counts.waveOutcomes-before;
                            if(node!=null)outcomes.Add(node);
                        }
                        beams[h]=Retain(outcomes,wave);Progress(wave,current[0].history);
                    }
                }
                var finals=new List<(Node parent,List<Material> plans)>[2];
                for(var h=0;h<2;h++)finals[h]=beams[h].Where(n=>n.preparation.waveIndex==7).Select(n=>(n,Materials(n,8))).ToList();
                var cursors=new int[2];var doneHist=new bool[2];
                while(Available&&counts.eligible==0&&doneHist.Any(x=>!x))
                {
                    for(var h=0;h<2&&Available&&counts.eligible==0;h++)
                    {
                        if(doneHist[h])continue;var group=finals[h];if(group.Count==0){doneHist[h]=true;continue;}
                        var i=cursors[h]++;var pi=i%group.Count;var variation=i/group.Count;var pair=group[pi];
                        if(pair.plans.Count==0||variation>=pair.plans.Count*24){if(group.All(g=>variation>=g.plans.Count*24))doneHist[h]=true;continue;}
                        var mi=(variation/2)%pair.plans.Count;var epoch=variation/(pair.plans.Count*2);var layout=(mi+epoch)%12;
                        var commands=Commands(pair.parent,pair.plans[mi],layout,8);if(commands==null)continue;
                        Evaluate(pair.parent,commands,CastPolicy(8,(mi*5+variation%2+epoch*2)%24),8);
                    }
                }
                // Полные NewGame replay — внутри того же cap; wave-only clones не являются доказательством победы.
                var verify=new List<Node>();foreach(var n in best.Values)verify.Add(n);
                if(counts.eligible>0)
                {
                    var winnerPath=output+"/winner.commands.json";var script=JsonUtility.FromJson<BossMissionScript>(File.ReadAllText(winnerPath));
                    counts.fullNewGameOutcomes++;var run=new AbilityBossTrace.Run(BossWaveTests.Config().CreateSimulation(),script);run.Play();
                    AbilityBossTrace.Write(output+"/winner.full",run);Assert.That(run.report.outcome,Is.EqualTo("Victory"));Assert.That(run.report.bossKills,Is.EqualTo(1));
                    Assert.That(run.report.abilityKills,Is.GreaterThan(0));Assert.That(run.report.abilityApplied,Is.GreaterThan(0));Assert.That(run.report.cells.All(c=>c.applied>0),Is.True);
                }
                foreach(var n in verify)
                {
                    counts.fullNewGameOutcomes++;var run=new AbilityBossTrace.Run(BossWaveTests.Config().CreateSimulation(),Script(n));run.Play();
                    AbilityBossTrace.Write(output+"/best-"+n.history+".full",run);
                    Assert.That(run.report.outcome,Is.EqualTo(n.row.phase));Assert.That(run.report.enemies.FirstOrDefault(e=>e.type=="rift-warden")?.remaining??1000,Is.EqualTo(n.row.bossHp));
                    Assert.That(run.report.cells.Select(c=>c.applied).ToArray(),Is.EqualTo(n.row.cells));Assert.That(run.report.abilityKills,Is.EqualTo(n.row.abilityKills));
                }
                counts.seconds=watch.Elapsed.TotalSeconds;counts.stop=counts.eligible>0?"first REG214 full-NewGame confirmed":counts.Total>=cap-4?"outcome cap":watch.Elapsed.TotalSeconds>=590?"wall cap (10s replay reserve)":"finite branch exhaustion";
                Progress(8,"complete");File.WriteAllText(output+"/summary.json",JsonUtility.ToJson(counts,true));
                File.WriteAllText(output+"/limits.txt",$"start={start};full={full};cap={cap};waveOutcomes={counts.waveOutcomes};fullNewGameOutcomes={counts.fullNewGameOutcomes};total={counts.Total};wallLimitSeconds=600;stop={counts.stop};beam32;materialTop80+profile64+reserve12;newUnitsMax4;armyMax8;layouts12;prefixPolicies16;finalPolicies24;no runtime/DTO/live edits;exact dedup before evaluation\n");
                Assert.That(counts.Total,Is.LessThanOrEqualTo(cap));Assert.That(counts.invalidSuffixes,Is.Zero);Assert.That(counts.waveOutcomes,Is.GreaterThan(0));
            }
        }
        [TestCase("flame")] [TestCase("gale")] [Timeout(650000)]
        public void RegBattle214_TargetedOrderedHistoriesBoundedPass(string start)
        {using(var engine=new Engine(start,Environment.GetEnvironmentVariable("P24F_TARGETED_SEARCH")=="1"))engine.Run();}
    }
}
