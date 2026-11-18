using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Veilward.Domain;
using Veilward.Tests.Shared;
using Veilward.Infrastructure;
namespace Veilward.Tests.EditMode
{
    public sealed class BossCandidateMatrixTests
    {
        public const string Output="TestResults/P2.4e/revision-3";
        // Definition заменяется до создания New Game, live state/checkpoint никогда не правятся.
        internal static BattleSimulation Candidate(int hp,int armor)
        {
            Assert.That((hp==1000&&armor==10)||(hp==900&&armor==10)||(hp==800&&armor==10)||(hp==1000&&armor==8)||(hp==1000&&armor==5),Is.True);
            var template=BossWaveTests.Config().CreateSimulation();
            var waves=template.Waves.ToArray();var old=waves[7].Groups.Last().Enemy;
            var boss=new EnemyDefinition(old.Id,hp,old.Speed,old.LeakDamage,old.Reward,armor);
            waves[7]=new WaveDefinition(waves[7].Groups.Select(g=>new WaveSpawnGroup(g.Enemy.Id==old.Id?boss:g.Enemy,g.Count,g.FirstSpawnTick,g.SpawnIntervalTicks)).ToArray());
            return new BattleSimulation(template.Grid.Map,template.Path,waves,template.TowerCatalog,20,150,20,
                template.ReactionCatalog,new ElementUnlockRules(new[]{ElementKind.Flame,ElementKind.Gale},new[]{3,6}),template.AbilityDefinition);
        }
        internal static BossMissionScript R2(string start)=>JsonUtility.FromJson<BossMissionScript>(File.ReadAllText("Docs/review/P2.4e/revision-2/"+start+".commands.json"));
        internal static bool Coverage(BossNaturalSmokeTests.Result r)=>new[]{"Steam/StatusPair","Steam/Intrinsic","Storm/StatusPair","Storm/Intrinsic"}.All(key=>r.reactions.Any(c=>c.kind+"/"+c.trigger==key&&c.appliedHpLoss>0));
        internal static void Write(string path,BossMissionScript script,BossNaturalSmokeTests.Result r)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path+".json",JsonUtility.ToJson(r,true));
            File.WriteAllLines(path+".waves.jsonl",r.waves.Select(e=>WaveCheckpointJson.Serialize(e)));
            script.casts=r.casts.ToArray();File.WriteAllText(path+".commands.json",JsonUtility.ToJson(script,true));
        }
        [TestCase(1000,10)] [TestCase(900,10)] [TestCase(800,10)] [TestCase(1000,8)] [TestCase(1000,5)]
        public void RegBattle213_R3ImmutableCandidateMatrix(int hp,int armor)
        {
            foreach(var start in new[]{"flame","gale"})foreach(var coverage in new[]{false,true})
            {
                var script=coverage?JsonUtility.FromJson<BossMissionScript>(File.ReadAllText("Docs/review/P2.3e.1/"+(start=="flame"?"A":"B")+"-coverage.commands.json")):R2(start);
                var sim=BossNaturalSmokeTests.Run(start,false,out var r,0,false,()=>Candidate(hp,armor),script);
                Write(Output+"/matrix/"+hp+"-"+armor+"/"+start+(coverage?"-coverage":"-stone"),script,r);
                Assert.That(r.bossMaxHealth,Is.EqualTo(hp));Assert.That(r.bossArmor,Is.EqualTo(armor));
                Assert.That(sim.State.Gold,Is.GreaterThanOrEqualTo(0));
                Assert.That(r.bossDirectHpLoss+r.bossBurnHpLoss+r.bossReactionHpLoss+r.bossAbilityHpLoss+r.bossHealthBeforeLeak,Is.EqualTo(hp));
                if(hp==1000&&armor==10&&!coverage)
                {Assert.That(sim.State.Phase,Is.EqualTo(BattlePhase.Defeat));Assert.That(r.bossHealthBeforeLeak,Is.EqualTo(start=="flame"?154:68));}
            }
        }
        [TestCase(1000,10)] [TestCase(900,10)] [TestCase(800,10)] [TestCase(1000,8)] [TestCase(1000,5)]
        public void RegBattle213_R3FactoryChangesOnlyApprovedBossField(int hp,int armor)
        {
            var a=Candidate(1000,10);var b=Candidate(hp,armor);
            Assert.That(WaveCheckpointJson.Serialize(BossNaturalSmokeTests.Envelope(b)),Is.EqualTo(WaveCheckpointJson.Serialize(BossNaturalSmokeTests.Envelope(a))));
            Assert.That(b.State.Gold,Is.EqualTo(150));Assert.That(b.State.BaseHealth,Is.EqualTo(20));
            for(var w=0;w<8;w++)for(var i=0;i<a.Waves[w].Schedule.Count;i++)
            {
                var x=a.Waves[w].Schedule[i];var y=b.Waves[w].Schedule[i];Assert.That(y.RelativeTick,Is.EqualTo(x.RelativeTick));
                Assert.That(y.Enemy.Id,Is.EqualTo(x.Enemy.Id));Assert.That(y.Enemy.Speed,Is.EqualTo(x.Enemy.Speed));
                Assert.That(y.Enemy.LeakDamage,Is.EqualTo(x.Enemy.LeakDamage));Assert.That(y.Enemy.Reward,Is.EqualTo(x.Enemy.Reward));
                Assert.That(y.Enemy.MaxHealth,Is.EqualTo(y.Enemy.Id.Value=="rift-warden"?hp:x.Enemy.MaxHealth));
                Assert.That(y.Enemy.Armor,Is.EqualTo(y.Enemy.Id.Value=="rift-warden"?armor:x.Enemy.Armor));
            }
        }
    }
}
