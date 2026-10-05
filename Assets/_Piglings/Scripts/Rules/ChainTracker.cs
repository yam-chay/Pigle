using System.Collections.Generic;
using Piglings.Events;
using Piglings.Runtime;

namespace Piglings.Rules
{
    /// <summary>
    /// Turns raw events into chain results. A chain stays open while its throwable or any robot it knocked loose is still in
    /// play; when the last one is removed, ChainClosed fires.
    ///
    /// Scoring (M10.S, ScoreCurve): each chain keeps a SCORE and a MULT. Every score gain goes into the night's score at
    /// once, raw (the bar moves all the time); at the close the result (score × mult × hour) arrives as the remainder —
    /// the mult lands as one jump. Each gain is published as ChainGained; the close as ChainClosed then ChainScored.
    /// </summary>
    public sealed class ChainTracker
    {
        private sealed class Open
        {
            public readonly HashSet<GameId> InPlay = new HashSet<GameId>();
            public int Dropped;
            public int MaxDepth;            // the deepest depth reached so far (0 = only the stone's own victims, or none)
            public int Score;               // raw: already in the night's score
            public float Mult = 1f;
            public int Hour;                // the hour its stone was thrown in (views colour by it)
            public float HourMultiplier;    // fixed at the throw, applied at the close

            // The thrown stones of this chain still flying: the original and any Splitter pieces (InPlay holds robots too).
            public readonly HashSet<GameId> Stones = new HashSet<GameId>();

            // How deep each robot of this chain is (a Bomb set off by its ball knocks robots one deeper).
            public readonly Dictionary<GameId, int> Depths = new Dictionary<GameId, int>();

            // Plain holds already touched, per stone / ball: each one adds once per hitter.
            public readonly HashSet<(int socket, GameId hitter)> Touched = new HashSet<(int, GameId)>();
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

        /// <summary>How many stones of this chain are flying right now (the original and any pieces). 0 if it isn't open.</summary>
        public int StonesInFlight(ChainId chain) =>
            !chain.IsNone && _open.TryGetValue(chain.Id, out var c) ? c.Stones.Count : 0;

        /// <summary>A robot's depth in this chain (from its RobotLostGrip), or -1 if it isn't one of the chain's robots.</summary>
        public int DepthOf(ChainId chain, GameId robot) =>
            !chain.IsNone && _open.TryGetValue(chain.Id, out var c) && c.Depths.TryGetValue(robot, out int d) ? d : -1;

        /// <summary>
        /// A stone or ball of this chain touched a plain hold (an empty socket or a Plain peg): + the plain-peg score, once
        /// per hold per hitter. False = nothing added (not an open chain, or already touched).
        /// </summary>
        public bool TouchPlain(ChainId chain, int socket, GameId hitter)
        {
            if (chain.IsNone || !_open.TryGetValue(chain.Id, out var c) || !c.Touched.Add((socket, hitter))) return false;
            Gain(chain, c, ChainGainCause.PlainPeg, hitter, socket, _curve.PlainPegScore, 0f);
            return true;
        }

        /// <summary>A special peg triggered for this chain: + its mult bonus. Returns the chain's mult now (1 if not open).</summary>
        public float AddPegMult(ChainId chain, int socket, GameId hitter, float bonus)
        {
            if (chain.IsNone || !_open.TryGetValue(chain.Id, out var c)) return 1f;
            if (bonus > 0f) Gain(chain, c, ChainGainCause.Peg, hitter, socket, 0, bonus);
            return c.Mult;
        }

        // A piece joins its parent's chain: the chain now waits for it too. It's not a throw: no stone base.
        private void OnPieceLaunched(StonePieceLaunched e)
        {
            if (!_open.TryGetValue(e.Chain.Id, out var c)) return;
            c.InPlay.Add(e.Piece);
            c.Stones.Add(e.Piece);
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
            _open[e.Chain.Id] = c;
            _state.ThrowsUsed++;
            Gain(e.Chain, c, ChainGainCause.Stone, e.Throwable, -1, _curve.StoneBase, 0f);
        }

        private void OnLostGrip(RobotLostGrip e)
        {
            if (!_open.TryGetValue(e.Chain.Id, out var c)) return;
            c.InPlay.Add(e.Robot);
            int depth = e.Cause.Depth;
            c.Depths[e.Robot] = depth;
            c.Dropped++;
            _state.RobotsDropped++;

            // A new depth level adds mult — once per level reached, so going wide never does.
            float mult = 0f;
            if (depth > c.MaxDepth)
            {
                mult = _curve.MultPerNewDepth * (depth - c.MaxDepth);
                c.MaxDepth = depth;
            }
            Gain(e.Chain, c, ChainGainCause.Wolf, e.Robot, -1, _curve.RobotValue(), mult);
        }

        // The one place a chain grows: the raw score goes into the night's score now; the mult waits for the close.
        private void Gain(ChainId chain, Open c, ChainGainCause cause, GameId source, int socket, int score, float mult)
        {
            if (score <= 0 && mult <= 0f) return;
            c.Score = ScoreMath.AddClamped(c.Score, score);
            c.Mult += mult;
            _state.Score = ScoreMath.AddClamped(_state.Score, score);
            _bus.Publish(new ChainGained(chain, cause, source, socket, score, mult, c.Score, c.Mult, c.Hour, c.HourMultiplier));
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
            // One chain's robots with its own depth (the post-run's "biggest chain"); a tie goes to the deeper one.
            if (c.Dropped > _state.BiggestChainRobots || (c.Dropped == _state.BiggestChainRobots && c.Dropped > 0 && c.MaxDepth > _state.BiggestChainDepth))
            {
                _state.BiggestChainRobots = c.Dropped;
                _state.BiggestChainDepth = c.MaxDepth;
            }

            // The mult lands: the result, less the raw score already counted, goes in as one jump.
            int total = _curve.Result(c.Score, c.Mult, c.HourMultiplier);
            int remainder = total > c.Score ? total - c.Score : 0;
            _state.Score = ScoreMath.AddClamped(_state.Score, remainder);
            _bus.Publish(new ChainClosed(chain, c.Dropped, c.MaxDepth));
            _bus.Publish(new ChainScored(chain, c.Dropped, c.MaxDepth, total, c.Hour, c.Score, c.Mult, c.HourMultiplier, remainder));
        }
    }
}
