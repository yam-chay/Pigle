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
    }

    public readonly struct PegHitResult
    {
        public readonly PegOutcome Outcome;
        public PegHitResult(PegOutcome outcome) { Outcome = outcome; }
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

        public PegEffects(EventBus bus, NightState state, ChainTracker chains, PegSetup pegs)
        {
            _bus = bus; _state = state; _chains = chains; _pegs = pegs ?? new PegSetup();
            _bus.Subscribe<RobotRemoved>(OnRobotRemoved);
        }

        public void Dispose() => _bus.Unsubscribe<RobotRemoved>(OnRobotRemoved);

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
            return PegHitResult.None;
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

        // A removed ball can't hit anything again: forget its bounces, so the set doesn't grow all night.
        private void OnRobotRemoved(RobotRemoved e) => _bounced.RemoveWhere(b => b.ball == e.Robot);
    }
}
