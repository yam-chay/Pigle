using UnityEngine;
using UnityEngine.InputSystem;

namespace Piglings.Simulation
{
    /// <summary>
    /// What the pig is aiming at right now. A snapshot for views: reading it changes nothing.
    /// Velocity is the ideal arc (the one <see cref="ThrowSolver"/> solved), which is also the path
    /// the stone flies — so a view can draw it with ThrowSolver.OffsetAt and trust the line.
    /// </summary>
    public readonly struct ThrowAim
    {
        public readonly bool IsAiming;      // false = nothing to draw
        public readonly Vector2 From;
        public readonly Vector2 Target;
        public readonly Vector2 Velocity;
        public readonly float Gravity;      // downward acceleration, positive
        public readonly float Duration;     // seconds of arc worth drawing
        public readonly bool InRange;       // false = drawn as refused (the peg thrower: no valid socket); the stone is always true

        public ThrowAim(Vector2 from, Vector2 target, Vector2 velocity, float gravity, float duration, bool inRange)
        {
            IsAiming = true;
            From = from;
            Target = target;
            Velocity = velocity;
            Gravity = gravity;
            Duration = duration;
            InRange = inRange;
        }
    }

    /// <summary>
    /// Press to aim, release to throw — ported from CCTD ThrowController. The stone always leaves
    /// at the same speed; only the angle is solved (ThrowSolver) so its arc passes through the
    /// point where the pointer was released.
    ///
    /// Why press-and-release instead of throw-on-click: without a held "aiming" moment there's no
    /// time for the trajectory line to help, and on touch there's no hover at all. A quick tap
    /// still throws exactly where you tapped.
    ///
    /// Input is read from Pointer.current, which covers mouse and touch with the same code.
    /// </summary>
    public sealed class ThrowController : MonoBehaviour
    {
        [SerializeField] private NightSession session;
        [SerializeField] private Throwable throwablePrefab;
        [Tooltip("ThrowOrigin: a child of the pig's throwing-hand bone, so the aim line and the launch start from the hand as it animates.")]
        [SerializeField] private Transform origin;
        [SerializeField] private Transform container;
        [SerializeField] private Camera cam;
        [Tooltip("The pile on the perch. Its stone in the pig's hand is the one thrown. Empty = the old way " +
                 "(a new stone from the prefab each throw), so the scene works before the pile is wired.")]
        [SerializeField] private StonePile pile;

        [Tooltip("Launch speed. Fixed — the player picks the target, never the power.\n" +
                 "Too low and the far corners of the wall can't be reached on the direct arc (the stone lobs at 45° instead).")]
        [SerializeField, Min(0.1f)] private float throwSpeed = 7f;

        public event System.Action<Vector2> Thrown;       // Presentation hooks the pig's throw anim here

        /// <summary>The current aim, for TrajectoryView. Default (IsAiming false) when not aiming.</summary>
        public ThrowAim CurrentAim { get; private set; }

        private void Update()
        {
            CurrentAim = default;

            var pointer = Pointer.current;
            if (pointer == null || cam == null) return;
            if (!session.State.CanThrow) return;   // out of stones, target reached, or night over (NightReferee)
            if (session.GameplayInputBlocked) return;   // the F1 debug panel is open: its clicks aren't throws
            // Cooling down: no aim, so no line and no raised arm. Holding the button through the
            // cooldown still works — the aim appears the moment it ends and release throws as usual.
            if (Time.time < readyAt) return;
            // The next stone is still hopping up from the pile: nothing in the hand to aim with yet.
            if (pile != null && !pile.HasStoneInHand) return;

            // Released this frame counts as "still aiming" so the throw uses the same aim the
            // player saw on the last frame of the drag.
            bool released = pointer.press.wasReleasedThisFrame;
            if (!pointer.press.isPressed && !released) return;

            Vector2 target = cam.ScreenToWorldPoint(pointer.position.ReadValue());
            CurrentAim = Solve(origin.position, target);

            if (released) Throw(CurrentAim);
        }

        private void Throw(ThrowAim aim)
        {
            // The physics body gets a slightly higher start so its stepped flight matches the
            // ideal arc (see ThrowSolver.CompensateForFixedStep). Everything else uses the ideal.
            Vector2 launch = new Vector2(
                aim.Velocity.x,
                ThrowSolver.CompensateForFixedStep(aim.Velocity.y, aim.Gravity, Time.fixedDeltaTime));

            // The stone in the hand (from the pile) is the one that flies; without a pile, make one.
            var t = pile != null ? pile.ReleaseHeld(container) : Instantiate(throwablePrefab, aim.From, Quaternion.identity, container);
            if (t == null) return;
            t.transform.position = aim.From;
            t.Launch(session, session.Weapon, launch);
            Thrown?.Invoke(aim.Velocity.normalized);
            readyAt = Time.time + throwCooldown;

            CurrentAim = default;
        }

        private ThrowAim Solve(Vector2 from, Vector2 target)
        {
            // The gravity the stone will actually feel: world gravity times the prefab's scale.
            float gravity = Mathf.Abs(Physics2D.gravity.y) * throwablePrefab.GravityScale;
            Vector2 delta = target - from;

            // Out of reach, TrySolve gives the farthest throw toward the target (45°). That's fine: the roof keeps the
            // stone in, so there's no "can't throw there" state and no red line — the line just shows where it goes.
            ThrowSolver.TrySolve(delta.x, delta.y, throwSpeed, gravity, out float vx, out float vy);

            // Stop the line where the arc passes the target's x, so the player sees where the stone will be. Straight
            // up/down has no horizontal travel (TimeToCross is 0): draw to the top of the arc instead.
            float duration = ThrowSolver.TimeToCross(vx, delta.x);
            if (duration <= 0f) duration = Mathf.Max(0.1f, vy / Mathf.Max(0.0001f, gravity));

            return new ThrowAim(from, target, new Vector2(vx, vy), gravity, duration, true);
        }

        [Tooltip("Seconds after a throw before the pig can aim again. Any value works — the pig's Animator restarts the throw on every release.")]
        [SerializeField, Min(0f)] private float throwCooldown = 0.4f;

        private float readyAt;   // Time.time when the next aim is allowed
    }
}
