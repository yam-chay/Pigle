using UnityEngine;

namespace Piglings.Definitions
{
    /// <summary>
    /// The scoring knobs, as an asset so a curve can be saved, swapped and compared in play.
    /// Separate from NightDefinition: it's how the game counts, not what a night contains.
    /// NightSession turns it into a Rules.ScoreCurve (Definitions can't reference Rules).
    /// Each value is the lever a future upgrade family will pull — see ScoreCurve.
    /// </summary>
    [CreateAssetMenu(menuName = "Piglings/Scoring Definition", fileName = "Scoring_")]
    public sealed class ScoringDefinition : ScriptableObject
    {
        // M10.S: a chain's SCORE × MULT × the hour. The stone's base is per evolution level (ThrowableDefinition ▸ Levels ▸
        // Base Score); special pegs' mult bonus is per peg level (PegDefinition ▸ Levels ▸ Mult Bonus).

        [Tooltip("SCORE: what each robot knocked loose adds to its chain (any cause: stone, ball, bomb, a split piece). Also what " +
                 "the dawn sweep scores per robot, flat. (Future: wolf upgrades.)")]
        [SerializeField, Min(0)] private int wolfValue = 10;

        [Tooltip("SCORE: what a plain hold (an empty socket or a Plain peg) adds every time a stone or ball touches it.")]
        [SerializeField, Min(0)] private int plainPegScore = 1;
        [Tooltip("Seconds before the same stone / ball scores again on the same plain hold — so rattling or resting balls can't " +
                 "farm a hold (Max Fall Seconds still ends a stuck ball).")]
        [SerializeField, Min(0f)] private float plainHoldCooldown = 0.2f;

        [Tooltip("MULT: added each time a chain reaches a NEW depth (1, 2, 3…) — once per level, never per robot, so going wide " +
                 "doesn't add mult. Chains start at ×1.")]
        [SerializeField, Min(0f)] private float multPerNewDepth = 1f;

        [Tooltip("HOUR: each hour reached adds this to the chain's final multiplier: 0.5 → hour 1 ×1, hour 2 ×1.5, hour 3 ×2… " +
                 "Fixed when the stone is thrown, applied once at the chain's close; never to the dawn sweep.")]
        [SerializeField, Min(0f)] private float hourMultiplierStep = 0.5f;

        public int WolfValue => wolfValue;
        public int PlainPegScore => plainPegScore;
        public float PlainHoldCooldown => plainHoldCooldown;
        public float MultPerNewDepth => multPerNewDepth;
        public float HourMultiplierStep => hourMultiplierStep;
    }
}