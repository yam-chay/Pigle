using System.Collections.Generic;

namespace Piglings.Meta
{
    /// <summary>Where the stone stands, all derived from its saved direct hits (StoneProgression.For).</summary>
    public readonly struct StoneStatus
    {
        public readonly int Stones;             // on the pile when a night starts
        public readonly bool Evolved;           // level 2: the evolved look and radius
        public readonly int Refill;             // stones added at each hour's placement round
        public readonly int NextThreshold;      // total hits for the next +1 stone; -1 at the cap
        public readonly int PreviousThreshold;  // total hits of the last +1 earned (0 before the first): where the bar starts

        public StoneStatus(int stones, bool evolved, int refill, int nextThreshold, int previousThreshold)
        {
            Stones = stones; Evolved = evolved; Refill = refill; NextThreshold = nextThreshold; PreviousThreshold = previousThreshold;
        }

        /// <summary>The stone's level (1, or 2 once evolved): its look, its radius and its trail.</summary>
        public int Level => Evolved ? 2 : 1;

        public bool AtCap => NextThreshold < 0;

        /// <summary>0..1 from the last +1 toward the next (1 at the cap).</summary>
        public float Progress(int hits) =>
            AtCap ? 1f : Clamp01((hits - PreviousThreshold) / (float)(NextThreshold - PreviousThreshold));

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }

    /// <summary>
    /// The stone's progression rule (campaign). Hits are the saved cause; everything else is derived here:
    /// start with StartStones; each threshold of total direct hits reached gives +1 stone, up to MaxStones; at EvolveAtStones
    /// the stone evolves (level 2) and the hourly refill goes from Refill to EvolvedRefill.
    /// Thresholds are cumulative and strictly rising (MasteryLevels.Problem checks them); only the first
    /// MaxStones − StartStones of them can ever count.
    /// </summary>
    public sealed class StoneProgression
    {
        private readonly int[] _thresholds;

        public int StartStones { get; }
        public int EvolveAtStones { get; }
        public int MaxStones { get; }
        public int Refill { get; }
        public int EvolvedRefill { get; }
        public IReadOnlyList<int> Thresholds => _thresholds;

        public StoneProgression(IReadOnlyList<int> thresholds, int startStones = 10, int evolveAtStones = 15, int maxStones = 20,
                                int refill = 1, int evolvedRefill = 2)
        {
            _thresholds = new int[thresholds?.Count ?? 0];
            for (int i = 0; i < _thresholds.Length; i++) _thresholds[i] = thresholds[i];
            StartStones = startStones < 0 ? 0 : startStones;
            MaxStones = maxStones < StartStones ? StartStones : maxStones;
            EvolveAtStones = evolveAtStones;
            Refill = refill < 0 ? 0 : refill;
            EvolvedRefill = evolvedRefill < 0 ? 0 : evolvedRefill;
        }

        public StoneStatus For(int hits)
        {
            int reached = MasteryLevels.LevelFor(hits, _thresholds) - 1;
            int stones = StartStones + reached;
            if (stones > MaxStones) { reached -= stones - MaxStones; stones = MaxStones; }
            bool evolved = stones >= EvolveAtStones;
            int next = stones < MaxStones && reached < _thresholds.Length ? _thresholds[reached] : -1;
            int previous = reached >= 1 ? _thresholds[reached - 1] : 0;
            return new StoneStatus(stones, evolved, evolved ? EvolvedRefill : Refill, next, previous);
        }
    }
}
