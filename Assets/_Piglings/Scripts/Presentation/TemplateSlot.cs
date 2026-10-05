using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Piglings.Presentation
{
    /// <summary>
    /// One laid-out row / dot / item that a view clones and fills from data (M10.D: the scoreboard's throw rows, the
    /// tutorial cards' depth rows and hour dots; M10.E: the post-run's rows, with a bar, a third text and a tag). Yam lays it out once in the editor; this only says which parts the code
    /// fills. Every part is optional. Presentation only: it holds references, no behaviour.
    /// </summary>
    public sealed class TemplateSlot : MonoBehaviour
    {
        [Tooltip("The main text (e.g. \"9 wolves · depth 2\", \"×1.5\").")]
        [SerializeField] private TMP_Text label;
        [Tooltip("A second text (e.g. the points \"+620\").")]
        [SerializeField] private TMP_Text detail;
        [Tooltip("An image the code tints (a marker pill, an hour dot, a wolf icon) — or, for an Image, gives a sprite (a peg, a stone).")]
        [SerializeField] private Graphic marker;
        [Tooltip("A third text (e.g. \"6 copies → 3 in a row\", \"unlocked by dawn on night 1\").")]
        [SerializeField] private TMP_Text note;
        [Tooltip("A bar the code fills (the post-run's rows).")]
        [SerializeField] private ProgressBarView bar;
        [Tooltip("A tag the code shows or hides (NEW, NEW RECORD).")]
        [SerializeField] private GameObject badge;

        public TMP_Text Label => label;
        public TMP_Text Detail => detail;
        public TMP_Text Note => note;
        public Graphic Marker => marker;
        public ProgressBarView Bar => bar;

        public void SetLabel(string text) { if (label != null) label.text = text; }
        public void SetDetail(string text) { if (detail != null) detail.text = text; }
        public void SetNote(string text) { if (note != null) note.text = text; }
        public void Tint(Color colour) { if (marker != null) marker.color = colour; }
        public void ShowBadge(bool on) { if (badge != null && badge.activeSelf != on) badge.SetActive(on); }

        /// <summary>The marker's sprite, when the marker is an Image (a missing sprite keeps the one laid out).</summary>
        public void SetSprite(Sprite sprite)
        {
            if (sprite != null && marker is Image image) image.sprite = sprite;
        }

        /// <summary>The marker's alpha (faded = not reached yet), keeping its colour.</summary>
        public void SetAlpha(float alpha)
        {
            if (marker == null) return;
            var c = marker.color;
            marker.color = new Color(c.r, c.g, c.b, alpha);
        }
    }
}
