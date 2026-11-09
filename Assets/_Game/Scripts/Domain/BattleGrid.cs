using System;

namespace Veilward.Domain
{
    public sealed class BattleGrid
    {
        private readonly GridCellState[] _cells;

        public BattleGrid(GridMapDefinition map, PathDefinition path)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            _cells = new GridCellState[checked(map.Width * map.Height)];
            for (var index = 0; index < path.Points.Count - 1; index++)
            {
                BlockSegment(path.Points[index], path.Points[index + 1]);
            }
        }

        public GridMapDefinition Map { get; }

        public GridCellState GetState(GridCoordinate coordinate)
        {
            if (!Map.Contains(coordinate))
            {
                throw new ArgumentOutOfRangeException(nameof(coordinate), "Cell is outside the map.");
            }

            return _cells[GetIndex(coordinate)];
        }

        internal bool TryOccupy(GridCoordinate coordinate)
        {
            if (!Map.Contains(coordinate))
            {
                return false;
            }

            var index = GetIndex(coordinate);
            if (_cells[index] != GridCellState.Available)
            {
                return false;
            }

            _cells[index] = GridCellState.Occupied;
            return true;
        }

        internal bool TryRelease(GridCoordinate coordinate)
        {
            if (!Map.Contains(coordinate))
            {
                return false;
            }

            var index = GetIndex(coordinate);
            if (_cells[index] != GridCellState.Occupied)
            {
                return false;
            }

            _cells[index] = GridCellState.Available;
            return true;
        }

        private int GetIndex(GridCoordinate coordinate)
        {
            return (coordinate.Y - Map.Origin.Y) * Map.Width + coordinate.X - Map.Origin.X;
        }

        private void BlockSegment(GridCoordinate start, GridCoordinate end)
        {
            var startX = start.X / (double)Map.CellSize;
            var startY = start.Y / (double)Map.CellSize;
            var endPositionX = end.X / (double)Map.CellSize;
            var endPositionY = end.Y / (double)Map.CellSize;
            var deltaX = endPositionX - startX;
            var deltaY = endPositionY - startY;
            var currentX = GetCoordinate(startX);
            var currentY = GetCoordinate(startY);
            var endX = GetCoordinate(endPositionX);
            var endY = GetCoordinate(endPositionY);
            var stepX = Math.Sign(deltaX);
            var stepY = Math.Sign(deltaY);
            var nextBoundaryX = stepX > 0 ? currentX + 0.5d : currentX - 0.5d;
            var nextBoundaryY = stepY > 0 ? currentY + 0.5d : currentY - 0.5d;
            var tMaxX = stepX == 0 ? double.PositiveInfinity : (nextBoundaryX - startX) / deltaX;
            var tMaxY = stepY == 0 ? double.PositiveInfinity : (nextBoundaryY - startY) / deltaY;
            var tDeltaX = stepX == 0 ? double.PositiveInfinity : Math.Abs(1d / deltaX);
            var tDeltaY = stepY == 0 ? double.PositiveInfinity : Math.Abs(1d / deltaY);
            var remainingSteps = Math.Abs(endX - currentX) + Math.Abs(endY - currentY) + 1;

            BlockCell(currentX, currentY);
            while ((currentX != endX || currentY != endY) && remainingSteps-- > 0)
            {
                if (Math.Abs(tMaxX - tMaxY) <= 1e-10d)
                {
                    BlockCell(currentX + stepX, currentY);
                    BlockCell(currentX, currentY + stepY);
                    currentX += stepX;
                    currentY += stepY;
                    tMaxX += tDeltaX;
                    tMaxY += tDeltaY;
                }
                else if (tMaxX < tMaxY)
                {
                    currentX += stepX;
                    tMaxX += tDeltaX;
                }
                else
                {
                    currentY += stepY;
                    tMaxY += tDeltaY;
                }

                BlockCell(currentX, currentY);
            }

            if (currentX != endX || currentY != endY)
            {
                throw new InvalidOperationException("Failed to rasterize path segment.");
            }
        }

        private static int GetCoordinate(double position)
        {
            var coordinate = Math.Floor(position + 0.5d);
            if (coordinate < int.MinValue || coordinate > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(position), "Path coordinate is outside the supported range.");
            }

            return (int)coordinate;
        }

        private void BlockCell(int x, int y)
        {
            var coordinate = new GridCoordinate(x, y);
            if (Map.Contains(coordinate))
            {
                _cells[GetIndex(coordinate)] = GridCellState.Blocked;
            }
        }
    }
}
