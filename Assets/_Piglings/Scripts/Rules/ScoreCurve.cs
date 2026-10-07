using System;

namespace Piglings.Rules
{
    /// <summary>
    /// How a chain scores (M10.S, Balatro-style): two running numbers per chain, both additive, multiplied once at the close.
    ///
    ///  - SCORE ("width"): + StoneBase when the stone is thrown (by the stone's evolution level); + WolfValue for each robot
    ///    knocked loose, whatever knocked it (stone, ball, bomb, a split piece); + PlainPegScore for every contact with a
    ///    plain hold (the same stone / ball on the same hold at most once per PlainHoldCooldown seconds).
    ///  - MULT ("depth"): starts at 1; + MultPerNewDepth each time the chain reaches a new depth (1, 2, 3… — once per
    ///    level, never per robot, so a wide chain doesn't gain mult); + a special peg's bonus when it triggers.
    ///  - Result = SCORE × MULT × the hour multiplier of the hour the chain was thrown in, rounded once.
    ///
    /// Example, a line stone → A → B → C (base 10, wolves 10): score 10 + 3 × 10 = 40, mult 1 + 2 (depths 1 and 2) = 3 →
    /// 120 in hour 1; a stone that knocks 3 directly: 40 × 1 = 40. Depth multiplies, width adds.
    ///
    /// Each number is a lever an upgrade family will pull (stone level → base; wolves → value; pegs → mult). Immutable:
    /// built once per night by NightSession, each value from its owner (R2): the stone's base from the Throwable's level,
    /// the wolf value from the night's RobotDefinition, the plain score and cooldown from Peg_Plain, the depth step from the
    /// Scoring asset, the hour multipliers from the NightDefinition.
    /// </summary>
    public sealed class ScoreCurve
    {
        public int StoneBase { get; }
        public int WolfValue { get; }
        public int PlainPegScore { get; }
        public float MultPerNewDepth { get; }
        public float FirstHourMultiplier { get; } // hour 1's multiplier (≥ 1): a later night can start higher
        public float HourMultiplierStep { get; }  // each hour reached adds this to what chains score: ×1, ×1.5, ×2…
        /// <summary>A plain hold scores every contact, but the same stone / ball on the same hold only once per this many
        /// seconds — so a ball rattling or resting on a hold can't farm it (MaxFallSeconds still ends a stuck ball).</summary>
        public float PlainHoldCooldown { get; }

        public ScoreCurve(int stoneBase = 10, int wolfValue = 10, int plainPegScore = 1, float multPerNewDepth = 1f,
                          float hourMultiplierStep = 0.5f, float plainHoldCooldown = 0.2f, float firstHourMultiplier = 1f)
        {
            FirstHourMultiplier = Math.Max(1f, firstHourMultiplier);
            PlainHoldCooldown = Math.Max(0f, plainHoldCooldown);
            StoneBase = Math.Max(0, stoneBase);
            WolfValue = Math.Max(0, wolfValue);
            PlainPegScore = Math.Max(0, plainPegScore);
            MultPerNewDepth = Math.Max(0f, multPerNewDepth);
            HourMultiplierStep = Math.Max(0f, hourMultiplierStep);
        }

        /// <summary>A robot's own worth: what a chain adds when one is knocked loose, and what the dawn sweep scores each, flat.</summary>
        public int RobotValue() => WolfValue;

        /// <summary>A chain's mult at this depth with no pegs: 1 + MultPerNewDepth × depth (the DEPTH card shows it).</summary>
        public float Multiplier(int depth) => 1f + MultPerNewDepth * Math.Max(0, depth);

        /// <summary>The score multiplier after this many hours have passed: hour 1 (0 passed) ×First, then + Step per hour
        /// (1 / 0.5: ×1, ×1.5, ×2…).</summary>
        public float HourMultiplier(int hoursPassed) => FirstHourMultiplier + HourMultiplierStep * Math.Max(0, hoursPassed);

        /// <summary>A chain's result: score × mult × hour multiplier, rounded once (never below the score itself).</summary>
        public int Result(int score, float mult, float hourMultiplier)
        {
            double total = Math.Max(0, score) * (double)Math.Max(1f, mult) * Math.Max(1f, hourMultiplier);
            double rounded = Math.Round(total, MidpointRounding.AwayFromZero);
            return rounded >= int.MaxValue ? int.MaxValue : (int)rounded;
        }
    }
}
