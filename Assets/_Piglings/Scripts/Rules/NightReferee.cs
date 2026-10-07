using Piglings.Events;
using Piglings.Runtime;

namespace Piglings.Rules
{
    /// <summary>
    /// Runs the night: the hours until dawn (GDD "שעות הלילה"). The one state machine for the night:
    /// transitions only through SetPhase → EnterPhase, and NightState.Phase is the source of truth.
    ///
    ///   Running(hour 1) ──threshold──▶ PegPlacement ──▶ Running(hour 2) ──▶ … ──last threshold──▶ Ended (dawn, won)
    ///      └──out of stones: none left, nothing in flight, no round to refill (lost)─────────▶ Ended
    ///
    /// - Dusk (campaign scene only, before all of the above): the night hasn't begun — nothing climbs, nothing is thrown.
    ///   Begin() → Running. With startImmediately the night starts straight in Running.
    /// - Running: the normal night. The stones are the pig's life. A robot that breaches takes the top stone (on an empty
    ///   pile it takes nothing). OUT OF STONES is the ONLY loss (M10.E), possible until dawn: no stone left, no chain in
    ///   flight and no placement round waiting (its refill would add stones). Throwing your last stone isn't the loss yet:
    ///   its chain can still cross a threshold. The night ends the moment it holds — Rules can't keep time; the wolf's
    ///   climb before the post-run is a delay in the campaign flow.
    /// - Crossing a threshold (not the last): play goes on — the wall keeps climbing and the pig keeps throwing, still
    ///   at the current hour's multiplier. The new hour (HourReached, its multiplier) starts inside the freeze, when
    ///   the placement round starts — that's the moment its visuals belong to. The placement round starts once the chains
    ///   that were in play AT the crossing have settled, so the chain that crossed is never cut. Chains thrown after
    ///   the crossing don't hold the round off (or steady throwing would postpone it forever): they keep falling and
    ///   scoring through the pause. A chain that crosses several thresholds earns one round each, back to back.
    /// - PegPlacement: the wall pauses (Simulation reads Phase). The pile gets its refill (StonesPerThreshold) and the
    ///   player gets PegThrowsPerThreshold pegs to place (PlacePeg) — plus, once per round, a chain of same-type follow-ups
    ///   after placing a type that has some (PegType.FollowUps: one per N copies; NightState.PegFollowUp restricts each next
    ///   throw to that type). The round ends when the throws are used, or —
    ///   when nothing can be placed — when Simulation calls EndPlacement after the refill pause. Unused throws are lost.
    /// - Crossing the last threshold: DawnReached. The night is won from here (no more catches); throwing stops, and
    ///   it ends once every chain has settled. No placement round at dawn, so no refill either.
    /// - Ended: Simulation sweeps the wall (every climbing robot falls). Won: each scores its own value, flat, with the
    ///   same robot-value function chains use. Lost: no score (the robots still count as dropped). Then the score and tonight's mastery tallies are banked,
    ///   and NightEnded published.
    ///
    /// Owns Phase, StonesLeft, CanThrow, the hours, the peg board and shelf, the breach count and the end-of-night
    /// tallies in NightState. Reads chain progress and the curve from ChainTracker, which must be constructed first.
    /// </summary>
    public sealed class NightReferee
    {
        private readonly EventBus _bus;
        private readonly NightState _state;
        private readonly ChainTracker _chains;
        private readonly NightGoal _goal;
        private readonly PegSetup _pegs;
        private bool _followUpGiven;   // this round's follow-up chain was already given (one chain per round)

        // True while Simulation sweeps the wall inside the Ended phase change; RobotSwept only counts then.
        private bool _sweeping;

        // One entry per round still to play, in order: the chains that were in play when its threshold was crossed.
        // A round waits for its own chains only — not for chains thrown later, nor for a later threshold's chains.
        private readonly System.Collections.Generic.Queue<System.Collections.Generic.HashSet<GameId>> _roundWaitsFor =
            new System.Collections.Generic.Queue<System.Collections.Generic.HashSet<GameId>>();

        public NightGoal Goal => _goal;
        public PegSetup Pegs => _pegs;

