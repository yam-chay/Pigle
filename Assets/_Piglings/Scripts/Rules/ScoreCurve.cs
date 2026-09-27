using System;

namespace Piglings.Rules
{
    /// <summary>
    /// Value flows down the chain. Every "hitter" — the stone, or a robot's falling ball — carries a
    /// value, and whatever it knocks loose scores that value times its depth multiplier:
    ///
    ///  - The stone starts carrying StoneValue.
    ///  - A robot knocked loose scores  received × (1 + MultiplierPerDepth × its depth),  paid now.
    ///  - The hitter grows by GrowthPerHit with every hit, so a stone or ball that knocks several
    ///    robots makes each next one worth more. Extra hits are never wasted.
    ///  - The knocked robot becomes a hitter itself, carrying WolfValue (its own worth) plus what it
    ///    received — or plus what it SCORED when CarryScoredTotal is on, which compounds hard.
    ///
    /// Example, a line stone → A → B → C with default values (10 / 10 / 10 / ×0.5, compounding off):
    ///   A 10 × 1 = 10, carries 10 + 10 = 20 · B 20 × 1.5 = 30, carries 10 + 20 = 30 · C 30 × 2 = 60.
    ///
    /// Why these are separate numbers: each is the lever a future upgrade family will pull —
    /// stone upgrades (StoneValue, GrowthPerHit), wolf upgrades (WolfValue), pegs and slices
    /// (MultiplierPerDepth). The defaults start low on purpose, so upgrades have room to matter.
    /// Immutable: built once per night by NightSession from the ScoringDefinition asset.
    /// </summary>
    public sealed class ScoreCurve
    {
        public int StoneValue { get; }
        public int GrowthPerHit { get; }
        public int WolfValue { get; }
        public float MultiplierPerDepth { get; }
        public bool CarryScoredTotal { get; }
        public int OvertimeMultiplier { get; }    // chain points in overtime are worth this many times more

        public ScoreCurve(int stoneValue = 10, int growthPerHit = 10, int wolfValue = 10,
                          float multiplierPerDepth = 0.5f, bool carryScoredTotal = false, int overtimeMultiplier = 2)
        {
            StoneValue = stoneValue;
            GrowthPerHit = growthPerHit;
            WolfValue = wolfValue;
            MultiplierPerDepth = Math.Max(0f, multiplierPerDepth);
            CarryScoredTotal = carryScoredTotal;
            OvertimeMultiplier = Math.Max(1, overtimeMultiplier);
        }

        /// <summary>
        /// A robot's own worth. The one robot-value function: chains add it when a robot passes value on
        /// (CarriedBy), and the end-of-night sweep scores each robot left on the wall with it, flat.
        /// </summary>
        public int RobotValue() => WolfValue;

        public float Multiplier(int depth) => 1f + MultiplierPerDepth * depth;

        /// <summary>
        /// What a robot knocked loose scores: received × depth multiplier, rounded, × phaseMultiplier.
        /// The one place chain points are multiplied — depth, and overtime (phaseMultiplier = OvertimeMultiplier,
        /// passed by ChainTracker only while the night is in Overtime). The phase multiplier is applied after
        /// rounding, so an overtime robot is always exactly phaseMultiplier × its Running self.
        /// </summary>
        public int RobotTotal(int received, int depth, int phaseMultiplier = 1)
        {
            double atDepth = Math.Round(received * (double)Multiplier(depth), MidpointRounding.AwayFromZero);
            return ClampToInt(atDepth * Math.Max(1, phaseMultiplier));
        }

        /// <summary>A stone or ball after it knocked one more robot loose.</summary>
        public int HitterAfterHit(int value) => ClampToInt((long)value + GrowthPerHit);

        /// <summary>What a freshly knocked robot carries into its own hits.</summary>
        public int CarriedBy(int received, int total) => ClampToInt((long)RobotValue() + (CarryScoredTotal ? total : received));

        // With CarryScoredTotal on, value grows faster than factorially down a line (depth ~12
        // passes int.MaxValue). Clamp instead of wrapping into a negative score.
        private static int ClampToInt(double v) => v >= int.MaxValue ? int.MaxValue : (int)v;
    }
}
