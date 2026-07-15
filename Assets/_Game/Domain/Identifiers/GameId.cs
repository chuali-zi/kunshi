using System;

namespace ConGame.Domain.Identifiers
{

    public readonly struct GameId : IEquatable<GameId>
    {
        public GameId(string value)
        {
            Value = value ?? string.Empty;
        }

        public string Value { get; }

        public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

        public bool Equals(GameId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is GameId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        public static implicit operator GameId(string value)
        {
            return new GameId(value);
        }

        public static implicit operator string(GameId id)
        {
            return id.Value;
        }
    }
}
