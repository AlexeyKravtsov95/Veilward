using System;
using System.Collections.Generic;

namespace Veilward.Domain
{
    public sealed class PathDefinition
    {
        private readonly float[] _segmentLengths;
        private readonly float[] _cumulativeLengths;

        public PathDefinition(IEnumerable<GridCoordinate> points)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            var copy = new List<GridCoordinate>(points);
            if (copy.Count < 2)
            {
                throw new ArgumentException("A path requires at least two points.", nameof(points));
            }

            _segmentLengths = new float[copy.Count - 1];
            _cumulativeLengths = new float[copy.Count];
            for (var index = 1; index < copy.Count; index++)
            {
                var start = copy[index - 1];
                var end = copy[index];
                if (start == end)
                {
                    throw new ArgumentException("Adjacent path points must be different.", nameof(points));
                }

                var deltaX = end.X - start.X;
                var deltaY = end.Y - start.Y;
                var length = (float)Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                _segmentLengths[index - 1] = length;
                _cumulativeLengths[index] = _cumulativeLengths[index - 1] + length;
            }

            Points = copy.AsReadOnly();
            TotalLength = _cumulativeLengths[_cumulativeLengths.Length - 1];
        }

        public IReadOnlyList<GridCoordinate> Points { get; }
        public float TotalLength { get; }

        public PathPosition GetPosition(float distance)
        {
            if (float.IsNaN(distance) || float.IsInfinity(distance))
            {
                throw new ArgumentOutOfRangeException(nameof(distance), "Path distance must be finite.");
            }

            var clampedDistance = Math.Max(0f, Math.Min(distance, TotalLength));
            var segmentIndex = 0;
            while (segmentIndex < _segmentLengths.Length - 1 &&
                   clampedDistance > _cumulativeLengths[segmentIndex + 1])
            {
                segmentIndex++;
            }

            var start = Points[segmentIndex];
            var end = Points[segmentIndex + 1];
            var segmentDistance = clampedDistance - _cumulativeLengths[segmentIndex];
            var t = segmentDistance / _segmentLengths[segmentIndex];
            return new PathPosition(
                start.X + (end.X - start.X) * t,
                start.Y + (end.Y - start.Y) * t);
        }
    }
}
