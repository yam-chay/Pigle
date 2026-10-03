using Piglings.Events;
using Piglings.Runtime;

namespace Piglings.Rules
{
    /// <summary>
    /// Runs the night's phases (GDD "סוף הלילה: להמשיך או ללכת"). The one state machine for the night:
    /// transitions only through SetPhase → EnterPhase, and NightState.Phase is the source of truth.
    ///
    ///   Running ──target reached, no chain open──▶ ChoicePending ──Stay──▶ Overtime ──▶ Ended
    ///      │                                           └────────Leave─────────────────▶ Ended
    ///      └──a robot breaches with no stones left (Caught, lost)───────────────────────▶ Ended
    ///
    /// - Running: the normal night. The stones are the pig's life. A robot that breaches takes the top
    ///   stone and leaves; a robot that breaches while the pile is empty catches the pigs — the ONLY loss.
    ///   Throwing your last stone isn't a loss: the night goes on, and a chain still in flight can reach
    ///   the target before the next breach. Once the score reaches the target the night can no longer be
    ///   lost — throwing stops and we wait for what's falling to land, so a chain is never cut.
    /// - ChoicePending: the wall pauses (Simulation reads Phase). Stay or Leave.
    /// - Overtime (Stay): throwing and the wall resume. Every chain point is doubled as it's scored
    ///   (ScoreCurve.RobotTotal, via ChainTracker) and banks to the barn as scored; the weapon earns
    ///   nothing — that's what keeps Leave worth choosing. Ends when the stones run out (after the
    ///   last chain closes) or when a robot that climbed on during overtime reaches the danger line.
    ///   Robots already on the wall at Stay can't end it (see _overtimeRobots). No loss either way.
    /// - Ended: Simulation sweeps the wall (every climbing robot falls); each one scores its own value,
    ///   flat, with the same robot-value function chains use. Then mastery is banked and NightEnded published.
    ///
    /// Owns Phase, StonesLeft, CanThrow, the breach count and the end-of-night tallies in NightState.
    /// Reads chain progress and the robot-value function from ChainTracker, which must be constructed first.
    /// </summary>
    public sealed class NightReferee
    {
        private readonly EventBus _bus;
        private readonly NightState _state;
        private readonly ChainTracker _chains;
        private readonly NightGoal _goal;

        // True while Simulation sweeps the wall inside the Ended phase change; RobotSwept only counts then.
        private bool _sweeping;

        // Robots that spawned during overtime — the only ones whose danger line ends it. Why: the wall
        // resumes exactly as it froze, so a robot parked just under the line would end overtime the instant
        // you press Stay. Robots already on the wall at Stay can't end it (they can still be knocked off
        // for double points; reaching the top costs nothing). So the earliest overtime can end is the
        // time a fresh robot takes to climb the wall: a fair, predictable window with no timer to tune.
        private readonly System.Collections.Generic.HashSet<GameId> _overtimeRobots = new System.Collections.Generic.HashSet<GameId>();

        public NightGoal Goal => _goal;

        public NightReferee(EventBus bus, NightState state, ChainTracker chains, NightGoal goal = null)
        {
            _bus = bus; _state = state; _chains = chains; _goal = goal ?? new NightGoal();
            _state.Phase = NightPhase.Running;
            _state.StonesLeft = _goal.ThrowsAvailable;
            UpdateCanThrow();

            _bus.Subscribe<ThrowReleased>(OnThrow);
            _bus.Subscribe<RobotScored>(OnRobotScored);
            _bus.Subscribe<RobotBreached>(OnRobotBreached);
            _bus.Subscribe<ChainScored>(OnChainScored);
            _bus.Subscribe<RobotEnteredDangerZone>(OnEnteredDangerZone);
            _bus.Subscribe<RobotSwept>(OnRobotSwept);
            _bus.Subscribe<RobotSpawned>(OnRobotSpawned);
        }

        public void Dispose()
        {
            _bus.Unsubscribe<ThrowReleased>(OnThrow);
            _bus.Unsubscribe<RobotScored>(OnRobotScored);
            _bus.Unsubscribe<RobotBreached>(OnRobotBreached);
            _bus.Unsubscribe<ChainScored>(OnChainScored);
            _bus.Unsubscribe<RobotEnteredDangerZone>(OnEnteredDangerZone);
            _bus.Unsubscribe<RobotSwept>(OnRobotSwept);
            _bus.Unsubscribe<RobotSpawned>(OnRobotSpawned);
        }

