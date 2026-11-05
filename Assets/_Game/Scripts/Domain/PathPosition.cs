using System;

namespace Veilward.Domain
{
    public readonly struct PathPosition
    {
        public PathPosition(float x, float y)
        {
            if (float.IsNaN(x) || float.IsInfinity(x))
            {
                throw new ArgumentOutOfRangeException(nameof(x), "X must be finite.");
            }

            if (float.IsNaN(y) || float.IsInfinity(y))
            {
                throw new ArgumentOutOfRangeException(nameof(y), "Y must be finite.");
            }

            X = x;
            Y = y;
        }

        public float X { get; }
        public float Y { get; }
    }
}
