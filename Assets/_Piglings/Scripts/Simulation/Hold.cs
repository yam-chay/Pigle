using Piglings.Events;
using Piglings.Rules;
using UnityEngine;

namespace Piglings.Simulation
{
    /// <summary>
    /// A climbing hold — and a peg socket. Its collider sits on the Holds layer, which only collides with RobotBall and
    /// Throwable, so it acts as a pin for falling balls, not for climbers.
    ///
    /// Every hit of a flying stone or a falling ball is reported to the Rules (NightSession.HitPeg), which decide what it
    /// does and publish the facts: a plain hold (no peg) adds the plain-peg score to the chain (M10.S, once per hold per
    /// stone / ball), a placed peg its effect.
    /// This does the physical half of what they decide: split the stone (Splitter), or blow the climbing robots in range
    /// loose and push what's falling (Bomb).
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
            if (_session == null || _socket < 0) return;

            if (c.collider.TryGetComponent(out Throwable stone))
            {
                if (!stone.InFlight) return;
                var result = _session.HitPeg(_socket, PegHitter.Stone, stone.Id, stone.Chain);
                if (result.Outcome == PegOutcome.Split) SplitStone(stone, result.NewPieces);
                else if (result.Outcome == PegOutcome.Exploded) Explode(result, stone.Chain);
            }
            else if (c.collider.TryGetComponent(out RobotController robot) && robot.State == RobotState.Falling)
            {
                var result = _session.HitPeg(_socket, PegHitter.Ball, robot.Id, robot.Chain);
                if (result.Outcome == PegOutcome.Exploded) Explode(result, robot.Chain);
            }
        }

        // The physics half of a Bomb the Rules set off: every climbing robot in the level's radius loses its grip — and
        // falls as a normal ball (gravity is still the weapon) — attributed to the explosion; then a push to whatever is
        // already falling or flying nearby.
        private void Explode(PegHitResult result, ChainId chain)
        {
            var placed = _session.State.Sockets[_socket];
            var peg = _session.FindPeg(placed.PegId);
            if (peg == null) return;
            Vector2 centre = transform.position;
            float radius = peg.BombRadiusAt(placed.Level);

            var climbing = Physics2D.OverlapCircleAll(centre, radius, LayerMask.GetMask(PhysicsLayers.RobotClimbing));
            foreach (var hit in climbing)
                if (hit.TryGetComponent(out RobotController robot) && robot.CanLoseGrip)
                    robot.LoseGrip(chain, Attribution.FromPeg(result.Explosion, result.VictimDepth));

            float impulse = peg.ImpulseAt(placed.Level);
            if (impulse <= 0f) return;
            var loose = Physics2D.OverlapCircleAll(centre, radius, LayerMask.GetMask(PhysicsLayers.RobotBall, PhysicsLayers.Throwable));
            foreach (var hit in loose)
            {
                var body = hit.attachedRigidbody;
                if (body == null || body.bodyType != RigidbodyType2D.Dynamic) continue;
                Vector2 away = body.position - centre;
                // Strongest at the bomb, nothing at the edge; straight up if it sits exactly on the bomb.
                float falloff = 1f - Mathf.Clamp01(away.magnitude / Mathf.Max(0.0001f, radius));
                body.AddForce((away.sqrMagnitude > 1e-6f ? away.normalized : Vector2.up) * (impulse * falloff), ForceMode2D.Impulse);
            }
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
