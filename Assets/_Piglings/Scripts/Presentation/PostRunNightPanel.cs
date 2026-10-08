using System.Collections.Generic;
using Piglings.Events;
using Piglings.Simulation;
using TMPro;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// The post-run's THE NIGHT panel (M10.E, PROTOTYPE_V2.md ▸ PR E; mockup Docs/Mockups/post_run_v5.png): "NIGHT n"; the
    /// result (DAWN in the Visuals' gold, or OUT OF STONES); a dot per hour of the night in its colour, faded past what was
    /// reached; "6 / 6" (hours reached / hours, in the last reached hour's colour — gold on a dawn); the score
    /// ("Total Score: X" — what the night keeps; the hours show in the dots); numbers with thousands separators; BEST THROW EACH HOUR — a row per hour reached: "Hn" in its colour, a bar
    /// against the night's best throw, the points coloured by their quality band (points ÷ that hour's gap — the rule every score
    /// follows: solid, pulse or rainbow); the biggest chain, avg per stone, stones
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
        [Tooltip("\"Total Score: 48,545\" — what the night keeps (the hours reached show above, in the dots).")]
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
        // Each hour row's points and its style (its quality band): painted every frame, so pulses and rainbows animate.
        private readonly List<(TMP_Text text, PopupColor style)> _painted = new List<(TMP_Text, PopupColor)>();
        private List<TemplateSlot> _dots = new List<TemplateSlot>();
        private List<TemplateSlot> _rows = new List<TemplateSlot>();

        /// <summary>Fills the panel from tonight's state (once the night has ended). Starts the bars after <paramref name="delay"/>.</summary>
        public void Show(float delay)
        {
            var s = session.State;
            if (_styles == null) _styles = new ScoreStyles(session.Visuals);
            int hours = session.ThresholdCount;
            bool dawn = s.Result == NightResult.Won;
            // The hour being played when the night ended (State.Hour moves on at a round's start; a loss has no round waiting).
            int reached = dawn ? hours : Mathf.Clamp(s.Hour, 1, hours);

            var texts = session.Texts;
            if (nightTitle != null) nightTitle.text = UiText.Fill(texts.nightTitle, ("night", (session.NightIndex + 1).ToString()));
            if (result != null)
            {
                result.text = dawn ? texts.resultDawn : texts.resultOutOfStones;
                result.color = dawn ? session.DawnColour : outOfStonesColour;
            }

            // One dot per hour. No extra dawn dot.
            if (_dots.Count == 0)
                _dots = TemplateList.Build(hourDot, null, hours);

            for (int i = 0; i < _dots.Count; i++)
            {
                int hour = i + 1;

                _dots[i].SetLabel(UiText.Fill(texts.hourDot, ("hour", hour.ToString())));
                _dots[i].Tint(session.HourColour(hour));
                _dots[i].SetAlpha(hour <= reached ? 1f : unreachedAlpha);
            }
            if (hoursReached != null)
            {
                hoursReached.text = UiText.Fill(texts.hoursReached, ("reached", reached.ToString()), ("hours", hours.ToString()));
                // In the night's own colours: the last hour reached, or the gold on a dawn (like DAWN on the board).
                hoursReached.color = dawn ? session.DawnColour : session.HourColour(reached);
            }

            if (score != null)
                // The night's real total (Yam, M11): what was scored, a loss included — not the last threshold the bank keeps.
                // {banked} is there for a template that wants to show the bank too.
                score.text = UiText.Fill(texts.totalScore, ("score", Numbers.Thousands(s.Score)), ("banked", Numbers.Thousands(s.BankedScore)));

            if (_rows.Count == 0) _rows = TemplateList.Build(hourRow, null, reached);
            int best = s.BestThrowPoints;
            _painted.Clear();
            for (int i = 0; i < _rows.Count; i++)
            {
                int hour = i + 1;
                int points = hour - 1 < s.Hours.Count ? s.Hours[hour - 1].BestThrow : 0;
                var row = _rows[i];
                var colour = session.HourColour(hour);
                row.SetLabel(UiText.Fill(texts.hourRow, ("hour", hour.ToString())));
                if (row.Label != null) row.Label.color = colour;
                row.Tint(colour);
                row.SetDetail(points > 0 ? Numbers.Thousands(points) : texts.nothing);
                if (row.Bar != null)
                {
                    row.Bar.TintGain(colour);
                    row.Bar.Show(0f, best > 0 ? points / (float)best : 0f, false, delay + i * rowDelay);
                }
                // The points by the rule every score follows: the throw's quality against ITS hour's gap (what that hour
                // needed), not the night's total — so any hour can be a rainbow. No special case for the night's best.
                if (points > 0 && row.Detail != null) _painted.Add((row.Detail, _styles.ForQuality(session.ThrowQuality(points, hour))));
            }

            if (biggestChain != null)
            {
                int robots = s.BiggestChainRobots;
                string wolves = UiText.Wolves(texts, robots);
                biggestChain.text = robots <= 0 ? texts.nothing
                    : s.BiggestChainDepth >= 1 ? UiText.Fill(texts.biggestChain, ("wolves", wolves), ("depth", s.BiggestChainDepth.ToString()))
                    : wolves;
            }
            if (avgPerStone != null)
            {
                int chainScore = Mathf.Max(0, s.Score - s.SweepScore);
                avgPerStone.text = s.ThrowsUsed > 0 ? Numbers.Thousands(Mathf.RoundToInt(chainScore / (float)s.ThrowsUsed)) : texts.nothing;
            }
            if (stones != null) stones.text = UiText.Fill(texts.stones, ("thrown", s.ThrowsUsed.ToString()), ("stolen", s.StonesStolen.ToString()));
        }

        // Pulses and rainbows animate: repainted every frame.
        private void LateUpdate()
        {
            foreach (var (text, style) in _painted) TextColouring.Apply(text, style, 0f, 0f, 1f);
        }
    }
}
