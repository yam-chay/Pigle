using TMPro;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// One floating score text: pops in, drifts up, fades, destroys itself. Pure visuals.
    /// Optionally shows a first text ("30 ×2") that resolves into a second one ("+60") with a
    /// second pop — so the player sees WHY a robot was worth what it was, then the result.
    ///
    /// Colour comes from a PopupColor style (solid / over lifetime / cycling / per-letter rainbow).
    /// Intensity (0..1, chosen by the spawner: deeper hits, bigger chains) makes it shake and stay longer,
    /// so big moments read as big without a separate effect.
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

        [Header("Big hits (scaled by intensity 0..1)")]
        [Tooltip("At full intensity the popup lives this much longer (0.8 = +80%).")]
        [SerializeField, Min(0f)] private float extraLifetime = 0.8f;
        [Tooltip("At full intensity the numbers shake by up to this many world units, calming down as they fade.")]
        [SerializeField, Min(0f)] private float shakeAmount = 0.06f;
        [Tooltip("Shake jitters per second.")]
        [SerializeField, Min(0f)] private float shakeSpeed = 25f;

        private Vector3 _start;
        private Vector3 _baseScale;
        private PopupColor _style;
        private float _key;             // the style's key (robots: how deep)
        private float _intensity;
        private float _life;            // this popup's final-text lifetime (lifetime stretched by intensity)
        private float _age;
        private string _resolvedText;   // null once resolved, or when there was nothing to resolve
        private float _resolveAt;       // when the final text appears (0 without a first text)
        private float _popAt;           // when the latest pop started
        private float _totalLife;
        private float _seed;            // so two popups don't shake in step

        private void Reset() => label = GetComponent<TMP_Text>();

        /// <param name="key">0..1 passed to the style (Solid picks its colour there; Cycle starts there).</param>
        /// <param name="intensity">0..1: how big this moment is. Longer life and more shake as it grows.</param>
        /// <param name="resolvedText">If set, <paramref name="text"/> shows first and turns into this after resolveDelay.</param>
        public void Show(string text, PopupColor style, float key, float scale, float intensity, string resolvedText = null)
        {
            label.text = text;
            _style = style;
            _key = key;
            _intensity = Mathf.Clamp01(intensity);
            _start = transform.position;
            _baseScale = Vector3.one * scale;
            transform.localScale = _baseScale * (1f + pop);

            _life = lifetime * (1f + extraLifetime * _intensity);
            _resolvedText = resolvedText;
            _resolveAt = resolvedText != null ? resolveDelay : 0f;
            _totalLife = _resolveAt + _life;
            _popAt = 0f;
            _age = 0f;
            _seed = Random.value * 100f;
            ApplyColor(1f);
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

            // Shake: smooth noise (not random jumps), strongest at the start, calming as it fades.
            float shake = shakeAmount * _intensity * (1f - t);
            var jitter = new Vector3(
                (Mathf.PerlinNoise(_seed, _age * shakeSpeed) - 0.5f) * 2f,
                (Mathf.PerlinNoise(_seed + 7f, _age * shakeSpeed) - 0.5f) * 2f, 0f) * shake;
            transform.position = _start + Vector3.up * (rise * eased) + jitter;

            // Each pop settles over the first 15% of the final text's lifetime.
            float settle = Mathf.Clamp01((_age - _popAt) / (0.15f * _life));
            transform.localScale = _baseScale * (1f + pop * (1f - settle));

            // Full alpha until halfway through the final text's life, then fade —
            // fading from frame one makes it unreadable, and the first text never fades.
            float f = (_age - _resolveAt) / _life;
            ApplyColor(f < 0.5f ? 1f : 1f - (f - 0.5f) / 0.5f);

            if (_age >= _totalLife) Destroy(gameObject);
        }

        private void ApplyColor(float alpha) =>
            TextColouring.Apply(label, _style, _key, Mathf.Clamp01(_age / Mathf.Max(0.0001f, _totalLife)), alpha);
    }
}
