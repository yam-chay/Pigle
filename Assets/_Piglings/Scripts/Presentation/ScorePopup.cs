using TMPro;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// One floating score text: pops in, drifts up, fades, destroys itself. Pure visuals.
    /// Lives on a prefab with a world-space TextMeshPro (not the UI one), spawned by ScorePopupSpawner.
    /// </summary>
    public sealed class ScorePopup : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;

        [Tooltip("Seconds from appearing to gone.")]
        [SerializeField, Min(0.1f)] private float lifetime = 0.9f;

        [Tooltip("World units it drifts up over its lifetime.")]
        [SerializeField] private float rise = 0.5f;

        [Tooltip("Overshoot on appear: starts this much bigger and settles. Makes it read as a hit, not a label.")]
        [SerializeField, Min(0f)] private float pop = 0.35f;

        private Vector3 _start;
        private Vector3 _baseScale;
        private Color _color;
        private float _age;

        private void Reset() => label = GetComponent<TMP_Text>();

        public void Show(string text, Color color, float scale)
        {
            label.text = text;
            _color = color;
            label.color = color;
            _start = transform.position;
            _baseScale = Vector3.one * scale;
            transform.localScale = _baseScale * (1f + pop);
            _age = 0f;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / lifetime);

            // Ease-out rise: fast at first, then hangs, so the number is readable while it fades.
            float eased = 1f - (1f - t) * (1f - t);
            transform.position = _start + Vector3.up * (rise * eased);

            // Settle the pop in the first 15% of its life.
            float settle = Mathf.Clamp01(t / 0.15f);
            transform.localScale = _baseScale * (1f + pop * (1f - settle));

            // Hold full alpha for the first half, then fade — fading from frame one makes it unreadable.
            float alpha = t < 0.5f ? 1f : 1f - (t - 0.5f) / 0.5f;
            label.color = new Color(_color.r, _color.g, _color.b, _color.a * alpha);

            if (_age >= lifetime) Destroy(gameObject);
        }
    }
}
