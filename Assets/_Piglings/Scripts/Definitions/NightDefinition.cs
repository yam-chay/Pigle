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
        [SerializeField, Min(1)] private int throwsAvailable = 30;

        [Header("Passing the night")]
        [Tooltip("Score needed to pass. The night ends once it's reached and everything in flight has settled.")]
        [SerializeField, Min(1)] private int targetScore = 500;
        [Tooltip("Stones lost each time a robot reaches the top. Ties defence to offence. 0 = breaches cost nothing.")]
        [SerializeField, Min(0)] private int stonesLostPerBreach = 1;
        [Tooltip("This many robots reaching the top loses the night outright. 0 = no limit.")]
        [SerializeField, Min(0)] private int maxBreaches = 5;

        public RobotDefinition Robot => robot;
        public ThrowableDefinition Throwable => throwable;
        public WallMaterialDefinition Wall => wall;
        public float SpawnInterval => spawnInterval;
        public int ThrowsAvailable => throwsAvailable;
        public int TargetScore => targetScore;
        public int StonesLostPerBreach => stonesLostPerBreach;
        public int MaxBreaches => maxBreaches;
    }
}
