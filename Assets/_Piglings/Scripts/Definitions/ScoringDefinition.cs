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
        [Tooltip("What the stone carries into its first hit. (Future: stone upgrades.)")]
        [SerializeField, Min(0)] private int stoneValue = 10;

        [Tooltip("How much a stone or a falling ball grows with each extra robot it knocks: 10 → hits worth 10, 20, 30… " +
                 "(Future: piercing stones.)")]
        [SerializeField, Min(0)] private int growthPerHit = 10;

        [Tooltip("What a knocked robot adds of its own when it passes value on to the robots it hits. (Future: wolf upgrades.)")]
        [SerializeField, Min(0)] private int wolfValue = 10;

        [Tooltip("Each robot's multiplier grows by this per step of its own depth. 0.5 → depth 0 ×1, depth 1 ×1.5, depth 2 ×2. " +
                 "(Future: pegs, slices.)")]
        [SerializeField, Min(0f)] private float multiplierPerDepth = 0.5f;

        [Tooltip("Off (default): a knocked robot passes on what it RECEIVED + wolfValue. Steady growth.\n" +
                 "On: it passes on what it SCORED + wolfValue. Compounds hard — kept as a switch for a future late upgrade.")]
        [SerializeField] private bool carryScoredTotal = false;

        [Tooltip("In overtime every chain point is worth this many times more, as it's scored (the popups show it). " +
                 "The end-of-night sweep is never multiplied.")]
        [SerializeField, Min(1)] private int overtimeMultiplier = 2;

        public int StoneValue => stoneValue;
        public int GrowthPerHit => growthPerHit;
        public int WolfValue => wolfValue;
        public float MultiplierPerDepth => multiplierPerDepth;
        public bool CarryScoredTotal => carryScoredTotal;
        public int OvertimeMultiplier => overtimeMultiplier;
    }
}