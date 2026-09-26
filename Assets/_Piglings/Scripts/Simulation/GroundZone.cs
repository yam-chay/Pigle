using UnityEngine;

namespace Piglings.Simulation
{
    /// <summary>Trigger below the barn. Falling balls and throwables are removed here.</summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class GroundZone : MonoBehaviour { }
}