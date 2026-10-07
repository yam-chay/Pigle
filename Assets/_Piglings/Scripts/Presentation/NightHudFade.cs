using Piglings.Simulation;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// Shows a piece of the night's HUD (the screen-space scoreboard) only while the night is on (M11): hidden in the day
    /// phase (barn room, tower, the boot), faded in when the night starts — as the camera rises (Show During Rise) or
    /// once it arrives — kept through the night's end, and faded out when the post-run comes in and while the camera goes
    /// down. A fast Retry boots straight into the night, so it starts shown there (no fade from nothing).
    /// Each fade waits its own delay first (Fade In Delay / Fade Out Delay), so the board doesn't pop in the moment the
    /// player leaves the barn, nor vanish the moment the post-run starts arriving. A change back before the delay is up
    /// cancels it.
    /// Put it on the object with the CanvasGroup it fades. This component owns that group's alpha: don't also list it in
    /// PostRunView ▸ Night Hud. Without a NightFlow (Night.unity) it's always shown. Reads only.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class NightHudFade : MonoBehaviour
    {
        [SerializeField] private NightFlow flow;
        [Tooltip("Fade in as the camera starts rising to the night (Start night). Off = only once the night begins.")]
        [SerializeField] private bool showDuringRise = true;
        [Tooltip("Keep it during the post-run (under its dim). Off = it fades out as the post-run comes in.")]
        [SerializeField] private bool showDuringPostRun;
        [Tooltip("Seconds to fade in or out. 0 = snap.")]
        [SerializeField, Min(0f)] private float fadeSeconds = 0.4f;
        [Tooltip("Seconds to wait before fading in (after Start night, or the night beginning).")]
        [SerializeField, Min(0f)] private float fadeInDelay = 0.8f;
        [Tooltip("Seconds to wait before fading out (after the post-run starts, or leaving the night).")]
        [SerializeField, Min(0f)] private float fadeOutDelay = 0.3f;

        private CanvasGroup _group;
        private bool _wanted;           // shown or hidden, as the flow says now
        private float _wantedSince;     // unscaled time it last changed: its delay counts from here

        private void Awake() => _group = GetComponent<CanvasGroup>();

        // Start, not Awake: the flow sets its first state in its own Start (a fast Retry is already in the night).
        private void Start()
        {
            _wanted = Shown();
            _wantedSince = Time.unscaledTime;
            Apply(_wanted ? 1f : 0f);
        }

        private void LateUpdate()
        {
            bool wanted = Shown();
            if (wanted != _wanted) { _wanted = wanted; _wantedSince = Time.unscaledTime; }
            float target = _wanted ? 1f : 0f;
            if (Mathf.Approximately(_group.alpha, target)) return;
            // Unscaled: the F1 panel's pause doesn't hold the HUD.
            if (Time.unscaledTime - _wantedSince < (_wanted ? fadeInDelay : fadeOutDelay)) return;
            Apply(fadeSeconds > 0f ? Mathf.MoveTowards(_group.alpha, target, Time.unscaledDeltaTime / fadeSeconds) : target);
        }

        private bool Shown()
        {
            if (flow == null) return true;
            switch (flow.State)
            {
                case FlowState.Rising: return showDuringRise;
                case FlowState.Night:
                case FlowState.Settling: return true;
                case FlowState.PostRun: return showDuringPostRun;
                default: return false;   // Boot, BarnRoom, Tower, Descending, Leaving
            }
        }

        // Hidden = out of the way: no clicks caught.
        private void Apply(float alpha)
        {
            _group.alpha = alpha;
            bool visible = alpha > 0.01f;
            _group.blocksRaycasts = visible;
            _group.interactable = visible;
        }
    }
}