        /// <summary>The next threshold to cross; the last one once dawn is reached.</summary>
        public int NextThreshold => _goal.Thresholds[System.Math.Min(_state.ThresholdsReached, _goal.ThresholdCount - 1)];

        /// <summary>What chains thrown now score with.</summary>
        public float HourMultiplier => _chains.Curve.HourMultiplier(_state.Hour - 1);

        /// <param name="startImmediately">
        /// True: the night is Running from the start. False (TestNight, the campaign flow): it waits in Dusk — the day
        /// phase, the camera rising — until Begin().
        /// </param>
        public NightReferee(EventBus bus, NightState state, ChainTracker chains, NightGoal goal = null, PegSetup pegs = null,
                            bool startImmediately = true)
        {
            _bus = bus; _state = state; _chains = chains; _goal = goal ?? new NightGoal(); _pegs = pegs ?? new PegSetup();
            _state.Phase = startImmediately ? NightPhase.Running : NightPhase.Dusk;
            _state.StonesLeft = _goal.ThrowsAvailable;
            _state.Sockets = new PegSocket[_pegs.SocketCount];
            for (int i = 0; i < _state.Sockets.Length; i++) _state.Sockets[i] = new PegSocket();
            _state.Shelf.Clear();
            foreach (var (type, count) in _pegs.Loadout) _state.Shelf.Add(new PegStack { PegId = type.Id, Count = count });
            UpdateCanThrow();

            _bus.Subscribe<ThrowReleased>(OnThrow);
            _bus.Subscribe<ChainGained>(OnChainGained);
            _bus.Subscribe<RobotBreached>(OnRobotBreached);
            _bus.Subscribe<ChainScored>(OnChainScored);
            _bus.Subscribe<RobotSwept>(OnRobotSwept);
        }

        public void Dispose()
        {
            _bus.Unsubscribe<ThrowReleased>(OnThrow);
            _bus.Unsubscribe<ChainGained>(OnChainGained);
            _bus.Unsubscribe<RobotBreached>(OnRobotBreached);
            _bus.Unsubscribe<ChainScored>(OnChainScored);
            _bus.Unsubscribe<RobotSwept>(OnRobotSwept);
        }

        /// <summary>
        /// No stone left, nothing in flight, no round waiting to refill, not dawn, the night Running: the night is lost.
        /// (A pending round is checked even though, with no chain open, it would already have started: it's the reason
        /// 0 stones isn't the end yet, so it's stated.)
        /// </summary>
        public bool OutOfStones => _state.Phase == NightPhase.Running && !_state.Dawn && _state.StonesLeft == 0
                                   && _chains.OpenChainCount == 0 && _state.PendingPegRounds == 0;

        /// <summary>The night begins: Dusk → Running (the wall starts climbing, the pig may throw). Ignored in any other phase.</summary>
        public void Begin()
        {
            if (_state.Phase == NightPhase.Dusk) SetPhase(NightPhase.Running);
        }

        // ---------- pegs (called by Simulation during PegPlacement; ignored otherwise) ----------

        /// <summary>
        /// Could a peg of this type go into this socket? An empty socket, or a placed peg of the same mergeable type
        /// below its max level (= a merge). Simulation highlights these and snaps only to them.
        /// </summary>
        public bool IsValidTarget(int socket, string pegId)
        {
            if (socket < 0 || socket >= _state.Sockets.Length) return false;
            var type = _pegs.Find(pegId);
            if (type == null) return false;
            // A follow-up throw must be the type that earned it.
            if (_state.PegFollowUp != null && pegId != _state.PegFollowUp) return false;
            var s = _state.Sockets[socket];
            if (s.IsEmpty) return true;
            return s.PegId == pegId && type.Mergeable && s.Level < type.MaxLevel;
        }

        /// <summary>Pegs of this type left on the shelf.</summary>
        public int ShelfCount(string pegId)
        {
            foreach (var stack in _state.Shelf) if (stack.PegId == pegId) return stack.Count;
            return 0;
        }

