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

        public string Id => id;
        public float ClimbSpeed => climbSpeed;
        public float BreakDuration => breakDuration;
        public float BallRadius => ballRadius;
        public float BallMass => ballMass;
        public PhysicsMaterial2D BallMaterial => ballMaterial;
    }
}
