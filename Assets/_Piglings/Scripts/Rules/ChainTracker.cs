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

            // A hitter's extra peg multiplier (the part above ×1) from the Bouncy pegs it bounced off. Same lifetime.
            public readonly Dictionary<GameId, float> PegExtra = new Dictionary<GameId, float>();

            // The thrown stones of this chain still flying: the original and any Splitter pieces (InPlay holds robots too).
            public readonly HashSet<GameId> Stones = new HashSet<GameId>();

            // How deep each robot of this chain is (a Bomb set off by its ball knocks robots one deeper).
            public readonly Dictionary<GameId, int> Depths = new Dictionary<GameId, int>();
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
            _bus.Subscribe<StonePieceLaunched>(OnPieceLaunched);
        }

        public void Dispose()
        {
            _bus.Unsubscribe<ThrowReleased>(OnThrow);
            _bus.Unsubscribe<RobotLostGrip>(OnLostGrip);
            _bus.Unsubscribe<RobotRemoved>(OnRobotRemoved);
            _bus.Unsubscribe<ThrowableRemoved>(OnThrowableRemoved);
            _bus.Unsubscribe<StonePieceLaunched>(OnPieceLaunched);
        }

        /// <summary>Is this chain still in play? (A swept robot falls with ChainId.None, which never is.)</summary>
        public bool IsOpen(ChainId chain) => !chain.IsNone && _open.ContainsKey(chain.Id);

        /// <summary>
        /// A Bouncy peg gave this hitter extra (its multiplier − 1). Pegs add on the extra part, so two ×2 pegs make ×3.
        /// It multiplies what the hitter's victims score from now on, never what they carry. Returns the hitter's total
        /// peg multiplier now; 1 if the chain isn't open (nothing to score into).
        /// </summary>
        public float AddPegExtra(ChainId chain, GameId hitter, float extra)
        {
            if (chain.IsNone || !_open.TryGetValue(chain.Id, out var c)) return 1f;
            c.PegExtra.TryGetValue(hitter, out float now);
            if (extra > 0f) c.PegExtra[hitter] = now += extra;
            return 1f + now;
        }

        /// <summary>How many stones of this chain are flying right now (the original and any pieces). 0 if it isn't open.</summary>
        public int StonesInFlight(ChainId chain) =>
            !chain.IsNone && _open.TryGetValue(chain.Id, out var c) ? c.Stones.Count : 0;

        /// <summary>
        /// Splitter: a stone's value is shared out — it now carries its current value × share (its pieces copy that when
        /// they launch). Rounded once; share 1 changes nothing.
        /// </summary>
        public void ShareStoneValue(ChainId chain, GameId stone, float share)
        {
            if (chain.IsNone || !_open.TryGetValue(chain.Id, out var c)) return;
            if (!c.Carried.TryGetValue(stone, out int value)) value = _curve.StoneValue;
            double shared = System.Math.Round(value * (double)System.Math.Max(0f, share), System.MidpointRounding.AwayFromZero);
            c.Carried[stone] = shared >= int.MaxValue ? int.MaxValue : (int)shared;
        }

        /// <summary>A robot's depth in this chain (from its RobotLostGrip), or -1 if it isn't one of the chain's robots.</summary>
        public int DepthOf(ChainId chain, GameId robot) =>
            !chain.IsNone && _open.TryGetValue(chain.Id, out var c) && c.Depths.TryGetValue(robot, out int d) ? d : -1;

        /// <summary>
        /// Bomb: the explosion becomes a hitter of this chain, starting with what its trigger carries now. It grows with
        /// each robot it knocks loose, like a stone that hits several; the trigger itself doesn't grow from those.
        /// </summary>
        public void StartExplosion(ChainId chain, GameId explosion, GameId trigger)
        {
            if (chain.IsNone || !_open.TryGetValue(chain.Id, out var c)) return;
            if (!c.Carried.TryGetValue(trigger, out int value)) value = _curve.StoneValue;
            c.Carried[explosion] = value;
        }

        // A piece joins its parent's chain: the chain now waits for it too, and it carries what its parent carries.
        private void OnPieceLaunched(StonePieceLaunched e)
        {
            if (!_open.TryGetValue(e.Chain.Id, out var c)) return;
            c.InPlay.Add(e.Piece);
            c.Stones.Add(e.Piece);
            if (!c.Carried.TryGetValue(e.Parent, out int value)) value = _curve.StoneValue;
            c.Carried[e.Piece] = value;
        }

        private void OnThrow(ThrowReleased e)
        {
            var c = new Open();
            c.InPlay.Add(e.Throwable);
            c.Stones.Add(e.Throwable);
            // Fixed at the throw: a chain scores at the hour it was thrown in, even if the next hour starts (in a
            // placement round) while it's still falling.
            c.Hour = _state.Hour;
            c.HourMultiplier = _curve.HourMultiplier(_state.Hour - 1);
            c.Carried[e.Throwable] = _curve.StoneValue;
            _open[e.Chain.Id] = c;
            _state.ThrowsUsed++;
        }

        private void OnLostGrip(RobotLostGrip e)
        {
            if (!_open.TryGetValue(e.Chain.Id, out var c)) return;
            c.InPlay.Add(e.Robot);
            c.Depths[e.Robot] = e.Cause.Depth;
            c.Dropped++;
            if (e.Cause.Depth > c.MaxDepth) c.MaxDepth = e.Cause.Depth;
            _state.RobotsDropped++;

            // Paid in full right now: the hitter's carried value is known the moment this robot
            // loses grip. A hitter we don't know (e.g. a future hazard) counts as a fresh stone.
            int order = c.Dropped;   // already counts this robot, so the first is 1
            int depth = e.Cause.Depth;
            if (!c.Carried.TryGetValue(e.Cause.Source, out int received)) received = _curve.StoneValue;

            // The hour and the Bouncy pegs multiply what this robot scores (RobotTotal applies them), but not what it passes
            // on: carrying uses the plain value, so each is paid once per robot and never compounds down a chain.
            c.PegExtra.TryGetValue(e.Cause.Source, out float pegExtra);
            float pegMultiplier = 1f + pegExtra;
            int total = _curve.RobotTotal(received, depth, c.HourMultiplier, pegMultiplier);
            int carries = _curve.CarriedBy(received, _curve.RobotTotal(received, depth));
            c.Carried[e.Cause.Source] = _curve.HitterAfterHit(received);   // its next hit is worth more
            c.Carried[e.Robot] = carries;                                     // this robot is now a stone too

            c.Total = ScoreMath.AddClamped(c.Total, total);
            _state.Score = ScoreMath.AddClamped(_state.Score, total);
            _bus.Publish(new RobotScored(e.Robot, e.Chain, order, depth, received, _curve.Multiplier(depth), c.Hour, c.HourMultiplier,
                pegMultiplier, total, carries));
        }

        private void OnRobotRemoved(RobotRemoved e)
        {
            // Breaches are counted by NightReferee on RobotBreached; a removal only releases its chain here.
            Release(e.Chain, e.Robot);
        }

        private void OnThrowableRemoved(ThrowableRemoved e)
        {
            if (!e.Chain.IsNone && _open.TryGetValue(e.Chain.Id, out var c)) c.Stones.Remove(e.Throwable);
            Release(e.Chain, e.Throwable);
        }

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