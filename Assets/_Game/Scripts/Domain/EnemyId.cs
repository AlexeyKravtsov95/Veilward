using System;

namespace Veilward.Domain
{
    public readonly struct EnemyId : IEquatable<EnemyId>
    {
        public EnemyId(int value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Enemy ID must be positive.");
            }

            Value = value;
        }

        public int Value { get; }

        public bool Equals(EnemyId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is EnemyId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static bool operator ==(EnemyId left, EnemyId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(EnemyId left, EnemyId right)
        {
            return !left.Equals(right);
        }
    }
}
