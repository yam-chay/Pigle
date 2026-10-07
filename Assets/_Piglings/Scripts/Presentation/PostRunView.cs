using Piglings.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Piglings.Presentation
{
    /// <summary>One post-run element sliding in: from Offset (canvas units, from where it's laid out) to its place, after Delay.</summary>
    [System.Serializable]
    public sealed class PostRunSlide
    {
        public RectTransform panel;
        [Tooltip("Where it starts, relative to where it's laid out (canvas units): (-900, 0) = from the left, (900, 0) = from the right, (0, -600) = from below.")]
        public Vector2 offset = new Vector2(-900f, 0f);
        [Tooltip("Seconds after the post-run starts before this one moves — stagger them so they arrive one after another.")]
        [Min(0f)] public float delay;
    }

    /// <summary>
    /// The post-run screen (M10.E, PROTOTYPE_V2.md ▸ PR E), over the Night frame: the scene dimmed (an overlay at Dim
    /// Alpha), THE NIGHT and ALL-TIME RECORDS on the left, PROGRESS on the right, two buttons (PostRunButtons). It fades in
    /// when the flow reaches PostRun — after the sweep has landed, and on a loss after the wolf's time — fills the panels
    /// once (the report is built then: tonight is banked by now), and fades out when a button sends the camera down to
    /// the doors. The camera never moves for it.
    /// Slides (M11): each listed panel (or button row) arrives from its Offset after its Delay, easing out over Slide
    /// Seconds — staggered, they come in one after another. They're placed from where Yam laid them out (read at start).
    /// Yam lays it out once (a screen-space Canvas: a CanvasGroup on the root, the dim overlay, the panel frames, template
    /// rows, the two buttons with a TMP label each). Every field but the flow and session is optional. Reads only — the
    /// buttons' clicks are PostRunButtons' (Simulation).
    /// </summary>
    public sealed class PostRunView : MonoBehaviour
    {
        [SerializeField] private NightSession session;
        [SerializeField] private NightFlow flow;

        [Header("Look")]
        [Tooltip("On the post-run's root: faded in / out as a whole (panels, buttons, the dim overlay).")]
        [SerializeField] private CanvasGroup group;
        [Tooltip("A full-screen Image behind the panels (black): the scene dimmed.")]
        [SerializeField] private Graphic dim;
        [Tooltip("How dark the scene gets behind the post-run (the overlay's alpha).")]
        [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.6f;
        [Tooltip("Seconds to fade in, and out when leaving.")]
        [SerializeField, Min(0f)] private float fadeSeconds = 0.35f;
        [Tooltip("Seconds after the fade-in starts before the bars fill in.")]
        [SerializeField, Min(0f)] private float barsDelay = 0.4f;

        [Header("The night's HUD (M10.S)")]
        [Tooltip("The live night HUD — the scoreboard (with its live throw rows), the tips: CanvasGroups dimmed while the post-run shows.")]
        [SerializeField] private CanvasGroup[] nightHud = new CanvasGroup[0];
        [Tooltip("The HUD's alpha while the post-run shows (1 = untouched).")]
        [SerializeField, Range(0f, 1f)] private float hudAlpha = 0.25f;

        [Header("Slides (M11)")]
        [Tooltip("Panels that slide in, each from its own side, after its own delay. Empty = everything just fades.")]
        [SerializeField] private PostRunSlide[] slides = new PostRunSlide[0];
        [Tooltip("Seconds each slide takes (eases out: fast, then settling).")]
        [SerializeField, Min(0.01f)] private float slideSeconds = 0.45f;

        [Header("Panels")]
        [SerializeField] private PostRunNightPanel nightPanel;
        [SerializeField] private PostRunRecordsPanel recordsPanel;
        [SerializeField] private PostRunProgressPanel progressPanel;

        [Header("Button labels (the slots are PostRunButtons')")]
        [SerializeField] private TMP_Text primaryLabel;
        [SerializeField] private TMP_Text secondaryLabel;
        [SerializeField] private string retryText = "Retry night";
        [SerializeField] private string toBarnText = "To the barn";
        [SerializeField] private string nextNightText = "Next night ▸";

        private bool _filled;
        private float _alpha;
        private float _shownAt = -1f;      // Time.time the post-run started showing; -1 = not yet
        private Vector2[] _homes;          // each slide's laid-out position

        private void Start()
        {
            if (dim != null)
            {
                var c = dim.color;
                dim.color = new Color(c.r, c.g, c.b, dimAlpha);
            }
            _homes = new Vector2[slides.Length];
            for (int i = 0; i < slides.Length; i++)
                if (slides[i] != null && slides[i].panel != null) _homes[i] = slides[i].panel.anchoredPosition;
            Apply(0f);
            PlaceSlides();
        }

        private void LateUpdate()
        {
            var state = flow.State;
            if (state == FlowState.PostRun && !_filled) Fill();
            if (state == FlowState.PostRun && _shownAt < 0f) _shownAt = Time.time;
            PlaceSlides();

            // In while the post-run is up; out once a button sends the camera down (or the scene reloads).
            float target = state == FlowState.PostRun ? 1f : 0f;
            if (!Mathf.Approximately(_alpha, target))
                Apply(fadeSeconds > 0f ? Mathf.MoveTowards(_alpha, target, Time.deltaTime / fadeSeconds) : target);
        }

        private void Fill()
        {
            _filled = true;
            if (nightPanel != null) nightPanel.Show(barsDelay);
            var report = session.BuildPostRunReport();
            if (recordsPanel != null) recordsPanel.Show(report.Records, session.Texts, barsDelay);
            if (progressPanel != null) progressPanel.Show(report, barsDelay);
            if (primaryLabel != null) primaryLabel.text = Label(flow.Primary);
            if (secondaryLabel != null) secondaryLabel.text = Label(flow.Secondary);
        }

        // Each slide eases from its offset to its place once its delay is up; before the post-run, it waits at its offset.
        // Leaving, they stay put and the whole screen fades out.
        private void PlaceSlides()
        {
            for (int i = 0; i < slides.Length; i++)
            {
                var slide = slides[i];
                if (slide == null || slide.panel == null) continue;
                float t = _shownAt < 0f ? 0f : Mathf.Clamp01((Time.time - _shownAt - slide.delay) / slideSeconds);
                float eased = 1f - (1f - t) * (1f - t) * (1f - t);   // ease out (cubic)
                slide.panel.anchoredPosition = _homes[i] + slide.offset * (1f - eased);
            }
        }

        private string Label(PostRunAction action)
        {
            switch (action)
            {
                case PostRunAction.Retry: return retryText;
                case PostRunAction.ToBarn: return toBarnText;
                case PostRunAction.NextNight: return nextNightText;
                default: return "";
            }
        }

        // Invisible = not in the way: no clicks caught while hidden. The night's HUD dims as the post-run comes in.
        private void Apply(float alpha)
        {
            _alpha = alpha;
            foreach (var hud in nightHud)
                if (hud != null) hud.alpha = Mathf.Lerp(1f, hudAlpha, alpha);
            if (group == null) return;
            group.alpha = alpha;
            group.blocksRaycasts = alpha > 0.01f;
            group.interactable = alpha > 0.99f;
        }
    }
}
