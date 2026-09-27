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

        // Read once in Awake — edit the asset outside play mode (or restart play) to try a new curve.
        [SerializeField] private ScoringDefinition scoring;

        public NightDefinition Night => night;
        public EventBus Bus { get; private set; }
        public NightState State { get; private set; }
        public IdAllocator Ids { get; private set; }

        private ChainTracker _chains;
        private NightReferee _referee;

        /// <summary>Robots climb and spawn only while the night is being played: paused for the choice, stopped once over.</summary>
        public bool WallMoving => State.Phase == NightPhase.Running || State.Phase == NightPhase.Overtime;

        // The end-of-night choice. Called by NightChoice (Simulation); the referee ignores them outside ChoicePending.
        public void ChooseStay() => _referee.Stay();
        public void ChooseLeave() => _referee.Leave();

        private void Awake()
        {
            Bus = new EventBus();
            State = new NightState();
            Ids = new IdAllocator();
            _chains = new ChainTracker(Bus, State, BuildCurve());
            // After ChainTracker: the referee reads its open-chain count.
            _referee = new NightReferee(Bus, State, _chains,
                new NightGoal(night.TargetScore, night.ThrowsAvailable));
        }

        // Without an asset the night still plays on ScoreCurve's defaults (10 / 10 / 10 / ×0.5),
        // but says so — silently scoring on values nobody chose would make tuning confusing.
        private ScoreCurve BuildCurve()
        {
            if (scoring == null)
            {
                Debug.LogWarning("NightSession: no ScoringDefinition assigned, using default scoring.", this);
                return new ScoreCurve();
            }
            if (scoring.OvertimeMultiplier <= 1)
                Debug.LogWarning($"NightSession: {scoring.name} has Overtime Multiplier {scoring.OvertimeMultiplier}, " +
                                 "so overtime doesn't double anything. Set it to 2 (select the asset in the Project window).", scoring);
            return new ScoreCurve(scoring.StoneValue, scoring.GrowthPerHit, scoring.WolfValue,
                                  scoring.MultiplierPerDepth, scoring.CarryScoredTotal, scoring.OvertimeMultiplier);
        }

        private void OnDestroy()
        {
            _referee?.Dispose();
            _chains?.Dispose();
        }
    }
}
