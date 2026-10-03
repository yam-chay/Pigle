using Piglings.Events;
using Piglings.Simulation;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// The danger line: a faint red warning just below the breach line, hidden once the night is over.
    /// A warning only — it never ends or changes the night (that was overtime's, which the hours replaced). Reads only.
    /// Put it on a thin SpriteRenderer lying along the DangerZone trigger.
    /// </summary>
    public sealed class DangerZoneView : MonoBehaviour
    {
        [SerializeField] private NightSession session;
        [SerializeField] private SpriteRenderer line;

        [SerializeField] private Color warningColor = new Color(1f, 0.25f, 0.2f, 0.35f);

        private void Reset() => line = GetComponent<SpriteRenderer>();

        private void Update()
        {
            if (line == null) return;
            line.enabled = session.State.Phase != NightPhase.Ended;
            line.color = warningColor;
        }
    }
}
