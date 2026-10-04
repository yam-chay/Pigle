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
    ///  - LAST THROWS: the last 3 closed chains, newest on top and brightest ("9 wolves · depth 2   +620"; depth only
    ///    when ≥ 1; misses not listed), the points and marker coloured by the throw's quality;
    ///  - BEST TONIGHT, the same way.
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

        [Header("Throws")]
        [Tooltip("One LAST THROWS row, laid out once (label = \"9 wolves · depth 2\", detail = \"+620\", marker = the pill). " +
                 "Cloned under the same parent, which needs a Vertical Layout Group.")]
        [SerializeField] private TemplateSlot throwRow;
        [Tooltip("Rows shown, newest first, and how bright each is (1 = full).")]
        [SerializeField] private float[] rowBrightness = { 1f, 0.65f, 0.4f };
        [Tooltip("The BEST TONIGHT row (not cloned): label, points, marker — like a throw row.")]
        [SerializeField] private TemplateSlot best;

        private struct Throw
        {
            public int Robots, Depth, Points, Hour;
        }

        private ScoreStyles _styles;
        private readonly List<Throw> _last = new List<Throw>();   // newest first
        private Throw _best;
        private List<TemplateSlot> _rows = new List<TemplateSlot>();
        private readonly List<CanvasGroup> _rowGroups = new List<CanvasGroup>();

        // What the texts show now, so they're only rebuilt when something changed (TMP rebuilds are not free).
        private int _shownScore = -1, _shownHour = -1, _shownReached = -1;
        private bool _shownDawn;

        private void Start()
        {
            _styles = new ScoreStyles(session.ScoreColours);
            _rows = TemplateList.Build(throwRow, null, rowBrightness.Length);
            foreach (var row in _rows)
            {
                // A CanvasGroup dims the whole row (texts, marker) without touching the colours the code sets.
                if (!row.TryGetComponent(out CanvasGroup group)) group = row.gameObject.AddComponent<CanvasGroup>();
                _rowGroups.Add(group);
            }
            session.Bus.Subscribe<ChainScored>(OnChainScored);
            ShowThrows();
            ShowBest();
        }

        private void OnDestroy()
        {
            if (session != null && session.Bus != null) session.Bus.Unsubscribe<ChainScored>(OnChainScored);
        }

        // Misses (no wolf dropped) aren't listed: the board is about what worked.
        private void OnChainScored(ChainScored e)
        {
            if (e.RobotsDropped <= 0) return;
            var t = new Throw { Robots = e.RobotsDropped, Depth = e.MaxDepth, Points = e.Total, Hour = e.Hour };
            _last.Insert(0, t);
            if (_last.Count > _rows.Count) _last.RemoveAt(_last.Count - 1);
            ShowThrows();
            // Same rule as NightState.BestThrowPoints (most points, the first one keeps it on a tie); kept here for its wolves and depth.
            if (t.Points > _best.Points) { _best = t; ShowBest(); }
        }

        private void LateUpdate()
        {
            ShowHour();
            // Quality colours can animate (pulse, rainbow): repainted every frame. Solid ones cost a colour set.
            for (int i = 0; i < _rows.Count && i < _last.Count; i++) PaintThrow(_rows[i], _last[i]);
            if (_best.Points > 0) PaintThrow(best, _best);
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
                hourText.color = session.HourColour(s.Dawn ? count : hour);
            }
            if (scoreText != null) scoreText.text = s.Dawn ? $"{s.Score}" : $"{s.Score} / {to}";
            if (gapText != null) gapText.text = s.Dawn ? "" : $"hour {segment} gap: {from} → {to}";
            if (fill != null)
            {
                float progress = s.Dawn ? 1f : Mathf.Clamp01((s.Score - from) / (float)Mathf.Max(1, to - from));
                var rect = fill.rectTransform;
                var max = rect.anchorMax;
                max.x = Mathf.Max(rect.anchorMin.x, progress);
                rect.anchorMax = max;
                fill.color = session.HourColour(segment);
            }
        }

        // Texts change only when a throw comes in; colours are painted every frame (LateUpdate).
        private void ShowThrows()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                bool used = i < _last.Count;
                if (_rows[i].gameObject.activeSelf != used) _rows[i].gameObject.SetActive(used);
                if (!used) continue;
                FillThrow(_rows[i], _last[i]);
                _rowGroups[i].alpha = i < rowBrightness.Length ? rowBrightness[i] : 1f;
            }
        }

        private void ShowBest()
        {
            if (best == null) return;
            if (_best.Points <= 0)
            {
                best.SetLabel("—");
                best.SetDetail("");
                return;
            }
            FillThrow(best, _best);
        }

        private static void FillThrow(TemplateSlot slot, Throw t)
        {
            string wolves = t.Robots == 1 ? "1 wolf" : $"{t.Robots} wolves";
            slot.SetLabel(t.Depth >= 1 ? $"{wolves} · depth {t.Depth}" : wolves);
            slot.SetDetail($"+{t.Points}");
        }

        private void PaintThrow(TemplateSlot slot, Throw t)
        {
            if (slot == null) return;
            float quality = session.ThrowQuality(t.Points, t.Hour);
            TextColouring.Apply(slot.Detail, _styles.ForQuality(quality), 0f, 0f, 1f);
            slot.Tint(_styles.QualityColour(quality));
        }
    }
}
