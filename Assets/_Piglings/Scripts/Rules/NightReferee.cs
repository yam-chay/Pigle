using Piglings.Events;
using Piglings.Runtime;

namespace Piglings.Rules
{
    /// <summary>
    /// Decides the night. Publishes NightEnded exactly once:
    ///  - Won:  score reached the target, and every chain in flight has settled
    ///          (so the chain that crossed the line pays out in full).
    ///  - Lost: out of stones and every chain settled below target, or
    ///          too many robots reached the top (MaxBreaches).
    ///
    /// Why a breach costs a stone: it ties defence to offence. Stones are the one resource both
    /// spend — ignoring the wall shrinks your score ceiling, and a chain that clears the wall
    /// does both jobs at once.
    ///
    /// Owns StonesLeft, CanThrow, the breach count (RobotsReachedTop) and the result in NightState.
    /// Reads chain progress from ChainTracker, which must be constructed first.
    /// </summary>
    public sealed class NightReferee
    {
        private readonly EventBus _bus;
        private readonly NightState _state;
        private readonly ChainTracker _chains;
        private readonly NightGoal _goal;

        public NightGoal Goal => _goal;

        public NightReferee(EventBus bus, NightState state, ChainTracker chains, NightGoal goal = null)
        {
            _bus = bus; _state = state; _chains = chains; _goal = goal ?? new NightGoal();
            _state.StonesLeft = _goal.ThrowsAvailable;
            UpdateCanThrow();

            _bus.Subscribe<ThrowReleased>(OnThrow);
            _bus.Subscribe<RobotScored>(OnRobotScored);
            _bus.Subscribe<RobotRemoved>(OnRobotRemoved);
            _bus.Subscribe<ChainScored>(OnChainScored);
        }

        public void Dispose()
        {
            _bus.Unsubscribe<ThrowReleased>(OnThrow);
            _bus.Unsubscribe<RobotScored>(OnRobotScored);
            _bus.Unsubscribe<RobotRemoved>(OnRobotRemoved);
            _bus.Unsubscribe<ChainScored>(OnChainScored);
        }

        private bool TargetReached => _state.Score >= _goal.TargetScore;

        private void OnThrow(ThrowReleased e)
        {
            if (_state.StonesLeft > 0) _state.StonesLeft--;
            UpdateCanThrow();
        }

        // Score moves as robots fall; once it passes the target, throwing stops right away,
        // so the night can end as soon as what's already falling has settled.
        private void OnRobotScored(RobotScored e) => UpdateCanThrow();

        private void OnRobotRemoved(RobotRemoved e)
        {
            if (e.Reason != RemovalReason.EnteredBarn || _state.Ended) return;

            _state.RobotsReachedTop++;
            _state.StonesLeft = System.Math.Max(0, _state.StonesLeft - _goal.StonesLostPerBreach);
            UpdateCanThrow();

            if (_goal.MaxBreaches > 0 && _state.RobotsReachedTop >= _goal.MaxBreaches)
            {
                End(NightResult.Lost, NightEndReason.BarnBreached);
                return;
            }

            // A breach can take the last stone while nothing is in flight: nothing left can score.
            CheckSettled();
        }

        // ChainTracker publishes this after it has closed the chain, so OpenChainCount is current.
        private void OnChainScored(ChainScored e) => CheckSettled();

        private void CheckSettled()
        {
            if (_state.Ended || _chains.OpenChainCount > 0) return;

            if (TargetReached) End(NightResult.Won, NightEndReason.TargetReached);
            else if (_state.StonesLeft == 0) End(NightResult.Lost, NightEndReason.OutOfStones);
        }

        private void UpdateCanThrow() => _state.CanThrow = !_state.Ended && _state.StonesLeft > 0 && !TargetReached;

        private void End(NightResult result, NightEndReason reason)
        {
            _state.Ended = true;
            _state.Result = result;
            _state.EndReason = reason;
            UpdateCanThrow();
            _bus.Publish(new NightEnded(result, reason, _state.Score, _goal.TargetScore, _state.StonesLeft, _state.RobotsReachedTop));
        }
    }
}
