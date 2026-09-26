using Piglings.Definitions;
using Piglings.Events;
using Piglings.Rules;
using Piglings.Runtime;
using UnityEngine;

namespace Piglings.Simulation
{
    /// <summary>
    /// Owner of one night. Lives in the Night scene and dies with it — no persistence,
    /// no DontDestroyOnLoad, no static access. Creates the engine-free layers
    /// (EventBus, NightState, Rules) and exposes them to scene objects that reference it.
    /// Scene objects get it through a serialized field, never through a lookup.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class NightSession : MonoBehaviour
    {
        [SerializeField] private NightDefinition night;

        // Scoring lives here, not in NightDefinition: it's how the game counts, not what a night contains.
        // Read once in Awake — change these outside play mode (or restart play) to try a new curve.
        [Header("Scoring (see ScoreCurve)")]
        [Tooltip("Points for a robot hit directly by the stone (depth 0).")]
        [SerializeField, Min(0)] private int basePoints = 10;
        [Tooltip("Extra points per step deeper in the chain. Depth 2 = base + 2 × this.")]
        [SerializeField, Min(0)] private int pointsPerDepth = 10;
        [Tooltip("Chain multiplier grows by this per depth reached. 0.5 → depth 1 ×1.5, depth 2 ×2.")]
        [SerializeField, Min(0f)] private float multiplierPerDepth = 0.5f;
        [Tooltip("The multiplier never goes above this.")]
        [SerializeField, Min(1f)] private float maxMultiplier = 4f;

        public NightDefinition Night => night;
        public EventBus Bus { get; private set; }
        public NightState State { get; private set; }
        public IdAllocator Ids { get; private set; }

        private ChainTracker _chains;

        private void Awake()
        {
            Bus = new EventBus();
            State = new NightState();
            Ids = new IdAllocator();
            var curve = new ScoreCurve(basePoints, pointsPerDepth, multiplierPerDepth, maxMultiplier);
            _chains = new ChainTracker(Bus, State, curve);
        }

        private void OnDestroy() => _chains?.Dispose();
    }
}
