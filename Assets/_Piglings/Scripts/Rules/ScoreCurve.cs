using System;

namespace Piglings.Rules
{
    /// <summary>
    /// How much a chain is worth. Two knobs work together:
    ///  1. Each robot is worth more the deeper it sits in the chain (RobotPoints) — paid the moment
    ///     it loses grip, so the "+N" popups climb as a chain goes on.
    ///  2. When the chain closes, its robot points are multiplied by how deep it got (Multiplier).
    ///     The extra is paid then, as the chain's bonus.
    /// Why depth and not robot count: one stone clipping three robots is luck; a ball knocking a ball
    /// knocking a ball is the game. Depth is what the scoring should teach.
    /// Immutable: built once per night by NightSession from its Inspector values.
    /// </summary>
    public sealed class ScoreCurve
    {
        public int BasePoints { get; }            // robot hit directly by the throw (depth 0)
        public int PointsPerDepth { get; }        // added per step deeper
        public float MultiplierPerDepth { get; }  // chain multiplier grows by this per depth reached
        public float MaxMultiplier { get; }       // cap, so one freak chain doesn't dwarf the whole night

        public ScoreCurve(int basePoints = 10, int pointsPerDepth = 10, float multiplierPerDepth = 0.5f, float maxMultiplier = 4f)
        {
            BasePoints = basePoints;
            PointsPerDepth = pointsPerDepth;
            MultiplierPerDepth = multiplierPerDepth;
            MaxMultiplier = Math.Max(1f, maxMultiplier);
        }

        public int RobotPoints(int depth) => BasePoints + PointsPerDepth * depth;

        /// <summary>1 at depth 0, then +MultiplierPerDepth per depth, never above MaxMultiplier.</summary>
        public float Multiplier(int maxDepth) => Math.Min(MaxMultiplier, 1f + MultiplierPerDepth * maxDepth);

        /// <summary>Final chain value: its robot points times the depth multiplier, rounded.</summary>
        public int ChainTotal(int robotPoints, int maxDepth) =>
            (int)Math.Round(robotPoints * Multiplier(maxDepth), MidpointRounding.AwayFromZero);
    }
}
