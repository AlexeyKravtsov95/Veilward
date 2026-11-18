using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using Veilward.Domain;
using Veilward.Presentation;

namespace Veilward.Tests.EditMode
{
    public sealed class BossWaveTests
    {
        internal const string ConfigPath = "Assets/_Game/Configs/EightWaveAbilityBoss.asset";
        internal static BattleScenarioConfig Config() => AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>(ConfigPath);
        [Test] public void RegBattle209_MultiGroupScheduleIsAvailable()
        {
            Assert.That(typeof(WaveDefinition).Assembly.GetType("Veilward.Domain.WaveSpawnGroup"), Is.Not.Null,
                "REG209 requires validated multi-group schedule");
        }
        [Test] public void RegBattle212_FinalContentEnablesAbilityAndNativeProgressionTogether()
        {
            var config = Config(); Assert.That(config, Is.Not.Null, "versioned final wrapper");
            Assert.That(config.ScenarioContentId, Is.EqualTo("eight-wave-ability-boss-v1"));
            var s = config.CreateSimulation();
            Assert.That(s.AbilityState.RulesId == "rift-seal-v1", Is.True);
            Assert.That(s.State.ElementProgression.PendingChoiceId, Is.EqualTo("start-resonance"));
        }

        internal static EnemyDefinition Boss() => new EnemyDefinition(new EnemyDefinitionId("rift-warden"), 1000, 1, 20, 50, 10);
        internal static EnemyDefinition Shade() => new EnemyDefinition(new EnemyDefinitionId("shade"), 100, 1.5f, 1, 5);
        internal static BattleSimulation Fixture(WaveDefinition wave, int hp = 20, TowerDefinition tower = null)
        {
            tower = tower ?? new TowerDefinition(new TowerDefinitionId("fixture"), 0, 10, 30, 1);
            return new BattleSimulation(new GridMapDefinition(12,8,new GridCoordinate(-6,-4),1),
                new PathDefinition(new[] { new GridCoordinate(0,0), new GridCoordinate(5,0) }),
                new[] { wave }, new TowerCatalog(new[] { TowerArchetypeDefinition.CreateCompatibility(new[] { tower }) }),
                hp,150,20,false,Config().CreateSimulation().ReactionCatalog,null,AbilityDefinition.RiftSeal);
        }
        internal static CastAbilityCommand Cast(BattleSimulation s, PathPosition? center = null) => new CastAbilityCommand(
            AbilityDefinitionId.RiftSeal, center ?? new PathPosition(0,0),s.State.WaveIndex,s.State.Tick,s.AbilityState.NextCastSequence);
        [Test] public void RegBattle209_OrderedScheduleAndLegacyAdapterHaveIdenticalSpawns()
        {
            var a=Shade();var b=Boss();
            var wave=new WaveDefinition(new[] { new WaveSpawnGroup(a,3,30,30),new WaveSpawnGroup(b,2,0,30) });
            CollectionAssert.AreEqual(new long[]{0,30,30,60,90},wave.Schedule.Select(e=>e.RelativeTick));
            CollectionAssert.AreEqual(new[]{1,0,1,0,0},wave.Schedule.Select(e=>e.GroupIndex));
            var s=Fixture(wave);s.StartWave();
            for(var i=0;i<90;i++)s.Step();
            CollectionAssert.AreEqual(new[]{1,2,3,4,5},s.State.Enemies.Select(e=>e.Id.Value));
            CollectionAssert.AreEqual(new[]{"rift-warden","shade","rift-warden","shade","shade"},s.State.Enemies.Select(e=>e.Definition.Id.Value));
            var legacy=new WaveDefinition(a,12,0,30);var grouped=new WaveDefinition(new[]{new WaveSpawnGroup(a,12,0,30)});
            CollectionAssert.AreEqual(legacy.Schedule.Select(e=>e.RelativeTick),grouped.Schedule.Select(e=>e.RelativeTick));
        }
        [Test] public void RegBattle209_FinalScheduleIsExactAndOldMissionIsUnchanged()
        {
            var s=Config().CreateSimulation();var old=AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWave.asset").CreateSimulation();
            Assert.That(old.AbilityDefinition,Is.Null);
            for(var w=0;w<8;w++)
            {
                Assert.That(s.Waves[w].Count,Is.EqualTo(5+w));
                for(var i=0;i<s.Waves[w].Count;i++)
                {
                    var spawn=s.Waves[w].Schedule[i];Assert.That(spawn.RelativeTick,Is.EqualTo(i*30));
                    Assert.That(spawn.Enemy.Id.Value,Is.EqualTo(w==7&&i==11?"rift-warden":"shade"));
                    Assert.That(old.Waves[w].Schedule[i].Enemy.Id.Value,Is.EqualTo("shade"));
                }
            }
        }
        [TestCase(0,0,30)] [TestCase(-1,0,30)] [TestCase(1,-1,30)] [TestCase(1,0,0)] [TestCase(1,0,-1)]
        public void RegBattle209_InvalidGroupsRejected(int count,int first,int interval) =>
            Assert.Throws<ArgumentOutOfRangeException>(()=>new WaveSpawnGroup(Shade(),count,first,interval));
        [Test] public void RegBattle209_BoundedCountsNullGroupsAndMissingDefinitionsReject()
        {
            Assert.Throws<ArgumentNullException>(()=>new WaveSpawnGroup(null,1,0,30));
            Assert.Throws<ArgumentException>(()=>new WaveDefinition(Array.Empty<WaveSpawnGroup>()));
            Assert.Throws<ArgumentException>(()=>new WaveDefinition(new WaveSpawnGroup[]{null}));
            Assert.Throws<ArgumentException>(()=>new WaveDefinition(new[]{new WaveSpawnGroup(Shade(),int.MaxValue,0,30)}));
            Assert.Throws<ArgumentException>(()=>new WaveDefinition(new[]{new WaveSpawnGroup(Shade(),32769,0,30),new WaveSpawnGroup(Boss(),32768,0,30)}));
        }
        [TestCase(false)] [TestCase(true)] public void RegBattle209_IdentifierAndAbsoluteTickOverflowRejectBeforePhaseMutation(bool tick)
        {
            var s=Fixture(new WaveDefinition(Boss(),2,int.MaxValue,int.MaxValue));var cp=s.ExportCheckpoint();
            if(tick)cp.tick=long.MaxValue-1;else cp.nextEnemyId=int.MaxValue-2;
            s.RestoreCheckpoint(cp);var before=BattleAbilityTests.FullSnapshot(s);
            Assert.That(s.StartWave(),Is.EqualTo(tick?WaveTransitionResult.RejectedScheduleOverflow:WaveTransitionResult.RejectedIdExhausted));
            Assert.That(BattleAbilityTests.FullSnapshot(s),Is.EqualTo(before));
        }
        [Test] public void RegBattle209_FutureBossAndFutureShadePreventEarlyCompletion()
        {
            var s=Fixture(new WaveDefinition(new[]{new WaveSpawnGroup(Shade(),1,0,30),new WaveSpawnGroup(Boss(),1,330,30),new WaveSpawnGroup(Shade(),1,600,30)}));
            s.StartWave();Assert.That(s.CastAbility(Cast(s)).Succeeded,Is.True);
            for(var i=0;i<329;i++)s.Step();Assert.That(s.State.Enemies,Is.Empty);Assert.That(s.State.Phase,Is.EqualTo(BattlePhase.Combat));
            s.Step();Assert.That(s.State.Enemies.Single().Definition.Id.Value,Is.EqualTo("rift-warden"));
            s.State.Enemies[0].ApplyDamage(1000);s.Step();Assert.That(s.State.Phase,Is.EqualTo(BattlePhase.Combat));
            for(var i=331;i<600;i++)s.Step();Assert.That(s.State.Enemies.Single().Definition.Id.Value,Is.EqualTo("shade"));
        }
        [TestCase(false)] [TestCase(true)] public void RegBattle210_AbilityArmorCrackAndStatusesHaveNoImmunityOrNewProc(bool crack)
        {
            var s=Fixture(new WaveDefinition(Boss(),1,0,30));s.StartWave();var e=s.State.Enemies[0];
            foreach(var kind in new[]{StatusKind.Burn,StatusKind.Wet,StatusKind.Gust,StatusKind.Crack})
                if(kind!=StatusKind.Crack||crack)e.ApplyStatus(new StatusEffectDefinition(kind,kind==StatusKind.Gust?50:kind==StatusKind.Crack?4:3,90,kind==StatusKind.Burn?15:0),new TowerId(1),0);
            var refs=e.Statuses.ToArray();var result=s.CastAbility(Cast(s));
            Assert.That(result.TotalAppliedHpLoss,Is.EqualTo(crack?114:110));Assert.That(s.ReactionEvents,Is.Empty);Assert.That(s.AttackEvents,Is.Empty);
            CollectionAssert.AreEqual(refs,e.Statuses);Assert.That(e.LastSteamTick,Is.Null);Assert.That(e.LastStormTick,Is.Null);
        }
        [TestCase(DamageOrigin.Direct)] [TestCase(DamageOrigin.Burn)] [TestCase(DamageOrigin.Reaction)] [TestCase(DamageOrigin.Ability)]
        public void RegBattle210_EachLethalOriginPaysExactlyOnce(DamageOrigin origin)
        {
            // Synthetic lethal boundary; boss definition remains1000/1/10/20/50.
            var tower=new TowerDefinition(new TowerDefinitionId("fixture"),0,10,30,origin==DamageOrigin.Reaction?60:1100,
                origin==DamageOrigin.Reaction?new StatusEffectDefinition(StatusKind.Wet,20,90,0):null);
            var s=Fixture(new WaveDefinition(Boss(),1,0,30),tower:tower);
            if(origin==DamageOrigin.Direct||origin==DamageOrigin.Reaction)s.PlaceTower(new PlaceTowerCommand(s.TowerDefinition.Id,new GridCoordinate(0,1)));
            s.StartWave();var boss=s.State.Enemies[0];
            if(origin==DamageOrigin.Reaction) {boss.ApplyDamage(900);boss.ApplyStatus(new StatusEffectDefinition(StatusKind.Burn,3,90,15),new TowerId(1),0);}
            if(origin==DamageOrigin.Burn) {boss.ApplyDamage(990);boss.ApplyStatus(new StatusEffectDefinition(StatusKind.Burn,30,90,1),new TowerId(1),0);}
            if(origin==DamageOrigin.Ability) {boss.ApplyDamage(900);Assert.That(s.CastAbility(Cast(s)).Succeeded,Is.True);}else s.Step();
            Assert.That(s.DamageEvents.Any(e=>e.Origin==origin&&e.WasLethal),Is.True);
            Assert.That(s.State.Phase,Is.EqualTo(BattlePhase.Victory));Assert.That(s.State.Gold,Is.EqualTo(220));Assert.That(s.State.Kills,Is.EqualTo(1));
            var before=BattleAbilityTests.FullSnapshot(s);s.Step();s.StartWave();s.ContinueToNextWave();Assert.That(s.CastAbility(Cast(s)).Succeeded,Is.False);
            Assert.That(BattleAbilityTests.FullSnapshot(s),Is.EqualTo(before));
        }
        [TestCase(1)] [TestCase(13)] [TestCase(20)] public void RegBattle211_BossLeakClampsAndNeverRewards(int hp)
        {
            var s=Fixture(new WaveDefinition(Boss(),1,0,30),hp);s.StartWave();s.State.Enemies[0].Advance(5);s.Step();
            Assert.That(s.State.Phase,Is.EqualTo(BattlePhase.Defeat));Assert.That(s.State.BaseHealth,Is.Zero);Assert.That(s.State.Kills,Is.Zero);Assert.That(s.State.Gold,Is.EqualTo(150));
            Assert.That(s.CastAbility(Cast(s)).FailureReason,Is.EqualTo(AbilityCastFailureReason.InvalidPhase));
        }
        [TestCase(false)] [TestCase(true)] public void RegBattle211_BurnOrCastBeforeMovementPreventsLethalLeak(bool cast)
        {
            var s=Fixture(new WaveDefinition(Boss(),1,0,30));s.StartWave();var boss=s.State.Enemies[0];boss.Advance(5);boss.ApplyDamage(990);
            if(cast)Assert.That(s.CastAbility(Cast(s,new PathPosition(5,0))).Succeeded,Is.True);
            else {boss.ApplyStatus(new StatusEffectDefinition(StatusKind.Burn,30,90,1),new TowerId(1),0);s.Step();}
            Assert.That(s.State.Phase,Is.EqualTo(BattlePhase.Victory));Assert.That(s.State.BaseHealth,Is.EqualTo(20));Assert.That(s.State.Gold,Is.EqualTo(220));
        }
        [Test] public void RegBattle211_DeadBossWithLivingShadeAndOtherKillOnLethalStep()
        {
            var s=Fixture(new WaveDefinition(new[]{new WaveSpawnGroup(Boss(),1,0,30),new WaveSpawnGroup(Shade(),1,0,30)}));s.StartWave();
            s.State.Enemies[0].ApplyDamage(990);s.State.Enemies[1].Advance(4);
            s.CastAbility(Cast(s));Assert.That(s.State.Phase,Is.EqualTo(BattlePhase.Combat));Assert.That(s.State.Gold,Is.EqualTo(200));
            var tower=new TowerDefinition(new TowerDefinitionId("fixture"),0,10,30,1100);
            s=Fixture(new WaveDefinition(new[]{new WaveSpawnGroup(Boss(),1,0,30),new WaveSpawnGroup(Shade(),1,0,30)}),tower:tower);
            s.PlaceTower(new PlaceTowerCommand(s.TowerDefinition.Id,new GridCoordinate(0,1)));s.StartWave();s.State.Enemies[0].Advance(5);s.Step();
            Assert.That(s.State.Phase,Is.EqualTo(BattlePhase.Defeat));Assert.That(s.State.Kills,Is.EqualTo(1));Assert.That(s.State.Gold,Is.EqualTo(155));
        }

