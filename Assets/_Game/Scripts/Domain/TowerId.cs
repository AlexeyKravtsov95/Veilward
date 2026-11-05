using System;

namespace Veilward.Domain
{
    public readonly struct TowerId : IEquatable<TowerId>
    {
        public TowerId(int value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Tower ID must be positive.");
            }

            Value = value;
        }

        public int Value { get; }

        public bool Equals(TowerId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is TowerId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static bool operator ==(TowerId left, TowerId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TowerId left, TowerId right)
        {
            return !left.Equals(right);
        }
    }
}
