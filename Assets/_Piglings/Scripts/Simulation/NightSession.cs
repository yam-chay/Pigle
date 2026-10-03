using System.Collections.Generic;
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

        [Tooltip("The wall's peg sockets (PegBoard on Barn). Empty = no sockets: every threshold is just a refill pause.")]
        [SerializeField] private PegBoard board;

        public NightDefinition Night => night;
        public EventBus Bus { get; private set; }
        public NightState State { get; private set; }
        public IdAllocator Ids { get; private set; }

        private ChainTracker _chains;
        private NightReferee _referee;

        // When the current placement round started (Time.time), for the refill pause.
        private float _roundStartedAt;

        /// <summary>Robots climb and spawn only while the night is Running: frozen for a placement round, stopped once over.</summary>
        public bool WallMoving => State.WallMoving;

        // For views (Presentation can't reference Rules): the hours as plain numbers.
        public int NextThreshold => _referee.NextThreshold;
        public int ThresholdCount => _referee.Goal.ThresholdCount;
        public float HourMultiplier => _referee.HourMultiplier;

        // Peg placement. Called by PegThrower; the referee ignores them outside a round.
        public bool PlacePeg(int socket, string pegId) => _referee.PlacePeg(socket, pegId);
        public bool IsValidPegTarget(int socket, string pegId) => _referee.IsValidTarget(socket, pegId);
        public bool CanPlaceAnyPeg => _referee.CanPlaceAnyPeg;

        /// <summary>The night's PegDefinition for a peg id (sprites for the shelf and the board). Null if unknown.</summary>
        public PegDefinition FindPeg(string id)
        {
            foreach (var entry in night.PegLoadout)
                if (entry != null && entry.peg != null && entry.peg.Id == id) return entry.peg;
            return null;
        }

        private void Awake()
        {
            Bus = new EventBus();
            State = new NightState();
            Ids = new IdAllocator();
            _chains = new ChainTracker(Bus, State, BuildCurve());
            // After ChainTracker: the referee reads its open-chain count.
            _referee = new NightReferee(Bus, State, _chains,
                new NightGoal(ToArray(night.Thresholds), night.ThrowsAvailable, night.StonesPerThreshold),
                BuildPegs());
            Bus.Subscribe<NightPhaseChanged>(OnPhaseChanged);
        }

        private void OnPhaseChanged(NightPhaseChanged e)
        {
            if (e.To == NightPhase.PegPlacement) _roundStartedAt = Time.time;
        }

        // A placement round with nothing to place (empty shelf, no valid socket, or the throws ran out of targets)
        // still pauses for the refill — long enough to watch the stones land — then ends by itself. Rules can't
        // keep time, so the clock lives here. Unused throws are lost (the referee decides that).
        private void Update()
        {
            if (State.Phase == NightPhase.PegPlacement && !_referee.CanPlaceAnyPeg
                && Time.time - _roundStartedAt >= night.RefillPauseSeconds)
                _referee.EndPlacement();
        }

        // One socket per Hold (PegBoard). No board = no sockets: every round is then just a refill pause.
        private PegSetup BuildPegs()
        {
            var loadout = new List<(PegType, int)>();
            foreach (var entry in night.PegLoadout)
            {
                if (entry == null || entry.peg == null) continue;
                loadout.Add((new PegType(entry.peg.Id, entry.peg.MaxLevel, entry.peg.Mergeable), entry.count));
            }
            var pegs = new PegSetup(loadout, night.PegThrowsPerThreshold, board != null ? board.SocketCount : 0);
            if (pegs.DroppedTypes > 0)
                Debug.LogWarning($"NightSession: {night.name} brings more than {PegSetup.ShelfCapacity} peg types; " +
                                 $"{pegs.DroppedTypes} ignored (the shelf holds {PegSetup.ShelfCapacity}).", night);
            return pegs;
        }

        private static int[] ToArray(IReadOnlyList<int> list)
        {
            var a = new int[list?.Count ?? 0];
            for (int i = 0; i < a.Length; i++) a[i] = list[i];
            return a;
        }

        // Without an asset the night still plays on ScoreCurve's defaults (10 / 10 / 10 / ×0.5, hours +0.5),
        // but says so — silently scoring on values nobody chose would make tuning confusing.
        private ScoreCurve BuildCurve()
        {
            if (scoring == null)
            {
                Debug.LogWarning("NightSession: no ScoringDefinition assigned, using default scoring.", this);
                return new ScoreCurve();
            }
            return new ScoreCurve(scoring.StoneValue, scoring.GrowthPerHit, scoring.WolfValue,
                                  scoring.MultiplierPerDepth, scoring.CarryScoredTotal, scoring.HourMultiplierStep);
        }

        private void OnDestroy()
        {
            Bus?.Unsubscribe<NightPhaseChanged>(OnPhaseChanged);
            _referee?.Dispose();
            _chains?.Dispose();
        }
    }
}
