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
    ///      └──out of stones / too many breaches (lost)─────────────────────────────────▶ Ended
    ///
    /// - Running: the normal night. A breach costs stones; too many breaches, or running out of stones
    ///   below the target, loses. Once the score reaches the target the night can no longer be lost —
    ///   throwing stops and we wait for what's falling to land, so a chain is never cut.
    /// - ChoicePending: the wall pauses (Simulation reads Phase). Stay or Leave.
    /// - Overtime (Stay): throwing and the wall resume. Everything scored banks ×2 to the barn; the
    ///   weapon earns nothing — that's what keeps Leave worth choosing. Ends when the stones run out
    ///   (after the last chain closes) or when a robot reaches the danger line. No loss either way.
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

        public NightGoal Goal => _goal;

        public NightReferee(EventBus bus, NightState state, ChainTracker chains, NightGoal goal = null)
        {
            _bus = bus; _state = state; _chains = chains; _goal = goal ?? new NightGoal();
            _state.Phase = NightPhase.Running;
            _state.StonesLeft = _goal.ThrowsAvailable;
            UpdateCanThrow();

            _bus.Subscribe<ThrowReleased>(OnThrow);
            _bus.Subscribe<RobotScored>(OnRobotScored);
            _bus.Subscribe<RobotRemoved>(OnRobotRemoved);
            _bus.Subscribe<ChainScored>(OnChainScored);
            _bus.Subscribe<RobotEnteredDangerZone>(OnEnteredDangerZone);
            _bus.Subscribe<RobotSwept>(OnRobotSwept);
        }

        public void Dispose()
        {
            _bus.Unsubscribe<ThrowReleased>(OnThrow);
            _bus.Unsubscribe<RobotScored>(OnRobotScored);
            _bus.Unsubscribe<RobotRemoved>(OnRobotRemoved);
            _bus.Unsubscribe<ChainScored>(OnChainScored);
            _bus.Unsubscribe<RobotEnteredDangerZone>(OnEnteredDangerZone);
            _bus.Unsubscribe<RobotSwept>(OnRobotSwept);
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
            if (_state.StonesLeft > 0) _state.StonesLeft--;
            UpdateCanThrow();
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

        private void OnRobotRemoved(RobotRemoved e)
        {
            if (e.Reason != RemovalReason.EnteredBarn || _state.Phase == NightPhase.Ended) return;
            _state.RobotsReachedTop++;

            switch (_state.Phase)
            {
                case NightPhase.Running when !TargetReached:
                    _state.StonesLeft = System.Math.Max(0, _state.StonesLeft - _goal.StonesLostPerBreach);
                    UpdateCanThrow();
                    if (_goal.MaxBreaches > 0 && _state.RobotsReachedTop >= _goal.MaxBreaches)
                    {
                        End(NightResult.Lost, NightEndReason.BarnBreached);
                        return;
                    }
                    // A breach can take the last stone while nothing is in flight: nothing left can score.
                    CheckSettled();
                    return;

                case NightPhase.Overtime:
                    // A robot that was already past the danger line when overtime began made it to the top:
                    // it crossed the line during overtime, so the bonus is over. Still no loss.
                    End(NightResult.Won, NightEndReason.DangerLine);
                    return;

                // Running after the target, or ChoicePending: the night can't be lost any more. Counted, costs nothing.
            }
        }

        // In Running it's a warning only (views turn the robot red). In Overtime it's the line that ends the bonus.
        private void OnEnteredDangerZone(RobotEnteredDangerZone e)
        {
            if (_state.Phase == NightPhase.Overtime) End(NightResult.Won, NightEndReason.DangerLine);
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
            // In the Stay path the sweep is still overtime: it banks ×2 like the rest of it.
            if (_state.Choice == StayOrLeave.Stay)
                _state.OvertimeScore = ScoreMath.AddClamped(_state.OvertimeScore, points);
        }

        // Called whenever something may have finished: a chain closed, a breach took a stone, overtime began.
        private void CheckSettled()
        {
            if (_chains.OpenChainCount > 0) return;

            if (_state.Phase == NightPhase.Running)
            {
                if (TargetReached) SetPhase(NightPhase.ChoicePending);
                else if (_state.StonesLeft == 0) End(NightResult.Lost, NightEndReason.OutOfStones);
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
            if (reason == NightEndReason.DangerLine) _state.StonesLeft = 0;
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
            // Stay with no stones left: nothing to play, overtime ends at once (the sweep still banks ×2).
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
                if (_state.Choice == StayOrLeave.Leave)
                {
                    Bank(MasteryDestination.Barn, _state.Score - _goal.TargetScore, 1);   // sweep included
                    Bank(MasteryDestination.Weapon, _state.StonesLeft, 1);                // the leftover stones
                }
                else if (_state.Choice == StayOrLeave.Stay)
                {
                    Bank(MasteryDestination.Barn, _state.ScoreAtChoice - _goal.TargetScore, 1);
                    Bank(MasteryDestination.Barn, _state.OvertimeScore, 2);               // sweep included
                    // No weapon mastery from overtime: unthrown stones are lost, thrown ones earned the barn ×2.
                }
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
