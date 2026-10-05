using TMPro;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// One live chain counter (M10.S): "SCORE × MULT × H2", numbers only, a small label under each (laid out by Yam — the
    /// "×" signs and the labels are static text). ChainCounterView clones the template per chain, fills it as the chain
    /// grows, and resolves it at the close (the result, then it flies to the score). Every field is optional. Reads only.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class ChainCounter : MonoBehaviour
    {
        [Tooltip("The chain's SCORE: \"40\".")]
        [SerializeField] private TMP_Text score;
        [Tooltip("The chain's MULT: \"3\".")]
        [SerializeField] private TMP_Text mult;
        [Tooltip("The hour it was thrown in: \"H2\".")]
        [SerializeField] private TMP_Text hour;
        [Tooltip("The result at the close (\"120\"), shown in place of the three. Empty = the score text shows it.")]
        [SerializeField] private TMP_Text result;
        [Tooltip("Hidden at the close when Result is set (the score × mult × hour row). Optional.")]
        [SerializeField] private GameObject liveRow;
        [Tooltip("For fading (added if missing).")]
        [SerializeField] private CanvasGroup group;

        public RectTransform Rect => (RectTransform)transform;

        public CanvasGroup Group
        {
            get
            {
                if (group == null && !TryGetComponent(out group)) group = gameObject.AddComponent<CanvasGroup>();
                return group;
            }
        }

        public void Show(int chainScore, float chainMult, int chainHour)
        {
            if (result != null) result.gameObject.SetActive(false);
            if (liveRow != null) liveRow.SetActive(true);
            if (score != null) score.text = Numbers.Thousands(chainScore);
            if (mult != null) mult.text = $"{chainMult:0.##}";
            if (hour != null) hour.text = $"H{chainHour}";
        }

        public void Resolve(int total)
        {
            if (result != null)
            {
                if (liveRow != null) liveRow.SetActive(false);
                result.gameObject.SetActive(true);
                result.text = Numbers.Thousands(total);
            }
            else if (score != null) score.text = Numbers.Thousands(total);
        }
    }
}
