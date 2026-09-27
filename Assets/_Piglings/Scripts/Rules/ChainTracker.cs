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
            public int Total;       // sum of the RobotScored totals; ChainScored reports it

            // What each hitter of this chain (the stone, and every robot knocked loose) carries into
            // its next hit. Lives with the chain, so it's forgotten when the chain closes.
            public readonly Dictionary<GameId, int> Carried = new Dictionary<GameId, int>();
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
            c.Carried[e.Throwable] = _curve.StoneValue;
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

            // Paid in full right now: the hitter's carried value is known the moment this robot
            // loses grip. A hitter we don't know (e.g. a future hazard) counts as a fresh stone.
            int order = c.Dropped;   // already counts this robot, so the first is 1
            int depth = e.Cause.Depth;
            if (!c.Carried.TryGetValue(e.Cause.Source, out int received)) received = _curve.StoneValue;

            int total = _curve.RobotTotal(received, depth);
            int carries = _curve.CarriedBy(received, total);
            c.Carried[e.Cause.Source] = _curve.HitterAfterHit(received);   // its next hit is worth more
            c.Carried[e.Robot] = carries;                                     // this robot is now a stone too

            c.Total = AddClamped(c.Total, total);
            _state.Score = AddClamped(_state.Score, total);
            _bus.Publish(new RobotScored(e.Robot, e.Chain, order, depth, received, _curve.Multiplier(depth), total, carries));
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

            // Nothing is paid here: every robot was scored as it fell, so this is just the sum.
            _bus.Publish(new ChainClosed(chain, c.Dropped, c.MaxDepth));
            _bus.Publish(new ChainScored(chain, c.Dropped, c.MaxDepth, c.Total));
        }

        // Scores can get huge with CarryScoredTotal on; saturate rather than wrap negative.
        private static int AddClamped(int a, int b) => (int)System.Math.Min(int.MaxValue, (long)a + b);
    }
}