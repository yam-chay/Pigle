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
            public int Hour;               // the hour its stone was thrown in (views colour by it)
            public float HourMultiplier;   // fixed at the throw: what every robot of this chain scores with

            // What each hitter of this chain (the stone, and every robot knocked loose) carries into
            // its next hit. Lives with the chain, so it's forgotten when the chain closes.
            public readonly Dictionary<GameId, int> Carried = new Dictionary<GameId, int>();
        }

        private readonly EventBus _bus;
        private readonly NightState _state;
        private readonly ScoreCurve _curve;
        private readonly Dictionary<GameId, Open> _open = new Dictionary<GameId, Open>();

        public int OpenChainCount => _open.Count;
        /// <summary>The chains still in play right now (ids).</summary>
        public IEnumerable<GameId> OpenChains => _open.Keys;
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
            // Fixed at the throw. Throwing stops the moment a threshold is crossed and only resumes in the next
            // hour, so a chain never straddles two hours — but recording it here makes that a fact, not a hope.
            c.Hour = _state.Hour;
            c.HourMultiplier = _curve.HourMultiplier(_state.ThresholdsReached);
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

            // The hour multiplies what this robot scores (RobotTotal applies it), but not what it passes on:
            // carrying uses the hour-1 value, so the hour is paid once per robot and never compounds down a chain.
            int total = _curve.RobotTotal(received, depth, c.HourMultiplier);
            int carries = _curve.CarriedBy(received, _curve.RobotTotal(received, depth));
            c.Carried[e.Cause.Source] = _curve.HitterAfterHit(received);   // its next hit is worth more
            c.Carried[e.Robot] = carries;                                     // this robot is now a stone too

            c.Total = ScoreMath.AddClamped(c.Total, total);
            _state.Score = ScoreMath.AddClamped(_state.Score, total);
            _bus.Publish(new RobotScored(e.Robot, e.Chain, order, depth, received, _curve.Multiplier(depth), c.Hour, c.HourMultiplier,
                total, carries));
        }

        private void OnRobotRemoved(RobotRemoved e)
        {
            // Breaches are counted by NightReferee on RobotBreached; a removal only releases its chain here.
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
            _bus.Publish(new ChainScored(chain, c.Dropped, c.MaxDepth, c.Total, c.Hour));
        }
    }
}