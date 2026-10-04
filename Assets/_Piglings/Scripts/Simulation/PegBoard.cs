using System.Collections.Generic;
using Piglings.Definitions;
using Piglings.Events;
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
    ///
    /// Bind (NightSession, in Awake) tells every Hold its socket so it can report peg hits, and keeps each socket's
    /// collider material matching its peg: a Bouncy peg gets a bouncier material for its level (made at runtime, one per
    /// bounciness), so balls and stones visibly pop off it. Other pegs keep the hold's own material.
    /// </summary>
    public sealed class PegBoard : MonoBehaviour
    {
        private Hold[] _holds;

        // Lazy: NightSession asks for the count in its Awake, which runs before this one's (execution order -1000).
        private Hold[] Holds => _holds ?? (_holds = GetComponentsInChildren<Hold>(includeInactive: true));

        private NightSession _session;
        private PhysicsMaterial2D[] _plainMaterials;   // each hold's own material, from the scene
        // One runtime material per (hold material, bounciness), shared by every socket that needs it.
        private readonly Dictionary<(PhysicsMaterial2D plain, float bounciness), PhysicsMaterial2D> _bouncy =
            new Dictionary<(PhysicsMaterial2D, float), PhysicsMaterial2D>();

        public int SocketCount => Holds.Length;
        public Vector3 Position(int socket) => Holds[socket].transform.position;
        public Hold HoldAt(int socket) => Holds[socket];

        /// <summary>
        /// The Holds changed (TowerBuilder built a tower): forget the old list and bind the new one. Sockets are per night —
        /// only call this before the night begins (the Rules sized the board from the count).
        /// </summary>
        public void Rebuild()
        {
            _holds = null;
            if (_session != null) Bind(_session);
        }

        /// <summary>Called once by NightSession in Awake: every Hold learns its socket and reports peg hits from now on.</summary>
        public void Bind(NightSession session)
        {
            _session = session;
            _plainMaterials = new PhysicsMaterial2D[Holds.Length];
            for (int i = 0; i < Holds.Length; i++)
            {
                Holds[i].Bind(session, i);
                _plainMaterials[i] = Holds[i].Collider != null ? Holds[i].Collider.sharedMaterial : null;
            }
        }

        // Start, not Awake: NightSession creates the bus in its Awake, and calls Bind there.
        private void Start()
        {
            if (_session == null) return;
            _session.Bus.Subscribe<PegPlaced>(OnPegPlaced);
            _session.Bus.Subscribe<PegMerged>(OnPegMerged);
        }

        private void OnDestroy()
        {
            if (_session == null || _session.Bus == null) return;
            _session.Bus.Unsubscribe<PegPlaced>(OnPegPlaced);
            _session.Bus.Unsubscribe<PegMerged>(OnPegMerged);
        }

        private void OnPegPlaced(PegPlaced e) => ApplyMaterial(e.Socket, e.PegId, e.Level);
        private void OnPegMerged(PegMerged e) => ApplyMaterial(e.Socket, e.PegId, e.Level);

        // Bounciness is the peg's physical side of Bouncy. Everything else (score) is the Rules'.
        private void ApplyMaterial(int socket, string pegId, int level)
        {
            var collider = Holds[socket].Collider;
            if (collider == null) return;
            var def = _session.FindPeg(pegId);
            float bounciness = def != null && def.Effect == PegEffect.Bouncy ? def.BouncinessAt(level) : 0f;
            collider.sharedMaterial = bounciness > 0f ? Bouncy(bounciness, _plainMaterials[socket]) : _plainMaterials[socket];
        }

        private PhysicsMaterial2D Bouncy(float bounciness, PhysicsMaterial2D plain)
        {
            if (_bouncy.TryGetValue((plain, bounciness), out var m)) return m;
            // Same friction as the hold, so only the bounce changes.
            m = new PhysicsMaterial2D($"PegBouncy {bounciness:0.##}") { bounciness = bounciness, friction = plain != null ? plain.friction : 0.4f };
            _bouncy[(plain, bounciness)] = m;
            return m;
        }

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
