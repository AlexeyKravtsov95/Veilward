using System.Linq;
using NUnit.Framework;
using UnityEditor;
using Veilward.Domain;
using Veilward.Presentation;

namespace Veilward.Tests.EditMode
{
    public sealed class StormIntrinsicContractTests
    {
        [TestCase(1,110,75,2f)] [TestCase(2,165,90,2.25f)] [TestCase(3,220,100,2.5f)]
        public void Art3F05_ProductionPayloadHasTwoAdditionalTargetsAndNoBasicStatus(int level,int price,int percent,float range)
        {
            var config=AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/TestWave.asset");
            var tower=config.CreateSimulation().TowerCatalog.Get(new TowerDefinitionId("storm")).Levels[level-1];
            Assert.That(tower.Price,Is.EqualTo(price)); Assert.That(tower.StatusEffect,Is.Null);
            Assert.That(tower.IntrinsicReaction.Kind,Is.EqualTo(ReactionKind.Storm));
            Assert.That(tower.IntrinsicReaction.DamagePercent,Is.EqualTo(percent));
            Assert.That(tower.IntrinsicReaction.Range,Is.EqualTo(range));
            Assert.That(tower.IntrinsicReaction.TargetLimit,Is.EqualTo(2));
        }

        // Synthetic lineup; настоящий resolver/production definition, но не earned lifecycle.
        private static BattleSimulation Fixture()
        {
            var config=AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/TestWave.asset");
            var definition=config.CreateSimulation().TowerCatalog.Get(new TowerDefinitionId("storm")).Levels[0];
            var s=BattleReactionTests.Create(new[]{definition},gold:110);
            BattleReactionTests.Place(s,"storm",3); s.StartWave(); s.State.Enemies[0].Advance(5);
            return s;
        }

        [Test]
        public void Art3F05_TwoEligibleAdditionalTargetsLoseHpOncePrimaryOnlyDirectAndCooldownSuppresses()
        {
            var s=Fixture();var primary=s.State.Enemies[0];
            var second=BattleReactionTests.Add(s,2,4.5f);var third=BattleReactionTests.Add(s,3,4f);
            var fourth=BattleReactionTests.Add(s,4,3.5f);
            s.Step();var reaction=s.ReactionEvents.Single();var attack=s.AttackEvents.Single();
            Assert.That(reaction.Kind,Is.EqualTo(ReactionKind.Storm));
            Assert.That(reaction.TriggerKind,Is.EqualTo(ReactionTriggerKind.Intrinsic));
            Assert.That(reaction.SourceTowerId,Is.EqualTo(attack.AttackerTowerId));
            Assert.That(reaction.PrimaryEnemyId,Is.EqualTo(attack.TargetEnemyId)); Assert.That(reaction.Tick,Is.EqualTo(attack.Tick));
            var direct=s.DamageEvents.Single(d=>d.Origin==DamageOrigin.Direct);
            Assert.That(direct.SequenceInTick,Is.LessThan(reaction.SequenceInTick));
            var hits=s.DamageEvents.Where(d=>ReactionPresentationState.Matches(reaction,d)).ToArray();
            Assert.That(hits.Length,Is.EqualTo(2));
            CollectionAssert.AreEqual(new[]{second.Id,third.Id},hits.Select(d=>d.TargetEnemyId));
            CollectionAssert.AreEqual(new[]{1,2},hits.Select(d=>d.HitIndex));
            Assert.That(hits.All(d=>d.AppliedHpLoss==6 && d.HealthAfter==994 && d.TargetEnemyId!=primary.Id),Is.True);
            Assert.That(second.Health,Is.EqualTo(994));Assert.That(third.Health,Is.EqualTo(994));
            Assert.That(primary.Health,Is.EqualTo(991));Assert.That(fourth.Health,Is.EqualTo(1000));
            Assert.That(s.State.Enemies.All(e=>e.Statuses.Count==0),Is.True);
            s.ClearReactionEvents();s.ClearDamageEvents();s.ClearAttackEvents();
            BattleReactionTests.StepTo(s,21);
            Assert.That(s.AttackEvents.Count,Is.EqualTo(1)); Assert.That(s.ReactionEvents,Is.Empty);
            Assert.That(second.Health,Is.EqualTo(994));Assert.That(third.Health,Is.EqualTo(994));
            Assert.That(primary.Health,Is.EqualTo(982));
        }

        [Test]
        public void Art3F05_NoNeighborDoesNotReserveCooldownAndNextAttackCanProc()
        {
            var s=Fixture();var primary=s.State.Enemies[0];s.Step();
            Assert.That(s.ReactionEvents,Is.Empty); Assert.That(primary.LastStormTick,Is.Null);
            Assert.That(primary.Health,Is.EqualTo(991));
            var second=BattleReactionTests.Add(s,2,4.5f);s.ClearAttackEvents();s.ClearDamageEvents();
            BattleReactionTests.StepTo(s,21);
            Assert.That(s.ReactionEvents.Single().TriggerKind,Is.EqualTo(ReactionTriggerKind.Intrinsic));
            Assert.That(second.Health,Is.EqualTo(994));Assert.That(primary.Health,Is.EqualTo(982));
        }
    }
}
