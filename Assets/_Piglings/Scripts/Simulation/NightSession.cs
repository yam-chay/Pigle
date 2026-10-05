using System.Collections.Generic;
using Piglings.Definitions;
using Piglings.Events;
using Piglings.Meta;
using Piglings.Rules;
using Piglings.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Piglings.Simulation
{
    /// <summary>
    /// Owner of one night. Lives in the Night scene and dies with it — no DontDestroyOnLoad, no static access.
    /// Creates the engine-free layers (EventBus, NightState, Rules, Meta's Progression) and exposes them to scene
    /// objects that reference it. Scene objects get it through a serialized field, never through a lookup.
    ///
    /// Persistence: the disk is the home of everything that outlives a night. This owner loads the player's profile in
    /// Awake and saves it at NightEnded (after the referee has banked the night into it, and tonight's bests went into the
    /// records) — and again when the campaign's buttons write where to go (GoToNight, RetryNight), and when a fast Retry's
    /// one-shot "start in the night" is used up at load. Play Again / Retry / Next reload the scene, so the next night
    /// loads it again from disk. Quitting mid-night saves nothing: that night's hits are lost.
    ///
    /// Campaign mode (M10, the v2 scene: a Campaign is assigned): the profile decides tonight's night (its saved index),
    /// the tower (the player's slices, or the night's own — built by TowerBuilder before anything reads the sockets), the
    /// stones and refill (StoneProgression) and the peg shelf (unlocked types × owned copies). Without a Campaign
    /// (Night.unity) the night, stones, refill and shelf come from the Night field, as before.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class NightSession : MonoBehaviour
    {
        [Tooltip("The night to play. In campaign mode, only the fallback when the Campaign has no nights.")]
        [SerializeField] private NightDefinition night;

        // Read once in Awake — edit the asset outside play mode (or restart play) to try a new curve.
        [SerializeField] private ScoringDefinition scoring;

        [Tooltip("The wall's peg sockets (PegBoard on Barn). Empty = no sockets: every threshold is just a refill pause.")]
        [SerializeField] private PegBoard board;

        [Header("Campaign (v2 scene; leave empty in Night.unity)")]
        [Tooltip("Set = campaign mode: the night, tower, stones and pegs come from the save and this campaign.")]
        [SerializeField] private CampaignDefinition campaign;
        [Tooltip("Builds the night's tower from its slices (and moves the tower top and walls). Empty = the scene's own tower.")]
        [SerializeField] private TowerBuilder tower;
        [Tooltip("The night's colours, first → last (dawn gold), spread over each night's hours (scoreboard, hour dots, post-run). Optional.")]
        [SerializeField] private HourPaletteDefinition hourPalette;

        [Header("Look (both scenes)")]
        [Tooltip("Score colours: popups by depth, throws by quality. Shared by every view. Empty = the built-in defaults.")]
        [SerializeField] private ScoreColoursDefinition scoreColours;

        [Header("Save and start")]
        [Tooltip("Which save this scene uses: piglings_<name>.json. \"dev\" for Night.unity, \"campaign\" for the v2 scene — " +
                 "separate files, so testing here never advances the campaign. Letters, digits, - and _ only.")]
        [SerializeField] private string profileName = "dev";
        [Tooltip("On (Night.unity): the night is Running as soon as the scene loads. Off (the campaign scene): it waits in " +
                 "Dusk — the day phase, the camera rising — until BeginNight().")]
        [SerializeField] private bool startImmediately = true;

        [Header("Debug (campaign, play mode)")]
        [Tooltip("For the context menu \"Campaign/Go to Debug Night\": the night (1-based) to jump to.")]
        [SerializeField, Min(1)] private int debugNight = 2;

        /// <summary>Tonight's night: the Night field, or in campaign mode the one at the saved index.</summary>
        public NightDefinition Night => _night;
        public bool IsCampaign => campaign != null;
        public CampaignDefinition Campaign => campaign;
        /// <summary>The campaign as plain values (unlocks, next night). Null outside campaign mode.</summary>
        public CampaignPlan Plan { get; private set; }
        /// <summary>Tonight's index in the campaign (0 = night 1); 0 outside campaign mode.</summary>
        public int NightIndex { get; private set; }
        /// <summary>The player's progress, for views (post-run, barn). Read only: Progression writes it.</summary>
        public PlayerProfile Profile => _progression.Profile;

        /// <summary>The profile as it was when this scene loaded — before tonight (a copy). The post-run's "before".</summary>
        public PlayerProfile ProfileBeforeTonight { get; private set; }

        /// <summary>
        /// A fast Retry brought us here (M10.E): the campaign flow skips the barn room and starts the night at once. Read from
        /// the save at load (a one-shot flag, cleared and saved right away).
        /// </summary>
        public bool StartsInNight { get; private set; }

        /// <summary>
        /// Hour n's colour: the palette sampled over tonight's hours, first → last (white without a palette). The last hour
        /// is the palette's last colour (dawn gold) on every night.
        /// </summary>
        public Color HourColour(int hour) =>
            hourPalette != null ? hourPalette.ColourAt(hour, _referee != null ? ThresholdCount : hour) : Color.white;

        /// <summary>The score colours (depth, throw quality) every view shares; the defaults when none is assigned.</summary>
        public ScoreColoursDefinition ScoreColours
        {
            get
            {
                if (scoreColours != null) return scoreColours;
                // The defaults live in a private instance, never in the serialized field (that would dirty the scene in edit mode).
                if (_defaultColours == null) _defaultColours = ScriptableObject.CreateInstance<ScoreColoursDefinition>();
                return _defaultColours;
            }
        }
        private ScoreColoursDefinition _defaultColours;

        public EventBus Bus { get; private set; }
        public NightState State { get; private set; }
        public IdAllocator Ids { get; private set; }

        private ChainTracker _chains;
        private NightReferee _referee;
        private MasteryTally _tally;
        private PegEffects _pegEffects;
        private SettleTracker _settle;
        private NightLog _log;
        private Progression _progression;
        private ProfileFile _profileFile;
        private NightDefinition _night;            // tonight's (see Night)
        private readonly List<WallSliceDefinition> _tower = new List<WallSliceDefinition>();   // tonight's slices as built
        private readonly List<PegDefinition> _pegDefs = new List<PegDefinition>();   // tonight's shelf types, for FindPeg
        private StoneProgression _stones;          // the stone's progression rule (Night.Throwable), read once
        private int _savedHitsAtStart;             // the stone's saved hits when this night started (tonight's are in State)

        // When the current placement round started (Time.time), for the refill pause.
        private float _roundStartedAt;

        /// <summary>Robots climb and spawn only while the night is Running: frozen for a placement round, stopped once over.</summary>
        public bool WallMoving => State.WallMoving;

        // For views (Presentation can't reference Rules): the hours as plain numbers.
        public int NextThreshold => _referee.NextThreshold;
        public int ThresholdCount => _referee.Goal.ThresholdCount;
        public float HourMultiplier => _referee.HourMultiplier;

        /// <summary>The score of threshold n (1-based); 0 for n ≤ 0 — where hour 1 starts. Past the last → the last.</summary>
        public int ThresholdAt(int n) => _referee.Goal.ScoreAtThreshold(n);
        /// <summary>The points hour n takes to get through (T(n) − T(n−1)).</summary>
        public int HourGap(int hour) => _referee.Goal.HourGap(hour);
        /// <summary>How good a throw was: points ÷ the gap of the hour it was thrown in (1 = a whole hour's worth).</summary>
        public float ThrowQuality(int points, int hour) => _referee.Goal.ThrowQuality(points, hour);
        /// <summary>A robot's multiplier at this depth (×1, ×1.5, ×2…): the tutorial card shows the real numbers.</summary>
        public float DepthMultiplier(int depth) => _chains.Curve.Multiplier(depth);
        /// <summary>What chains thrown in hour n are multiplied by (×1, ×1.5…).</summary>
        public float HourMultiplierAt(int hour) => _chains.Curve.HourMultiplier(hour - 1);

        /// <summary>
        /// Where the stone stood when the night started (StoneProgression from the saved hits): its stones, level and refill.
        /// Fixed for the whole night — a stone earned (or an evolution) tonight applies from the next night.
        /// </summary>
        public StoneStatus StoneAtStart { get; private set; }

        /// <summary>Where the stone stands with tonight's hits so far (after the night is banked, = the saved total).</summary>
        public StoneStatus StoneNow => _stones.For(WeaponHitsSoFar);

        /// <summary>The level tonight's stones play at (the evolution its stone count reached), fixed when the night started.</summary>
        public int WeaponLevel => StoneAtStart.Level;

        /// <summary>
        /// The level the saved hits give right now. Before the night ends it's WeaponLevel; after NightEnded (banked) it's
        /// the level the next night starts at — the pile shows it then.
        /// </summary>
        public int BankedWeaponLevel => _stones.For(_progression.Profile.DirectHits(_night.Throwable.Id)).Level;

        // For views (M9.3): progress is shown during the night, never applied (the stone stays as it started).

        /// <summary>Saved hits when the night started + tonight's hits so far. After the night is banked, = the saved total.</summary>
        public int WeaponHitsSoFar
        {
            get
            {
                State.WeaponHits.TryGetValue(_night.Throwable.Id, out int tonight);
                return _savedHitsAtStart + tonight;
            }
        }

        /// <summary>0..1 from where the stone started tonight toward its next +1 stone (1 at the cap, and once earned).</summary>
        public float WeaponProgress => StoneAtStart.Progress(WeaponHitsSoFar);

        /// <summary>Tonight's hits have earned a stone (or more): it's added when the night ends ("upgrade ready").</summary>
        public bool WeaponUpgradeReady => StoneNow.Stones > StoneAtStart.Stones;

        // Peg placement. Called by PegThrower; the referee ignores them outside a round.
        public bool PlacePeg(int socket, string pegId) => _referee.PlacePeg(socket, pegId);
        public bool IsValidPegTarget(int socket, string pegId) => _referee.IsValidTarget(socket, pegId);
        public bool CanPlaceAnyPeg => _referee.CanPlaceAnyPeg;

        /// <summary>The night begins (Dusk → Running): called by the campaign flow once the camera reaches the Night frame.</summary>
        public void BeginNight() => _referee.Begin();

        /// <summary>
        /// Nothing left to land: no robot off the wall (breaking, falling, breaching) and no chain open. After NightEnded
        /// this is the sweep finishing — the campaign flow waits for it before the camera goes down to the doors.
        /// </summary>
        public bool WallSettled => _settle.Settled;

        /// <summary>
        /// Leave this night for another one (the campaign's To the barn / Next night): saves where the player is and reloads
        /// the scene, which then starts that night clean, in the barn room. Outside campaign mode it just reloads.
        /// Tonight's hits are only in the save if the night already ended (banked) — a jump mid-night loses them.
        /// </summary>
        public void GoToNight(int index, string why) => GoToNight(index, why, false);

        /// <summary>
        /// The campaign's fast Retry (M10.E): this same night again, straight into the night — the reloaded scene skips the
        /// barn room (the one-shot flag in the save). Outside campaign mode it just reloads.
        /// </summary>
        public void RetryNight(string why) => GoToNight(NightIndex, why, true);

        private void GoToNight(int index, string why, bool startInNight)
        {
            if (IsCampaign && Plan != null && Plan.NightCount > 0)
            {
                index = Mathf.Clamp(index, 0, Plan.NightCount - 1);
                _progression.SetCurrentNight(index);
                _progression.SetStartInNight(startInNight);
                Save($"{why} (to night {index + 1}{(startInNight ? ", straight into the night" : "")})");
            }
            ReloadScene();
        }

        /// <summary>
        /// What the post-run's PROGRESS and ALL-TIME RECORDS show (M10.E): the profile before tonight against the profile now.
        /// Call it after NightEnded (tonight banked); before that, "now" is still "before". Built on each call — call once.
        /// </summary>
        public PostRunReport BuildPostRunReport()
        {
            var types = new List<string>();
            foreach (var peg in CampaignPegTypes()) types.Add(peg.Id);
            return PostRunProgress.Build(ProfileBeforeTonight, Profile, _night.Throwable.Id, _stones, Plan, types, id =>
            {
                var peg = campaign != null ? campaign.FindPeg(id) : null;
                return peg != null ? ProgressionFor(peg) : null;
            });
        }

        /// <summary>
        /// A flying stone or a falling ball hit the peg in this socket (called by Hold). The Rules decide the effect and
        /// publish the facts; the result says what to do physically.
        /// </summary>
        public PegHitResult HitPeg(int socket, PegHitter hitter, GameId hitterId, ChainId chain) =>
            _pegEffects.Hit(socket, hitter, hitterId, chain);

        /// <summary>Tonight's PegDefinition for a peg id (sprites for the shelf and the board). Null if unknown.</summary>
        public PegDefinition FindPeg(string id)
        {
            foreach (var peg in _pegDefs) if (peg.Id == id) return peg;
            return null;
        }

        private void Awake()
        {
            Bus = new EventBus();
            State = new NightState();
            Ids = new IdAllocator();
            // The profile first: in campaign mode it decides tonight's night, its tower, the stones and the pegs.
            _progression = new Progression(Bus, LoadProfile());
            ProfileBeforeTonight = ProfileJson.Copy(_progression.Profile);
            ChooseNight();
            FixStone();
            UseStartInNight();
            // The tower before anything counts the sockets (BuildPegs, the board's Bind).
            BuildTower();
            _chains = new ChainTracker(Bus, State, BuildCurve());
            // After ChainTracker: the referee reads its open-chain count.
            _referee = new NightReferee(Bus, State, _chains, BuildGoal(), BuildPegs(), startImmediately);
            _pegEffects = new PegEffects(Bus, State, _chains, _referee.Pegs, Ids);
            if (board != null) board.Bind(this);
            _tally = new MasteryTally(Bus, State);
            _settle = new SettleTracker(Bus, _chains);
            _log = new NightLog(this);
            CheckPalette();
            Bus.Subscribe<NightPhaseChanged>(OnPhaseChanged);
            Bus.Subscribe<NightEnded>(OnNightEnded);
        }

        // ---------- tonight: which night, its tower, its stones ----------

        private void ChooseNight()
        {
            _night = night;
            if (campaign == null) return;

            var ids = new List<string>();
            var unlocks = new List<string>();
            foreach (var entry in campaign.Nights)
            {
                ids.Add(entry != null && entry.night != null ? entry.night.Id : null);
                unlocks.Add(entry != null && entry.unlocksOnDawn != null ? entry.unlocksOnDawn.Id : null);
            }
            var starting = new List<string>();
            foreach (var peg in campaign.StartingPegs) if (peg != null) starting.Add(peg.Id);
            Plan = new CampaignPlan(ids, unlocks, starting);

            NightIndex = Plan.CurrentNight(_progression.Profile);
            var picked = campaign.NightAt(NightIndex);
            if (picked != null) _night = picked;
            else Debug.LogWarning($"NightSession: {campaign.name} has no night {NightIndex + 1}; playing {night.name}.", campaign);
            // Copies per type too, so the shelf can be checked without counting it.
            var pegs = new List<string>();
            foreach (var id in Plan.UnlockedPegs(_progression.Profile))
            {
                var peg = campaign.FindPeg(id);
                pegs.Add(peg != null ? $"{id}×{CopiesOwned(peg)}" : $"{id} (not in the campaign)");
            }
            Debug.Log($"Piglings campaign: night {NightIndex + 1}/{Plan.NightCount} ({_night.Id}), pegs {string.Join(", ", pegs)}", this);
        }

        // The stone's progression, from the saved hits: tonight's stones, level and refill (fixed for the night).
        private void FixStone()
        {
            var weapon = _night.Throwable;
            // The evolutions: one per entry of the weapon's Levels list (its look stays in the definition).
            var evolutions = new List<StoneEvolution>();
            foreach (var level in weapon.Levels)
                if (level != null) evolutions.Add(new StoneEvolution(level.stonesNeeded, level.refill));
            _stones = new StoneProgression(weapon.StoneThresholds, weapon.StartStones, weapon.MaxStones, evolutions);
            var problem = MasteryLevels.Problem(weapon.StoneThresholds);
            if (problem != null) Debug.LogWarning($"{weapon.name}: stone thresholds — {problem}.", weapon);
            var evolutionProblem = StoneProgression.Problem(evolutions);
            if (evolutionProblem != null)
                Debug.LogWarning($"{weapon.name}: Levels — {evolutionProblem}. Fill each level's Stones Needed (rising) and Refill.", weapon);
            int hits = _progression.Profile.DirectHits(weapon.Id);
            _savedHitsAtStart = hits;
            StoneAtStart = _stones.For(hits);
            var s = StoneAtStart;
            Debug.Log($"Piglings save: {weapon.Id} tonight — level {s.Level}, {s.Stones} stones, refill {s.Refill} ({hits} hits" +
                      (s.AtCap ? ", at the cap" : $", +1 stone at {s.NextThreshold}") +
                      (s.NextEvolutionStones < 0 ? ")" : $"; level {s.NextEvolutionLevel} at {s.NextEvolutionStones} stones)") +
                      (IsCampaign ? "" : $"; this night's own pile ({_night.ThrowsAvailable}) and refill ({_night.StonesPerThreshold}) apply"), this);
        }

        // A fast Retry left a one-shot flag in the save: this load starts straight in the night. Cleared and saved now, so a
        // later launch (after quitting mid-night) boots in the barn as usual.
        private void UseStartInNight()
        {
            if (!_progression.Profile.StartInNight) return;
            _progression.SetStartInNight(false);
            StartsInNight = IsCampaign;
            Save("fast Retry: straight into the night (flag used)");
        }

        // Stones and refill: the stone's progression in the campaign, the night's own numbers in Night.unity.
        private NightGoal BuildGoal()
        {
            var thresholds = ToArray(_night.Thresholds);
            return IsCampaign
                ? new NightGoal(thresholds, StoneAtStart.Stones, StoneAtStart.Refill)
                : new NightGoal(thresholds, _night.ThrowsAvailable, _night.StonesPerThreshold);
        }

        private void BuildTower()
        {
            _tower.Clear();
            if (tower == null) return;
            var slices = SlicesForTonight();
            if (slices.Count == 0) return;   // a night without slices keeps the scene's own tower
            _tower.AddRange(slices);
            tower.Build(_tower);
        }

        // ---------- the day phase: slice placement (M10.F) ----------

        /// <summary>Tonight's tower as built, bottom → top (empty without a TowerBuilder or slices).</summary>
        public IReadOnlyList<WallSliceDefinition> TowerSlices => _tower;
        /// <summary>The tower builder (the slice picker asks it where the slices are). Null in a scene without one.</summary>
        public TowerBuilder Tower => tower;

        /// <summary>Slices can be swapped: the campaign scene, a built tower, slices to choose from, and the night not begun.</summary>
        public bool CanEditTower => IsCampaign && tower != null && _tower.Count > 0 && campaign.Slices.Count > 0
                                    && State.Phase == NightPhase.Dusk;

        /// <summary>
        /// The slices the player can build with tonight: the campaign's, with the same hold count as tonight's tower (a
        /// night's sockets were counted at load and can't change). The day phase's tray shows these.
        /// </summary>
        public List<WallSliceDefinition> SlicesToChoose()
        {
            var list = new List<WallSliceDefinition>();
            if (!IsCampaign || _tower.Count == 0) return list;
            int holds = _tower[0].Holds.Length;
            foreach (var slice in campaign.Slices)
                if (slice != null && slice.Holds.Length == holds && !list.Contains(slice)) list.Add(slice);
            return list;
        }

        /// <summary>The day phase: slice <paramref name="index"/> becomes <paramref name="slice"/> (dragged in from the tray). False = nothing changed.</summary>
        public bool SetSlice(int index, WallSliceDefinition slice)
        {
            if (!CanEditTower || index < 0 || index >= _tower.Count || slice == null || slice == _tower[index]) return false;
            if (!SlicesToChoose().Contains(slice)) return false;
            _tower[index] = slice;
            ApplyTower($"slice {index + 1} → {slice.Id}");
            return true;
        }

        /// <summary>The day phase: two slices of the tower trade places (one dragged onto the other). False = nothing changed.</summary>
        public bool SwapSlices(int a, int b)
        {
            if (!CanEditTower || a == b || a < 0 || b < 0 || a >= _tower.Count || b >= _tower.Count) return false;
            if (_tower[a] == _tower[b]) return false;
            var held = _tower[a];
            _tower[a] = _tower[b];
            _tower[b] = held;
            ApplyTower($"slices {a + 1} ⇄ {b + 1}");
            return true;
        }

        // The tower changed: rebuild it now (TowerBuilder → PegBoard re-binds its holds) and save the night's choice.
        private void ApplyTower(string why)
        {
            tower.Build(_tower);
            var ids = new List<string>();
            foreach (var slice in _tower) ids.Add(slice.Id);
            _progression.SetTower(_night.Id, ids);
            Save($"{why} ({_night.Id}: {string.Join(", ", ids)})");
        }

        /// <summary>
        /// Every peg type of the campaign in the order it's met: the starting ones, then each night's unlock. The barn's
        /// pegboard has a group of holes per type, in this order. Empty outside campaign mode.
        /// </summary>
        public List<PegDefinition> CampaignPegTypes()
        {
            var types = new List<PegDefinition>();
            if (campaign == null) return types;
            foreach (var peg in campaign.StartingPegs) if (peg != null && !types.Contains(peg)) types.Add(peg);
            foreach (var entry in campaign.Nights)
                if (entry != null && entry.unlocksOnDawn != null && !types.Contains(entry.unlocksOnDawn)) types.Add(entry.unlocksOnDawn);
            return types;
        }

        /// <summary>Is this peg type the player's (a starting type, or unlocked by a dawn)?</summary>
        public bool IsPegUnlocked(PegDefinition peg) => peg != null && Plan != null && Plan.IsUnlocked(Profile, peg.Id);

        /// <summary>Tonight's slices, bottom → top: the player's saved choice (campaign), or the night's own.</summary>
        public List<WallSliceDefinition> SlicesForTonight()
        {
            var authored = new List<WallSliceDefinition>();
            foreach (var slice in _night.Slices) if (slice != null) authored.Add(slice);
            if (!IsCampaign) return authored;

            var authoredIds = new List<string>();
            foreach (var slice in authored) authoredIds.Add(slice.Id);
            var chosen = CampaignPlan.TowerFor(_progression.Profile, _night.Id, authoredIds);
            var result = new List<WallSliceDefinition>();
            for (int i = 0; i < chosen.Count; i++)
            {
                var slice = campaign.FindSlice(chosen[i]);
                // A saved slice the campaign no longer has: the night's own at that height.
                result.Add(slice != null ? slice : authored[i]);
            }
            return result;
        }

        // Any number of colours fits any night now (sampled over its hours); only an empty palette is worth a word.
        private void CheckPalette()
        {
            if (hourPalette != null && hourPalette.Count == 0)
                Debug.LogWarning($"{hourPalette.name} has no colours: every hour is white.", hourPalette);
        }

        // ---------- the save ----------

        private PlayerProfile LoadProfile()
        {
            _profileFile = new ProfileFile(Application.persistentDataPath, profileName);
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

        // NightBanked (applied by Progression) comes just before NightEnded, so the profile is complete here.
        // A dawn in the campaign is recorded too (the cause behind unlocks and "Next night"), and tonight's bests go into the
        // all-time records (best night = the live score the night ended with — Yam's call).
        private void OnNightEnded(NightEnded e)
        {
            if (IsCampaign) _progression.RecordNightResult(_night.Id, e.Result == NightResult.Won);
            var broken = _progression.RecordNight(State.BestThrowPoints, State.LongestChain, State.DeepestChain, e.Score);
            Save($"night banked ({e.Reason}, {Tonight()}{(broken.Any ? ", a record broken" : "")})");
        }

        private void Save(string why)
        {
            var error = _profileFile.Save(_progression.Profile);
            if (error == null) Debug.Log($"Piglings save: saved after {why} — {_progression.Profile.Describe()}; " +
                                         $"next night {_night.Throwable.Id}: {_stones.For(_progression.Profile.DirectHits(_night.Throwable.Id)).Stones} stones, " +
                                         $"level {BankedWeaponLevel}", this);
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

        [ContextMenu("Campaign/Go to Debug Night")]
        private void DebugGoToNight()
        {
            if (!DebugCampaignReady()) return;
            GoToNight(debugNight - 1, "Go to Debug Night");
        }

        [ContextMenu("Campaign/Reset campaign")]
        private void DebugResetCampaign()
        {
            if (!DebugCampaignReady()) return;
            _progression.ResetCampaign();
            Save("Reset campaign (night 1, no dawns, no tower choices; mastery kept)");
            ReloadScene();
        }

        private bool DebugCampaignReady()
        {
            if (Application.isPlaying && _progression != null && IsCampaign && Plan != null && Plan.NightCount > 0) return true;
            Debug.LogWarning("Campaign debug works in play mode, in the campaign scene (a Campaign assigned).", this);
            return false;
        }

        // A debug jump or reset mid-night keeps only what was saved before it (tonight's hits aren't banked).
        // Reloads by build index, like PlayAgain: the scene must be in Build Settings.
        private void ReloadScene()
        {
            int index = SceneManager.GetActiveScene().buildIndex;
            if (index < 0)
            {
                Debug.LogError($"Can't reload {SceneManager.GetActiveScene().name}: it isn't in File ▸ Build Profiles (Scene List). " +
                               "Add it there — the campaign's Retry / Next reload it too. (The save was written.)", this);
                return;
            }
            SceneManager.LoadScene(index);
        }

        [ContextMenu("Mastery/Add 10 hits")]
        private void DebugAddTenHits()
        {
            if (!Application.isPlaying || _tally == null) { Debug.LogWarning("Add 10 hits works in play mode only.", this); return; }
            if (State.Ended) { Debug.LogWarning("Add 10 hits: this night is already banked — play again first.", this); return; }
            // Into tonight's tally, like real hits: they bank and save at the end of the night.
            _tally.AddWeaponHits(_night.Throwable.Id, 10);
            Debug.Log($"Piglings save: +10 {_night.Throwable.Id} hits added to tonight ({Tonight()}); they bank at night end.", this);
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
                && Time.time - _roundStartedAt >= _night.RefillPauseSeconds)
                _referee.EndPlacement();

            // Spent bombs recharge on wall time: the Rules ignore this unless the night is Running.
            _pegEffects.Advance(Time.deltaTime);
        }

        // One socket per Hold (PegBoard). No board = no sockets: every round is then just a refill pause.
        // The shelf: in the campaign, every unlocked type × the copies its mastery owns; otherwise the night's loadout.
        private PegSetup BuildPegs()
        {
            var loadout = new List<(PegType, int)>();
            _pegDefs.Clear();
            if (IsCampaign)
            {
                // A type earns one same-type follow-up per N copies; the referee gives the chain once per round.
                var followUps = new List<string>();
                foreach (var id in Plan.UnlockedPegs(_progression.Profile))
                {
                    var peg = campaign.FindPeg(id);
                    if (peg == null) continue;
                    var status = PegStatusFor(peg);
                    _pegDefs.Add(peg);
                    if (status.FollowUps > 0) followUps.Add($"{peg.Id} +{status.FollowUps}");
                    if (status.Copies > 0) loadout.Add((ToPegType(peg, status.FollowUps), status.Copies));
                }
                Debug.Log($"Piglings campaign: {_night.PegThrowsPerThreshold} peg throw(s) per round; same-type follow-ups: " +
                          (followUps.Count == 0 ? "none yet" : string.Join(", ", followUps)), this);
            }
            else
            {
                foreach (var entry in _night.PegLoadout)
                {
                    if (entry == null || entry.peg == null) continue;
                    _pegDefs.Add(entry.peg);
                    loadout.Add((ToPegType(entry.peg), entry.count));
                }
            }
            var pegs = new PegSetup(loadout, _night.PegThrowsPerThreshold, board != null ? board.SocketCount : 0);
            if (pegs.DroppedTypes > 0)
                Debug.LogWarning($"NightSession: {_night.name} brings more than {PegSetup.ShelfCapacity} peg types; " +
                                 $"{pegs.DroppedTypes} ignored (the shelf holds {PegSetup.ShelfCapacity}).", _night);
            return pegs;
        }

        /// <summary>The peg's progression rule (copy thresholds, weight per level), from its definition.</summary>
        public static PegProgression ProgressionFor(PegDefinition peg)
        {
            var weights = new float[Mathf.Max(peg.MaxLevel, peg.LevelCount)];
            for (int i = 0; i < weights.Length; i++) weights[i] = peg.MasteryWeightAt(i + 1);
            return new PegProgression(peg.CopyThresholds, weights, 1, peg.MaxCopies, peg.FollowUpEveryCopies);
        }

        /// <summary>Where an (unlocked) peg type stands, from its saved triggers: copies, the follow-up, progress to the next.</summary>
        public PegStatus PegStatusFor(PegDefinition peg)
        {
            _progression.Profile.Pegs.TryGetValue(peg.Id, out var record);
            return ProgressionFor(peg).For(record != null ? record.Triggers : null);
        }

        /// <summary>How many copies of this (unlocked) peg type the player owns, from its saved triggers.</summary>
        public int CopiesOwned(PegDefinition peg) => PegStatusFor(peg).Copies;

        // The Rules' view of a peg: id, levels, effect, the per-level numbers the Rules use (physics stays here), and whether
        // how many same-type follow-up throws it has earned (campaign).
        private PegType ToPegType(PegDefinition peg, int followUps = 0)
        {
            var multipliers = new float[peg.LevelCount];
            var pieces = new int[peg.LevelCount];
            var shares = new float[peg.LevelCount];
            var cooldowns = new float[peg.LevelCount];
            for (int i = 0; i < multipliers.Length; i++)
            {
                multipliers[i] = peg.ScoreMultiplierAt(i + 1);
                pieces[i] = peg.PiecesAt(i + 1);
                shares[i] = peg.ValueShareAt(i + 1);
                cooldowns[i] = peg.CooldownAt(i + 1);
            }
            if (peg.Effect == PegEffect.Bouncy && peg.ScoreMultiplierAt(1) <= 1f)
                Debug.LogWarning($"{peg.name}: Bouncy with a score multiplier of {peg.ScoreMultiplierAt(1)} at level 1 gives no " +
                                 "bonus — fill its Levels list (a new entry starts at 0).", peg);
            if (peg.Effect == PegEffect.Bomb && peg.BombRadiusAt(1) <= 0f)
                Debug.LogWarning($"{peg.name}: Bomb with a radius of {peg.BombRadiusAt(1)} at level 1 knocks nothing loose — fill its " +
                                 "Levels list (a new entry starts at 0).", peg);
            if (peg.Effect == PegEffect.Splitter && peg.PiecesAt(1) < 2)
                Debug.LogWarning($"{peg.name}: Splitter with {peg.PiecesAt(1)} piece(s) at level 1 never splits — fill its Levels " +
                                 "list (a new entry starts at 0).", peg);
            return new PegType(peg.Id, peg.MaxLevel, peg.Mergeable, peg.Effect, multipliers, pieces, shares,
                               peg.MaxStonesPerThrow, peg.CountSplitHitsForMastery, cooldowns, followUps);
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
            _log?.Dispose();
            _settle?.Dispose();
            _progression?.Dispose();
            _pegEffects?.Dispose();
            _tally?.Dispose();
            _referee?.Dispose();
            _chains?.Dispose();
        }
    }
}
