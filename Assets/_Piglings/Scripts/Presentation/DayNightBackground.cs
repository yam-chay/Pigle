using System.Collections;
using UnityEngine;
using Piglings.Events;
using Piglings.Simulation;

namespace Piglings.Presentation
{
    // Fades the bright "day" tiles in over the night tiles when the night ends.
    // Lives on a child of the scrolling background, so it scrolls along without any code of its own.
    // Presentation only: it listens to NightEnded, it never changes game state.
    [RequireComponent(typeof(SpriteRenderer))]
    public class DayNightBackground : MonoBehaviour
    {
        [SerializeField] NightSession session;
        [SerializeField] float fadeSeconds = 1.2f;

        SpriteRenderer dayTiles;

        void Awake()
        {
            dayTiles = GetComponent<SpriteRenderer>();
            SetAlpha(0f); // every night starts dark
        }

        // Start, not Awake: guarantees NightSession has already created the bus.
        void Start() => session.Bus.Subscribe<NightEnded>(OnNightEnded);
        void OnDestroy() => session?.Bus.Unsubscribe<NightEnded>(OnNightEnded);

        void OnNightEnded(NightEnded e) => FadeTo(1f);

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