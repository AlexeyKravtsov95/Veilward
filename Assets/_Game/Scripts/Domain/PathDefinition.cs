using System;
using System.Collections.Generic;

namespace Veilward.Domain
{
    public sealed class PathDefinition
    {
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

            for (var index = 1; index < copy.Count; index++)
            {
                if (copy[index - 1] == copy[index])
                {
                    throw new ArgumentException("Adjacent path points must be different.", nameof(points));
                }
            }

            Points = copy.AsReadOnly();
        }

        public IReadOnlyList<GridCoordinate> Points { get; }
    }
}
