using UnityEngine;

namespace Piglings.Simulation
{
    /// <summary>
    /// A climbing hold. Its collider sits on the Holds layer, which only collides with
    /// RobotBall and Throwable — so it acts as a pin for falling balls, not for climbers.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class Hold : MonoBehaviour { }
}