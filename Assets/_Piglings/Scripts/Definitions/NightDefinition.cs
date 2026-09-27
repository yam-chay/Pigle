using UnityEngine;

namespace Piglings.Definitions
{
    [CreateAssetMenu(menuName = "Piglings/Night Definition", fileName = "Night_")]
    public sealed class NightDefinition : ScriptableObject
    {
        [SerializeField] private RobotDefinition robot;
        [SerializeField] private ThrowableDefinition throwable;
        [SerializeField] private WallMaterialDefinition wall;
        [SerializeField, Min(0.1f)] private float spawnInterval = 1.6f;
        [Tooltip("Stones on the pile when the night starts. They're the pig's life: a robot that breaches takes one, " +
                 "and a robot that breaches when there are none left catches the pigs.")]
        [SerializeField, Min(1)] private int throwsAvailable = 30;

        [Header("Passing the night")]
        [Tooltip("Score needed to pass. The night ends once it's reached and everything in flight has settled.")]
        [SerializeField, Min(1)] private int targetScore = 500;

        public RobotDefinition Robot => robot;
        public ThrowableDefinition Throwable => throwable;
        public WallMaterialDefinition Wall => wall;
        public float SpawnInterval => spawnInterval;
        public int ThrowsAvailable => throwsAvailable;
        public int TargetScore => targetScore;
    }
}
