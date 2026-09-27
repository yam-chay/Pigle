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
            public int Points;      // sum of RobotPoints so far; the multiplier is applied at close
        }

        private readonly EventBus _bus;
        private readonly NightState _state;
        private readonly ScoreCurve _curve;
        private readonly Dictionary<GameId, Open> _open = new Dictionary<GameId, Open>();

        public int OpenChainCount => _open.Count;
        public ScoreCurve Curve => _curve;

        public ChainTracker(EventBus bus, NightState state, ScoreCurve curve = null)
        {
            _bus = bus; _state = state; _curve = curve ?? new ScoreCurve();
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

            // Robot points are paid now, so the score moves while the chain is still going.
            int points = _curve.RobotPoints(e.Cause.Depth);
            c.Points += points;
            _state.Score += points;
            _bus.Publish(new RobotScored(e.Robot, e.Chain, e.Cause.Depth, points));
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

            // The multiplier's extra is paid at close: only now do we know how deep the chain got.
            // State is updated before publishing, so every listener sees the final score.
            int total = _curve.ChainTotal(c.Points, c.MaxDepth);
            _state.Score += total - c.Points;

            _bus.Publish(new ChainClosed(chain, c.Dropped, c.MaxDepth));
            _bus.Publish(new ChainScored(chain, c.Dropped, c.MaxDepth, c.Points, _curve.Multiplier(c.MaxDepth), total));
        }
    }
}