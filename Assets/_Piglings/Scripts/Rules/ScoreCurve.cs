using System;

namespace Piglings.Rules
{
    /// <summary>
    /// What each robot in a chain is worth. Everything is paid the moment the robot loses grip —
    /// there is no end-of-chain bonus, so the score the player sees is always the real score.
    ///
    ///  - Points grow with the robot's ORDER in the chain (1st, 2nd, 3rd… to lose grip):
    ///    basePoints × order → 10, 20, 30…  Rewards keeping a chain going, however it spreads.
    ///  - The multiplier grows with the robot's own DEPTH (how many hand-offs from the stone):
    ///    1 + multiplierPerDepth × depth → ×1, ×2, ×3…  Rewards the ball-knocks-ball shots.
    ///  - Robot total = points × multiplier, rounded.
    ///
    /// Immutable: built once per night by NightSession from the ScoringDefinition asset.
    /// </summary>
    public sealed class ScoreCurve
    {
        public int BasePoints { get; }
        public float MultiplierPerDepth { get; }

        public ScoreCurve(int basePoints = 10, float multiplierPerDepth = 1f)
        {
            BasePoints = basePoints;
            MultiplierPerDepth = Math.Max(0f, multiplierPerDepth);
        }

        /// <summary>order is 1-based: the first robot of a chain is order 1.</summary>
        public int RobotPoints(int order) => BasePoints * order;

        public float Multiplier(int depth) => 1f + MultiplierPerDepth * depth;

        public int RobotTotal(int order, int depth) =>
            (int)Math.Round(RobotPoints(order) * Multiplier(depth), MidpointRounding.AwayFromZero);
    }
}
