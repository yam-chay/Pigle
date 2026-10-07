using Piglings.Simulation;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// Draws the predicted throw arc while the player aims — ported from CCTD TrajectoryView.
    ///
    /// View only: it reads ThrowController.CurrentAim and never calls anything that changes the
    /// game. It doesn't compute the arc either — each point comes from ThrowSolver.OffsetAt, the
    /// same math the throw was solved with, so the line can't drift away from the real flight.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class TrajectoryView : MonoBehaviour
    {
        [SerializeField] private ThrowController thrower;
        [Tooltip("Optional: the peg thrower. Only one of the two ever aims at a time (stones can't be thrown in a placement round).")]
        [SerializeField] private PegThrower pegThrower;
        [SerializeField] private LineRenderer line;

        [Tooltip("Points sampled along the arc. More = smoother line.")]
        [SerializeField, Min(2)] private int sampleCount = 32;

        [Tooltip("Line colour while aiming (the stone, or a peg with a valid socket).")]
        [SerializeField] private Color inRangeColor = new Color(1f, 1f, 1f, 0.55f);

        [Tooltip("Line colour when a peg throw is refused (no valid socket in reach). The stone never uses it.")]
        [SerializeField] private Color outOfRangeColor = new Color(1f, 0.35f, 0.3f, 0.55f);

        private Vector3[] _points;

        private void Reset() => line = GetComponent<LineRenderer>();

        private void Awake()
        {
            if (line == null) line = GetComponent<LineRenderer>();
            _points = new Vector3[sampleCount];   // allocated once, reused every frame
            line.useWorldSpace = true;
            line.positionCount = 0;
        }

        // LateUpdate so we draw the aim ThrowController settled on this frame, whatever the script order.
        private void LateUpdate()
        {
            ThrowAim aim = thrower.CurrentAim;
            if (!aim.IsAiming && pegThrower != null) aim = pegThrower.CurrentAim;
            if (!aim.IsAiming)
            {
                line.positionCount = 0;
                return;
            }

            // _points.Length, not sampleCount: the array was sized in Awake, and the Inspector
            // value can change during play.
            int n = _points.Length;
            float step = aim.Duration / (n - 1);
            for (int i = 0; i < n; i++)
            {
                ThrowSolver.OffsetAt(aim.Velocity.x, aim.Velocity.y, aim.Gravity, step * i, out float x, out float y);
                _points[i] = new Vector3(aim.From.x + x, aim.From.y + y, 0f);
            }

            line.positionCount = n;
            line.SetPositions(_points);

            // Refused only changes the colour (the peg thrower: no valid socket there, releasing won't throw); the line is
            // still drawn. The stone is never refused. The tail fades so the end doesn't read as a wall.
            Color c = aim.InRange ? inRangeColor : outOfRangeColor;
            line.startColor = c;
            line.endColor = new Color(c.r, c.g, c.b, c.a * 0.25f);
        }
    }
}
