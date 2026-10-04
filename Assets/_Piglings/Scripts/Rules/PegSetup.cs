using System.Collections.Generic;
using Piglings.Events;

namespace Piglings.Rules
{
    /// <summary>What Rules need to know about a peg type: plain values from PegDefinition (Rules can't read it).</summary>
    public sealed class PegType
    {
        public string Id { get; }
        public int MaxLevel { get; }
        public bool Mergeable { get; }   // throwing one onto a placed peg of the same type levels it up
        public PegEffect Effect { get; }
        /// <summary>The type has reached its follow-up copies (stage 2): placing one gives one more throw of the same type, once per round.</summary>
        public bool FollowUp { get; }

        // Splitter, whatever the level.
        public int MaxStonesPerThrow { get; }         // stones of one throw flying at once, the original included
        public bool SplitHitsCountForMastery { get; } // pieces' direct hits count as stone hits

        // Per level (index 0 = level 1). A level past the list uses the last entry.
        private readonly float[] _scoreMultipliers;   // Bouncy
        private readonly int[] _pieces;               // Splitter: stones after a split, the original included
        private readonly float[] _valueShares;        // Splitter: × the stone's value each stone carries after it
        private readonly float[] _cooldowns;          // Bomb: seconds spent after it goes off (wall time)

        public PegType(string id, int maxLevel = 3, bool mergeable = true, PegEffect effect = PegEffect.Plain,
                       IReadOnlyList<float> scoreMultipliers = null, IReadOnlyList<int> pieces = null,
                       IReadOnlyList<float> valueShares = null, int maxStonesPerThrow = 4, bool splitHitsCountForMastery = true,
                       IReadOnlyList<float> cooldowns = null, bool followUp = false)
        {
            FollowUp = followUp;
            Id = id;
            MaxLevel = maxLevel < 1 ? 1 : maxLevel;
            Mergeable = mergeable;
            Effect = effect;
            _scoreMultipliers = Copy(scoreMultipliers);
            _pieces = Copy(pieces);
            _valueShares = Copy(valueShares);
            _cooldowns = Copy(cooldowns);
            MaxStonesPerThrow = maxStonesPerThrow < 1 ? 1 : maxStonesPerThrow;
            SplitHitsCountForMastery = splitHitsCountForMastery;
        }

        /// <summary>Bouncy: the score multiplier at this level. Never below 1 (a 0 from an unfilled Inspector entry = no bonus).</summary>
        public float ScoreMultiplierAt(int level) => System.Math.Max(1f, At(_scoreMultipliers, level, 1f));

        /// <summary>Splitter: stones after a split at this level, the original included. At least 1 (1 = no split).</summary>
        public int PiecesAt(int level) => System.Math.Max(1, At(_pieces, level, 1));

        /// <summary>Splitter: × the stone's value each stone carries after a split at this level. Never negative.</summary>
        public float ValueShareAt(int level) => System.Math.Max(0f, At(_valueShares, level, 1f));

        /// <summary>Bomb: seconds it stays spent after going off at this level (counted only while the wall moves).</summary>
        public float CooldownAt(int level) => System.Math.Max(0f, At(_cooldowns, level, 0f));

        private static T At<T>(T[] perLevel, int level, T none)
        {
            if (perLevel.Length == 0) return none;
            return perLevel[level < 1 ? 0 : level > perLevel.Length ? perLevel.Length - 1 : level - 1];
        }

        private static T[] Copy<T>(IReadOnlyList<T> list)
        {
            var a = new T[list?.Count ?? 0];
            for (int i = 0; i < a.Length; i++) a[i] = list[i];
            return a;
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