        // ---------- the player's choice (called by Simulation's NightChoice; ignored outside ChoicePending) ----------

        public void Stay()
        {
            if (_state.Phase != NightPhase.ChoicePending) return;
            _state.Choice = StayOrLeave.Stay;
            _bus.Publish(new NightChoiceMade(StayOrLeave.Stay));
            SetPhase(NightPhase.Overtime);
        }

        public void Leave()
        {
            if (_state.Phase != NightPhase.ChoicePending) return;
            _state.Choice = StayOrLeave.Leave;
            _bus.Publish(new NightChoiceMade(StayOrLeave.Leave));
            End(NightResult.Won, NightEndReason.Left);
        }

        // ---------- events ----------

        private bool TargetReached => _state.Score >= _goal.TargetScore;

        private void OnThrow(ThrowReleased e)
        {
            if (_state.StonesLeft > 0) ChangeStones(-1, StoneChange.Thrown, GameId.None);
            else UpdateCanThrow();
        }

        /// <summary>
        /// Stones join the pile mid-run (a pickup, a reward — nothing calls this yet). Ignored once the night is over.
        /// </summary>
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

        private void OnRobotScored(RobotScored e)
        {
            if (_state.Phase == NightPhase.Overtime)
                _state.OvertimeScore = ScoreMath.AddClamped(_state.OvertimeScore, e.Total);

            // First time over the line: throwing stops now, the choice comes once everything has landed.
            if (_state.Phase == NightPhase.Running && _state.StonesAtTarget < 0 && TargetReached)
            {
                _state.StonesAtTarget = _state.StonesLeft;
                _bus.Publish(new NightTargetReached(_state.Score, _state.StonesLeft));
            }
            UpdateCanThrow();
        }

        // Counted when the breach STARTS, not when the robot is removed at the end of it. Why: the breach
        // sequence takes time (RobotDefinition.BreachSeconds), and the outcome must be fixed at the moment the
        // player sees the robot arrive — a chain landing during the sequence can't undo a theft, and an empty
        // pile at the robot's later removal must not count as a second breach. RobotRemoved(EnteredBarn) is
        // cleanup, and this class doesn't listen to it.
        private void OnRobotBreached(RobotBreached e)
        {
            if (_state.Phase == NightPhase.Ended) return;
            _state.RobotsReachedTop++;

            switch (_state.Phase)
            {
                case NightPhase.Running when !TargetReached:
                    // The robots are clearing the way for the wolf: disarming the pig is part of it.
                    // Stones left: the robot takes the top one and leaves. None left: the pigs are caught.
                    if (_state.StonesLeft > 0) ChangeStones(-1, StoneChange.Stolen, e.Robot);
                    else End(NightResult.Lost, NightEndReason.Caught);
                    return;

                case NightPhase.Overtime:
                    // An overtime robot got to the top (it crossed the line on the way): the bonus is over.
                    // A robot that was already on the wall at Stay costs nothing. Never a loss.
                    if (_overtimeRobots.Contains(e.Robot)) End(NightResult.Won, NightEndReason.DangerLine);
                    return;

                // Running after the target, or ChoicePending: the night can't be lost any more. Counted, costs nothing.
            }
        }

        // In Running it's a warning only (views turn the robot red). In Overtime it's the line that ends the bonus.
        private void OnEnteredDangerZone(RobotEnteredDangerZone e)
        {
            if (_state.Phase == NightPhase.Overtime && _overtimeRobots.Contains(e.Robot))
                End(NightResult.Won, NightEndReason.DangerLine);
        }

        private void OnRobotSpawned(RobotSpawned e)
        {
            if (_state.Phase == NightPhase.Overtime) _overtimeRobots.Add(e.Robot);
        }

        // ChainTracker publishes this after it has closed the chain, so OpenChainCount is current.
        private void OnChainScored(ChainScored e) => CheckSettled();

