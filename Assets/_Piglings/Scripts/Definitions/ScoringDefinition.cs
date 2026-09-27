using UnityEngine;

namespace Piglings.Definitions
{
    /// <summary>
    /// The scoring knobs, as an asset so a curve can be saved, swapped and compared in play.
    /// Separate from NightDefinition: it's how the game counts, not what a night contains.
    /// NightSession turns it into a Rules.ScoreCurve (Definitions can't reference Rules).
    /// </summary>
    [CreateAssetMenu(menuName = "Piglings/Scoring Definition", fileName = "Scoring_")]
    public sealed class ScoringDefinition : ScriptableObject
    {
        [Tooltip("What the stone carries into its first hit, what every robot adds of its own when it passes value on, " +
                 "and how much a stone or ball grows with each extra robot it knocks.")]
        [SerializeField, Min(0)] private int basePoints = 10;

        [Tooltip("Each robot's multiplier grows by this per step of its own depth. 1 → depth 0 ×1, depth 1 ×2, depth 2 ×3.")]
        [SerializeField, Min(0f)] private float multiplierPerDepth = 1f;

        [Tooltip("On: a knocked robot passes on what it SCORED + its own base (10 → 40 → 150 → 640 down a line; steep).\n" +
                 "Off: it passes on what it RECEIVED + its own base (10 → 40 → 90 → 160; gentle).")]
        [SerializeField] private bool carryScoredTotal = true;

        public int BasePoints => basePoints;
        public float MultiplierPerDepth => multiplierPerDepth;
        public bool CarryScoredTotal => carryScoredTotal;
    }
}
