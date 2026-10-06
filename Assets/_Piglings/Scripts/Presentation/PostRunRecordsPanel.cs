using Piglings.Definitions;
using Piglings.Meta;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// The post-run's ALL-TIME RECORDS panel (M10.E; rows reshaped in M10.S): best throw, longest chain (wolves), deepest
    /// chain, best night score. Each row = label · bar · value: the value is the record as it stands now (tonight
    /// included); the bar shows how close TONIGHT came to it (tonight ÷ the record). One broken tonight: the bar full and
    /// gold, the value gold, and its small NEW tag (the row's badge — lay it out next to the value, not stretched).
    /// Yam lays out the four rows (label text, bar = a ProgressBarView, detail = the value, badge = the tag); this fills them.
    /// Every field is optional. Filled once, by PostRunView. Reads only.
    /// </summary>
    public sealed class PostRunRecordsPanel : MonoBehaviour
    {
        [SerializeField] private TemplateSlot bestThrow;
        [SerializeField] private TemplateSlot longestChain;
        [SerializeField] private TemplateSlot deepestChain;
        [SerializeField] private TemplateSlot bestNightScore;
        [Tooltip("A record broken tonight: its value and its bar in this colour (the others keep the colours laid out).")]
        [SerializeField] private Color newRecordColour = new Color(1f, 0.8f, 0.25f);

        /// <param name="texts">The UI Texts (NightSession.Texts).</param>
        /// <param name="delay">Seconds before the bars fill in.</param>
        public void Show(RecordsReport records, UiTextsDefinition texts, float delay = 0f)
        {
            if (records == null || texts == null) return;
            string none = texts.nothing;
            var now = records.After;
            var tonight = records.Tonight;
            Fill(bestThrow, now.BestThrow > 0 ? Numbers.Thousands(now.BestThrow) : none, records.New.BestThrow,
                 RecordsReport.Closeness(tonight.BestThrow, now.BestThrow), delay);
            Fill(longestChain, now.LongestChain > 0 ? UiText.Wolves(texts, now.LongestChain) : none, records.New.LongestChain,
                 RecordsReport.Closeness(tonight.LongestChain, now.LongestChain), delay);
            Fill(deepestChain, now.DeepestChain > 0 ? UiText.Fill(texts.recordDepth, ("depth", now.DeepestChain.ToString())) : none, records.New.DeepestChain,
                 RecordsReport.Closeness(tonight.DeepestChain, now.DeepestChain), delay);
            Fill(bestNightScore, now.BestNightScore > 0 ? Numbers.Thousands(now.BestNightScore) : none, records.New.BestNightScore,
                 RecordsReport.Closeness(tonight.BestNightScore, now.BestNightScore), delay);
        }

        private void Fill(TemplateSlot row, string value, bool broken, float closeness, float delay)
        {
            if (row == null) return;
            row.SetDetail(value);
            row.ShowBadge(broken);
            if (broken && row.Detail != null) row.Detail.color = newRecordColour;
            if (row.Bar == null) return;
            if (broken) row.Bar.TintGain(newRecordColour);
            row.Bar.Show(0f, broken ? 1f : closeness, false, delay);
        }
    }
}
