using UnityEngine;

namespace Piglings.Simulation
{
    /// <summary>Trigger at the roof line. A climbing robot touching it has reached the top.</summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class BarnTopZone : MonoBehaviour { }

    /// <summary>Trigger below the barn. Falling balls and throwables are removed here.</summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class GroundZone : MonoBehaviour { }

    /// <summary>
    /// A climbing hold. Visual part of the slice art; its collider sits on the Holds layer,
    /// which only collides with RobotBall and Throwable — so it acts as a pin for falling balls.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class Hold : MonoBehaviour { }
}
