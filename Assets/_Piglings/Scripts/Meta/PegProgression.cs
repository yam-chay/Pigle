using System.Collections.Generic;

namespace Piglings.Meta
{
    /// <summary>Where one peg type stands, derived from its saved triggers (PegProgression.For).</summary>
    public readonly struct PegStatus
    {
        public readonly int Copies;              // how many of it the player owns (can place in a night)
        public readonly float Mastery;           // its triggers weighted by level
        public readonly int NextThreshold;       // mastery for the next copy; -1 at the max
        public readonly int PreviousThreshold;   // mastery of the last copy earned (0 before the first): where the bar starts
        public readonly int FollowUpAt;          // copies that earn the same-type follow-up throw; 0 = never (or past the max)

        public PegStatus(int copies, float mastery, int nextThreshold, int previousThreshold, int followUpAt = 0)
        {
            Copies = copies; Mastery = mastery; NextThreshold = nextThreshold; PreviousThreshold = previousThreshold;
            FollowUpAt = followUpAt;
        }

        /// <summary>Owns enough copies for the follow-up: placing one in a round gives one more throw of the same type.</summary>
        public bool HasFollowUp => FollowUpAt > 0 && Copies >= FollowUpAt;

        public bool AtMax => NextThreshold < 0;

        /// <summary>0..1 from the last copy toward the next (1 at the max).</summary>
        public float Progress =>
            AtMax ? 1f : Clamp01((Mastery - PreviousThreshold) / (NextThreshold - PreviousThreshold));

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }

    /// <summary>
    /// One peg type's progression rule (campaign). Triggers per merged level are the saved cause; mastery = Σ triggers ×
    /// that level's weight (default weight = the level: a level-2 peg's trigger counts double). A newly unlocked type owns
    /// StartCopies; each threshold of mastery reached adds one, up to MaxCopies (8 by default). Locked types own none —
    /// that's the campaign's call (CampaignPlan), not this one's.
    /// The follow-up (stage 2): from FollowUpAt copies (4 by default) a type earns a same-type follow-up throw in each round
    /// (the referee gives it once per round; it never stacks across types).
    /// </summary>
    public sealed class PegProgression
    {
        private readonly int[] _thresholds;
        private readonly float[] _weights;   // index 0 = level 1; empty / past the list = the level itself

        public int StartCopies { get; }
        public int MaxCopies { get; }
        /// <summary>Copies that earn the same-type follow-up throw; 0 = never.</summary>
        public int FollowUpAt { get; }
        public IReadOnlyList<int> Thresholds => _thresholds;

        /// <param name="followUpAt">Copies that earn the follow-up throw (0 = never).</param>
        public PegProgression(IReadOnlyList<int> thresholds, IReadOnlyList<float> levelWeights = null, int startCopies = 1,
                              int maxCopies = 8, int followUpAt = 4)
        {
            _thresholds = new int[thresholds?.Count ?? 0];
            for (int i = 0; i < _thresholds.Length; i++) _thresholds[i] = thresholds[i];
            _weights = new float[levelWeights?.Count ?? 0];
            for (int i = 0; i < _weights.Length; i++) _weights[i] = levelWeights[i] < 0f ? 0f : levelWeights[i];
            StartCopies = startCopies < 0 ? 0 : startCopies;
            MaxCopies = maxCopies < StartCopies ? StartCopies : maxCopies;
            // A follow-up past the max can never be earned: the same as none.
            FollowUpAt = followUpAt > 0 && followUpAt <= MaxCopies ? followUpAt : 0;
        }

        public float WeightAt(int level) => level >= 1 && level <= _weights.Length ? _weights[level - 1] : level < 1 ? 1f : level;

        public float Mastery(IReadOnlyList<int> triggersPerLevel)
        {
            float sum = 0f;
            if (triggersPerLevel == null) return sum;
            for (int i = 0; i < triggersPerLevel.Count; i++) sum += triggersPerLevel[i] * WeightAt(i + 1);
            return sum;
        }

        public PegStatus For(IReadOnlyList<int> triggersPerLevel)
        {
            float mastery = Mastery(triggersPerLevel);
            // Walked in order, stopping at the first not reached — like MasteryLevels, but against a weighted (float) total.
            int reached = 0;
            while (reached < _thresholds.Length && mastery >= _thresholds[reached]) reached++;
            int copies = StartCopies + reached;
            if (copies > MaxCopies) { reached -= copies - MaxCopies; copies = MaxCopies; }
            int next = copies < MaxCopies && reached < _thresholds.Length ? _thresholds[reached] : -1;
            int previous = reached >= 1 ? _thresholds[reached - 1] : 0;
            return new PegStatus(copies, mastery, next, previous, FollowUpAt);
        }
    }
}
