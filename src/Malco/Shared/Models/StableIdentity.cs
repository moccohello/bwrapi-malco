using System;

namespace Malco.Models
{
    // StableIdentity is a domain key, not a native handle. Unit tags and
    // projection/resource identities are kept as canonical text so a hash
    // collision cannot merge two HUD records.
    internal readonly struct StableIdentity : IEquatable<StableIdentity>, IComparable<StableIdentity>
    {
        private readonly string _value;

        public StableIdentity(string value)
        {
            _value = value ?? string.Empty;
        }

        public static StableIdentity Empty => default(StableIdentity);

        public string Value => _value ?? string.Empty;

        public bool IsEmpty => string.IsNullOrEmpty(Value);

        public static StableIdentity FromUnitTag(string unitTag)
        {
            return string.IsNullOrWhiteSpace(unitTag)
                ? Empty
                : Create("unit", unitTag);
        }

        public static StableIdentity Create(string scope, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Empty;
            }

            return new StableIdentity(
                string.IsNullOrWhiteSpace(scope)
                    ? value
                    : scope.Trim() + ":" + value);
        }

        public int CompareTo(StableIdentity other)
        {
            return string.Compare(Value, other.Value, StringComparison.Ordinal);
        }

        public bool Equals(StableIdentity other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is StableIdentity && Equals((StableIdentity)obj);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value;
        }

        public static bool operator ==(StableIdentity left, StableIdentity right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(StableIdentity left, StableIdentity right)
        {
            return !left.Equals(right);
        }
    }
}