        /// <summary>Is there a peg on the shelf, a throw left this round, and a socket it could go into?</summary>
        public bool CanPlaceAnyPeg
        {
            get
            {
                if (_state.Phase != NightPhase.PegPlacement || _state.PegThrowsLeft <= 0) return false;
                foreach (var stack in _state.Shelf)
                {
                    if (stack.Count <= 0) continue;
                    for (int i = 0; i < _state.Sockets.Length; i++)
                        if (IsValidTarget(i, stack.PegId)) return true;
                }
                return false;
            }
        }

        /// <summary>
        /// A peg thrown from the shelf landed on this socket. Places it (empty socket, level 1) or merges it (same type,
        /// +1 level). False = refused, nothing used. Uses one peg and one throw; the round ends when the throws run out.
        /// Never touches the stones and never starts a chain.
        /// </summary>
        public bool PlacePeg(int socket, string pegId)
        {
            if (_state.Phase != NightPhase.PegPlacement || _state.PegThrowsLeft <= 0) return false;
            if (ShelfCount(pegId) <= 0 || !IsValidTarget(socket, pegId)) return false;

            foreach (var stack in _state.Shelf) if (stack.PegId == pegId) { stack.Count--; break; }
            _state.PegThrowsLeft--;

            var s = _state.Sockets[socket];
            if (s.IsEmpty)
            {
                s.PegId = pegId;
                s.Level = 1;
                _bus.Publish(new PegPlaced(socket, pegId, s.Level));
            }
            else
            {
                s.Level++;
                _bus.Publish(new PegMerged(socket, pegId, s.Level));
            }

            // Follow-ups (stage 2): placing a type with follow-ups starts its chain — one more throw of the same type at a
            // time, as many as it has — once per round. Each is given only if that type can still be placed (else the round
            // would wait on a throw that can't happen); a chain that can't go on just ends.
            if (_state.PegFollowUp != null)
            {
                if (_state.PegFollowUpsLeft > 0 && CanPlace(pegId)) GiveFollowUp(pegId, _state.PegFollowUpsLeft - 1);
                else { _state.PegFollowUp = null; _state.PegFollowUpsLeft = 0; }
            }
            else if (!_followUpGiven && _pegs.Find(pegId).FollowUps > 0 && CanPlace(pegId))
            {
                _followUpGiven = true;
                GiveFollowUp(pegId, _pegs.Find(pegId).FollowUps - 1);
            }

            if (_state.PegThrowsLeft <= 0) EndPlacement();
            return true;
        }

        /// <summary>
        /// Debug (M11.T2, board edit): put this peg type at this level into a socket, or empty it (pegId null / ""), in any
        /// phase but Ended — no shelf, no throw, no follow-up, charged. Only types in tonight's setup (the Rules know no
        /// others); the level is clamped to the type's max. Publishes PegSocketSet. False = refused, nothing changed.
        /// </summary>
        public bool SetSocket(int socket, string pegId, int level)
        {
            if (_state.Phase == NightPhase.Ended || socket < 0 || socket >= _state.Sockets.Length) return false;
            var s = _state.Sockets[socket];
            if (string.IsNullOrEmpty(pegId))
            {
                s.PegId = null; s.Level = 0; s.Recharge = 0f;
                _bus.Publish(new PegSocketSet(socket, null, 0));
                return true;
            }
            var type = _pegs.Find(pegId);
            if (type == null) return false;
            s.PegId = pegId;
            s.Level = level < 1 ? 1 : level > type.MaxLevel ? type.MaxLevel : level;
            s.Recharge = 0f;
            _bus.Publish(new PegSocketSet(socket, pegId, s.Level));
            return true;
        }

        /// <summary>
        /// Debug (the F1 panel): end the night now — a loss (out of stones) or a dawn — so the end sequence and the post-run
        /// can be checked without playing there. Only while the night is on (Running or a placement round). A dawn counts
        /// every hour as reached (DawnReached published). Chains still falling just finish; the night ends as usual
        /// (the sweep, banking, NightEnded). False = refused.
        /// </summary>
        public bool DebugEnd(bool dawn)
        {
            if (_state.Phase != NightPhase.Running && _state.Phase != NightPhase.PegPlacement) return false;
            if (dawn)
            {
                if (!_state.Dawn)
                {
                    _state.ThresholdsReached = _goal.ThresholdCount;
                    _state.Dawn = true;
                    _bus.Publish(new DawnReached(_state.Score));
                }
                End(NightResult.Won, NightEndReason.Dawn);
            }
            else End(NightResult.Lost, NightEndReason.OutOfStones);
            return true;
        }

