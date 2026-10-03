using System.Collections.Generic;

namespace Piglings.Meta
{
    /// <summary>
    /// The level rule: a level is DERIVED from total hits and a list of thresholds, never stored. So thresholds can be
    /// retuned on the weapon definition without breaking anyone's save.
    ///
    /// Thresholds are cumulative totals, strictly rising: [50, 150] = level 2 at 50 total hits, level 3 at 150.
    /// Level 1 needs nothing. The list is walked in order and stops at the first threshold not reached. A list that
    /// breaks the rule still gives some level, never an error — Problem() says what's wrong, for an Inspector warning.
    /// </summary>
    public static class MasteryLevels
    {
        public static int LevelFor(int hits, IReadOnlyList<int> thresholds)
        {
            int level = 1;
            if (thresholds == null) return level;
            foreach (int t in thresholds)
            {
                if (hits < t) break;
                level++;
            }
            return level;
        }

        /// <summary>Hits needed for the next level (a total, like the thresholds), or -1 at the top level.</summary>
        public static int NextThreshold(int hits, IReadOnlyList<int> thresholds)
        {
            int level = LevelFor(hits, thresholds);
            return thresholds != null && level - 1 < thresholds.Count ? thresholds[level - 1] : -1;
        }

        /// <summary>Why this list breaks the rule (each ≥ 1, strictly rising), or null if it's fine.</summary>
        public static string Problem(IReadOnlyList<int> thresholds)
        {
            if (thresholds == null) return null;
            for (int i = 0; i < thresholds.Count; i++)
            {
                if (thresholds[i] < 1) return $"threshold {i + 1} is {thresholds[i]}: each must be at least 1";
                if (i > 0 && thresholds[i] <= thresholds[i - 1])
                    return $"threshold {i + 1} ({thresholds[i]}) isn't above threshold {i} ({thresholds[i - 1]}): they're cumulative totals";
            }
            return null;
        }
    }
}
