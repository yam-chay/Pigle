using System;

namespace Piglings.Rules
{
    /// <summary>
    /// Value flows down the chain. Every "hitter" — the stone, or a robot's falling ball — carries a
    /// value, and whatever it knocks loose scores that value times its depth multiplier:
    ///
    ///  - The stone starts carrying basePoints (10).
    ///  - A robot knocked loose scores  received × (1 + multiplierPerDepth × its depth),  paid now.
    ///  - The hitter grows by basePoints with every hit, so a stone or ball that knocks
    ///    several robots makes each next one worth more (10, 20, 30…). Extra hits are never wasted.
    ///  - The knocked robot becomes a hitter itself, carrying basePoints (its own worth) plus
    ///    what it scored (CarryScoredTotal) — or plus what it received, for a gentler curve.
    ///
    /// Example, a line stone → A → B → C with default values:
    ///   A 10 × 1 = 10, carries 10 + 10 = 20 · B 20 × 2 = 40, carries 40 + 10 = 50 · C 50 × 3 = 150.
    ///
    /// Why value is carried instead of counted: a robot that falls on others should feel like it
    /// "became the stone" — the player sees the value they started travel and grow down the chain.
    /// Immutable: built once per night by NightSession from the ScoringDefinition asset.
    /// </summary>
    public sealed class ScoreCurve
    {
        public int BasePoints { get; }
        public float MultiplierPerDepth { get; }
        public bool CarryScoredTotal { get; }

        public ScoreCurve(int basePoints = 10, float multiplierPerDepth = 1f, bool carryScoredTotal = true)
        {
            BasePoints = basePoints;
            MultiplierPerDepth = Math.Max(0f, multiplierPerDepth);
            CarryScoredTotal = carryScoredTotal;
        }

        /// <summary>What the stone carries into its first hit.</summary>
        public int StoneValue => BasePoints;

        public float Multiplier(int depth) => 1f + MultiplierPerDepth * depth;

        public int RobotTotal(int received, int depth) => ClampToInt(Math.Round(received * (double)Multiplier(depth), MidpointRounding.AwayFromZero));

        /// <summary>A stone or ball after it knocked one more robot loose.</summary>
        public int HitterAfterHit(int value) => ClampToInt((long)value + BasePoints);

        /// <summary>What a freshly knocked robot carries into its own hits.</summary>
        public int CarriedBy(int received, int total) => ClampToInt((long)BasePoints + (CarryScoredTotal ? total : received));

        // Carrying the scored total grows faster than factorially down a line (depth 12 passes
        // int.MaxValue). Clamp instead of wrapping into a negative score if a chain ever gets there.
        private static int ClampToInt(double v) => v >= int.MaxValue ? int.MaxValue : (int)v;
    }
}
