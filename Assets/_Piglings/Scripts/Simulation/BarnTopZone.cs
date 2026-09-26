using UnityEngine;

namespace Piglings.Simulation
{
    /// <summary>Trigger at the roof line. A climbing robot touching it has reached the top.</summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class BarnTopZone : MonoBehaviour { }
}