        private void GiveFollowUp(string pegId, int remaining)
        {
            _state.PegFollowUp = pegId;
            _state.PegFollowUpsLeft = remaining;
            _state.PegThrowsLeft++;
            _bus.Publish(new PegFollowUpGranted(pegId, remaining));
        }

        // A peg of this type is on the shelf and has a socket to go into.
        private bool CanPlace(string pegId)
        {
            if (ShelfCount(pegId) <= 0) return false;
            for (int i = 0; i < _state.Sockets.Length; i++)
                if (IsValidTarget(i, pegId)) return true;
            return false;
        }

        /// <summary>
        /// Ends the current placement round: straight into the next one if another threshold is waiting and ready,
        /// else back to Running. Called here when the throws run out, and by Simulation when nothing can be placed
        /// (after the refill pause). Unused throws are lost.
        /// </summary>
        public void EndPlacement()
        {
            if (_state.Phase != NightPhase.PegPlacement) return;
            _state.PegThrowsLeft = 0;
            _state.PegFollowUp = null;
            _state.PegFollowUpsLeft = 0;
            if (RoundReady) SetPhase(NightPhase.PegPlacement);
            else
            {
                SetPhase(NightPhase.Running);
                CheckSettled();   // dawn may have been reached by a chain that fell during the round
            }
        }

        private bool RoundReady => !_state.Dawn && _roundWaitsFor.Count > 0 && _roundWaitsFor.Peek().Count == 0;

        // ---------- stones ----------

        private void OnThrow(ThrowReleased e)
        {
            if (_state.StonesLeft > 0) ChangeStones(-1, StoneChange.Thrown, GameId.None);
            else UpdateCanThrow();
        }

        /// <summary>Stones join the pile mid-run (a pickup, a reward). Ignored once the night is over.</summary>
        public void AddStones(int count)
        {
            if (count <= 0 || _state.Phase == NightPhase.Ended) return;
            ChangeStones(count, StoneChange.Added, GameId.None);
        }

        // The one place the count changes: write it, refresh CanThrow, tell the pile.
        private void ChangeStones(int delta, StoneChange cause, GameId robot)
        {
            _state.StonesLeft = System.Math.Max(0, _state.StonesLeft + delta);
            UpdateCanThrow();
            _bus.Publish(new StonesChanged(_state.StonesLeft, delta, cause, robot));
        }

        // ---------- the hours ----------

        // ChainTracker has already added this gain's raw score to the night's score (M10.S: raw as it comes, the mult's
        // remainder at the close — OnChainScored). Thresholds are checked on those real totals, so a chain can cross one
        // mid-flight or with its remainder. Also during a placement round: a chain thrown after a crossing keeps falling
        // and scoring through it.
        private void OnChainGained(ChainGained e)
        {
            if (_state.Phase == NightPhase.Ended || e.ScoreAdded <= 0) return;
            AddToHour(e.Hour, e.ScoreAdded);
        }

        // Per hour by the hour the chain was thrown in (the hour its multiplier came from), not when the points landed.
        // One chain can cross several thresholds: each one counts (its own HourReached and, before dawn, its own round).
        private void AddToHour(int chainHour, int points)
        {
            var hour = HourStatsFor(chainHour);
            hour.Score = ScoreMath.AddClamped(hour.Score, points);
            while (!_state.Dawn && _state.Score >= _goal.Thresholds[_state.ThresholdsReached])
            {
                _state.ThresholdsReached++;
                if (_state.ThresholdsReached >= _goal.ThresholdCount)
                {
                    _state.Dawn = true;
                    _bus.Publish(new DawnReached(_state.Score));
                }
                else
                {
                    _state.PendingPegRounds++;
                    _roundWaitsFor.Enqueue(new System.Collections.Generic.HashSet<GameId>(_chains.OpenChains));
                }
            }
            UpdateCanThrow();
        }

