using System.Collections.Generic;
using Piglings.Events;
using Piglings.Simulation;
using TMPro;
using UnityEngine;
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
    ///    row resolves into its result ("+360", in the hour's palette colour with the quality style) and stays as one of
    ///    the last throws. Newest on top; older rows step down, shrink and dim; at most Row Brightness's length show. Misses
    ///    (no wolf) leave no row.
    ///  - BEST TONIGHT: the best result, the same way.
    /// The SCORE NUMBER moves only when a chain closes — straight to the new total, result included, with a climb; the hour
    /// BAR follows the raw score live and takes the remainder at the close (thresholds are checked on the real totals).
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
        [Tooltip("How fast rows glide to their place (higher = snappier).")]
        [SerializeField, Min(0.1f)] private float rowGlide = 12f;
        [Tooltip("The BEST TONIGHT row (not cloned): detail = the best result, marker — like a resolved throw row.")]
        [SerializeField] private TemplateSlot best;

        // One row per chain: live while it falls, then its result.
        private sealed class Row
        {
            public TemplateSlot Slot;
            public CanvasGroup Group;
            public bool Live = true;
            public int Total, Hour;
        }

        private ScoreStyles _styles;
        private readonly Dictionary<GameId, Row> _byChain = new Dictionary<GameId, Row>();
        private readonly List<Row> _rows = new List<Row>();   // newest first
        private int _bestTotal, _bestHour;
        private Vector2 _rowTop;
        private Vector3 _rowScale;

        // What the texts show now, so they're only rebuilt when something changed (TMP rebuilds are not free).
        private int _shownScore = -1, _shownHour = -1, _shownReached = -1;
        private float _climbShown, _climbTarget, _climbRate;
        private int _climbDrawn = -1;
        private string _scoreSuffix = "", _suffixDrawn;
        private bool _shownDawn;

        // The bar glides: where the score is (target) vs. what's drawn (shown), each with the hour it belongs to.
        private float _barTarget, _barShown, _barVelocity;
        private int _targetSegment = 1, _shownSegment = 1;

        private void Start()
        {
            _styles = new ScoreStyles(session.ScoreColours);
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
            row.Slot.SetLabel($"{Numbers.Thousands(e.Score)} × {e.Mult:0.##} × H{e.Hour}");
            row.Slot.SetDetail("");
            var colour = session.HourColour(e.Hour);
            if (row.Slot.Label != null) row.Slot.Label.color = colour;
            row.Slot.Tint(colour);
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
            row.Slot.SetLabel("");
            row.Slot.SetDetail($"+{Numbers.Thousands(e.Total)}");
            // Same rule as NightState.BestThrowPoints (most points, the first one keeps it on a tie).
            if (e.Total > _bestTotal) { _bestTotal = e.Total; _bestHour = e.Hour; ShowBest(); }
            Trim();
        }

        // The dawn sweep adds after the last chain: the number takes it too.
        private void OnNightEnded(NightEnded e) => SetScoreTarget();

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
            foreach (var row in _rows) if (!row.Live) PaintResult(row.Slot, row.Total, row.Hour);
            if (_bestTotal > 0) PaintResult(best, _bestTotal, _bestHour);
        }

        // Newest on top; each older one a step down, smaller and dimmer; past the shown count, invisible.
        private void FloatRows()
        {
            float k = 1f - Mathf.Exp(-rowGlide * Time.deltaTime);
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
            if (s.Score == _shownScore && s.Hour == _shownHour && s.ThresholdsReached == _shownReached && s.Dawn == _shownDawn) return;
            _shownScore = s.Score; _shownHour = s.Hour; _shownReached = s.ThresholdsReached; _shownDawn = s.Dawn;

            int count = session.ThresholdCount;
            // The hour the score is climbing through: the one after the last threshold crossed. (State.Hour only moves
            // on when that threshold's placement round starts — the header follows it, the bar doesn't wait.)
            int segment = Mathf.Min(s.ThresholdsReached + 1, count);
            int from = session.ThresholdAt(segment - 1);
            int to = session.ThresholdAt(segment);

            if (hourText != null)
            {
                int hour = Mathf.Min(s.Hour, count);
                hourText.text = s.Dawn ? "DAWN" : $"HOUR {hour} · ×{session.HourMultiplierAt(hour):0.##}";
                hourText.color = s.Dawn ? session.DawnColour : session.HourColour(hour);
            }
            _scoreSuffix = s.Dawn ? "" : $" / {to}";
            if (gapText != null) gapText.text = s.Dawn ? "" : $"hour {segment} gap: {from} -> {to}";
            // The bar's target; GlideBar moves it there.
            _barTarget = s.Dawn ? 1f : Mathf.Clamp01((s.Score - from) / (float)Mathf.Max(1, to - from));
            _targetSegment = segment;
        }

        // The number's new total: the night's score now (a chain's result included), climbed to at a rate set here — so a big
        // jump takes as long as a small one.
        private void SetScoreTarget()
        {
            int score = session.State.Score;
            _climbRate = scoreClimbSeconds > 0f ? Mathf.Max(1f, Mathf.Abs(score - _climbShown) / scoreClimbSeconds) : float.MaxValue;
            _climbTarget = score;
        }

        private void ClimbScore()
        {
            if (scoreText == null) return;
            _climbShown = Mathf.MoveTowards(_climbShown, _climbTarget, _climbRate * Time.deltaTime);
            int shown = Mathf.RoundToInt(_climbShown);
            if (shown == _climbDrawn && _scoreSuffix == _suffixDrawn) return;
            _climbDrawn = shown; _suffixDrawn = _scoreSuffix;
            scoreText.text = $"{shown}{_scoreSuffix}";
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
            best.SetLabel(_bestTotal > 0 ? "" : "—");
            best.SetDetail(_bestTotal > 0 ? $"+{Numbers.Thousands(_bestTotal)}" : "");
        }

        // A result: the hour's palette colour, with the quality style for its size (pulse, rainbow).
        private void PaintResult(TemplateSlot slot, int total, int hour)
        {
            if (slot == null) return;
            var colour = session.HourColour(hour);
            TextColouring.Apply(slot.Detail, _styles.ForHour(colour, session.ThrowQuality(total, hour)), 0f, 0f, 1f);
            slot.Tint(colour);
        }
    }
}
