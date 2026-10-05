using System.Collections.Generic;
using Piglings.Events;
using Piglings.Runtime;

namespace Piglings.Rules
{
    /// <summary>
    /// Counts what tonight's use is worth to mastery, into NightState (WeaponHits, BallKnocks, KnockedByBall).
    /// NightReferee banks those counts when the night ends; Meta applies them to the saved profile.
    ///
    /// - A weapon hit = RobotLostGrip caused by the thrown weapon itself (Attribution.FromThrowable). One throw can knock
    ///   several robots: each one counts.
    /// - A robot-ball knock (FromRobotBall) never counts for the weapon. It's recorded from both sides, per robot type:
    ///   the knocker's BallKnocks and the knocked robot's KnockedByBall.
    /// - The end-of-night sweep isn't a RobotLostGrip (it's RobotSwept), so it never counts for mastery.
    /// - Dropped (M10.E, per robot type): every robot knocked off the wall tonight, whatever did it — a RobotLostGrip of any
    ///   cause (stone, ball, bomb, or none we know) and the end-of-night sweep (RobotSwept while the night ends, before it's
    ///   banked). For the post-run's "wolves dropped" and later lineage progression. The sweep's share is also counted on
    ///   its own (Swept), so a later rule can tell knocked-in-play (Dropped − Swept) from swept.
    ///
    /// - A Bomb's victims (CauseKind.Peg) count for the peg type that exploded (PegKnocks), whatever set it off — and as
    ///   stone hits only when the stone itself (or a piece that counts) set it off by hitting it. A ball-triggered
    ///   explosion gives the stone nothing.
    /// - Peg triggers (PegTriggers, per peg id and merged level): each time a peg's effect fires — a Bouncy bonus granted
    ///   (already once per peg per ball), a Splitter split, a Bomb explosion. Peg mastery (copies owned) is derived from them.
    /// - A Splitter piece's direct hits count as its stone's hits when the split said so (StoneSplit.PiecesCountForMastery,
    ///   the Splitter's toggle); otherwise only the original stone's own hits count.
    ///
    /// Events carry GameIds; the definition ids come from ThrowReleased.Weapon and RobotSpawned.RobotType, kept here
    /// until the thing leaves play.
    /// </summary>
    public sealed class MasteryTally
    {
        private readonly EventBus _bus;
        private readonly NightState _state;
        private readonly Dictionary<GameId, string> _weapons = new Dictionary<GameId, string>();
        private readonly Dictionary<GameId, string> _robotTypes = new Dictionary<GameId, string>();
        private readonly Dictionary<GameId, bool> _piecesCount = new Dictionary<GameId, bool>();   // stone → do its pieces count?
        private readonly Dictionary<GameId, string> _explosionPegs = new Dictionary<GameId, string>();    // explosion → peg id
        private readonly Dictionary<GameId, string> _explosionWeapons = new Dictionary<GameId, string>(); // explosion → stone's weapon
        private bool _banked;   // NightEnded came: anything after it would never be saved

        public MasteryTally(EventBus bus, NightState state)
        {
            _bus = bus; _state = state;
            _bus.Subscribe<ThrowReleased>(OnThrow);
            _bus.Subscribe<RobotSpawned>(OnSpawned);
            _bus.Subscribe<RobotLostGrip>(OnLostGrip);
            _bus.Subscribe<ThrowableRemoved>(OnThrowableRemoved);
            _bus.Subscribe<RobotRemoved>(OnRobotRemoved);
            _bus.Subscribe<StoneSplit>(OnSplit);
            _bus.Subscribe<StonePieceLaunched>(OnPieceLaunched);
            _bus.Subscribe<BombExploded>(OnBombExploded);
            _bus.Subscribe<PegBounced>(OnPegBounced);
            _bus.Subscribe<RobotSwept>(OnSwept);
            _bus.Subscribe<NightEnded>(OnNightEnded);
        }

        public void Dispose()
        {
            _bus.Unsubscribe<ThrowReleased>(OnThrow);
            _bus.Unsubscribe<RobotSpawned>(OnSpawned);
            _bus.Unsubscribe<RobotLostGrip>(OnLostGrip);
            _bus.Unsubscribe<ThrowableRemoved>(OnThrowableRemoved);
            _bus.Unsubscribe<RobotRemoved>(OnRobotRemoved);
            _bus.Unsubscribe<StoneSplit>(OnSplit);
            _bus.Unsubscribe<StonePieceLaunched>(OnPieceLaunched);
            _bus.Unsubscribe<BombExploded>(OnBombExploded);
            _bus.Unsubscribe<PegBounced>(OnPegBounced);
            _bus.Unsubscribe<RobotSwept>(OnSwept);
            _bus.Unsubscribe<NightEnded>(OnNightEnded);
        }

        /// <summary>
        /// Debug (playtesting): adds hits to tonight's tally, as if they were real — so they bank, save and show like
        /// real ones. Ignored once the night has ended (it's already banked).
        /// </summary>
        public void AddWeaponHits(string weapon, int count)
        {
            if (count <= 0 || _state.Ended) return;
            Add(_state.WeaponHits, weapon, count);
        }