        // Counted when the breach STARTS (RobotBreached), not when the robot is removed at the end of its sequence:
        // the outcome is fixed the moment the player sees it arrive. RobotRemoved(EnteredBarn) is cleanup, and this
        // class doesn't listen to it.
        private void OnRobotBreached(RobotBreached e)
        {
            if (_state.Phase == NightPhase.Ended) return;
            _state.RobotsReachedTop++;
            HourStatsFor(_state.Hour).Breaches++;

            // Only a running night before dawn can be hurt. (In PegPlacement the wall is frozen, so no new breach
            // starts; after dawn the night is already won.)
            if (_state.Phase != NightPhase.Running || _state.Dawn) return;

            // The robots are clearing the way for the wolf: disarming the pig is part of it — the robot takes the top
            // stone and leaves. On an empty pile there's nothing to take: the breach changes nothing (a chain still in
            // flight decides the night). Taking the last stone with nothing in flight is the loss.
            if (_state.StonesLeft <= 0) return;
            _state.StonesStolen++;
            ChangeStones(-1, StoneChange.Stolen, e.Robot);
            CheckSettled();
        }

        // ChainTracker publishes this after it has closed the chain (so OpenChainCount is current) and added the remainder
        // to the score. The remainder can cross thresholds too: their rounds wait for the chains still open (not this one).
        private void OnChainScored(ChainScored e)
        {
            if (_state.Phase != NightPhase.Ended && e.Remainder > 0) AddToHour(e.Hour, e.Remainder);
            // The best throw tonight: most points (its quality, points ÷ its hour's gap, is for the views).
            if (e.Total > _state.BestThrowPoints)
            {
                _state.BestThrowPoints = e.Total;
                _state.BestThrowHour = e.Hour;
            }
            // And per hour (the post-run's BEST THROW EACH HOUR), by the hour it was thrown in. Misses add no row.
            if (e.Total > 0)
            {
                var hour = HourStatsFor(e.Hour);
                if (e.Total > hour.BestThrow) hour.BestThrow = e.Total;
            }
            foreach (var waiting in _roundWaitsFor) waiting.Remove(e.Chain.Id);
            CheckSettled();
        }

        // Won: flat, the robot's own value (the wolf value a chain adds for it), no mult, no hour multiplier. Lost: the robots still fall (Simulation), but nothing is scored: the bank stays at the last threshold.
        private void OnRobotSwept(RobotSwept e)
        {
            if (!_sweeping || _state.Result != NightResult.Won) return;
            int points = _chains.Curve.RobotValue();
            _state.Score = ScoreMath.AddClamped(_state.Score, points);
            _state.SweepScore = ScoreMath.AddClamped(_state.SweepScore, points);
        }

        // Called whenever a chain closes, a theft takes a stone, or a round ends: is it time for dawn, for a placement
        // round, or is the pig out of stones? Dawn waits for every chain (the night ends once all is still); a round only
        // for those open at its crossing; the loss for every chain AND the round (its refill comes first).
        private void CheckSettled()
        {
            if (_state.Phase != NightPhase.Running) return;
            if (_state.Dawn)
            {
                if (_chains.OpenChainCount == 0) End(NightResult.Won, NightEndReason.Dawn);
            }
            else if (RoundReady) SetPhase(NightPhase.PegPlacement);
            else if (OutOfStones) End(NightResult.Lost, NightEndReason.OutOfStones);
        }

        // Hour n's stats (1-based), the list grown as hours are reached.
        private HourStats HourStatsFor(int hour)
        {
            int i = hour < 1 ? 0 : hour - 1;
            while (_state.Hours.Count <= i) _state.Hours.Add(new HourStats());
            return _state.Hours[i];
        }

        private void UpdateCanThrow()
        {
            // Throwing goes on after a threshold (the round doesn't wait for new chains); it stops at dawn, when the
            // night is won and only waits for what's falling.
            _state.CanThrow = _state.Phase == NightPhase.Running && _state.StonesLeft > 0 && !_state.Dawn;
        }

        // ---------- the state machine ----------

        private void End(NightResult result, NightEndReason reason)
        {
            _state.Result = result;
            _state.EndReason = reason;
            SetPhase(NightPhase.Ended);
        }

