using System;
using NUnit.Framework;
using Veilward.Domain;

namespace Veilward.Tests.EditMode
{
    public sealed class PathGeometryTests
    {
        [Test]
        public void GetPosition_AcrossCorner_InterpolatesSecondSegment()
        {
            var path = CreateCornerPath();

            var position = path.GetPosition(3f);

            Assert.That(path.TotalLength, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(position.X, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(position.Y, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void GetPosition_ExactSegmentBoundary_ReturnsCorner()
        {
            var path = CreateCornerPath();

            var position = path.GetPosition(2f);

            Assert.That(position.X, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(position.Y, Is.Zero.Within(0.0001f));
        }

        [TestCase(-1f, 0f, 0f)]
        [TestCase(10f, 2f, 2f)]
        public void GetPosition_OutsidePath_ClampsToEndpoints(
            float distance,
            float expectedX,
            float expectedY)
        {
            var path = CreateCornerPath();

            var position = path.GetPosition(distance);

            Assert.That(position.X, Is.EqualTo(expectedX).Within(0.0001f));
            Assert.That(position.Y, Is.EqualTo(expectedY).Within(0.0001f));
        }

        [Test]
        public void GetPosition_Diagonal_UsesEuclideanLength()
        {
            var path = new PathDefinition(new[]
            {
                new GridCoordinate(0, 0),
                new GridCoordinate(3, 4)
            });

            var position = path.GetPosition(2.5f);

            Assert.That(path.TotalLength, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(position.X, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(position.Y, Is.EqualTo(2f).Within(0.0001f));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void GetPosition_NonFiniteDistance_Throws(float distance)
        {
            var path = CreateCornerPath();

            Assert.Throws<ArgumentOutOfRangeException>(() => path.GetPosition(distance));
        }

        private static PathDefinition CreateCornerPath()
        {
            return new PathDefinition(new[]
            {
                new GridCoordinate(0, 0),
                new GridCoordinate(2, 0),
                new GridCoordinate(2, 2)
            });
        }
    }
}
