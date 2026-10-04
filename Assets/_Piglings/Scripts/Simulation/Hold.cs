using Piglings.Events;
using Piglings.Rules;
using UnityEngine;

namespace Piglings.Simulation
{
    /// <summary>
    /// A climbing hold — and a peg socket. Its collider sits on the Holds layer, which only collides with RobotBall and
    /// Throwable, so it acts as a pin for falling balls, not for climbers.
    ///
    /// When a peg is placed here, hits matter: a flying stone or a falling ball touching it is reported to the Rules
    /// (NightSession.HitPeg), which decide the effect and publish the facts. An empty socket reports nothing.
    /// Its socket number and session come from PegBoard.Bind (NightSession calls it in Awake) — no lookups.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class Hold : MonoBehaviour
    {
        private NightSession _session;
        private int _socket = -1;

        public int Socket => _socket;
        public Collider2D Collider { get; private set; }

        public void Bind(NightSession session, int socket)
        {
            _session = session;
            _socket = socket;
            Collider = GetComponent<Collider2D>();
        }

        private void OnCollisionEnter2D(Collision2D c)
        {
            if (_session == null || _socket < 0 || _session.State.Sockets[_socket].IsEmpty) return;

            if (c.collider.TryGetComponent(out Throwable stone))
            {
                if (!stone.InFlight) return;
                var result = _session.HitPeg(_socket, PegHitter.Stone, stone.Id, stone.Chain);
                if (result.Outcome == PegOutcome.Split) SplitStone(stone, result.NewPieces);
            }
            else if (c.collider.TryGetComponent(out RobotController robot) && robot.State == RobotState.Falling)
                _session.HitPeg(_socket, PegHitter.Ball, robot.Id, robot.Chain);
        }

        // The physics half of a split the Rules decided: the fan and the pieces' size come from the peg's definition.
        private void SplitStone(Throwable stone, int newPieces)
        {
            var placed = _session.State.Sockets[_socket];
            var peg = _session.FindPeg(placed.PegId);
            if (peg == null) return;
            stone.Split(newPieces, peg.FanAngleAt(placed.Level), peg.PieceScale, _socket);
        }
    }
}
