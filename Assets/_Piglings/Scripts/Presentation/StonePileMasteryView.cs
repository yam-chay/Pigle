using System.Collections.Generic;
using Piglings.Events;
using Piglings.Simulation;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// The stone's mastery, shown on the pile — progress is shown here, never applied (the level stays fixed all night).
    /// No progress bar: the stones themselves tell you.
    /// - Glints: a sparkle on a random stone now and then, more often as progress toward the next level rises
    ///   (NightSession.WeaponProgress = saved hits + tonight's).
    /// - Upgrade ready (tonight crossed the threshold): the stones pulse gold until the night ends.
    /// - Night end with a level earned: a burst ring over the pile, as StonePile swaps the stones to the new level.
    ///
    /// Put it on the pile (StonePile's object). Only tints the stones' sprites — never their size, which is gameplay.
    /// Missing sprites = no glint / no ring; the pulse needs no art.
    /// </summary>
    public sealed class StonePileMasteryView : MonoBehaviour
    {
        [SerializeField] private NightSession session;
        [SerializeField] private StonePile pile;

        [Header("Art (white, tinted here)")]
        [SerializeField] private Sprite sparkle;      // fx_sparkle
        [SerializeField] private Sprite burstRing;    // fx_burst_ring

        [Header("Glints")]
        [Tooltip("Seconds between glints with no progress toward the next level…")]
        [SerializeField, Min(0.05f)] private float slowestGlint = 4f;
        [Tooltip("…and right before the threshold. At the top level the pile glints at this rate too.")]
        [SerializeField, Min(0.05f)] private float fastestGlint = 0.4f;
        [Tooltip("0 = no glints at all until there's this much progress (0..1).")]
        [SerializeField, Range(0f, 1f)] private float glintFrom = 0f;
        [SerializeField] private Color glintColour = new Color(1f, 0.95f, 0.75f, 1f);
        [SerializeField] private FxMotion glintMotion = new FxMotion { seconds = 0.45f, startScale = 0.15f, endScale = 0.45f, spin = 120f };

        [Header("Upgrade ready")]
        [SerializeField] private Color restColour = Color.white;
        [Tooltip("Pulses per second.")]
        [SerializeField, Min(0.1f)] private float pulseRate = 1.5f;
        [Tooltip("How far toward gold the pulse goes at its peak (1 = fully gold).")]
        [SerializeField, Range(0f, 1f)] private float pulseStrength = 0.8f;

        [Header("Level up (night end)")]
        [SerializeField] private Color ringColour = new Color(1f, 0.85f, 0.35f, 1f);
        [SerializeField] private FxMotion ringMotion = new FxMotion { seconds = 0.7f, startScale = 0.3f, endScale = 3f };
        [Tooltip("Where the ring centres, from the pile's origin (the middle of the bottom row).")]
        [SerializeField] private Vector3 ringOffset = new Vector3(0f, 0.15f, 0f);

        private FxSprites _fx;
        private float _nextGlintAt;
        private bool _pulsing;
        // Stones tinted last frame: one that has left the pile since (thrown, stolen) gets its rest colour back.
        private readonly List<Throwable> _tinted = new List<Throwable>();
        private readonly List<Throwable> _now = new List<Throwable>();

        private void Awake() => _fx = new FxSprites(transform);

        // Start, not Awake: the session's bus is created in its Awake.
        private void Start()
        {
            session.Bus.Subscribe<NightEnded>(OnNightEnded);
            _nextGlintAt = Time.time + GlintInterval();
        }

        private void OnDestroy()
        {
            if (session == null || session.Bus == null) return;
            session.Bus.Unsubscribe<NightEnded>(OnNightEnded);
        }

        private void Update()
        {
            _fx.Update(Time.deltaTime);
            if (session.State.Ended) return;

            float progress = session.WeaponProgress;
            if (progress >= glintFrom && Time.time >= _nextGlintAt && pile.Stones.Count > 0)
            {
                Glint(pile.Stones[Random.Range(0, pile.Stones.Count)]);
                _nextGlintAt = Time.time + GlintInterval();
            }

            if (session.WeaponUpgradeReady)
            {
                _pulsing = true;
                // 0 → 1 → 0, eased, so it breathes rather than blinks.
                float wave = 0.5f - 0.5f * Mathf.Cos(Time.time * pulseRate * 2f * Mathf.PI);
                Tint(Color.Lerp(restColour, session.Visuals.Gold, wave * pulseStrength));
            }
        }

        // The interval is picked when the last glint fires, so a new hit shortens the wait from the next glint on.
        private float GlintInterval()
        {
            float t = session.WeaponProgress;
            // A little jitter, so a full pile doesn't tick like a clock.
            return Mathf.Lerp(slowestGlint, fastestGlint, t) * Random.Range(0.75f, 1.25f);
        }

        private void Glint(Throwable stone)
        {
            if (stone == null || stone.Look == null) return;
            // Somewhere on the stone's face, just above it in draw order.
            float r = stone.Look.bounds.extents.x * 0.6f;
            var at = stone.transform.position + (Vector3)(Random.insideUnitCircle * r);
            _fx.Spawn(sparkle, at, glintColour, glintMotion, stone.Look.sortingLayerID, stone.Look.sortingOrder + 1);
        }

        private void Tint(Color colour)
        {
            _now.Clear();
            _now.AddRange(pile.Stones);
            if (pile.Held != null) _now.Add(pile.Held);
            foreach (var stone in _now)
                if (stone != null && stone.Look != null) stone.Look.color = colour;
            foreach (var gone in _tinted)
                if (gone != null && !_now.Contains(gone) && gone.Look != null) gone.Look.color = restColour;
            _tinted.Clear();
            _tinted.AddRange(_now);
        }

        // StonePile swaps the stones to the banked level on this same event; this adds the celebration.
        private void OnNightEnded(NightEnded e)
        {
            if (_pulsing) { Tint(restColour); _pulsing = false; }
            if (session.BankedWeaponLevel <= session.WeaponLevel) return;

            int layer = 0, order = 0;
            if (pile.Stones.Count > 0 && pile.Stones[0] != null && pile.Stones[0].Look != null)
            {
                layer = pile.Stones[0].Look.sortingLayerID;
                order = pile.Stones[0].Look.sortingOrder + 2;   // over the stones and their glints
            }
            _fx.Spawn(burstRing, transform.position + ringOffset, ringColour, ringMotion, layer, order);
        }
    }
}