        private void OnThrow(ThrowReleased e) => _weapons[e.Throwable] = e.Weapon;
        private void OnSpawned(RobotSpawned e) => _robotTypes[e.Robot] = e.RobotType;
        private void OnThrowableRemoved(ThrowableRemoved e)
        {
            _weapons.Remove(e.Throwable);
            _piecesCount.Remove(e.Throwable);
        }

        // A piece is credited to its parent's weapon only if this split counts — and only if the parent itself counts
        // (a piece of an uncounted piece stays uncounted).
        private void OnSplit(StoneSplit e)
        {
            TriggerAt(e.Socket);
            _piecesCount[e.Stone] = e.PiecesCountForMastery;
        }

        private void OnPegBounced(PegBounced e) => TriggerAt(e.Socket);

        // Splitter and Bouncy events carry the socket: the peg there now (written by the referee) is the one that fired.
        private void TriggerAt(int socket)
        {
            if (socket < 0 || socket >= _state.Sockets.Length || _state.Sockets[socket].IsEmpty) return;
            Trigger(_state.Sockets[socket].PegId, _state.Sockets[socket].Level);
        }

        private void Trigger(string pegId, int level)
        {
            if (_state.Ended || string.IsNullOrEmpty(pegId)) return;
            if (!_state.PegTriggers.TryGetValue(pegId, out var perLevel)) _state.PegTriggers[pegId] = perLevel = new List<int>();
            int i = level < 1 ? 0 : level - 1;
            while (perLevel.Count <= i) perLevel.Add(0);
            perLevel[i] = ScoreMath.AddClamped(perLevel[i], 1);
        }

        // Explosions live only as long as their knocks (synchronous, inside the Simulation's handling of the hit), but
        // nights are short and an explosion is rare: these maps aren't worth pruning.
        private void OnBombExploded(BombExploded e)
        {
            Trigger(e.PegId, e.Level);
            _explosionPegs[e.Explosion] = e.PegId;
            // _weapons only holds stones that count (a piece whose split didn't count isn't in it), so this one lookup
            // is the whole rule: the stone or a counted piece → the stone's hits; a ball → nothing.
            if (e.Trigger == PegHitter.Stone && _weapons.TryGetValue(e.TriggerId, out var weapon)) _explosionWeapons[e.Explosion] = weapon;
        }

        private void OnPieceLaunched(StonePieceLaunched e)
        {
            if (_piecesCount.TryGetValue(e.Parent, out bool counts) && counts && _weapons.TryGetValue(e.Parent, out var weapon))
                _weapons[e.Piece] = weapon;
        }

        private void OnNightEnded(NightEnded e) => _banked = true;

        // The sweep runs inside the end (the phase is already Ended) but before the night is banked, so its robots still make
        // it into tonight's Dropped. A RobotSwept at any other time isn't the sweep.
        private void OnSwept(RobotSwept e)
        {
            if (_state.Phase != NightPhase.Ended || _banked) return;
            if (!_robotTypes.TryGetValue(e.Robot, out var type)) return;
            Add(_state.Dropped, type, 1);
            Add(_state.Swept, type, 1);
        }

        // A robot's own removal comes after anything its ball could knock, so its type isn't needed past this point.
        private void OnRobotRemoved(RobotRemoved e) => _robotTypes.Remove(e.Robot);

        private void OnLostGrip(RobotLostGrip e)
        {
            // Banking happened when the night ended: anything counted after it would never be saved, and NightState
            // would disagree with what was banked.
            if (_state.Ended) return;

            if (_robotTypes.TryGetValue(e.Robot, out var droppedType)) Add(_state.Dropped, droppedType, 1);

            if (e.Cause.Kind == CauseKind.Throwable)
            {
                if (_weapons.TryGetValue(e.Cause.Source, out var weapon)) Add(_state.WeaponHits, weapon, 1);
            }
            else if (e.Cause.Kind == CauseKind.RobotBall)
            {
                if (_robotTypes.TryGetValue(e.Cause.Source, out var knocker)) Add(_state.BallKnocks, knocker, 1);
                if (_robotTypes.TryGetValue(e.Robot, out var knocked)) Add(_state.KnockedByBall, knocked, 1);
            }
            else if (e.Cause.Kind == CauseKind.Peg)
            {
                if (_explosionPegs.TryGetValue(e.Cause.Source, out var peg)) Add(_state.PegKnocks, peg, 1);
                if (_explosionWeapons.TryGetValue(e.Cause.Source, out var weapon)) Add(_state.WeaponHits, weapon, 1);
            }
        }

        // An id we don't have (no definition id) can't be saved against anything, so it isn't counted.
        private static void Add(SortedDictionary<string, int> counts, string id, int amount)
        {
            if (string.IsNullOrEmpty(id)) return;
            counts.TryGetValue(id, out int now);
            counts[id] = ScoreMath.AddClamped(now, amount);
        }
    }
}
