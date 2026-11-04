using System;
using System.Collections.Generic;
using NUnit.Framework;
using Veilward.Domain;

namespace Veilward.Tests.EditMode
{
    public sealed class BattleStateTests
    {
        [Test]
        public void CreateInitial_WithValidArguments_StartsInCleanPreparationState()
        {
            var state = BattleState.CreateInitial(baseHealth: 20, startingGold: 150);

            Assert.That(state.Phase, Is.EqualTo(BattlePhase.Preparation));
            Assert.That(state.Tick, Is.Zero);
            Assert.That(state.WaveIndex, Is.Zero);
            Assert.That(state.BaseHealth, Is.EqualTo(20));
            Assert.That(state.Gold, Is.EqualTo(150));
            Assert.That(state.Enemies, Is.Empty);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void CreateInitial_NonPositiveBaseHealth_Throws(int baseHealth)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BattleState.CreateInitial(baseHealth, startingGold: 150));
        }

        [Test]
        public void CreateInitial_NegativeGold_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BattleState.CreateInitial(baseHealth: 20, startingGold: -1));
        }

        [Test]
        public void Constructor_DuplicateEnemyIds_Throws()
        {
            var definition = CreateEnemyDefinition();
            var enemies = new[]
            {
                EnemyState.CreateInitial(new EnemyId(1), definition),
                EnemyState.CreateInitial(new EnemyId(1), definition)
            };

            Assert.Throws<ArgumentException>(() =>
                new BattleState(BattlePhase.Combat, tick: 0, waveIndex: 0, baseHealth: 20, gold: 150, enemies: enemies));
        }

        internal static EnemyDefinition CreateEnemyDefinition()
        {
            return new EnemyDefinition(
                new EnemyDefinitionId("shade"),
                maxHealth: 100,
                speed: 1.5f,
                leakDamage: 1,
                reward: 10);
        }
    }

    public sealed class EnemyDefinitionTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void Constructor_InvalidId_Throws(string id)
        {
            Assert.Throws<ArgumentException>(() => new EnemyDefinitionId(id));
        }

        [Test]
        public void Constructor_DefaultId_Throws()
        {
            Assert.Throws<ArgumentException>(() => new EnemyDefinition(
                default, maxHealth: 100, speed: 1.5f, leakDamage: 1, reward: 10));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Constructor_NonPositiveMaxHealth_Throws(int maxHealth)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateDefinition(maxHealth: maxHealth));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Constructor_InvalidSpeed_Throws(float speed)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateDefinition(speed: speed));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Constructor_NonPositiveLeakDamage_Throws(int leakDamage)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateDefinition(leakDamage: leakDamage));
        }

        [Test]
        public void Constructor_NegativeReward_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateDefinition(reward: -1));
        }

        private static EnemyDefinition CreateDefinition(
            int maxHealth = 100,
            float speed = 1.5f,
            int leakDamage = 1,
            int reward = 10)
        {
            return new EnemyDefinition(new EnemyDefinitionId("shade"), maxHealth, speed, leakDamage, reward);
        }
    }

    public sealed class EnemyStateTests
    {
        [Test]
        public void CreateInitial_UsesDefinitionHealthAndZeroPathDistance()
        {
            var state = EnemyState.CreateInitial(new EnemyId(1), BattleStateTests.CreateEnemyDefinition());

            Assert.That(state.Health, Is.EqualTo(100));
            Assert.That(state.DistanceAlongPath, Is.Zero);
        }

        [Test]
        public void Constructor_DefaultId_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                EnemyState.CreateInitial(default, BattleStateTests.CreateEnemyDefinition()));
        }

        [Test]
        public void Constructor_NegativeHealth_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnemyState(
                new EnemyId(1), BattleStateTests.CreateEnemyDefinition(), health: -1, distanceAlongPath: 0f));
        }

        [Test]
        public void Constructor_HealthAboveMaximum_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnemyState(
                new EnemyId(1), BattleStateTests.CreateEnemyDefinition(), health: 101, distanceAlongPath: 0f));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void EnemyId_NonPositiveValue_Throws(int id)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnemyId(id));
        }
    }

    public sealed class GridCoordinateTests
    {
        [Test]
        public void Equality_SameCoordinates_ReturnsTrueAndSameHashCode()
        {
            var first = new GridCoordinate(2, 3);
            var second = new GridCoordinate(2, 3);

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first == second, Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }

        [Test]
        public void Equality_DifferentCoordinates_ReturnsFalse()
        {
            var first = new GridCoordinate(2, 3);
            var second = new GridCoordinate(3, 2);

            Assert.That(first, Is.Not.EqualTo(second));
            Assert.That(first != second, Is.True);
        }
    }

    public sealed class PathDefinitionTests
    {
        [TestCase(0)]
        [TestCase(1)]
        public void Constructor_FewerThanTwoPoints_Throws(int pointCount)
        {
            var points = new List<GridCoordinate>();
            for (var index = 0; index < pointCount; index++)
            {
                points.Add(new GridCoordinate(index, 0));
            }

            Assert.Throws<ArgumentException>(() => new PathDefinition(points));
        }

        [Test]
        public void Constructor_RepeatedAdjacentPoints_Throws()
        {
            var points = new[]
            {
                new GridCoordinate(0, 0),
                new GridCoordinate(0, 0),
                new GridCoordinate(1, 0)
            };

            Assert.Throws<ArgumentException>(() => new PathDefinition(points));
        }

        [Test]
        public void Constructor_ValidPoints_PreservesSequenceAndCopiesInput()
        {
            var points = new[]
            {
                new GridCoordinate(0, 0),
                new GridCoordinate(1, 0),
                new GridCoordinate(1, 1)
            };

            var path = new PathDefinition(points);
            points[0] = new GridCoordinate(9, 9);

            CollectionAssert.AreEqual(
                new[] { new GridCoordinate(0, 0), new GridCoordinate(1, 0), new GridCoordinate(1, 1) },
                path.Points);
        }
    }

    public sealed class SimulationContractTests
    {
        [Test]
        public void FixedSimulationFrequency_Is30Hz()
        {
            Assert.That(SimulationTiming.TicksPerSecond, Is.EqualTo(30));
        }
    }
}
