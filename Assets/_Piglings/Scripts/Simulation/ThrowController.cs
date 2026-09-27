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
        public readonly bool InRange;       // false = the stone won't reach Target at this speed

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
        [SerializeField] private Transform origin;       // the pig's hand / barn hole
        [SerializeField] private Transform container;
        [SerializeField] private Camera cam;

        [Tooltip("Launch speed. Fixed — the player picks the target, never the power.\n" +
                 "Too low and the far corners of the wall turn out of range (the line goes red).")]
        [SerializeField, Min(0.1f)] private float throwSpeed = 7f;

        [Tooltip("How many seconds of arc to show when the target is out of range, " +
                 "since there's no point where the arc meets it to stop at.")]
        [SerializeField, Min(0.1f)] private float outOfRangePreviewSeconds = 1.2f;

        public event System.Action<Vector2> Thrown;       // Presentation hooks the pig's throw anim here

        /// <summary>The current aim, for TrajectoryView. Default (IsAiming false) when not aiming.</summary>
        public ThrowAim CurrentAim { get; private set; }

        private void Update()
        {
            CurrentAim = default;

            var pointer = Pointer.current;
            if (pointer == null || cam == null) return;
            if (!session.State.CanThrow) return;   // out of stones, target reached, or night over (NightReferee)

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

            var t = Instantiate(throwablePrefab, aim.From, Quaternion.identity, container);
            t.Launch(session, session.Night.Throwable, launch);
            Thrown?.Invoke(aim.Velocity.normalized);

            CurrentAim = default;
        }

        private ThrowAim Solve(Vector2 from, Vector2 target)
        {
            // The gravity the stone will actually feel: world gravity times the prefab's scale.
            float gravity = Mathf.Abs(Physics2D.gravity.y) * throwablePrefab.GravityScale;
            Vector2 delta = target - from;

            bool inRange = ThrowSolver.TrySolve(delta.x, delta.y, throwSpeed, gravity, out float vx, out float vy);

            // In range: stop the line exactly at the target, so the player sees where the stone
            // will be. Out of range there's no such point — show a fixed length instead.
            float duration = inRange ? ThrowSolver.TimeToCross(vx, delta.x) : outOfRangePreviewSeconds;

            // Straight up/down has no horizontal travel, so TimeToCross is 0 — fall back too.
            if (duration <= 0f) duration = outOfRangePreviewSeconds;

            return new ThrowAim(from, target, new Vector2(vx, vy), gravity, duration, inRange);
        }
    }
}
