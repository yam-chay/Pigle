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
        public readonly int StagesReached;       // copy stages reached: each adds +1 peg throw per hour
        public readonly int NextStage;           // copies the next stage needs; -1 = none left (or past the max)

        public PegStatus(int copies, float mastery, int nextThreshold, int previousThreshold, int stagesReached = 0, int nextStage = -1)
        {
            Copies = copies; Mastery = mastery; NextThreshold = nextThreshold; PreviousThreshold = previousThreshold;
            StagesReached = stagesReached; NextStage = nextStage;
        }

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
    /// Copy stages (stage 2): at 4, 6, 8 copies (by default) a type adds +1 to the hour's peg-throw pool — ThrowsPerHour.
    /// </summary>
    public sealed class PegProgression
    {
        private readonly int[] _thresholds;
        private readonly float[] _weights;   // index 0 = level 1; empty / past the list = the level itself
        private readonly int[] _stages;      // copies at which a stage is reached, rising

        /// <summary>The default copy stages: 4, 6 and 8 copies.</summary>
        public static readonly int[] DefaultStages = { 4, 6, 8 };

        public int StartCopies { get; }
        public int MaxCopies { get; }
        public IReadOnlyList<int> Thresholds => _thresholds;
        public IReadOnlyList<int> Stages => _stages;

        /// <param name="stages">Copies at which each stage is reached. Null = the defaults (4, 6, 8); empty = no stages.</param>
        public PegProgression(IReadOnlyList<int> thresholds, IReadOnlyList<float> levelWeights = null, int startCopies = 1,
                              int maxCopies = 8, IReadOnlyList<int> stages = null)
        {
            _thresholds = new int[thresholds?.Count ?? 0];
            for (int i = 0; i < _thresholds.Length; i++) _thresholds[i] = thresholds[i];
            _weights = new float[levelWeights?.Count ?? 0];
            for (int i = 0; i < _weights.Length; i++) _weights[i] = levelWeights[i] < 0f ? 0f : levelWeights[i];
            StartCopies = startCopies < 0 ? 0 : startCopies;
            MaxCopies = maxCopies < StartCopies ? StartCopies : maxCopies;
            if (stages == null) _stages = (int[])DefaultStages.Clone();
            else
            {
                _stages = new int[stages.Count];
                for (int i = 0; i < _stages.Length; i++) _stages[i] = stages[i];
            }
        }

        /// <summary>Copy stages reached with this many copies (in order: a stage not reached stops the count).</summary>
        public int StagesReached(int copies)
        {
            int n = 0;
            while (n < _stages.Length && copies >= _stages[n]) n++;
            return n;
        }

        /// <summary>
        /// The hour's peg-throw pool: the night's base throws + every owned type's stages reached. Shared across types
        /// (still limited by the pegs on the shelf).
        /// </summary>
        public static int ThrowsPerHour(int baseThrows, IEnumerable<int> stagesReachedPerType)
        {
            int throws = baseThrows < 0 ? 0 : baseThrows;
            if (stagesReachedPerType != null) foreach (int s in stagesReachedPerType) if (s > 0) throws += s;
            return throws;
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
            int stages = StagesReached(copies);
            int nextStage = stages < _stages.Length && _stages[stages] <= MaxCopies ? _stages[stages] : -1;
            return new PegStatus(copies, mastery, next, previous, stages, nextStage);
        }
    }
}
