using UnityEngine;

namespace Piglings.Definitions
{
    /// <summary>Immutable design data for one robot type. Never modified at runtime.</summary>
    [CreateAssetMenu(menuName = "Piglings/Robot Definition", fileName = "Robot_")]
    public sealed class RobotDefinition : ScriptableObject
    {
        [Tooltip("Save key: this robot type's ball stats are saved under this id. Never rename it once players have a " +
                 "save — their progress would be orphaned (it'd need a migration in ProfileJson).")]
        [SerializeField] private string id = "wolfbot_basic";
        [SerializeField, Min(0f)] private float climbSpeed = 0.35f;         // units / second
        [SerializeField, Min(0f)] private float breakDuration = 0.6f;        // flail+crack+collapse clip length
        [SerializeField, Min(0.01f)] private float ballRadius = 0.174f;      // 58 art px * 0.3 scale * 0.01
        [SerializeField, Min(0.01f)] private float ballMass = 1f;
        [SerializeField] private PhysicsMaterial2D ballMaterial;
        [Tooltip("A falling ball still in the air after this many seconds is removed (e.g. it came to rest on a hold). " +
                 "Without it, its chain never closes and the night can't reach the choice.")]
        [SerializeField, Min(0.5f)] private float maxFallSeconds = 6f;
        [Tooltip("A falling ball slower than this (units/s)… — e.g. wedged between pegs or holds.")]
        [SerializeField, Min(0f)] private float stuckSpeed = 0.2f;
        [Tooltip("…for this many seconds in a row is removed, so it doesn't hold its chain (and the next hour) open. " +
                 "Max Fall Seconds stays as the hard limit.")]
        [SerializeField, Min(0.05f)] private float stuckSeconds = 0.5f;
        [Tooltip("Hard limit on the breach sequence (reached the roof → gone). Whatever the animation does, the robot is " +
                 "removed after this many seconds — a breach must never hold the night open. The breach itself (stone " +
                 "theft or catch) is counted when the sequence starts, so this changes only how long it's on screen.")]
        [SerializeField, Min(0.1f)] private float breachSeconds = 1.5f;

        public string Id => id;
        public float ClimbSpeed => climbSpeed;
        public float BreakDuration => breakDuration;
        public float BallRadius => ballRadius;
        public float BallMass => ballMass;
        public PhysicsMaterial2D BallMaterial => ballMaterial;
        public float MaxFallSeconds => maxFallSeconds;
        public float StuckSpeed => stuckSpeed;
        public float StuckSeconds => stuckSeconds;
        public float BreachSeconds => breachSeconds;
    }
}
