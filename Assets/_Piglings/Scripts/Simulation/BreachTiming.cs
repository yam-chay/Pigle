using System;

namespace Piglings.Simulation
{
    /// <summary>
    /// The shape of a breach sequence in time, as fractions of RobotDefinition.BreachSeconds:
    ///
    ///   0 ──climb onto the perch──▶ ClimbEnds ──hold──▶ JumpStarts ──jump off──▶ 1 (removed)
    ///   0 ─────────stolen stone hops pile → robot─────────▶ StoneLands
    ///
    /// Why fractions and not seconds: BreachSeconds is the one tuning value (in the robot asset). Every part
    /// scales with it, so the order can't break when it's tuned — the stone always lands before the robot
    /// leaves, and the robot is always on the perch before it jumps. These fractions and the jump shape are
    /// the sequence's choreography, not tuning knobs.
    ///
    /// Engine-free (plain floats, no UnityEngine) so Tools/CoreCheck can check that order. RobotController
    /// moves the robot with it and StonePile times the stone's hop with it — one timeline, never two copies.
    /// </summary>
    public static class BreachTiming
    {
        public const float ClimbEnds = 0.35f;   // on the perch, above the throw line
        public const float StoneLands = 0.6f;   // the stolen stone is in the robot's hands
        public const float JumpStarts = 0.8f;   // it has visibly held the stone for a moment; now it leaves

        // The jump off the perch, in ball radii, so it scales with the robot: a little up and out, then down.
        private const float JumpOutRadii = 4f;
        private const float JumpUpRadii = 2f;
        private const float JumpDropRadii = 10f;

        /// <summary>0..1 through the sequence. A non-positive length counts as already over.</summary>
        public static float Phase(float elapsed, float breachSeconds) =>
            breachSeconds <= 0f ? 1f : Clamp01(elapsed / breachSeconds);

        /// <summary>0..1 from where the robot touched the breach line to the perch, eased in and out.</summary>
        public static float ClimbProgress(float phase)
        {
            float t = Clamp01(phase / ClimbEnds);
            return t * t * (3f - 2f * t);
        }

        /// <summary>How long the stolen stone's hop takes, so it lands at StoneLands.</summary>
        public static float StoneHopSeconds(float breachSeconds) => Math.Max(0f, breachSeconds) * StoneLands;

        /// <summary>0..1 through the jump off; 0 until JumpStarts.</summary>
        public static float JumpProgress(float phase) => Clamp01((phase - JumpStarts) / (1f - JumpStarts));

        /// <summary>
        /// Offset from the perch during the jump. <paramref name="away"/> is +1 or -1: the side the robot jumps to
        /// (away from the barn's middle). Zero at the start of the jump, so it leaves the perch without a pop.
        /// </summary>
        public static void JumpOffset(float jump, float away, float ballRadius, out float dx, out float dy)
        {
            dx = away * JumpOutRadii * ballRadius * jump;
            dy = ballRadius * (JumpUpRadii * 4f * jump * (1f - jump) - JumpDropRadii * jump * jump);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
