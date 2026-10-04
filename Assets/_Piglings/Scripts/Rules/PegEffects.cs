using System.Collections.Generic;
using Piglings.Events;
using Piglings.Runtime;

namespace Piglings.Rules
{
    /// <summary>What a peg hit did, for the Simulation to act on (the physics part). The Rules have already published the facts.</summary>
    public enum PegOutcome
    {
        None,      // empty socket, plain peg, a stone on a Bouncy peg, a ball already boosted by this peg, night over
        Bounced,   // a Bouncy peg gave a falling ball its bonus
        Split,     // a Splitter needle splits the stone: launch NewPieces more from it (StoneSplit published)
    }

    public readonly struct PegHitResult
    {
        public readonly PegOutcome Outcome;
        public readonly int NewPieces;   // Split: how many stones to launch from the one that hit (it keeps flying too)
        public PegHitResult(PegOutcome outcome, int newPieces = 0) { Outcome = outcome; NewPieces = newPieces; }
        public static readonly PegHitResult None = new PegHitResult(PegOutcome.None);
    }

    /// <summary>
    /// What placed pegs do when something hits them. The Simulation reports every hit on an occupied socket
    /// (NightSession.HitPeg, called by the Hold) and gets back what to do physically; everything that touches score or
    /// state is decided here, and published as facts.
    ///
    /// - PegHit: every hit on an occupied socket (plain pegs too — views may react; nothing else does).
    /// - Bouncy: a falling ball gets the peg's multiplier on what its victims score from now on (ChainTracker.AddPegExtra),
    ///   once per peg per ball. Pegs add on the extra part (two ×2 = ×3). Stones bounce off physically, no bonus.
    /// - Splitter: a thrown stone (or piece) becomes PiecesAt(level) stones — it keeps flying and the Simulation launches
    ///   the rest from it, in the same chain. Once per needle per stone (a new piece counts as already split at its
    ///   needle), and never more than MaxStonesPerThrow of one throw in flight: a split is cut short to fit. Every stone
    ///   (the original too) carries the value × ValueShareAt(level). Robot balls never split.
    ///
    /// The peg's type and level come from NightState.Sockets (the board, written by NightReferee), never from the caller.
    /// Construct after ChainTracker and NightReferee.
    /// </summary>
    public sealed class PegEffects
    {
        private readonly EventBus _bus;
        private readonly NightState _state;
        private readonly ChainTracker _chains;
        private readonly PegSetup _pegs;

        // Bouncy: (socket, ball) pairs that already got their bonus. A ball can't be boosted twice by the same peg.
        private readonly HashSet<(int socket, GameId ball)> _bounced = new HashSet<(int, GameId)>();

        // Splitter: (socket, stone) pairs that already split there (and new pieces at their needle). Once per needle.
        private readonly HashSet<(int socket, GameId stone)> _split = new HashSet<(int, GameId)>();

        public PegEffects(EventBus bus, NightState state, ChainTracker chains, PegSetup pegs)
        {
            _bus = bus; _state = state; _chains = chains; _pegs = pegs ?? new PegSetup();
            _bus.Subscribe<RobotRemoved>(OnRobotRemoved);
            _bus.Subscribe<StonePieceLaunched>(OnPieceLaunched);
            _bus.Subscribe<ThrowableRemoved>(OnThrowableRemoved);
        }

        public void Dispose()
        {
            _bus.Unsubscribe<RobotRemoved>(OnRobotRemoved);
            _bus.Unsubscribe<StonePieceLaunched>(OnPieceLaunched);
            _bus.Unsubscribe<ThrowableRemoved>(OnThrowableRemoved);
        }

        /// <summary>Something hit the peg in this socket. Returns what the Simulation should do about it.</summary>
        public PegHitResult Hit(int socket, PegHitter hitter, GameId hitterId, ChainId chain)
        {
            if (_state.Ended || socket < 0 || socket >= _state.Sockets.Length) return PegHitResult.None;
            var placed = _state.Sockets[socket];
            if (placed.IsEmpty) return PegHitResult.None;
            var type = _pegs.Find(placed.PegId);
            var effect = type != null ? type.Effect : PegEffect.Plain;

            _bus.Publish(new PegHit(socket, placed.PegId, placed.Level, effect, hitter, hitterId, chain));

            if (effect == PegEffect.Bouncy && hitter == PegHitter.Ball) return Bounce(socket, type, placed.Level, hitterId, chain);
            if (effect == PegEffect.Splitter && hitter == PegHitter.Stone) return Split(socket, type, placed.Level, hitterId, chain);
            return PegHitResult.None;
        }

        private PegHitResult Split(int socket, PegType type, int level, GameId stone, ChainId chain)
        {
            if (!_chains.IsOpen(chain) || _split.Contains((socket, stone))) return PegHitResult.None;
            // Cut short to fit the cap: the stones already flying in this throw count, the one splitting included.
            int room = type.MaxStonesPerThrow - _chains.StonesInFlight(chain);
            int newPieces = System.Math.Min(type.PiecesAt(level) - 1, room);
            if (newPieces <= 0) return PegHitResult.None;

            _split.Add((socket, stone));
            float share = type.ValueShareAt(level);
            _chains.ShareStoneValue(chain, stone, share);   // before the pieces launch: they copy it
            _bus.Publish(new StoneSplit(socket, chain, stone, newPieces, share, type.SplitHitsCountForMastery));
            return new PegHitResult(PegOutcome.Split, newPieces);
        }

        private PegHitResult Bounce(int socket, PegType type, int level, GameId ball, ChainId chain)
        {
            // A ball outside an open chain (the end-of-night sweep) has nothing to score into.
            if (!_chains.IsOpen(chain) || !_bounced.Add((socket, ball))) return PegHitResult.None;
            float multiplier = type.ScoreMultiplierAt(level);
            float ballMultiplier = _chains.AddPegExtra(chain, ball, multiplier - 1f);
            _bus.Publish(new PegBounced(socket, ball, chain, multiplier, ballMultiplier));
            return new PegHitResult(PegOutcome.Bounced);
        }

        // A new piece is born at its needle, touching it: it must not split there again on its first contact.
        private void OnPieceLaunched(StonePieceLaunched e) => _split.Add((e.Socket, e.Piece));

        // A removed ball or stone can't hit anything again: forget it, so the sets don't grow all night.
        private void OnRobotRemoved(RobotRemoved e) => _bounced.RemoveWhere(b => b.ball == e.Robot);
        private void OnThrowableRemoved(ThrowableRemoved e) => _split.RemoveWhere(s => s.stone == e.Throwable);
    }
}
