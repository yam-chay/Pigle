using UnityEngine;
using UnityEngine.UI;

namespace Piglings.Presentation
{
    /// <summary>
    /// One post-run bar (M10.E): what was there BEFORE tonight (a dim fill) and tonight's GAIN (a bright fill right after
    /// it), which fills in with an ease when the bar is shown. A bar tonight completed shows its READY tag once full.
    /// Yam lays it out once: a track, then two fills stretched inside it (anchors 0..1, offsets 0, pivot left) — the code
    /// moves their anchors' x only: Before from 0 to the start, Gain from the start toward the end. Every part is optional.
    /// Reads nothing: the panel that owns it says what to show (Show). Presentation only.
    /// </summary>
    public sealed class ProgressBarView : MonoBehaviour
    {
        [Tooltip("The dim fill: progress from before tonight.")]
        [SerializeField] private Graphic before;
        [Tooltip("The bright fill: tonight's gain, right after the dim one.")]
        [SerializeField] private Graphic gain;
        [Tooltip("Shown once the bar is full, when tonight completed it (READY).")]
        [SerializeField] private GameObject readyTag;
        [Tooltip("How long the gain takes to fill in.")]
        [SerializeField, Min(0f)] private float fillSeconds = 0.8f;

        private float _from, _to, _delay, _elapsed;
        private bool _ready, _playing;

        /// <summary>
        /// Shows the bar: <paramref name="from"/> (0..1) dim, then the gain fills to <paramref name="to"/> after
        /// <paramref name="delay"/> seconds. <paramref name="ready"/> = show the READY tag once it's full.
        /// </summary>
        public void Show(float from, float to, bool ready, float delay = 0f)
        {
            _from = Mathf.Clamp01(from);
            _to = Mathf.Max(_from, Mathf.Clamp01(to));
            _ready = ready;
            _delay = Mathf.Max(0f, delay);
            _elapsed = 0f;
            _playing = true;
            if (readyTag != null) readyTag.SetActive(false);
            Draw(_from);
        }

        /// <summary>The gain's colour (the hour rows tint it with the hour's colour).</summary>
        public void TintGain(Color colour) { if (gain != null) gain.color = colour; }

        private void Update()
        {
            if (!_playing) return;
            _elapsed += Time.deltaTime;
            float t = fillSeconds > 0f ? Mathf.Clamp01((_elapsed - _delay) / fillSeconds) : 1f;
            // Ease out: quick at first, settling into place — the gain reads as "landing".
            float eased = 1f - (1f - t) * (1f - t);
            Draw(Mathf.Lerp(_from, _to, eased));
            if (t < 1f) return;
            _playing = false;
            if (_ready && readyTag != null) readyTag.SetActive(true);
        }

        private void Draw(float end)
        {
            SetSpan(before, 0f, _from);
            SetSpan(gain, _from, end);
        }

        private static void SetSpan(Graphic fill, float min, float max)
        {
            if (fill == null) return;
            var rect = fill.rectTransform;
            rect.anchorMin = new Vector2(min, rect.anchorMin.y);
            rect.anchorMax = new Vector2(Mathf.Max(min, max), rect.anchorMax.y);
            // An empty span stays drawn as nothing (a 9-sliced pill would otherwise show its caps).
            fill.enabled = max - min > 0.001f;
        }
    }
}
