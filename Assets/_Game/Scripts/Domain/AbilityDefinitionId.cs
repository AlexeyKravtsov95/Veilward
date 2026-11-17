using System;

namespace Veilward.Domain
{
    public readonly struct AbilityDefinitionId : IEquatable<AbilityDefinitionId>
    {
        public AbilityDefinitionId(string value)
        {
            if (!string.Equals(value, "rift-seal", StringComparison.Ordinal))
                throw new ArgumentException("Unknown ability ID.", nameof(value));
            Value = value;
        }

        public static AbilityDefinitionId RiftSeal { get; } = new AbilityDefinitionId("rift-seal");
        public string Value { get; }
        public bool Equals(AbilityDefinitionId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is AbilityDefinitionId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(AbilityDefinitionId left, AbilityDefinitionId right) => left.Equals(right);
        public static bool operator !=(AbilityDefinitionId left, AbilityDefinitionId right) => !left.Equals(right);
    }
}
