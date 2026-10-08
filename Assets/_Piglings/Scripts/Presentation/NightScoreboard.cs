using System.Collections.Generic;
using Piglings.Events;
using Piglings.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Piglings.Presentation
{
    /// <summary>
    /// The night's chalkboard (M10.D, PROTOTYPE_V2.md §6): a world-space board on a post, left of the tower. Yam lays the
    /// board out once (Canvas, board_frame, the texts, one throw row); this fills it from the night:
    ///  - "HOUR n · ×m" in the hour's palette colour;
    ///  - the score against the next threshold, with a fill bar (ui_pill) through the current hour, in its colour;
    ///  - "hour n gap: a → b";
    ///  - LAST THROWS (M10.S): the chains as they happen. A live chain's row ticks "SCORE × MULT × H2"; when it closes the
    ///    row keeps that label and reveals its result next to it ("+360"), staying as one of the last throws. Newest on top; older rows step down,
    ///    shrink and dim; at most Row Brightness's length show. Misses (no wolf) leave no row.
    ///  - BEST TONIGHT: the best throw's breakdown ("84 × 3 × H2 =") and its result.
    /// Colours: the Visuals asset's quality bands (cream → amber → orange → red → magenta pulse → rainbow) by the
    /// throw's quality (its worth ÷ its hour's gap) — a live row by its worth so far.
    /// The SCORE NUMBER, the hour BAR and the gap line all move only when a chain closes (M11.T6) — together with the chain's
    /// popup above the pig, to the new total with its result: the bar glides there, the number climbs. During a chain they
    /// hold still (the live row ticks instead). The Rules still count the raw score live (thresholds, the balance log); this
    /// is only what the board shows.
    /// Every field is optional. Reads only.
    /// </summary>
    public sealed class NightScoreboard : MonoBehaviour
    {
        [SerializeField] private NightSession session;

        [Header("The hour")]
        [Tooltip("\"HOUR 3 · ×2\", in the hour's colour. \"DAWN\" once the last threshold is crossed.")]
        [SerializeField] private TMP_Text hourText;
        [Tooltip("\"1250 / 1500\": the score and the next threshold (the score alone after dawn).")]
        [SerializeField] private TMP_Text scoreText;
        [Tooltip("The fill bar (a ui_pill Image, 9-sliced). Its width follows the progress through the hour: anchor Min X stays, " +
                 "Max X goes 0 → 1 — set it up stretched inside its track. Tinted with the hour's colour.")]
        [SerializeField] private Graphic fill;
        [Tooltip("\"hour 3 gap: 500 → 1000\".")]
        [SerializeField] private TMP_Text gapText;
        [Tooltip("How long the bar takes to glide to a new score (about; 0 = snap). At a threshold it fills up first, then " +
                 "starts the new hour from empty — it never runs backwards.")]
        [SerializeField, Min(0f)] private float barEaseSeconds = 0.35f;
        [Tooltip("M10.S: the score number changes only when a chain closes, climbing to the new total (its result included) in " +
                 "about this many seconds. 0 = snap.")]
        [SerializeField, Min(0f)] private float scoreClimbSeconds = 0.4f;

        [Header("Throws")]
        [Tooltip("One LAST THROWS row, laid out once where the NEWEST row sits (label = the live \"SCORE × MULT × H\", detail = " +
                 "the result \"+360\", marker = the pill). Cloned per chain and placed by code — its parent must NOT have a " +
                 "Layout Group.")]
        [SerializeField] private TemplateSlot throwRow;
        [Tooltip("Rows shown, newest first, and how bright each is (1 = full). Its length = how many show (3).")]
        [SerializeField] private float[] rowBrightness = { 1f, 0.65f, 0.4f };
        [Tooltip("Each older row sits this far from the one above (canvas units; negative y = down).")]
        [SerializeField] private Vector2 rowStep = new Vector2(0f, -40f);
        [Tooltip("Each older row is this much smaller than the one above.")]
        [SerializeField, Range(0.3f, 1f)] private float olderScale = 0.85f;
        [Tooltip("About how many seconds a row takes to glide to its new place (smaller = snappier).")]
        [FormerlySerializedAs("rowGlide")]
        [SerializeField, Min(0.01f)] private float rowGlideSeconds = 0.2f;
        [Tooltip("The BEST TONIGHT row (not cloned): label = the breakdown \"84 × 3 × H2 =\", detail = the result, marker.")]
        [SerializeField] private TemplateSlot best;

        // One row per chain: live while it falls, then its result.
        private sealed class Row
        {
            public TemplateSlot Slot;
            public CanvasGroup Group;
            public bool Live = true;
            public int Score, Total, Hour;
            public float Mult = 1f, HourMultiplier = 1f;
            public int Worth => Live ? Mathf.RoundToInt(Score * Mult * HourMultiplier) : Total;
        }

        private ScoreStyles _styles;
        private readonly Dictionary<GameId, Row> _byChain = new Dictionary<GameId, Row>();
        private readonly List<Row> _rows = new List<Row>();   // newest first
        private int _bestTotal, _bestHour, _bestScore;
        private float _bestMult, _bestHourMult = 1f;
        private Vector2 _rowTop;
        private Vector3 _rowScale;

        // What the texts show now, so they're only rebuilt when something changed (TMP rebuilds are not free).
        private int _shownScore = -1, _shownHour = -1, _shownReached = -1;
        private float _climbShown, _climbTarget, _climbRate;
        private int _climbDrawn = -1;
        private int _scoreTo = -1, _toDrawn = -2;   // the next threshold the score line shows (-1 = dawn: none)
        private bool _shownDawn;

        // The bar glides: where the score is (target) vs. what's drawn (shown), each with the hour it belongs to.
        private float _barTarget, _barShown, _barVelocity;
        private int _targetSegment = 1, _shownSegment = 1;

        private void Start()
        {
            _styles = new ScoreStyles(session.Visuals);
            if (throwRow != null)
            {
                _rowTop = throwRow.GetComponent<RectTransform>().anchoredPosition;
                _rowScale = throwRow.transform.localScale;
                throwRow.gameObject.SetActive(false);
            }
            session.Bus.Subscribe<ChainGained>(OnChainGained);
            session.Bus.Subscribe<ChainScored>(OnChainScored);
            session.Bus.Subscribe<NightEnded>(OnNightEnded);
            ShowBest();
        }

        private void OnDestroy()
        {
            if (session == null || session.Bus == null) return;
            session.Bus.Unsubscribe<ChainGained>(OnChainGained);
            session.Bus.Unsubscribe<ChainScored>(OnChainScored);
            session.Bus.Unsubscribe<NightEnded>(OnNightEnded);
        }

        // A chain grows: its live row ticks "SCORE × MULT × H" (a new chain gets a new row, on top).
        private void OnChainGained(ChainGained e)
        {
            if (throwRow == null) return;
            if (!_byChain.TryGetValue(e.Chain.Id, out var row))
            {
                var slot = Instantiate(throwRow, throwRow.transform.parent);
                slot.name = $"{throwRow.name} (chain)";
                slot.gameObject.SetActive(true);
                var rect = slot.GetComponent<RectTransform>();
                rect.anchoredPosition = _rowTop;
                rect.localScale = _rowScale;
                // A CanvasGroup dims the whole row (texts, marker) without touching the colours the code sets.
                if (!slot.TryGetComponent(out CanvasGroup group)) group = slot.gameObject.AddComponent<CanvasGroup>();
                row = new Row { Slot = slot, Group = group, Hour = e.Hour };
                _byChain[e.Chain.Id] = row;
                _rows.Insert(0, row);
            }
            row.Score = e.Score; row.Mult = e.Mult; row.HourMultiplier = e.HourMultiplier;
            row.Slot.SetLabel(UiText.Fill(session.Texts.throwRow, ("score", Numbers.Thousands(e.Score)), ("mult", Numbers.Mult(e.Mult)),
                                          ("hour", e.Hour.ToString()), ("hourMult", Numbers.Mult(e.HourMultiplier))));
            row.Slot.SetDetail("");
        }

        // The chain closes: its row resolves into the result and stays; a miss leaves no row. The number moves now.
        private void OnChainScored(ChainScored e)
        {
            SetScoreTarget();
            if (!_byChain.TryGetValue(e.Chain.Id, out var row)) return;
            _byChain.Remove(e.Chain.Id);
            if (e.RobotsDropped <= 0) { _rows.Remove(row); Destroy(row.Slot.gameObject); return; }
            row.Live = false;
            row.Total = e.Total;
            row.Slot.SetDetail(UiText.Fill(session.Texts.throwResult, ("total", Numbers.Thousands(e.Total))));
            // Same rule as NightState.BestThrowPoints (most points, the first one keeps it on a tie).
            if (e.Total > _bestTotal)
            {
                _bestTotal = e.Total; _bestHour = e.Hour; _bestScore = e.Score; _bestMult = e.Mult; _bestHourMult = e.HourMultiplier;
                ShowBest();
            }
            Trim();
        }

        // The dawn sweep adds after the last chain: the number takes it too.
        private void OnNightEnded(NightEnded e) => SetScoreTarget();

        // What the board shows as the night's score (M11.T6): the real score as of the last chain close (or the night's end),
        // not the raw score growing mid-chain. The number, the bar and the gap line all read it.
        private int _boardScore;

        // Resolved rows past what shows go; live ones stay (hidden) until their chain closes.
        private void Trim()
        {
            for (int i = _rows.Count - 1; i >= rowBrightness.Length; i--)
            {
                if (_rows[i].Live) continue;
                Destroy(_rows[i].Slot.gameObject);
                _rows.RemoveAt(i);
            }
        }

        private void LateUpdate()
        {
            ShowHour();
            ClimbScore();
            GlideBar();
            FloatRows();
            // Quality colours can animate (pulse, rainbow): repainted every frame. Solid ones cost a colour set.
            // The label ("84 × 3 × H2") stays after the close; the result appears in the detail next to it.
            foreach (var row in _rows)
            {
                Paint(row.Slot, row.Slot.Label, row.Worth, row.Hour);
                if (!row.Live) Paint(null, row.Slot.Detail, row.Total, row.Hour);
            }
            if (_bestTotal > 0 && best != null)
            {
                Paint(best, best.Detail, _bestTotal, _bestHour);
                Paint(null, best.Label, _bestTotal, _bestHour);
            }
        }

        // Newest on top; each older one a step down, smaller and dimmer; past the shown count, invisible.
        private void FloatRows()
        {
            // Exponential glide: ~95% of the way in rowGlideSeconds.
            float k = 1f - Mathf.Exp(-3f * Time.deltaTime / rowGlideSeconds);
            for (int i = 0; i < _rows.Count; i++)
            {
                var rect = _rows[i].Slot.GetComponent<RectTransform>();
                rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, _rowTop + rowStep * i, k);
                rect.localScale = Vector3.Lerp(rect.localScale, _rowScale * Mathf.Pow(olderScale, i), k);
                float alpha = i < rowBrightness.Length ? rowBrightness[i] : 0f;
                _rows[i].Group.alpha = Mathf.Lerp(_rows[i].Group.alpha, alpha, k);
            }
        }

        private void ShowHour()
        {
            var s = session.State;
            int count = session.ThresholdCount;
            // Thresholds the board's score has crossed (not the Rules' raw count: that one moves mid-chain).
            int reached = 0;
            while (reached < count && _boardScore >= session.ThresholdAt(reached + 1)) reached++;
            bool dawn = count > 0 && reached >= count;
            if (_boardScore == _shownScore && s.Hour == _shownHour && reached == _shownReached && dawn == _shownDawn) return;
            _shownScore = _boardScore; _shownHour = s.Hour; _shownReached = reached; _shownDawn = dawn;

            // The hour the score is climbing through: the one after the last threshold crossed. (State.Hour only moves
            // on when that threshold's placement round starts — the header follows it, the bar doesn't wait.)
            int segment = Mathf.Min(reached + 1, count);
            int from = session.ThresholdAt(segment - 1);
            int to = session.ThresholdAt(segment);

            if (hourText != null)
            {
                int hour = Mathf.Min(s.Hour, count);
                hourText.text = dawn ? session.Texts.dawnTitle
                    : UiText.Fill(session.Texts.hourTitle, ("hour", hour.ToString()), ("mult", Numbers.Mult(session.HourMultiplierAt(hour))));
                hourText.color = dawn ? session.DawnColour : session.HourColour(hour);
            }
            _scoreTo = dawn ? -1 : to;
            if (gapText != null)
                gapText.text = dawn ? "" : UiText.Fill(session.Texts.hourGap, ("hour", segment.ToString()), ("from", from.ToString()), ("to", to.ToString()));
            // The bar's target; GlideBar moves it there.
            _barTarget = dawn ? 1f : Mathf.Clamp01((_boardScore - from) / (float)Mathf.Max(1, to - from));
            _targetSegment = segment;
        }

        // The number's new total: the night's score now (a chain's result included), climbed to at a rate set here — so a big
        // jump takes as long as a small one.
        private void SetScoreTarget()
        {
            int score = session.State.Score;
            _boardScore = score;
            _climbRate = scoreClimbSeconds > 0f ? Mathf.Max(1f, Mathf.Abs(score - _climbShown) / scoreClimbSeconds) : float.MaxValue;
            _climbTarget = score;
        }

        private void ClimbScore()
        {
            if (scoreText == null) return;
            _climbShown = Mathf.MoveTowards(_climbShown, _climbTarget, _climbRate * Time.deltaTime);
            int shown = Mathf.RoundToInt(_climbShown);
            if (shown == _climbDrawn && _scoreTo == _toDrawn) return;
            _climbDrawn = shown; _toDrawn = _scoreTo;
            scoreText.text = _scoreTo < 0
                ? UiText.Fill(session.Texts.scoreAtDawn, ("score", shown.ToString()))
                : UiText.Fill(session.Texts.scoreLine, ("score", shown.ToString()), ("target", _scoreTo.ToString()));
        }

        // Eases the drawn fill toward the score. A new hour: finish filling the old one first, then start the new one
        // from empty in its colour — so a threshold reads as "full!" and never as the bar dropping.
        private void GlideBar()
        {
            if (fill == null) return;
            bool newHour = _targetSegment > _shownSegment;
            float goal = newHour ? 1f : _barTarget;
            _barShown = barEaseSeconds > 0f ? Mathf.SmoothDamp(_barShown, goal, ref _barVelocity, barEaseSeconds) : goal;
            if (newHour && _barShown >= 0.995f)
            {
                _shownSegment = _targetSegment;
                _barShown = 0f;
                _barVelocity = 0f;
            }
            else if (_targetSegment < _shownSegment) _shownSegment = _targetSegment;   // never expected; just follow

            var rect = fill.rectTransform;
            var max = rect.anchorMax;
            max.x = Mathf.Max(rect.anchorMin.x, _barShown);
            rect.anchorMax = max;
            fill.color = session.HourColour(_shownSegment);
        }

        private void ShowBest()
        {
            if (best == null) return;
            var texts = session.Texts;
            best.SetLabel(_bestTotal > 0
                ? UiText.Fill(texts.bestRow, ("score", Numbers.Thousands(_bestScore)), ("mult", Numbers.Mult(_bestMult)),
                              ("hour", _bestHour.ToString()), ("hourMult", Numbers.Mult(_bestHourMult)))
                : texts.nothing);
            best.SetDetail(_bestTotal > 0 ? UiText.Fill(texts.bestResult, ("total", Numbers.Thousands(_bestTotal))) : "");
        }

        // A score's look from the Visuals asset: its quality band (points ÷ its hour's gap) — solid, pulse or rainbow.
        // The marker follows the same style (pulsing, cycling the rainbow), not the band's flat colour — the rainbow band's
        // flat colour is white, which made the best rows' dots white.
        private void Paint(TemplateSlot slot, TMPro.TMP_Text text, int points, int hour)
        {
            var style = _styles.ForQuality(session.ThrowQuality(points, hour));
            TextColouring.Apply(text, style, 0f, 0f, 1f);
            if (slot != null) slot.Tint(TextColouring.Sample(style, 0f, 0f));
        }
    }
}
