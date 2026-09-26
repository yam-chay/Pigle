using UnityEngine;

namespace Piglings.Definitions
{
    [CreateAssetMenu(menuName = "Piglings/Throwable Definition", fileName = "Throwable_")]
    public sealed class ThrowableDefinition : ScriptableObject
    {
        [SerializeField] private string id = "stone";
        [SerializeField, Min(0.01f)] private float radius = 0.08f;
        [SerializeField, Min(0.01f)] private float mass = 1f;
        [SerializeField, Min(0f)] private float lifetime = 6f;             // safety cleanup
        [SerializeField] private PhysicsMaterial2D material;

        public string Id => id;
        public float Radius => radius;
        public float Mass => mass;
        public float Lifetime => lifetime;
        public PhysicsMaterial2D Material => material;
    }
}