        private void SetPhase(NightPhase next)
        {
            var from = _state.Phase;
            if (from == NightPhase.Ended) return;   // Ended is final
            // PegPlacement → PegPlacement is a real change: the next round (one per threshold crossed).
            if (from == next && next != NightPhase.PegPlacement) return;

            _state.Phase = next;
            EnterPhase(next);

            // Simulation reacts synchronously (the bus is synchronous): pauses or resumes the wall, and on
            // Ended sweeps it — each swept robot publishes RobotSwept, scored above while _sweeping is on.
            _sweeping = next == NightPhase.Ended;
            _bus.Publish(new NightPhaseChanged(from, next));
            _sweeping = false;

            if (next == NightPhase.Ended) FinishNight();
        }

        private void EnterPhase(NightPhase phase)
        {
            if (phase == NightPhase.PegPlacement)
            {
                _state.PendingPegRounds--;
                _roundWaitsFor.Dequeue();
                // The new hour starts here, in the freeze: its multiplier applies to the next throw.
                _state.Hour++;
                _bus.Publish(new HourReached(_state.Hour, HourMultiplier, _goal.Thresholds[_state.Hour - 2]));
                _state.PegThrowsLeft = _pegs.ThrowsPerThreshold;
                _state.PegFollowUp = null;
                _state.PegFollowUpsLeft = 0;
                _followUpGiven = false;
                // The refill lands during the pause, where the player can watch the pile grow.
                if (_goal.StonesPerThreshold > 0) ChangeStones(_goal.StonesPerThreshold, StoneChange.Added, GameId.None);
            }
            UpdateCanThrow();
        }

        // After the sweep: bank (Meta's Progression applies NightBanked to the saved profile), then announce the result.
        private void FinishNight()
        {
            // Dawn keeps the full live score (sweep included). Out of stones keeps only the last threshold reached:
            // the hour you finished is yours, the one you were in is lost.
            _state.BankedScore = _state.Result == NightResult.Won
                ? _state.Score
                : _goal.ScoreAtThreshold(_state.ThresholdsReached);
            Bank(MasteryDestination.Barn, null, MasteryStat.Score, _state.BankedScore, 1);

            // Mastery from use: banked in full on BOTH outcomes — unlike the score, a loss takes none of it away.
            foreach (var hits in _state.WeaponHits) Bank(MasteryDestination.Weapon, hits.Key, MasteryStat.DirectHits, hits.Value, 1);
            foreach (var knocks in _state.BallKnocks) Bank(MasteryDestination.Lineage, knocks.Key, MasteryStat.BallKnocks, knocks.Value, 1);
            foreach (var knocked in _state.KnockedByBall) Bank(MasteryDestination.Lineage, knocked.Key, MasteryStat.KnockedByBall, knocked.Value, 1);
            foreach (var dropped in _state.Dropped) Bank(MasteryDestination.Lineage, dropped.Key, MasteryStat.Dropped, dropped.Value, 1);
            foreach (var swept in _state.Swept) Bank(MasteryDestination.Lineage, swept.Key, MasteryStat.Swept, swept.Value, 1);
            foreach (var peg in _state.PegKnocks) Bank(MasteryDestination.Peg, peg.Key, MasteryStat.PegKnocks, peg.Value, 1);
            foreach (var peg in _state.PegTriggers)
                for (int i = 0; i < peg.Value.Count; i++)
                    Bank(MasteryDestination.Peg, peg.Key, MasteryStat.PegTriggers, peg.Value[i], 1, level: i + 1);

            _bus.Publish(new NightEnded(_state.Result, _state.EndReason, _state.Score, _state.BankedScore,
                _state.ThresholdsReached, _state.StonesLeft, _state.RobotsReachedTop, _state.ThrowsUsed));
        }

        private void Bank(MasteryDestination destination, string id, MasteryStat stat, int amount, int multiplier, int level = 0)
        {
            if (amount <= 0) return;
            int gained = (int)System.Math.Min(int.MaxValue, (long)amount * multiplier);
            if (destination == MasteryDestination.Barn) _state.BarnMastery = ScoreMath.AddClamped(_state.BarnMastery, gained);
            _bus.Publish(new NightBanked(destination, id, stat, amount, multiplier, level));
        }
    }
}
