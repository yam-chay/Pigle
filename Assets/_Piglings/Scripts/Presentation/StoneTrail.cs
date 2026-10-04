using Piglings.Simulation;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// The thrown stone's trail (M6.3: the stone was hard to follow). Coloured per mastery level (the level's Trail
    /// Colour on ThrowableDefinition), so a levelled stone also reads as one in flight. Only while flying: never on the
    /// pile, in the hand, or on a thief. As wide as the stone, so a bigger stone leaves a bigger trail.
    ///
    /// Put it on the Stone prefab next to a TrailRenderer (material with fx_trail). Visual only.
    /// </summary>
    [RequireComponent(typeof(TrailRenderer))]
    public sealed class StoneTrail : MonoBehaviour
    {
        [SerializeField] private Throwable stone;
        [SerializeField] private TrailRenderer trail;
        [Tooltip("Trail width at its head, as a fraction of the stone's diameter.")]
        [SerializeField, Range(0.1f, 2f)] private float widthOfStone = 0.8f;
        [Tooltip("Opacity at the head; it fades to 0 at the tail.")]
        [SerializeField, Range(0f, 1f)] private float headAlpha = 0.9f;

        private bool _wasFlying;

        private void Reset()
        {
            stone = GetComponent<Throwable>();
            trail = GetComponent<TrailRenderer>();
        }

        private void Awake()
        {
            // Prefabs set up before this component's fields were wired.
            if (stone == null) stone = GetComponent<Throwable>();
            if (trail == null) trail = GetComponent<TrailRenderer>();
            trail.emitting = false;
            trail.Clear();
        }

        private void LateUpdate()
        {
            bool flying = stone != null && stone.InFlight;
            if (flying == _wasFlying) return;
            _wasFlying = flying;

            if (flying)
            {
                // Clear first: points recorded while it hopped from the pile to the hand would draw a streak.
                trail.Clear();

                var colour = stone.Definition != null
                    ? stone.Definition.TrailColourFor(stone.Level)
                    : Color.white;

                var head = colour;
                head.a *= headAlpha;

                var tail = colour;
                tail.a *= 0.5f;

                trail.startColor = head;
                trail.endColor = tail;

                // No definition (a stone never given a level): keep the Inspector width.
                if (stone.Definition != null)
                    trail.widthMultiplier =
                        2f * stone.Definition.RadiusAt(stone.Level) * stone.SizeScale * widthOfStone;
            }
            trail.emitting = flying;
        }
    }
}
