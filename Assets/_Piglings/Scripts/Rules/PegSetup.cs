using System.Collections.Generic;

namespace Piglings.Rules
{
    /// <summary>What Rules need to know about a peg type: plain values from PegDefinition (Rules can't read it).</summary>
    public sealed class PegType
    {
        public string Id { get; }
        public int MaxLevel { get; }
        public bool Mergeable { get; }   // throwing one onto a placed peg of the same type levels it up

        public PegType(string id, int maxLevel = 3, bool mergeable = true)
        {
            Id = id;
            MaxLevel = maxLevel < 1 ? 1 : maxLevel;
            Mergeable = mergeable;
        }
    }

    /// <summary>
    /// The night's pegs, as plain values: the shelf's loadout (types and counts), how many pegs may be thrown per
    /// threshold, and how many sockets the wall has. Built once per night by NightSession.
    /// </summary>
    public sealed class PegSetup
    {
        /// <summary>The peg shelf holds at most this many peg types (one pile each). Design rule, not a tuning value.</summary>
        public const int ShelfCapacity = 5;

        private readonly List<(PegType type, int count)> _loadout = new List<(PegType, int)>();

        public IReadOnlyList<(PegType type, int count)> Loadout => _loadout;
        public int ThrowsPerThreshold { get; }   // the seam for a future "+1 peg per threshold" upgrade
        public int SocketCount { get; }          // one per Hold; also the most pegs the wall can hold

        /// <summary>
        /// Loadout entries with the same id are added together; types past ShelfCapacity, nulls and counts
        /// below 1 are dropped (DroppedTypes says how many types didn't fit, so the caller can warn).
        /// </summary>
        public PegSetup(IEnumerable<(PegType type, int count)> loadout = null, int throwsPerThreshold = 1, int socketCount = 0)
        {
            ThrowsPerThreshold = throwsPerThreshold < 0 ? 0 : throwsPerThreshold;
            SocketCount = socketCount < 0 ? 0 : socketCount;
            if (loadout == null) return;
            foreach (var (type, count) in loadout)
            {
                if (type == null || string.IsNullOrEmpty(type.Id) || count < 1) continue;
                int i = _loadout.FindIndex(e => e.type.Id == type.Id);
                if (i >= 0) { _loadout[i] = (_loadout[i].type, _loadout[i].count + count); continue; }
                if (_loadout.Count >= ShelfCapacity) { DroppedTypes++; continue; }
                _loadout.Add((type, count));
            }
        }

        public int DroppedTypes { get; }

        public PegType Find(string id)
        {
            foreach (var (type, _) in _loadout) if (type.Id == id) return type;
            return null;
        }
    }
}
