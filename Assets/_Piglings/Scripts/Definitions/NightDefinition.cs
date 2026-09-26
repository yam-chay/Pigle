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

        public RobotDefinition Robot => robot;
        public ThrowableDefinition Throwable => throwable;
        public WallMaterialDefinition Wall => wall;
        public float SpawnInterval => spawnInterval;
        public int ThrowsAvailable => throwsAvailable;
    }
}
