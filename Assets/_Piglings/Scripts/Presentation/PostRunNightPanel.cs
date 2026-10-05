using System.Collections.Generic;
using Piglings.Events;
using Piglings.Simulation;
using TMPro;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// The post-run's THE NIGHT panel (M10.E, PROTOTYPE_V2.md ▸ PR E; mockup Docs/Mockups/post_run_v5.png): "NIGHT n"; the
    /// result (DAWN in the dawn gold — the palette's last colour — or OUT OF STONES); a dot per hour of the night in its
    /// colour plus a last one for dawn (gold), faded past what was reached; "6 / 6" (hours reached / hours); the score
    /// ("kept X (hour n)" on a loss); numbers with thousands separators; BEST THROW EACH HOUR — a row per hour reached: "Hn" in its colour, a bar
    /// against the night's best throw, the points, the night's best in rainbow; the biggest chain, avg per stone, stones
    /// thrown · stolen ("26 · 2"). Yam lays out the labels ("biggest chain"…), one hour dot and one hour row; this fills the values.
    /// Every field is optional. Filled once, by PostRunView. Reads only.
    /// </summary>
    public sealed class PostRunNightPanel : MonoBehaviour
    {
        [SerializeField] private NightSession session;

        [Header("Head")]
        [Tooltip("\"NIGHT 2\".")]
        [SerializeField] private TMP_Text nightTitle;
        [Tooltip("\"DAWN\" (in the palette's last colour, the dawn gold) or \"OUT OF STONES\".")]
        [SerializeField] private TMP_Text result;
        [SerializeField] private Color outOfStonesColour = new Color(0.85f, 0.35f, 0.3f);
        [Tooltip("One hour dot (marker = the dot), cloned once per hour of the night + one for dawn (the last, gold) under a " +
                 "Horizontal Layout Group.")]
        [SerializeField] private TemplateSlot hourDot;
        [Tooltip("A dot past the hour reached: its colour at this alpha.")]
        [SerializeField, Range(0f, 1f)] private float unreachedAlpha = 0.2f;
        [Tooltip("Hours reached / the night's hours: \"6 / 6\".")]
        [SerializeField] private TMP_Text hoursReached;
        [Tooltip("The score: \"48,545\" at dawn, \"kept 1,500 (hour 3)\" on a loss.")]
        [SerializeField] private TMP_Text score;

        [Header("Best throw each hour")]
        [Tooltip("One row, cloned per hour reached under a Vertical Layout Group: label = \"H2\" (hour colour), detail = the " +
                 "points, bar = against the night's best throw (its gain tinted with the hour colour), marker = tinted too.")]
        [SerializeField] private TemplateSlot hourRow;
        [Tooltip("Seconds between one row's bar and the next.")]
        [SerializeField, Min(0f)] private float rowDelay = 0.08f;

        [Header("The rest")]
        [Tooltip("\"9 wolves · depth 2\" (depth only when ≥ 1): the chain with the most wolves.")]
        [SerializeField] private TMP_Text biggestChain;
        [Tooltip("Points per stone thrown (the chains' score, without the dawn sweep).")]
        [SerializeField] private TMP_Text avgPerStone;
        [Tooltip("Stones thrown · stolen by wolves: \"26 · 2\".")]
        [SerializeField] private TMP_Text stones;

        private ScoreStyles _styles;
        private TemplateSlot _bestRow;   // the row holding the night's best throw: its points are painted rainbow
        private List<TemplateSlot> _dots = new List<TemplateSlot>();
        private List<TemplateSlot> _rows = new List<TemplateSlot>();

        /// <summary>Fills the panel from tonight's state (once the night has ended). Starts the bars after <paramref name="delay"/>.</summary>
        public void Show(float delay)
        {
            var s = session.State;
            if (_styles == null) _styles = new ScoreStyles(session.ScoreColours);
            int hours = session.ThresholdCount;
            bool dawn = s.Result == NightResult.Won;
            // The hour being played when the night ended (State.Hour moves on at a round's start; a loss has no round waiting).
            int reached = dawn ? hours : Mathf.Clamp(s.Hour, 1, hours);

            if (nightTitle != null) nightTitle.text = $"NIGHT {session.NightIndex + 1}";
            if (result != null)
            {
                result.text = dawn ? "DAWN" : "OUT OF STONES";
                result.color = dawn ? session.DawnColour : outOfStonesColour;
            }

            // One dot per hour. No extra dawn dot.
            if (_dots.Count == 0)
                _dots = TemplateList.Build(hourDot, null, hours);

            for (int i = 0; i < _dots.Count; i++)
            {
                int hour = i + 1;

                _dots[i].SetLabel($"{hour}");
                _dots[i].Tint(session.HourColour(hour));
                _dots[i].SetAlpha(hour <= reached ? 1f : unreachedAlpha);
            }
            if (hoursReached != null) hoursReached.text = $"{reached} / {hours}";

            if (score != null)
                score.text = dawn ? Numbers.Thousands(s.Score)
                    : s.ThresholdsReached > 0 ? $"kept {Numbers.Thousands(s.BankedScore)} (hour {s.ThresholdsReached})" : "kept 0";

            if (_rows.Count == 0) _rows = TemplateList.Build(hourRow, null, reached);
            int best = s.BestThrowPoints;
            _bestRow = null;
            for (int i = 0; i < _rows.Count; i++)
            {
                int hour = i + 1;
                int points = hour - 1 < s.Hours.Count ? s.Hours[hour - 1].BestThrow : 0;
                var row = _rows[i];
                var colour = session.HourColour(hour);
                row.SetLabel($"H{hour}");
                if (row.Label != null) row.Label.color = colour;
                row.Tint(colour);
                row.SetDetail(points > 0 ? Numbers.Thousands(points) : "—");
                if (row.Bar != null)
                {
                    row.Bar.TintGain(colour);
                    row.Bar.Show(0f, best > 0 ? points / (float)best : 0f, false, delay + i * rowDelay);
                }
                // The night's best (the first hour holding it, like BestThrowPoints keeps the first on a tie).
                if (_bestRow == null && best > 0 && points == best) _bestRow = row;
            }

            if (biggestChain != null)
            {
                int robots = s.BiggestChainRobots;
                string wolves = robots == 1 ? "1 wolf" : $"{robots} wolves";
                biggestChain.text = robots <= 0 ? "—" : s.BiggestChainDepth >= 1 ? $"{wolves} · depth {s.BiggestChainDepth}" : wolves;
            }
            if (avgPerStone != null)
            {
                int chainScore = Mathf.Max(0, s.Score - s.SweepScore);
                avgPerStone.text = s.ThrowsUsed > 0 ? Numbers.Thousands(Mathf.RoundToInt(chainScore / (float)s.ThrowsUsed)) : "—";
            }
            if (stones != null) stones.text = $"{s.ThrowsUsed} · {s.StonesStolen}";
        }

        // The rainbow animates letter by letter: repainted every frame.
        private void LateUpdate()
        {
            if (_bestRow != null && _styles != null) TextColouring.Apply(_bestRow.Detail, _styles.Rainbow, 0f, 0f, 1f);
        }
    }
}
