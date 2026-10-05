using System.Collections;
using UnityEngine;
using Piglings.Events;
using Piglings.Simulation;

namespace Piglings.Presentation
{
    // Fades the bright "day" tiles in over the night tiles when the night ends (Night.unity).
    // With a NightFlow (the campaign scene, M10.S) it follows the flow's Daylight instead: day in the barn, night from the
    // rise, crossfading with the camera's moves (the moon rises / sets) — the post-run stays at night.
    // Lives on a child of the scrolling background, so it scrolls along without any code of its own.
    // Presentation only: it reads, it never changes game state.
    [RequireComponent(typeof(SpriteRenderer))]
    public class DayNightBackground : MonoBehaviour
    {
        [SerializeField] NightSession session;
        [SerializeField] float fadeSeconds = 1.2f;
        [Tooltip("The campaign scene: follow its flow's day / night (optional). Empty = fade to day when the night ends.")]
        [SerializeField] NightFlow flow;

        SpriteRenderer dayTiles;

        void Awake()
        {
            dayTiles = GetComponent<SpriteRenderer>();
            SetAlpha(0f); // every night starts dark
        }

        // Start, not Awake: guarantees NightSession has already created the bus.
        void Start() => session.Bus.Subscribe<NightEnded>(OnNightEnded);
        void OnDestroy() => session?.Bus.Unsubscribe<NightEnded>(OnNightEnded);

        void OnNightEnded(NightEnded e) { if (flow == null) FadeTo(1f); }

        void LateUpdate()
        {
            if (flow != null) SetAlpha(flow.Daylight);
        }

        // For a restart that doesn't reload the scene (see note below).
        public void BackToNight() => FadeTo(0f);

        void FadeTo(float target)
        {
            StopAllCoroutines();
            StartCoroutine(Fade(target));
        }

        IEnumerator Fade(float target)
        {
            float start = dayTiles.color.a;
            for (float t = 0f; t < fadeSeconds; t += Time.deltaTime)
            {
                SetAlpha(Mathf.Lerp(start, target, t / fadeSeconds));
                yield return null;
            }
            SetAlpha(target);
        }

        void SetAlpha(float a)
        {
            Color c = dayTiles.color;
            c.a = a;
            dayTiles.color = c;
        }
    }
}