using Piglings.Events;
using Piglings.Simulation;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// Dims the screen during a placement round: fades a dark overlay sprite in, and out again after. Its sorting
    /// order decides what's dimmed — put it above the background and barn, below the Holds, the shelf and the pig.
    /// Reads only.
    /// </summary>
    public sealed class PlacementDim : MonoBehaviour
    {
        [SerializeField] private NightSession session;
        [Tooltip("A big dark sprite covering the camera view (e.g. a black Square scaled up).")]
        [SerializeField] private SpriteRenderer overlay;
        [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.45f;
        [SerializeField, Min(0.01f)] private float fadeSeconds = 0.2f;

        private float _alpha;

        private void Reset() => overlay = GetComponent<SpriteRenderer>();

        private void Update()
        {
            if (overlay == null) return;
            float target = session.State.Phase == NightPhase.PegPlacement ? dimAlpha : 0f;
            _alpha = Mathf.MoveTowards(_alpha, target, Time.deltaTime * Mathf.Max(dimAlpha, 0.01f) / fadeSeconds);
            var c = overlay.color;
            c.a = _alpha;
            overlay.color = c;
            overlay.enabled = _alpha > 0.001f;
        }
    }
}
