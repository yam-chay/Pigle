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
        [Tooltip("Points per place in the chain: the 1st robot to lose grip is worth this, the 2nd twice this, the 3rd three times…")]
        [SerializeField, Min(0)] private int basePoints = 10;

        [Tooltip("Each robot's multiplier grows by this per step of its own depth. 1 → depth 0 ×1, depth 1 ×2, depth 2 ×3.")]
        [SerializeField, Min(0f)] private float multiplierPerDepth = 1f;

        public int BasePoints => basePoints;
        public float MultiplierPerDepth => multiplierPerDepth;
    }
}
