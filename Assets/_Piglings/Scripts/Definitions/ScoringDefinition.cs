using UnityEngine;

namespace Piglings.Definitions
{
    /// <summary>
    /// The global scoring formula, and nothing else (the tuning refactor, R2): a chain's SCORE × MULT × the hour multiplier,
    /// with MULT growing by depth. What things are worth lives on the things — a robot's value on its RobotDefinition, a
    /// plain hold's on Peg_Plain, the stone's base per evolution on the Throwable — and the hour multipliers on each
    /// NightDefinition, next to the score targets they're balanced against. Depth stays here: it's a property of the chain,
    /// not of one robot.
    /// </summary>
    [CreateAssetMenu(menuName = "Piglings/Scoring Definition", fileName = "Scoring_")]
    public sealed class ScoringDefinition : ScriptableObject
    {
        [Tooltip("MULT: added each time a chain reaches a NEW depth (1, 2, 3…) — once per level, never per robot, so going wide " +
                 "doesn't add mult. Chains start at ×1.")]
        [SerializeField, Min(0f)] private float multPerNewDepth = 1f;

        public float MultPerNewDepth => multPerNewDepth;
    }
}