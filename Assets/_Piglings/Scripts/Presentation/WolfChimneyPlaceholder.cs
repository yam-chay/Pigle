using Piglings.Simulation;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// A stand-in for the wolf's chimney climb on a loss (M11.T1), until there's a real sequence: when the flow says the
    /// wolf starts (NightFlow.WolfClimbStarts, after the loss beat), the Wolf object appears at Start and moves to End over
    /// Wolf Seconds, then hides (or stays, Hide At End off). Any sprite will do — what matters is the beat it gives the loss.
    /// Hidden the rest of the time. Reads only: the flow lets the robots go when its time is up, not this.
    /// </summary>
    public sealed class WolfChimneyPlaceholder : MonoBehaviour
    {
        [SerializeField] private NightFlow flow;
        [Tooltip("The object shown and moved (a SpriteRenderer with any wolf sprite) — a child, not this object: it's switched off " +
                 "until the wolf's climb.")]
        [SerializeField] private Transform wolf;
        [Tooltip("Where the climb starts (e.g. the barn's side, below the roof).")]
        [SerializeField] private Transform start;
        [Tooltip("Where it ends (the chimney).")]
        [SerializeField] private Transform end;
        [Tooltip("Hide the wolf when the climb is over (he's gone down the chimney).")]
        [SerializeField] private bool hideAtEnd = true;

        private float _seconds;
        private float _elapsed = -1f;   // -1 = not climbing

        private void Awake()
        {
            if (wolf != null) wolf.gameObject.SetActive(false);
        }

        private void OnEnable() { if (flow != null) flow.WolfClimbStarts += OnWolfClimbStarts; }
        private void OnDisable() { if (flow != null) flow.WolfClimbStarts -= OnWolfClimbStarts; }

        private void OnWolfClimbStarts(float seconds)
        {
            if (wolf == null || start == null || end == null) return;
            _seconds = seconds;
            _elapsed = 0f;
            wolf.position = start.position;
            wolf.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (_elapsed < 0f) return;
            _elapsed += Time.deltaTime;
            float t = _seconds > 0f ? Mathf.Clamp01(_elapsed / _seconds) : 1f;
            wolf.position = Vector3.Lerp(start.position, end.position, Mathf.SmoothStep(0f, 1f, t));
            if (t < 1f) return;
            _elapsed = -1f;
            if (hideAtEnd) wolf.gameObject.SetActive(false);
        }
    }
}
