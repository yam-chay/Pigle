using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Piglings.Presentation
{
    /// <summary>
    /// One laid-out row / dot / item that a view clones and fills from data (M10.D: the scoreboard's throw rows, the
    /// tutorial cards' depth rows and hour dots). Yam lays it out once in the editor; this only says which parts the code
    /// fills. Every part is optional. Presentation only: it holds references, no behaviour.
    /// </summary>
    public sealed class TemplateSlot : MonoBehaviour
    {
        [Tooltip("The main text (e.g. \"9 wolves · depth 2\", \"×1.5\").")]
        [SerializeField] private TMP_Text label;
        [Tooltip("A second text (e.g. the points \"+620\").")]
        [SerializeField] private TMP_Text detail;
        [Tooltip("An image the code tints (a marker pill, an hour dot, a wolf icon).")]
        [SerializeField] private Graphic marker;

        public TMP_Text Label => label;
        public TMP_Text Detail => detail;
        public Graphic Marker => marker;

        public void SetLabel(string text) { if (label != null) label.text = text; }
        public void SetDetail(string text) { if (detail != null) detail.text = text; }
        public void Tint(Color colour) { if (marker != null) marker.color = colour; }
    }
}
