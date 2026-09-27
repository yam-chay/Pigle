using UnityEngine;

namespace Piglings.Definitions
{
    /// <summary>Immutable design data for one robot type. Never modified at runtime.</summary>
    [CreateAssetMenu(menuName = "Piglings/Robot Definition", fileName = "Robot_")]
    public sealed class RobotDefinition : ScriptableObject
    {
        [SerializeField] private string id = "wolfbot_basic";
        [SerializeField, Min(0f)] private float climbSpeed = 0.35f;         // units / second
        [SerializeField, Min(0f)] private float breakDuration = 0.6f;        // flail+crack+collapse clip length
        [SerializeField, Min(0.01f)] private float ballRadius = 0.174f;      // 58 art px * 0.3 scale * 0.01
        [SerializeField, Min(0.01f)] private float ballMass = 1f;
        [SerializeField] private PhysicsMaterial2D ballMaterial;
        [Tooltip("A falling ball still in the air after this many seconds is removed (e.g. it came to rest on a hold). " +
                 "Without it, its chain never closes and the night can't reach the choice.")]
        [SerializeField, Min(0.5f)] private float maxFallSeconds = 6f;

        public string Id => id;
        public float ClimbSpeed => climbSpeed;
        public float BreakDuration => breakDuration;
        public float BallRadius => ballRadius;
        public float BallMass => ballMass;
        public PhysicsMaterial2D BallMaterial => ballMaterial;
        public float MaxFallSeconds => maxFallSeconds;
    }
}
