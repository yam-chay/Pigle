using Piglings.Simulation;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// The HOURS tutorial card (M10.D): a track from the moon to the sun, one dot per hour of tonight in the hour's palette
    /// colour with its multiplier ("×2"), and a peg marker between hours (each threshold brings a peg placement). The
    /// numbers come from tonight's night and scoring (NightSession), so a night with 7 hours shows 7 dots.
    /// Yam lays out the track once: icon_moon, one hour dot (marker = the dot, label = "×m", detail = the hour number),
    /// one peg marker, icon_sun — in that order under a Horizontal Layout Group; this clones the dot and the marker
    /// between the moon and the sun. Built once, at Start. Reads only.
    /// </summary>
    public sealed class HoursCardView : MonoBehaviour
    {
        [SerializeField] private NightSession session;
        [Tooltip("One hour dot, laid out once. Its parent needs a Horizontal (or Vertical) Layout Group.")]
        [SerializeField] private TemplateSlot hour;
        [Tooltip("Placed between hours: a peg (each threshold brings a placement round). Optional.")]
        [SerializeField] private GameObject pegMarker;

        private void Start()
        {
            // Fail safe when not fully wired (it goes in PR G): say so once, draw nothing.
            if (session == null || hour == null)
            {
                Debug.LogWarning("HoursCardView: Session or Hour isn't set — the card stays empty.", this);
                return;
            }
            int count = session.ThresholdCount;
            var slots = TemplateList.Build(hour, pegMarker, count);
            for (int i = 0; i < slots.Count; i++)
            {
                int h = i + 1;
                slots[i].SetLabel($"×{session.HourMultiplierAt(h):0.##}");
                slots[i].SetDetail($"{h}");
                slots[i].Tint(session.HourColour(h));
            }
        }
    }
}
