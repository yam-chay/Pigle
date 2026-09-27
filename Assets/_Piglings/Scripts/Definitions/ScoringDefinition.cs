using UnityEngine;

namespace Piglings.Definitions
{
    /// <summary>
    /// The four scoring knobs, as an asset so a curve can be saved, swapped and compared in play.
    /// Separate from NightDefinition: it's how the game counts, not what a night contains.
    /// NightSession turns it into a Rules.ScoreCurve (Definitions can't reference Rules).
    /// </summary>
    [CreateAssetMenu(menuName = "Piglings/Scoring Definition", fileName = "Scoring_")]
    public sealed class ScoringDefinition : ScriptableObject
    {
        [Tooltip("Points for a robot hit directly by the stone (depth 0).")]
        [SerializeField, Min(0)] private int basePoints = 10;

        [Tooltip("Extra points per step deeper in the chain. Depth 2 = base + 2 × this.")]
        [SerializeField, Min(0)] private int pointsPerDepth = 10;

        [Tooltip("Chain multiplier grows by this per depth reached. 0.5 → depth 1 ×1.5, depth 2 ×2.")]
        [SerializeField, Min(0f)] private float multiplierPerDepth = 0.5f;

        [Tooltip("The multiplier never goes above this.")]
        [SerializeField, Min(1f)] private float maxMultiplier = 4f;

        public int BasePoints => basePoints;
        public int PointsPerDepth => pointsPerDepth;
        public float MultiplierPerDepth => multiplierPerDepth;
        public float MaxMultiplier => maxMultiplier;
    }
}
