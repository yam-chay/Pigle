using System;

namespace Piglings.Events
{
    /// <summary>
    /// Stable id for anything that can appear in an event (robot, throwable, chain).
    /// Plain value type so engine-free layers never hold references to GameObjects.
    /// Named GameId because Unity 6.5 ships its own UnityEngine.EntityId.
    /// </summary>
    public readonly struct GameId : IEquatable<GameId>
    {
        public readonly int Value;
        public GameId(int value) { Value = value; }

        public static readonly GameId None = new GameId(0);
        public bool IsNone => Value == 0;

        public bool Equals(GameId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is GameId o && Equals(o);
        public override int GetHashCode() => Value;
        public static bool operator ==(GameId a, GameId b) => a.Value == b.Value;
        public static bool operator !=(GameId a, GameId b) => a.Value != b.Value;
        public override string ToString() => $"#{Value}";
    }

    /// <summary>Hands out unique ids for one night. Owned by the session, never static.</summary>
    public sealed class IdAllocator
    {
        private int _next = 1;
        public GameId Next() => new GameId(_next++);
    }
}