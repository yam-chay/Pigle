using Piglings.Simulation;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// The materials pile in the barn room's corner (M10.F): it says "click me" without words. Idle: a faint glow behind it
    /// (room_materials_glow, white, tinted). Hovered (BarnInteraction): it lifts, pops a little, the glow comes up full and
    /// sparkles rise off it; the camera leans in (BarnInteraction → CameraDirector). Reads only.
    /// </summary>
    public sealed class MaterialsPileView : MonoBehaviour
    {
        [SerializeField] private BarnInteraction interaction;
        [Tooltip("What lifts and pops: the pile's sprite (room_materials, pivot Bottom).")]
        [SerializeField] private Transform pile;
        [Tooltip("The glow behind it (room_materials_glow): its alpha is driven here.")]
        [SerializeField] private SpriteRenderer glow;
        [SerializeField] private Color glowColour = new Color(1f, 0.85f, 0.45f, 1f);
        [SerializeField, Range(0f, 1f)] private float idleGlow = 0.25f;
        [SerializeField, Range(0f, 1f)] private float hoverGlow = 1f;
        [Tooltip("How far it lifts on hover (world units) and how much bigger it gets (0.04 = 4%).")]
        [SerializeField] private float lift = 0.04f;
        [SerializeField, Range(0f, 0.3f)] private float pop = 0.04f;
        [Tooltip("Seconds to ease in and out of the hover look.")]
        [SerializeField, Min(0.01f)] private float easeSeconds = 0.12f;

        [Header("Sparkles while hovered")]
        [SerializeField] private Sprite sparkle;   // fx_sparkle
        [SerializeField, Min(0f)] private float sparklesPerSecond = 6f;
        [SerializeField] private FxMotion sparkleMotion = new FxMotion { seconds = 0.6f, startScale = 0.25f, endScale = 0.1f, rise = 0.25f, spin = 90f };

        private Vector3 _restPosition, _restScale;
        private float _hover, _hoverVelocity, _sparkleDue;
        private FxSprites _fx;

        private void Awake() => _fx = new FxSprites(transform);

        private void Start()
        {
            if (pile == null) return;
            _restPosition = pile.localPosition;
            _restScale = pile.localScale;
        }

        private void LateUpdate()
        {
            bool hovered = interaction != null && interaction.Hovered == BarnSpot.Materials;
            _hover = Mathf.SmoothDamp(_hover, hovered ? 1f : 0f, ref _hoverVelocity, easeSeconds);

            if (pile != null)
            {
                pile.localPosition = _restPosition + Vector3.up * (lift * _hover);
                pile.localScale = _restScale * (1f + pop * _hover);
            }
            if (glow != null)
            {
                var c = glowColour;
                c.a *= Mathf.Lerp(idleGlow, hoverGlow, _hover);
                glow.color = c;
            }

            if (hovered && sparkle != null && sparklesPerSecond > 0f && Time.time >= _sparkleDue)
            {
                _sparkleDue = Time.time + 1f / sparklesPerSecond;
                var source = glow != null ? glow : (pile != null ? pile.GetComponent<SpriteRenderer>() : null);
                if (source != null)
                {
                    var b = source.bounds;
                    var at = new Vector3(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.center.y + b.extents.y * 0.5f), b.center.z);
                    _fx.Spawn(sparkle, at, glowColour, sparkleMotion, source.sortingLayerID, source.sortingOrder + 5);
                }
            }
            _fx.Update(Time.deltaTime);
        }
    }
}
