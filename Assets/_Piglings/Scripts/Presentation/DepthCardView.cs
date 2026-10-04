using Piglings.Simulation;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// The DEPTH tutorial card (M10.D): a column of wolves, an arrow between each, and each depth's multiplier ("×1.5")
    /// in that depth's popup colour — so the player connects "orange popup" with "a wolf knocked by a wolf knocked by the
    /// stone". The numbers come from the scoring (NightSession.DepthMultiplier), never typed into the card.
    /// Yam lays out one row (wolf icon = marker, "×m" = label, optional "depth n" = detail) and one arrow; this clones
    /// them. Built once, at Start. Reads only.
    /// </summary>
    public sealed class DepthCardView : MonoBehaviour
    {
        [SerializeField] private NightSession session;
        [Tooltip("One depth row, laid out once. Its parent needs a Vertical Layout Group.")]
        [SerializeField] private TemplateSlot row;
        [Tooltip("Placed between rows (icon_arrow_down). Optional.")]
        [SerializeField] private GameObject arrow;
        [Tooltip("Rows: depth 0 (hit by the stone) to depth Rows − 1. The last one reads \"+\" (that deep or deeper).")]
        [SerializeField, Min(1)] private int rows = 4;
        [Tooltip("Tint the wolf icons with the depth's colour too (off = the icon's own colours).")]
        [SerializeField] private bool tintWolves = false;

        private void Start()
        {
            var colours = session.ScoreColours;
            var slots = TemplateList.Build(row, arrow, rows);
            for (int depth = 0; depth < slots.Count; depth++)
            {
                var slot = slots[depth];
                bool last = depth == slots.Count - 1;
                slot.SetLabel($"×{session.DepthMultiplier(depth):0.##}");
                slot.SetDetail(last && depth > 0 ? $"depth {depth}+" : $"depth {depth}");
                if (slot.Label != null) slot.Label.color = colours.DepthColour(depth);
                if (tintWolves) slot.Tint(colours.DepthColour(depth));
            }
        }
    }
}
