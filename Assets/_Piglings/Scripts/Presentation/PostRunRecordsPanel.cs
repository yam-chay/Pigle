using Piglings.Meta;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// The post-run's ALL-TIME RECORDS panel (M10.E): best throw, longest chain (wolves), deepest chain, best night score —
    /// each the record as it stands now (tonight included). One broken tonight shows its NEW RECORD tag (the row's badge)
    /// and its value in gold. Yam lays out the four rows (label text, detail = the value, badge = the tag); this fills them.
    /// Every field is optional. Filled once, by PostRunView. Reads only.
    /// </summary>
    public sealed class PostRunRecordsPanel : MonoBehaviour
    {
        [SerializeField] private TemplateSlot bestThrow;
        [SerializeField] private TemplateSlot longestChain;
        [SerializeField] private TemplateSlot deepestChain;
        [SerializeField] private TemplateSlot bestNightScore;
        [Tooltip("A record broken tonight: its value in this colour (the others keep the colour laid out).")]
        [SerializeField] private Color newRecordColour = new Color(1f, 0.8f, 0.25f);

        public void Show(RecordsReport records)
        {
            if (records == null) return;
            var now = records.After;
            Fill(bestThrow, now.BestThrow > 0 ? Numbers.Thousands(now.BestThrow) : "—", records.New.BestThrow);
            Fill(longestChain, now.LongestChain > 0 ? (now.LongestChain == 1 ? "1 wolf" : $"{now.LongestChain} wolves") : "—", records.New.LongestChain);
            Fill(deepestChain, now.DeepestChain > 0 ? $"depth {now.DeepestChain}" : "—", records.New.DeepestChain);
            Fill(bestNightScore, now.BestNightScore > 0 ? Numbers.Thousands(now.BestNightScore) : "—", records.New.BestNightScore);
        }

        private void Fill(TemplateSlot row, string value, bool broken)
        {
            if (row == null) return;
            row.SetDetail(value);
            row.ShowBadge(broken);
            if (broken && row.Detail != null) row.Detail.color = newRecordColour;
        }
    }
}
