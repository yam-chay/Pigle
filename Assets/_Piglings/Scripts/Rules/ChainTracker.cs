using System.Collections.Generic;
using Piglings.Events;
using Piglings.Runtime;

namespace Piglings.Rules
{
    /// <summary>
    /// Turns raw events into chain results. A chain stays open while its throwable or any
    /// robot it knocked loose is still in play; when the last one is removed, ChainClosed fires.
    /// </summary>
    public sealed class ChainTracker
    {
        private sealed class Open
        {
            public readonly HashSet<GameId> InPlay = new HashSet<GameId>();
            public int Dropped;
            public int MaxDepth;
        }

        private readonly EventBus _bus;
        private readonly NightState _state;
        private readonly Dictionary<GameId, Open> _open = new Dictionary<GameId, Open>();

        public int OpenChainCount => _open.Count;

        public ChainTracker(EventBus bus, NightState state)
        {
            _bus = bus; _state = state;
            _bus.Subscribe<ThrowReleased>(OnThrow);
            _bus.Subscribe<RobotLostGrip>(OnLostGrip);
            _bus.Subscribe<RobotRemoved>(OnRobotRemoved);
            _bus.Subscribe<ThrowableRemoved>(OnThrowableRemoved);
        }

        public void Dispose()
        {
            _bus.Unsubscribe<ThrowReleased>(OnThrow);
            _bus.Unsubscribe<RobotLostGrip>(OnLostGrip);
            _bus.Unsubscribe<RobotRemoved>(OnRobotRemoved);
            _bus.Unsubscribe<ThrowableRemoved>(OnThrowableRemoved);
        }

        private void OnThrow(ThrowReleased e)
        {
            var c = new Open();
            c.InPlay.Add(e.Throwable);
            _open[e.Chain.Id] = c;
            _state.ThrowsUsed++;
        }

        private void OnLostGrip(RobotLostGrip e)
        {
            if (!_open.TryGetValue(e.Chain.Id, out var c)) return;
            c.InPlay.Add(e.Robot);
            c.Dropped++;
            if (e.Cause.Depth > c.MaxDepth) c.MaxDepth = e.Cause.Depth;
            _state.RobotsDropped++;
            _state.Score += ScoreFor(e.Cause.Depth);
        }

        private void OnRobotRemoved(RobotRemoved e)
        {
            if (e.Reason == RemovalReason.EnteredBarn) _state.RobotsReachedTop++;
            Release(e.Chain, e.Robot);
        }

        private void OnThrowableRemoved(ThrowableRemoved e) => Release(e.Chain, e.Throwable);

        private void Release(ChainId chain, GameId member)
        {
            if (chain.IsNone || !_open.TryGetValue(chain.Id, out var c)) return;
            c.InPlay.Remove(member);
            if (c.InPlay.Count > 0) return;

            _open.Remove(chain.Id);
            if (c.Dropped > _state.LongestChain) _state.LongestChain = c.Dropped;
            if (c.MaxDepth > _state.DeepestChain) _state.DeepestChain = c.MaxDepth;
            _bus.Publish(new ChainClosed(chain, c.Dropped, c.MaxDepth));
        }

        /// <summary>Deeper = worth more. Placeholder curve — tune in play.</summary>
        public static int ScoreFor(int depth) => 10 * (depth + 1);
    }
}