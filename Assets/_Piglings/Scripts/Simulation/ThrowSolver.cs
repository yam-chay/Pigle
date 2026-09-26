using System;

namespace Piglings.Simulation
{
    /// <summary>
    /// Pure math for the throw, ported from CCTD ThrowSolver.
    ///
    /// Why it exists: throwing straight at the click always lands short, because gravity pulls the
    /// stone under the straight line — and the miss grows with distance. This keeps the throw SPEED
    /// fixed and solves only the ANGLE, so the stone passes through the clicked point on a real arc.
    ///
    /// Why it's engine-free (no UnityEngine, plain floats): Tools/CoreCheck compiles this one file
    /// and checks that the arc really passes through the target. It lives in Simulation because
    /// that's the layer that throws; ThrowController and TrajectoryView both call it, so the line
    /// that's drawn is by construction the path the stone flies — never a second, drifting copy.
    /// Keep it free of UnityEngine, or CoreCheck stops compiling.
    /// </summary>
    public static class ThrowSolver
    {
        /// <summary>
        /// Launch velocity (vx, vy) at <paramref name="speed"/> that passes through a target
        /// <paramref name="dx"/>, <paramref name="dy"/> away from the launch point.
        /// <paramref name="gravity"/> is the downward acceleration as a positive number.
        ///
        /// Returns false when the target is out of reach at this speed; the velocity is then the
        /// farthest throw towards it (45°), so an out-of-range click still throws somewhere sensible.
        /// </summary>
        public static bool TrySolve(float dx, float dy, float speed, float gravity, out float vx, out float vy)
        {
            float x = Math.Abs(dx);
            float dirX = dx < 0f ? -1f : 1f;

            // No gravity means no parabola: the straight throw is exact.
            // A target straight above/below has no horizontal distance to solve for: aim straight at it.
            if (gravity <= 0f || x < 0.0001f)
            {
                float len = MathF.Sqrt(dx * dx + dy * dy);
                if (len > 0f) { vx = dx / len * speed; vy = dy / len * speed; }
                else { vx = 0f; vy = speed; }
                return gravity <= 0f || ReachesVertically(dy, speed, gravity);
            }

            // Path: y = x·tanθ − g·x² / (2·v²·cos²θ). With 1/cos²θ = 1 + tan²θ that becomes a
            // quadratic in tanθ:   k·tan²θ − x·tanθ + (y + k) = 0,   where k = g·x² / (2·v²).
            float k = gravity * x * x / (2f * speed * speed);
            float disc = x * x - 4f * k * (dy + k);

            if (disc < 0f)
            {
                float c = MathF.Sqrt(0.5f); // cos 45° = sin 45°
                vx = dirX * c * speed;
                vy = c * speed;
                return false;
            }

            // Two roots: a low, direct throw and a high lob. Take the low one — the player clicks a
            // robot they can see, and the lob hangs in the air long enough for the robot to move.
            float tan = (x - MathF.Sqrt(disc)) / (2f * k);
            float angle = MathF.Atan(tan);
            vx = dirX * MathF.Cos(angle) * speed;
            vy = MathF.Sin(angle) * speed;
            return true;
        }

        /// <summary>
        /// Seconds the stone needs to cover a horizontal distance. Used to end the drawn arc exactly
        /// at the target instead of at an arbitrary length.
        /// </summary>
        public static float TimeToCross(float vx, float horizontalDistance)
        {
            float speedX = Math.Abs(vx);
            if (speedX < 0.0001f) return 0f;
            return Math.Abs(horizontalDistance) / speedX;
        }

        /// <summary>Offset from the launch point after <paramref name="t"/> seconds of flight.</summary>
        public static void OffsetAt(float vx, float vy, float gravity, float t, out float x, out float y)
        {
            x = vx * t;
            y = vy * t - 0.5f * gravity * t * t;
        }

        /// <summary>
        /// The vertical launch speed to hand the physics engine so its stepped flight lies on the
        /// ideal arc from <see cref="TrySolve"/>.
        ///
        /// Why: 2D physics moves in fixed steps (velocity first, then position), and that sinks the
        /// body by an extra ½·g·dt·t compared with the smooth formula — about 0.1 units after one
        /// second at 50 Hz, more than half a robot ball. Starting ½·g·dt faster upwards cancels it
        /// exactly at every step. Only the physics body gets this; the drawn arc uses the ideal one.
        /// </summary>
        public static float CompensateForFixedStep(float vy, float gravity, float fixedDeltaTime)
        {
            return vy + 0.5f * gravity * fixedDeltaTime;
        }

        // Straight up needs v² ≥ 2·g·h to get there; straight down always arrives.
        private static bool ReachesVertically(float dy, float speed, float gravity)
        {
            return dy <= 0f || speed * speed >= 2f * gravity * dy;
        }
    }
}
