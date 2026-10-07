using System.Collections.Generic;
using Piglings.Events;

namespace Piglings.Rules
{
    /// <summary>
    /// Is anything still moving on the wall? The campaign flow waits for this after the night ends, so the camera only
    /// goes down to the doors once the sweep has finished — every swept robot landed, every chain closed.
    ///
    /// "Moving" = a robot that let go of the wall and isn't gone yet: knocked loose (RobotLostGrip), swept (RobotSwept)
    /// or breaching (RobotBreached — its sequence still plays). RobotRemoved takes it out, whatever the reason. Flying
    /// stones and pieces are always in a chain, so the open-chain count covers them.
    /// Climbing robots don't count: after the sweep there are none, and before it they aren't settling anything.
    /// </summary>
    public sealed class SettleTracker
    {
        private readonly EventBus _bus;
        private readonly ChainTracker _chains;
        private readonly HashSet<GameId> _moving = new HashSet<GameId>();

        public SettleTracker(EventBus bus, ChainTracker chains)
        {
            _bus = bus; _chains = chains;
            _bus.Subscribe<RobotLostGrip>(OnLostGrip);
            _bus.Subscribe<RobotSwept>(OnSwept);
            _bus.Subscribe<RobotBreached>(OnBreached);
            _bus.Subscribe<RobotRemoved>(OnRemoved);
        }

        public void Dispose()
        {
            _bus.Unsubscribe<RobotLostGrip>(OnLostGrip);
            _bus.Unsubscribe<RobotSwept>(OnSwept);
            _bus.Unsubscribe<RobotBreached>(OnBreached);
            _bus.Unsubscribe<RobotRemoved>(OnRemoved);
        }

        /// <summary>Nothing left to land: no robot in motion and no chain open.</summary>
        public bool Settled => _moving.Count == 0 && _chains.OpenChainCount == 0;

        private void OnLostGrip(RobotLostGrip e) => _moving.Add(e.Robot);
        private void OnSwept(RobotSwept e) => _moving.Add(e.Robot);
        private void OnBreached(RobotBreached e) => _moving.Add(e.Robot);
        private void OnRemoved(RobotRemoved e) => _moving.Remove(e.Robot);
    }
}
