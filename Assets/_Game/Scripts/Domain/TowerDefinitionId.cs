using System;

namespace Veilward.Domain
{
    public readonly struct TowerDefinitionId : IEquatable<TowerDefinitionId>
    {
        public TowerDefinitionId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Tower definition ID cannot be empty.", nameof(value));
            }

            Value = value;
        }

        public string Value { get; }

        public bool Equals(TowerDefinitionId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is TowerDefinitionId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        public static bool operator ==(TowerDefinitionId left, TowerDefinitionId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TowerDefinitionId left, TowerDefinitionId right)
        {
            return !left.Equals(right);
        }
    }
}
