using UnityEngine;

namespace Piglings.Simulation
{
    /// <summary>
    /// Trigger at the breach line. A climbing robot touching it has reached the top and starts its breach.
    /// It also knows where a breaching robot goes: up onto the perch, above the throw line, so it's clearly
    /// out of play and never sits where the player aims at the robots still climbing.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class BarnTopZone : MonoBehaviour
    {
        [Tooltip("Where a breaching robot climbs to: a point on the perch above the throw line (e.g. beside the stone pile). " +
                 "It jumps off from there, away from the barn's middle.")]
        [SerializeField] private Transform perch;

        private bool _warned;

        /// <summary>
        /// The perch point, and the side to jump off (+1 right, -1 left: away from this zone's centre).
        /// Without a perch the robot breaches where it is — playable, but it'll sit in the line of fire.
        /// </summary>
        public Vector2 Perch(Vector2 robotPosition, out float away)
        {
            Vector2 target = robotPosition;
            if (perch != null) target = perch.position;
            else if (!_warned)
            {
                _warned = true;
                Debug.LogWarning("BarnTopZone: no Perch assigned — breaching robots stay where they reached the top.", this);
            }
            away = target.x >= transform.position.x ? 1f : -1f;
            return target;
        }
    }
}
