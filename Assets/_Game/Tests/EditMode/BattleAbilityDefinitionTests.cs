using System;
using NUnit.Framework;
using Veilward.Domain;

namespace Veilward.Tests.EditMode
{
    public sealed class BattleAbilityDefinitionTests
    {
        [TestCase(null)] [TestCase("")] [TestCase(" ")] [TestCase("unknown")]
        public void RegBattle190_UnknownIdRejected(string id)
        {
            Assert.Throws<ArgumentException>(() => new AbilityDefinitionId(id));
        }

        [Test]
        public void RegBattle190_DefaultIdRejectedAndEnumValuesPreserved()
        {
            Assert.Throws<ArgumentException>(() => new AbilityDefinition(default, 120, 1.5f, 600));
            Assert.That((int)DamageOrigin.Direct, Is.EqualTo(0));
            Assert.That((int)DamageOrigin.Burn, Is.EqualTo(1));
            Assert.That((int)DamageOrigin.Reaction, Is.EqualTo(2));
            Assert.That((int)DamageOrigin.Ability, Is.EqualTo(3));
        }

        [TestCase(0)] [TestCase(-1)]
        public void RegBattle190_NonpositiveDamageRejected(int damage)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AbilityDefinition(AbilityDefinitionId.RiftSeal, damage, 1.5f, 600));
        }

        [TestCase(0f)] [TestCase(-1f)] [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        public void RegBattle190_InvalidRadiusRejected(float radius)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AbilityDefinition(AbilityDefinitionId.RiftSeal, 120, radius, 600));
        }

        [TestCase(0)] [TestCase(-1)] [TestCase(601)]
        public void RegBattle190_InvalidCooldownRejected(int cooldown)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AbilityDefinition(AbilityDefinitionId.RiftSeal, 120, 1.5f, cooldown));
        }

        [Test]
        public void RegBattle190_DefinitionAndCommandAreReadOnly()
        {
            foreach (var type in new[] { typeof(AbilityDefinition), typeof(AbilityDefinitionId), typeof(AbilityState),
                typeof(CastAbilityCommand), typeof(AbilityCastEvent), typeof(AbilityCastResult) })
            foreach (var property in type.GetProperties())
                Assert.That(property.GetSetMethod(), Is.Null, type.Name + "." + property.Name);
            Assert.That(new AbilityDefinitionId("rift-seal"), Is.EqualTo(AbilityDefinitionId.RiftSeal));
        }
    }
}