        // Flat: the robot's own value, no order escalation, no depth multiplier — the same function chains use.
        private void OnRobotSwept(RobotSwept e)
        {
            if (!_sweeping) return;
            int points = _chains.Curve.RobotValue();
            _state.Score = ScoreMath.AddClamped(_state.Score, points);
            _state.SweepScore = ScoreMath.AddClamped(_state.SweepScore, points);
            // ×1 on both paths: the sweep isn't earned by throwing, and doubling it would make Stay a free win.
        }

        // Called whenever something may have finished: a chain closed, a breach took a stone, overtime began.
        private void CheckSettled()
        {
            if (_chains.OpenChainCount > 0) return;

            // Out of stones below the target is not a loss: the night goes on until a robot breaches.
            if (_state.Phase == NightPhase.Running)
            {
                if (TargetReached) SetPhase(NightPhase.ChoicePending);
            }
            else if (_state.Phase == NightPhase.Overtime && _state.StonesLeft == 0)
            {
                End(NightResult.Won, NightEndReason.OutOfStones);
            }
        }

        private void UpdateCanThrow()
        {
            switch (_state.Phase)
            {
                case NightPhase.Running: _state.CanThrow = _state.StonesLeft > 0 && !TargetReached; break;
                case NightPhase.Overtime: _state.CanThrow = _state.StonesLeft > 0; break;
                default: _state.CanThrow = false; break;   // paused for the choice, or over
            }
        }

        // ---------- the state machine ----------

        private void End(NightResult result, NightEndReason reason)
        {
            _state.Result = result;
            _state.EndReason = reason;
            // Overtime cut short by the danger line: the unthrown stones are lost.
            if (reason == NightEndReason.DangerLine && _state.StonesLeft > 0)
                ChangeStones(-_state.StonesLeft, StoneChange.Forfeited, GameId.None);
            SetPhase(NightPhase.Ended);
        }

        private void SetPhase(NightPhase next)
        {
            var from = _state.Phase;
            if (from == next || from == NightPhase.Ended) return;   // Ended is final

            _state.Phase = next;
            EnterPhase(next);

            // Simulation reacts synchronously (the bus is synchronous): pauses or resumes the wall, and on
            // Ended sweeps it — each swept robot publishes RobotSwept, scored above while _sweeping is on.
            _sweeping = next == NightPhase.Ended;
            _bus.Publish(new NightPhaseChanged(from, next));
            _sweeping = false;

            if (next == NightPhase.Ended) FinishNight();
            // Stay with no stones left: nothing to play, overtime ends at once.
            else if (next == NightPhase.Overtime) CheckSettled();
        }

        private void EnterPhase(NightPhase phase)
        {
            if (phase == NightPhase.ChoicePending) _state.ScoreAtChoice = _state.Score;
            UpdateCanThrow();
        }

        // After the sweep: bank mastery (Meta subscribes to NightBanked later), then announce the result.
        private void FinishNight()
        {
            if (_state.Result == NightResult.Won)
            {
                // Everything above the target goes to the barn as scored, on both paths. Overtime's ×2 is
                // already in those points (doubled when scored), so it isn't applied again here.
                Bank(MasteryDestination.Barn, _state.Score - _goal.TargetScore, 1);
                // Leftover stones go to the weapon only when you Leave. In overtime the weapon earns nothing:
                // unthrown stones are lost, thrown ones were worth double.
                if (_state.Choice == StayOrLeave.Leave) Bank(MasteryDestination.Weapon, _state.StonesLeft, 1);
            }

            _bus.Publish(new NightEnded(_state.Result, _state.EndReason, _state.Score, _goal.TargetScore,
                _state.StonesLeft, _state.RobotsReachedTop, _state.ThrowsUsed));
        }

        private void Bank(MasteryDestination destination, int amount, int multiplier)
        {
            if (amount <= 0) return;
            int gained = (int)System.Math.Min(int.MaxValue, (long)amount * multiplier);
            if (destination == MasteryDestination.Barn) _state.BarnMastery = ScoreMath.AddClamped(_state.BarnMastery, gained);
            else _state.WeaponMastery = ScoreMath.AddClamped(_state.WeaponMastery, gained);
            _bus.Publish(new NightBanked(destination, amount, multiplier));
        }
    }
}