        [TestCase(ReactionKind.Steam,false)] [TestCase(ReactionKind.Steam,true)]
        [TestCase(ReactionKind.Storm,false)] [TestCase(ReactionKind.Storm,true)]
        public void RegBattle210_BothReactionsReserveBossAsPrimaryAndSecondary(ReactionKind kind,bool secondary)
        {
            var tower=new TowerDefinition(new TowerDefinitionId("fixture"),0,10,30,30,new StatusEffectDefinition(StatusKind.Wet,20,90,0));
            var s=Fixture(new WaveDefinition(new[]{new WaveSpawnGroup(Boss(),1,0,30),new WaveSpawnGroup(Shade(),1,0,30)}),tower:tower);
            s.PlaceTower(new PlaceTowerCommand(tower.Id,new GridCoordinate(0,1)));s.StartWave();var boss=s.State.Enemies[0];var shade=s.State.Enemies[1];var primary=secondary?shade:boss;
            primary.Advance(1);primary.ApplyStatus(new StatusEffectDefinition(kind==ReactionKind.Steam?StatusKind.Burn:StatusKind.Gust,20,90,kind==ReactionKind.Steam?15:0),new TowerId(1),0);
            s.Step();Assert.That(s.ReactionEvents.Single().Kind,Is.EqualTo(kind));Assert.That(s.ReactionEvents.Single().PrimaryEnemyId,Is.EqualTo(primary.Id));
            Assert.That(kind==ReactionKind.Steam?boss.LastSteamTick:boss.LastStormTick,Is.EqualTo(1));
            Assert.That(s.DamageEvents.Any(e=>e.Origin==DamageOrigin.Reaction&&e.AppliedHpLoss>0),Is.True);
            if(secondary||kind==ReactionKind.Steam)Assert.That(s.DamageEvents.Any(e=>e.Origin==DamageOrigin.Reaction&&e.TargetEnemyId==boss.Id&&e.AppliedHpLoss>0),Is.True);
        }
        [Test] public void RegBattle210_WetMovementAndGustCrackUseOrdinaryBossRules()
        {
            var tower=new TowerDefinition(new TowerDefinitionId("fixture"),0,10,30,30);
            var s=Fixture(new WaveDefinition(Boss(),1,0,30),tower:tower);s.PlaceTower(new PlaceTowerCommand(tower.Id,new GridCoordinate(0,1)));s.StartWave();var boss=s.State.Enemies[0];
            boss.ApplyStatus(new StatusEffectDefinition(StatusKind.Wet,20,90,0),new TowerId(1),0);
            boss.ApplyStatus(new StatusEffectDefinition(StatusKind.Gust,50,90,0),new TowerId(1),0);
            boss.ApplyStatus(new StatusEffectDefinition(StatusKind.Crack,4,90,0),new TowerId(1),0);
            s.Step();Assert.That(boss.DistanceAlongPath,Is.EqualTo(1f/30*.8f).Within(.000001f));
            Assert.That(s.DamageEvents.Single().AppliedHpLoss,Is.EqualTo(36));Assert.That(boss.Health,Is.EqualTo(964));
        }
    }
}
