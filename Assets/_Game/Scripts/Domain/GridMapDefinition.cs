using System;

namespace Veilward.Domain
{
    public sealed class GridMapDefinition
    {
        public GridMapDefinition(int width, int height, GridCoordinate origin, float cellSize)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Grid width must be positive.");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height), "Grid height must be positive.");
            }

            if (cellSize <= 0f || float.IsNaN(cellSize) || float.IsInfinity(cellSize))
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be finite and positive.");
            }

            Width = width;
            Height = height;
            Origin = origin;
            CellSize = cellSize;
        }

        public int Width { get; }
        public int Height { get; }
        public GridCoordinate Origin { get; }
        public float CellSize { get; }

        public bool Contains(GridCoordinate coordinate)
        {
            return (long)coordinate.X >= Origin.X &&
                   (long)coordinate.Y >= Origin.Y &&
                   (long)coordinate.X < (long)Origin.X + Width &&
                   (long)coordinate.Y < (long)Origin.Y + Height;
        }

        public bool TryGetCoordinate(PathPosition position, out GridCoordinate coordinate)
        {
            var x = Math.Floor(position.X / (double)CellSize + 0.5d);
            var y = Math.Floor(position.Y / (double)CellSize + 0.5d);
            if (x < int.MinValue || x > int.MaxValue || y < int.MinValue || y > int.MaxValue)
            {
                coordinate = default;
                return false;
            }

            coordinate = new GridCoordinate((int)x, (int)y);
            if (Contains(coordinate))
            {
                return true;
            }

            coordinate = default;
            return false;
        }

        public PathPosition GetCellCenter(GridCoordinate coordinate)
        {
            if (!Contains(coordinate))
            {
                throw new ArgumentOutOfRangeException(nameof(coordinate), "Cell is outside the map.");
            }

            return new PathPosition(coordinate.X * CellSize, coordinate.Y * CellSize);
        }
    }
}
