using System;
using NUnit.Framework;
using Veilward.Domain;

namespace Veilward.Tests.EditMode
{
    public sealed class BattleGridTests
    {
        [Test]
        public void CoordinateConversion_RoundTripsCellCenter()
        {
            var map = CreateMap();
            var cell = new GridCoordinate(-5, -2);

            var center = map.GetCellCenter(cell);
            var found = map.TryGetCoordinate(center, out var actual);

            Assert.That(found, Is.True);
            Assert.That(actual, Is.EqualTo(cell));
            Assert.That(center.X, Is.EqualTo(-5f));
            Assert.That(center.Y, Is.EqualTo(-2f));
        }

        [TestCase(-6.5f, -4.5f, -6, -4, true)]
        [TestCase(5.49f, 3.49f, 5, 3, true)]
        [TestCase(5.5f, 0f, 0, 0, false)]
        [TestCase(0f, 3.5f, 0, 0, false)]
        public void TryGetCoordinate_RespectsMapBoundaries(
            float x,
            float y,
            int expectedX,
            int expectedY,
            bool expected)
        {
            var map = CreateMap();

            var found = map.TryGetCoordinate(new PathPosition(x, y), out var cell);

            Assert.That(found, Is.EqualTo(expected));
            if (expected)
            {
                Assert.That(cell, Is.EqualTo(new GridCoordinate(expectedX, expectedY)));
            }
        }

        [Test]
        public void Constructor_PathBlocksIntermediateCells()
        {
            var grid = CreateGrid(new[]
            {
                new GridCoordinate(-5, -2),
                new GridCoordinate(0, -2),
                new GridCoordinate(0, 2),
                new GridCoordinate(5, 2)
            });

            Assert.That(grid.GetState(new GridCoordinate(-3, -2)), Is.EqualTo(GridCellState.Blocked));
            Assert.That(grid.GetState(new GridCoordinate(0, 1)), Is.EqualTo(GridCellState.Blocked));
            Assert.That(grid.GetState(new GridCoordinate(3, 2)), Is.EqualTo(GridCellState.Blocked));
            Assert.That(grid.GetState(new GridCoordinate(-3, 0)), Is.EqualTo(GridCellState.Available));
        }

        [Test]
        public void Constructor_HalfSizeCells_BlockWorldSpacePathAtMatchingCoordinates()
        {
            var map = new GridMapDefinition(12, 8, new GridCoordinate(-6, -4), 0.5f);
            var path = new PathDefinition(new[]
            {
                new GridCoordinate(-2, 0),
                new GridCoordinate(2, 0)
            });

            var grid = new BattleGrid(map, path);

            Assert.That(grid.GetState(new GridCoordinate(-4, 0)), Is.EqualTo(GridCellState.Blocked));
            Assert.That(grid.GetState(new GridCoordinate(4, 0)), Is.EqualTo(GridCellState.Blocked));
            Assert.That(grid.GetState(new GridCoordinate(-5, 0)), Is.EqualTo(GridCellState.Available));
            Assert.That(map.GetCellCenter(new GridCoordinate(-4, 0)).X, Is.EqualTo(-2f));
        }

        [Test]
        public void Constructor_DiagonalPathBlocksEveryTraversedCell()
        {
            var grid = CreateGrid(new[]
            {
                new GridCoordinate(-2, -1),
                new GridCoordinate(0, 0)
            });

            Assert.That(grid.GetState(new GridCoordinate(-2, -1)), Is.EqualTo(GridCellState.Blocked));
            Assert.That(grid.GetState(new GridCoordinate(-1, -1)), Is.EqualTo(GridCellState.Blocked));
            Assert.That(grid.GetState(new GridCoordinate(-1, 0)), Is.EqualTo(GridCellState.Blocked));
            Assert.That(grid.GetState(new GridCoordinate(0, 0)), Is.EqualTo(GridCellState.Blocked));
        }

        [Test]
        public void Constructor_DiagonalCornerBlocksBothAdjacentCells()
        {
            var grid = CreateGrid(new[]
            {
                new GridCoordinate(-1, -1),
                new GridCoordinate(0, 0)
            });

            Assert.That(grid.GetState(new GridCoordinate(0, -1)), Is.EqualTo(GridCellState.Blocked));
            Assert.That(grid.GetState(new GridCoordinate(-1, 0)), Is.EqualTo(GridCellState.Blocked));
        }

        [TestCase(-7, -4)]
        [TestCase(6, -4)]
        [TestCase(-6, -5)]
        [TestCase(-6, 4)]
        public void GetState_OutsideMap_Throws(int x, int y)
        {
            var grid = CreateEmptyGrid();

            Assert.Throws<ArgumentOutOfRangeException>(() => grid.GetState(new GridCoordinate(x, y)));
        }

        [Test]
        public void Release_OnlyOccupiedCell_Succeeds()
        {
            var grid = CreateEmptyGrid();
            var occupied = new GridCoordinate(-3, 0);
            var blocked = new GridCoordinate(-6, -4);
            var available = new GridCoordinate(-2, 0);
            Assert.That(grid.TryOccupy(occupied), Is.True);

            Assert.That(grid.TryRelease(blocked), Is.False);
            Assert.That(grid.GetState(blocked), Is.EqualTo(GridCellState.Blocked));
            Assert.That(grid.TryRelease(available), Is.False);
            Assert.That(grid.GetState(available), Is.EqualTo(GridCellState.Available));
            Assert.That(grid.TryRelease(new GridCoordinate(20, 20)), Is.False);
            Assert.That(grid.TryRelease(occupied), Is.True);
            Assert.That(grid.GetState(occupied), Is.EqualTo(GridCellState.Available));
            Assert.That(grid.TryRelease(occupied), Is.False);
            Assert.That(grid.GetState(occupied), Is.EqualTo(GridCellState.Available));
        }

        [Test]
        public void Release_IsInternalToDomain()
        {
            var method = typeof(BattleGrid).GetMethod(
                "TryRelease",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);
            Assert.That(method.IsPublic, Is.False);
            Assert.That(method.IsAssembly, Is.True);
        }

        [Test]
        public void TryOccupy_IsInternalToDomain()
        {
            var method = typeof(BattleGrid).GetMethod(
                "TryOccupy",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);
            Assert.That(method.IsPublic, Is.False);
            Assert.That(method.IsAssembly, Is.True);
        }

        [TestCase(0, 8, 1f)]
        [TestCase(-1, 8, 1f)]
        [TestCase(12, 0, 1f)]
        [TestCase(12, 8, 0f)]
        [TestCase(12, 8, -1f)]
        [TestCase(12, 8, float.NaN)]
        [TestCase(12, 8, float.PositiveInfinity)]
        public void MapDefinition_InvalidDimensionsOrCellSize_Throws(int width, int height, float cellSize)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GridMapDefinition(
                width,
                height,
                new GridCoordinate(-6, -4),
                cellSize));
        }

        private static GridMapDefinition CreateMap()
        {
            return new GridMapDefinition(12, 8, new GridCoordinate(-6, -4), 1f);
        }

        private static BattleGrid CreateEmptyGrid()
        {
            return new BattleGrid(CreateMap(), new PathDefinition(new[]
            {
                new GridCoordinate(-6, -4),
                new GridCoordinate(-5, -4)
            }));
        }

        private static BattleGrid CreateGrid(GridCoordinate[] points)
        {
            return new BattleGrid(CreateMap(), new PathDefinition(points));
        }
    }
}
