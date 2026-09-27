using Piglings.Events;
using Piglings.Simulation;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// Makes the danger line read differently per phase. Before the target it's a warning (a faint red
    /// line); in overtime it's the line that ends the bonus, so it takes the overtime colour (an animated
    /// rainbow by default, like the overtime popups) and pulses. Reads only.
    /// Put it on a thin SpriteRenderer lying along the DangerZone trigger.
    /// </summary>
    public sealed class DangerZoneView : MonoBehaviour
    {
        [SerializeField] private NightSession session;
        [SerializeField] private SpriteRenderer line;

        [SerializeField] private Color warningColor = new Color(1f, 0.25f, 0.2f, 0.35f);
        [Tooltip("Overtime look. Cycle mode walks the whole line through the gradient (PerLetter doesn't apply to a line).")]
        [SerializeField] private PopupColor overtimeColor = new PopupColor
            { mode = PopupColorMode.Cycle, gradient = PopupColor.Rainbow(), speed = 0.6f };
        [Tooltip("Pulses per second in overtime.")]
        [SerializeField, Min(0f)] private float overtimePulse = 2f;

        private void Reset() => line = GetComponent<SpriteRenderer>();

        private void Update()
        {
            if (line == null) return;
            var phase = session.State.Phase;
            line.enabled = phase != NightPhase.Ended;

            if (phase == NightPhase.Overtime)
            {
                // Alpha breathes between 40% and 100% while the colour runs through the style.
                float t = 0.5f + 0.5f * Mathf.Sin(Time.time * overtimePulse * 2f * Mathf.PI);
                var c = overtimeColor.Evaluate(0f, 0f, Time.time);
                line.color = new Color(c.r, c.g, c.b, c.a * Mathf.Lerp(0.4f, 1f, t));
            }
            else
            {
                line.color = warningColor;
            }
        }
    }
}
