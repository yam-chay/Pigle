using TMPro;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// One floating score text: pops in, drifts up, fades, destroys itself. Pure visuals.
    /// Optionally shows a first text ("30 ×2") that resolves into a second one ("+60") with a
    /// second pop — so the player sees WHY a robot was worth what it was, then the result.
    /// Lives on a prefab with a world-space TextMeshPro (not the UI one), spawned by ScorePopupSpawner.
    /// </summary>
    public sealed class ScorePopup : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;

        [Tooltip("Seconds the final text stays, from appearing (or resolving) to gone.")]
        [SerializeField, Min(0.1f)] private float lifetime = 0.9f;

        [Tooltip("Seconds the first text (\"30 ×2\") shows before it resolves into the total. " +
                 "Only used when there is something to resolve.")]
        [SerializeField, Min(0f)] private float resolveDelay = 0.35f;

        [Tooltip("World units it drifts up over its whole life.")]
        [SerializeField] private float rise = 0.5f;

        [Tooltip("Overshoot on appear (and on resolve): starts this much bigger and settles. Makes it read as a hit, not a label.")]
        [SerializeField, Min(0f)] private float pop = 0.35f;

        private Vector3 _start;
        private Vector3 _baseScale;
        private Color _color;
        private float _age;
        private string _resolvedText;   // null once resolved, or when there was nothing to resolve
        private float _resolveAt;       // when the final text appears (0 without a first text)
        private float _popAt;           // when the latest pop started
        private float _totalLife;

        private void Reset() => label = GetComponent<TMP_Text>();

        /// <param name="resolvedText">If set, <paramref name="text"/> shows first and turns into this after resolveDelay.</param>
        public void Show(string text, Color color, float scale, string resolvedText = null)
        {
            label.text = text;
            _color = color;
            label.color = color;
            _start = transform.position;
            _baseScale = Vector3.one * scale;
            transform.localScale = _baseScale * (1f + pop);

            _resolvedText = resolvedText;
            _resolveAt = resolvedText != null ? resolveDelay : 0f;
            _totalLife = _resolveAt + lifetime;
            _popAt = 0f;
            _age = 0f;
        }

        private void Update()
        {
            _age += Time.deltaTime;

            if (_resolvedText != null && _age >= _resolveAt)
            {
                label.text = _resolvedText;
                _resolvedText = null;
                _popAt = _age;   // pop again, so the total lands like a second hit
            }

            // Ease-out rise over the whole life: fast at first, then hangs, so the number is readable.
            float t = Mathf.Clamp01(_age / _totalLife);
            float eased = 1f - (1f - t) * (1f - t);
            transform.position = _start + Vector3.up * (rise * eased);

            // Each pop settles over the first 15% of the final text's lifetime.
            float settle = Mathf.Clamp01((_age - _popAt) / (0.15f * lifetime));
            transform.localScale = _baseScale * (1f + pop * (1f - settle));

            // Full alpha until halfway through the final text's life, then fade —
            // fading from frame one makes it unreadable, and the first text never fades.
            float f = (_age - _resolveAt) / lifetime;
            float alpha = f < 0.5f ? 1f : 1f - (f - 0.5f) / 0.5f;
            label.color = new Color(_color.r, _color.g, _color.b, _color.a * alpha);

            if (_age >= _totalLife) Destroy(gameObject);
        }
    }
}
