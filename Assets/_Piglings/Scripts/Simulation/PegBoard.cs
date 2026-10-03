using UnityEngine;

namespace Piglings.Simulation
{
    /// <summary>
    /// The wall's peg sockets: every Hold under this object, in hierarchy order. Socket i = the i-th Hold — that
    /// index is the socket id the Rules store pegs under (NightState.Sockets), so don't reorder Holds mid-night.
    /// A plain hold is an empty socket. The count is also the most pegs the wall can hold.
    ///
    /// Put it on Barn (the parent of every slice). Holds are found down its own hierarchy (GetComponentsInChildren),
    /// not by a scene-wide lookup, and only once.
    /// </summary>
    public sealed class PegBoard : MonoBehaviour
    {
        private Hold[] _holds;

        // Lazy: NightSession asks for the count in its Awake, which runs before this one's (execution order -1000).
        private Hold[] Holds => _holds ?? (_holds = GetComponentsInChildren<Hold>(includeInactive: true));

        public int SocketCount => Holds.Length;
        public Vector3 Position(int socket) => Holds[socket].transform.position;
        public Hold HoldAt(int socket) => Holds[socket];

        /// <summary>
        /// The socket nearest to <paramref name="point"/>, within <paramref name="radius"/>, that a peg of this type can
        /// go into (an empty socket, or a same-type peg it can merge with). -1 = none. Pegs it can't go on are ignored,
        /// so a throw can pass a full socket and snap to a valid one behind it.
        /// </summary>
        public int NearestValid(Vector2 point, string pegId, float radius, NightSession session)
        {
            if (string.IsNullOrEmpty(pegId)) return -1;
            int best = -1;
            float bestSq = radius * radius;
            for (int i = 0; i < Holds.Length; i++)
            {
                if (!session.IsValidPegTarget(i, pegId)) continue;
                float sq = ((Vector2)Holds[i].transform.position - point).sqrMagnitude;
                if (sq <= bestSq) { bestSq = sq; best = i; }
            }
            return best;
        }
    }
}
