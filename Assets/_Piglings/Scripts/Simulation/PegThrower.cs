using Piglings.Events;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Piglings.Simulation
{
    /// <summary>
    /// Throwing a peg in a placement round: the same press-to-aim, release-to-throw and the same arc as the stone
    /// (ThrowSolver), but the peg goes to a socket, not into physics.
    ///
    /// - The aim snaps to the nearest VALID socket within Snap Radius of the pointer (an empty Hold, or a same-type
    ///   peg it can merge with — PegBoard.NearestValid asks the Rules). The arc is solved to that socket, so the line
    ///   ends exactly where the peg will stick. No valid socket near the pointer = the line goes red and releasing
    ///   does nothing: the throw is refused, nothing is used.
    /// - The peg flies the solved arc (moved along it, no physics: it can't hit robots and never starts a chain),
    ///   and on landing the Rules place or merge it (NightSession.PlacePeg). Refused = it hops back to the shelf.
    /// - Right mouse = swap to the next peg type on the shelf. A press that starts on a shelf peg picks it and is
    ///   used up — it never becomes an aim, so letting go never throws the peg at the shelf.
    ///
    /// ThrowController is idle meanwhile (CanThrow is false in a placement round), so the two never both aim.
    /// </summary>
    public sealed class PegThrower : MonoBehaviour
    {
        [SerializeField] private NightSession session;
        [SerializeField] private PegShelf shelf;
        [SerializeField] private PegBoard board;
        [Tooltip("ThrowOrigin — the same launch point as the stone.")]
        [SerializeField] private Transform origin;
        [SerializeField] private Camera cam;

        [Tooltip("Launch speed of a peg. Like the stone's: fixed, only the angle is solved.")]
        [SerializeField, Min(0.1f)] private float throwSpeed = 7f;
        [Tooltip("The aim snaps to a valid socket this close to the pointer (world units). Farther = red line, refused.")]
        [SerializeField, Min(0.05f)] private float snapRadius = 0.6f;
        [Tooltip("Gravity the peg's arc uses, × world gravity. 1 = the same arc shape as a stone at gravity scale 1.")]
        [SerializeField, Min(0.01f)] private float gravityScale = 1f;
        [Tooltip("How many seconds of arc to show when the aim is refused (no point to stop the line at).")]
        [SerializeField, Min(0.1f)] private float refusedPreviewSeconds = 1.2f;

        public event System.Action<Vector2> Thrown;   // PigView plays the throw anim

        /// <summary>The current aim, for TrajectoryView. InRange false = refused (no valid socket in reach).</summary>
        public ThrowAim CurrentAim { get; private set; }

        /// <summary>The shelf peg under the pointer during a placement round (for PegShelfView's hover); null otherwise.</summary>
        public SpriteRenderer HoveredShelfPeg { get; private set; }

        private bool _pressOnShelf;   // this press began on a shelf peg: a pick, never an aim
        private bool _cancelled;      // this press was cancelled with the right button: no aim, no throw until it's released

        // The peg in the air.
        private SpriteRenderer _flying;
        private string _flyingId;
        private int _flyingSocket;
        private Vector2 _flyFrom, _flyVelocity;
        private float _flyGravity, _flyTime, _flyDuration;

        private void Update()
        {
            CurrentAim = default;
            HoveredShelfPeg = null;

            if (_flying != null) { Fly(); return; }
            if (session.State.Phase != NightPhase.PegPlacement || cam == null) { _pressOnShelf = false; return; }
            if (session.GameplayInputBlocked) { _pressOnShelf = false; return; }   // the F1 debug panel is open

            var pointer = Pointer.current;
            if (pointer == null) return;
            Vector2 world = cam.ScreenToWorldPoint(pointer.position.ReadValue());

            HoveredShelfPeg = session.State.PegThrowsLeft > 0 ? shelf.ItemAt(world) : null;

            // The right button: while aiming it cancels the throw (the peg stays in the hand — Yam, 2026-10-09, like the
            // stone); otherwise it swaps the peg in the hand.
            // Cleared the frame AFTER the release: on the release frame itself it must still block the throw.
            if (_cancelled && !pointer.press.isPressed && !pointer.press.wasReleasedThisFrame) _cancelled = false;
            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.wasPressedThisFrame)
            {
                bool aiming = pointer.press.isPressed && !_pressOnShelf && shelf.HasPegInHand && !_cancelled;
                if (aiming) _cancelled = true;
                else shelf.SwapNext();
            }

            if (pointer.press.wasPressedThisFrame)
            {
                var onShelf = shelf.ItemAt(world);
                if (onShelf != null) { shelf.Pick(onShelf); _pressOnShelf = true; return; }
            }
            if (_pressOnShelf)
            {
                if (!pointer.press.isPressed) _pressOnShelf = false;
                return;
            }

            if (!shelf.HasPegInHand) return;
            bool released = pointer.press.wasReleasedThisFrame;
            if (!pointer.press.isPressed && !released) return;
            if (_cancelled) return;

            int socket = board.NearestValid(world, shelf.HeldPegId, snapRadius, session);
            CurrentAim = socket >= 0 ? Solve(origin.position, board.Position(socket), true) : Solve(origin.position, world, false);

            if (released && socket >= 0 && CurrentAim.InRange) Launch(socket);
        }

        private ThrowAim Solve(Vector2 from, Vector2 target, bool validSocket)
        {
            float gravity = Mathf.Abs(Physics2D.gravity.y) * gravityScale;
            Vector2 delta = target - from;
            bool reachable = ThrowSolver.TrySolve(delta.x, delta.y, throwSpeed, gravity, out float vx, out float vy);
            bool ok = validSocket && reachable;
            float duration = ok ? ThrowSolver.TimeToCross(vx, delta.x) : refusedPreviewSeconds;
            if (duration <= 0f) duration = ok ? Mathf.Max(0.1f, delta.magnitude / throwSpeed) : refusedPreviewSeconds;
            return new ThrowAim(from, target, new Vector2(vx, vy), gravity, duration, ok);
        }

        private void Launch(int socket)
        {
            var aim = CurrentAim;
            var peg = shelf.ReleaseHeld(out _flyingId);
            if (peg == null) return;
            _flying = peg;
            _flyingSocket = socket;
            _flyFrom = aim.From;
            _flyVelocity = aim.Velocity;
            _flyGravity = aim.Gravity;
            _flyDuration = aim.Duration;
            _flyTime = 0f;
            Thrown?.Invoke(aim.Velocity.normalized);
            CurrentAim = default;
        }

        // Moved along the solved arc, not simulated: it lands exactly on the socket, every time.
        private void Fly()
        {
            _flyTime = Mathf.Min(_flyDuration, _flyTime + Time.deltaTime);
            ThrowSolver.OffsetAt(_flyVelocity.x, _flyVelocity.y, _flyGravity, _flyTime, out float x, out float y);
            _flying.transform.position = new Vector3(_flyFrom.x + x, _flyFrom.y + y, _flying.transform.position.z);
            if (_flyTime < _flyDuration) return;

            var peg = _flying;
            _flying = null;
            // The board shows the placed peg on its Hold (PegBoardView), so the thrown object's job is done.
            if (session.PlacePeg(_flyingSocket, _flyingId)) Destroy(peg.gameObject);
            else shelf.Return(peg, _flyingId);
        }
    }
}
