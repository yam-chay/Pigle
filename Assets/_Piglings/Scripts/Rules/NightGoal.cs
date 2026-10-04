using System.Collections.Generic;

namespace Piglings.Rules
{
    /// <summary>
    /// What a night asks for, as plain values (Rules can't read NightDefinition): the hours until dawn.
    /// Built once per night by NightSession from the NightDefinition asset.
    /// There is no breach limit: the pile of stones is the pig's life (see NightReferee).
    /// </summary>
    public sealed class NightGoal
    {
        private readonly int[] _thresholds;

        /// <summary>Score thresholds, rising. Crossing one starts the next hour; crossing the last one is dawn.</summary>
        public IReadOnlyList<int> Thresholds => _thresholds;
        public int ThresholdCount => _thresholds.Length;
        public int ThrowsAvailable { get; }    // stones on the pile when the night starts
        public int StonesPerThreshold { get; } // stones added at each hour's placement round (not at dawn)

        public NightGoal(int[] thresholds = null, int throwsAvailable = 30, int stonesPerThreshold = 0)
        {
            // Each threshold at least 1 and above the one before: a threshold of 0 would be crossed before the first
            // throw, and two equal thresholds would make an hour that lasts no time at all.
            var t = thresholds != null && thresholds.Length > 0 ? (int[])thresholds.Clone() : new[] { 500 };
            for (int i = 0; i < t.Length; i++)
            {
                int min = i == 0 ? 1 : t[i - 1] + 1;
                if (t[i] < min) t[i] = min;
            }
            _thresholds = t;
            ThrowsAvailable = throwsAvailable;
            StonesPerThreshold = stonesPerThreshold < 0 ? 0 : stonesPerThreshold;
        }

        /// <summary>The score a night keeps when caught after crossing this many thresholds (0 before the first).</summary>
        public int ScoreAtThreshold(int reached) => reached <= 0 ? 0 : _thresholds[System.Math.Min(reached, _thresholds.Length) - 1];

        /// <summary>
        /// Hour n's gap: the points it takes to get through it, T(n) − T(n−1) with T(0) = 0. Hours past the last threshold
        /// (there are none in play; a guard) use the last gap. Never below 1.
        /// </summary>
        public int HourGap(int hour)
        {
            int h = hour < 1 ? 1 : hour > _thresholds.Length ? _thresholds.Length : hour;
            int gap = _thresholds[h - 1] - (h >= 2 ? _thresholds[h - 2] : 0);
            return gap < 1 ? 1 : gap;
        }

        /// <summary>
        /// How good a throw was: its points ÷ the gap of the hour it was thrown in (1 = it alone was worth a whole hour).
        /// The scoreboard and the post-run screen colour throws by this; the bands are a Presentation choice.
        /// </summary>
        public float ThrowQuality(int points, int hour) => points <= 0 ? 0f : points / (float)HourGap(hour);
    }
}
