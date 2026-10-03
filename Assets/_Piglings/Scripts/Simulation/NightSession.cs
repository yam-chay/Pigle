using System.Collections.Generic;
using Piglings.Definitions;
using Piglings.Events;
using Piglings.Meta;
using Piglings.Rules;
using Piglings.Runtime;
using UnityEngine;

namespace Piglings.Simulation
{
    /// <summary>
    /// Owner of one night. Lives in the Night scene and dies with it — no DontDestroyOnLoad, no static access.
    /// Creates the engine-free layers (EventBus, NightState, Rules, Meta's Progression) and exposes them to scene
    /// objects that reference it. Scene objects get it through a serialized field, never through a lookup.
    ///
    /// Persistence: the disk is the home of everything that outlives a night. This owner loads the player's profile in
    /// Awake and saves it once, at NightEnded (after the referee has banked the night into it). Play Again reloads the
    /// scene, so the next night loads it again from disk. Quitting mid-night saves nothing: that night's hits are lost.
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
        private MasteryTally _tally;
        private Progression _progression;
        private ProfileFile _profileFile;
        private int[] _weaponThresholds;   // night.Throwable's level thresholds, read once (like the scoring curve)
        private int _savedHitsAtStart;     // night.Throwable's saved hits when this night started (tonight's are in State)

        // When the current placement round started (Time.time), for the refill pause.
        private float _roundStartedAt;

        /// <summary>Robots climb and spawn only while the night is Running: frozen for a placement round, stopped once over.</summary>
        public bool WallMoving => State.WallMoving;

        // For views (Presentation can't reference Rules): the hours as plain numbers.
        public int NextThreshold => _referee.NextThreshold;
        public int ThresholdCount => _referee.Goal.ThresholdCount;
        public float HourMultiplier => _referee.HourMultiplier;

        /// <summary>
        /// The mastery level tonight's stones (night.Throwable) play at: derived from the saved hits when the night started.
        /// Fixed for the whole night — a level earned tonight applies from the next night.
        /// </summary>
        public int WeaponLevel { get; private set; } = 1;

        /// <summary>
        /// The level the saved hits give right now. Before the night ends it's WeaponLevel; after NightEnded (banked) it's
        /// the level the next night starts at — the pile shows it then.
        /// </summary>
        public int BankedWeaponLevel => MasteryLevels.LevelFor(_progression.Profile.DirectHits(night.Throwable.Id), _weaponThresholds);

        // For views (M9.3): progress is shown during the night, never applied (WeaponLevel stays fixed).

        /// <summary>Saved hits when the night started + tonight's hits so far. After the night is banked, = the saved total.</summary>
        public int WeaponHitsSoFar
        {
            get
            {
                State.WeaponHits.TryGetValue(night.Throwable.Id, out int tonight);
                return _savedHitsAtStart + tonight;
            }
        }

        /// <summary>0..1 from tonight's level toward the next one (1 at the top level, and once tonight reached it).</summary>
        public float WeaponProgress => MasteryLevels.ProgressFrom(WeaponLevel, WeaponHitsSoFar, _weaponThresholds);

        /// <summary>Tonight's hits have reached the next level: it applies when the night ends ("upgrade ready").</summary>
        public bool WeaponUpgradeReady => MasteryLevels.LevelFor(WeaponHitsSoFar, _weaponThresholds) > WeaponLevel;

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
            _tally = new MasteryTally(Bus, State);
            _progression = new Progression(Bus, LoadProfile());
            FixWeaponLevel();
            Bus.Subscribe<NightPhaseChanged>(OnPhaseChanged);
            Bus.Subscribe<NightEnded>(OnNightEnded);
        }

        // ---------- the save ----------

        private PlayerProfile LoadProfile()
        {
            _profileFile = new ProfileFile(Application.persistentDataPath);
            var load = _profileFile.Load();
            string line = $"Piglings save: loaded ({load.Source}) — {load.Profile.Describe()}  ({_profileFile.MainPath})";
            if (load.Problems.Count == 0 && load.SaveError == null) Debug.Log(line, this);
            else
            {
                // A bad save is never silent: say what was wrong and where the copy went.
                if (load.Problems.Count > 0) line += "\n  problems: " + string.Join("; ", load.Problems);
                if (load.BackedUp.Count > 0) line += "\n  backed up to: " + string.Join(", ", load.BackedUp);
                if (load.SaveError != null) line += "\n  couldn't write the save: " + load.SaveError;
                Debug.LogWarning(line, this);
            }
            return load.Profile;
        }

        private void FixWeaponLevel()
        {
            var weapon = night.Throwable;
            _weaponThresholds = weapon.Thresholds();
            var problem = MasteryLevels.Problem(_weaponThresholds);
            if (problem != null) Debug.LogWarning($"{weapon.name}: mastery levels — {problem}.", weapon);
            int hits = _progression.Profile.DirectHits(weapon.Id);
            _savedHitsAtStart = hits;
            WeaponLevel = MasteryLevels.LevelFor(hits, _weaponThresholds);
            int next = MasteryLevels.NextThreshold(hits, _weaponThresholds);
            Debug.Log($"Piglings save: {weapon.Id} plays at level {WeaponLevel} tonight ({hits} hits" +
                      (next < 0 ? ", top level)" : $", level {WeaponLevel + 1} at {next})"), this);
        }

        // NightBanked (applied by Progression) comes just before NightEnded, so the profile is complete here.
        private void OnNightEnded(NightEnded e) => Save($"night banked ({e.Reason}, {Tonight()})");

        private void Save(string why)
        {
            var error = _profileFile.Save(_progression.Profile);
            if (error == null) Debug.Log($"Piglings save: saved after {why} — {_progression.Profile.Describe()}; " +
                                         $"{night.Throwable.Id} level {BankedWeaponLevel} next night", this);
            else Debug.LogError($"Piglings save: NOT saved after {why} — {error}. The previous save is untouched.", this);
        }

        // "+12 stone hits tonight", for the save log.
        private string Tonight()
        {
            var parts = new List<string>();
            foreach (var hits in State.WeaponHits) parts.Add($"+{hits.Value} {hits.Key} hits");
            return parts.Count == 0 ? "no weapon hits" : string.Join(", ", parts) + " tonight";
        }

        // Playtest tools (right-click the component's header in play mode).

        [ContextMenu("Mastery/Reset progress")]
        private void DebugResetProgress()
        {
            if (!Application.isPlaying || _progression == null) { Debug.LogWarning("Reset progress works in play mode only.", this); return; }
            _progression.Reset();
            _savedHitsAtStart = 0;   // progress views count from the empty profile now (tonight's level stays as it started)
            // Tonight's hits still bank at the end of this night, on top of the empty profile.
            Save("Reset progress (the old save is in .prev until the next save)");
        }

        [ContextMenu("Mastery/Add 10 hits")]
        private void DebugAddTenHits()
        {
            if (!Application.isPlaying || _tally == null) { Debug.LogWarning("Add 10 hits works in play mode only.", this); return; }
            if (State.Ended) { Debug.LogWarning("Add 10 hits: this night is already banked — play again first.", this); return; }
            // Into tonight's tally, like real hits: they bank and save at the end of the night.
            _tally.AddWeaponHits(night.Throwable.Id, 10);
            Debug.Log($"Piglings save: +10 {night.Throwable.Id} hits added to tonight ({Tonight()}); they bank at night end.", this);
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
            Bus?.Unsubscribe<NightEnded>(OnNightEnded);
            _progression?.Dispose();
            _tally?.Dispose();
            _referee?.Dispose();
            _chains?.Dispose();
        }
    }
}
