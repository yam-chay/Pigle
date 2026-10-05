using System.Collections.Generic;
using Piglings.Events;
using Piglings.Runtime;

namespace Piglings.Rules
{
    /// <summary>What a peg hit did, for the Simulation to act on (the physics part). The Rules have already published the facts.</summary>
    public enum PegOutcome
    {
        None,      // a plain hold or peg (it may have added to the chain's score), a stone / ball already boosted here, night over
        Bounced,   // a Bouncy peg gave the chain its mult bonus
        Split,     // a Splitter needle splits the stone: launch NewPieces more from it (StoneSplit published)
        Exploded,  // a Bomb went off: knock the climbing robots in its radius loose (BombExploded published)
    }

    public readonly struct PegHitResult
    {
        public readonly PegOutcome Outcome;
        public readonly int NewPieces;       // Split: how many stones to launch from the one that hit (it keeps flying too)
        public readonly GameId Explosion;    // Exploded: the hitter to attribute the knocks to (Attribution.FromPeg)
        public readonly int VictimDepth;     // Exploded: how deep its victims are

        public PegHitResult(PegOutcome outcome, int newPieces = 0, GameId explosion = default, int victimDepth = 0)
        {
            Outcome = outcome; NewPieces = newPieces; Explosion = explosion; VictimDepth = victimDepth;
        }

        public static readonly PegHitResult None = new PegHitResult(PegOutcome.None);
    }

    /// <summary>
    /// What holds and placed pegs do when something hits them. The Simulation reports every hit of a flying stone or a
    /// falling ball on a hold (NightSession.HitPeg, called by the Hold) and gets back what to do physically; everything that
    /// touches score or state is decided here, and published as facts.
    ///
    /// - A plain hold (an empty socket) or a Plain peg: + the plain-peg score to the hitter's chain on every contact, with a
    ///   short cooldown per hold per stone / ball (ChainTracker.TouchPlain, M10.S). A placed peg also publishes PegHit (views may react).
    /// - Every special peg adds its level's mult bonus to the chain when it triggers (PegType.MultBonusAt; 0 = none):
    /// - Bouncy: a stone or a falling ball bouncing off it triggers it, once per peg per stone / ball (Bouncy +1 by level).
    /// - Splitter: a thrown stone (or piece) becomes PiecesAt(level) stones — it keeps flying and the Simulation launches
    ///   the rest from it, in the same chain. Once per needle per stone (a new piece counts as already split at its
    ///   needle), and never more than MaxStonesPerThrow of one throw in flight: a split is cut short to fit. Robot balls
    ///   never split.
    /// - Bomb: a stone or a falling ball sets it off — if it's charged, the night is Running (during PegPlacement it's a
    ///   plain hold and keeps its charge) and the trigger is in an open chain. The explosion is its own hitter (a new
    ///   GameId), its victims 1 deep from a stone, the ball's depth + 1 from a ball. Then it's spent for CooldownAt(level)
    ///   seconds of Running time (Advance), a plain hold until PegRecharged.
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
        private readonly IdAllocator _ids;   // the night's, shared with the Simulation: an explosion is a hitter like any other

        // Bouncy: (socket, stone / ball) pairs that already gave their bonus. The same peg can't boost a hitter twice.
        private readonly HashSet<(int socket, GameId hitter)> _bounced = new HashSet<(int, GameId)>();

        // Splitter: (socket, stone) pairs that already split there (and new pieces at their needle). Once per needle.
        private readonly HashSet<(int socket, GameId stone)> _split = new HashSet<(int, GameId)>();

        public PegEffects(EventBus bus, NightState state, ChainTracker chains, PegSetup pegs, IdAllocator ids)
        {
            _bus = bus; _state = state; _chains = chains; _pegs = pegs ?? new PegSetup(); _ids = ids;
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

        /// <summary>
        /// Something hit the hold / peg in this socket. Returns what the Simulation should do about it.
        /// <paramref name="time"/>: the caller's clock (Time.time), for the plain holds' cooldown.
        /// </summary>
        public PegHitResult Hit(int socket, PegHitter hitter, GameId hitterId, ChainId chain, float time = 0f)
        {
            if (_state.Ended || socket < 0 || socket >= _state.Sockets.Length) return PegHitResult.None;
            var placed = _state.Sockets[socket];
            // A plain hold: only its score (no PegHit — balls rattle through holds all night).
            if (placed.IsEmpty) { _chains.TouchPlain(chain, socket, hitterId, time); return PegHitResult.None; }
            var type = _pegs.Find(placed.PegId);
            var effect = type != null ? type.Effect : PegEffect.Plain;

            _bus.Publish(new PegHit(socket, placed.PegId, placed.Level, effect, hitter, hitterId, chain));

            if (effect == PegEffect.Plain) { _chains.TouchPlain(chain, socket, hitterId, time); return PegHitResult.None; }
            if (effect == PegEffect.Bouncy) return Bounce(socket, type, placed.Level, hitterId, chain);
            if (effect == PegEffect.Splitter && hitter == PegHitter.Stone) return Split(socket, type, placed.Level, hitterId, chain);
            if (effect == PegEffect.Bomb) return Explode(socket, placed, hitter, hitterId, chain, type);
            return PegHitResult.None;
        }

        /// <summary>
        /// Spent bombs count down. Called every frame by NightSession (the Rules can't keep time) with the frame's seconds;
        /// ignored unless the night is Running, so the recharge pauses with the wall during PegPlacement and stops for good
        /// once the night has ended.
        /// </summary>
        public void Advance(float seconds)
        {
            if (_state.Phase != NightPhase.Running || seconds <= 0f) return;
            for (int i = 0; i < _state.Sockets.Length; i++)
            {
                var s = _state.Sockets[i];
                if (!s.IsSpent) continue;
                s.Recharge -= seconds;
                if (s.Recharge > 0f) continue;
                s.Recharge = 0f;
                _bus.Publish(new PegRecharged(i));
            }
        }

        private PegHitResult Explode(int socket, PegSocket placed, PegHitter hitter, GameId trigger, ChainId chain, PegType type)
        {
            // Placing pegs: the wall is frozen, so a bomb is just a hold — and it keeps its charge for when play resumes.
            if (_state.Phase != NightPhase.Running || placed.IsSpent || !_chains.IsOpen(chain)) return PegHitResult.None;

            int depth;
            if (hitter == PegHitter.Stone) depth = 1;   // the stone's own victims are 0 deep; the bomb is one link further
            else
            {
                int ballDepth = _chains.DepthOf(chain, trigger);
                if (ballDepth < 0) return PegHitResult.None;   // not one of this chain's robots: nothing to score into
                depth = ballDepth + 1;
            }

            var explosion = _ids.Next();
            placed.Recharge = type.CooldownAt(placed.Level);
            _chains.AddPegMult(chain, socket, trigger, type.MultBonusAt(placed.Level));
            _bus.Publish(new BombExploded(socket, placed.PegId, placed.Level, chain, explosion, hitter, trigger, depth));
            return new PegHitResult(PegOutcome.Exploded, explosion: explosion, victimDepth: depth);
        }

        private PegHitResult Split(int socket, PegType type, int level, GameId stone, ChainId chain)
        {
            if (!_chains.IsOpen(chain) || _split.Contains((socket, stone))) return PegHitResult.None;
            // Cut short to fit the cap: the stones already flying in this throw count, the one splitting included.
            int room = type.MaxStonesPerThrow - _chains.StonesInFlight(chain);
            int newPieces = System.Math.Min(type.PiecesAt(level) - 1, room);
            if (newPieces <= 0) return PegHitResult.None;

            _split.Add((socket, stone));
            _chains.AddPegMult(chain, socket, stone, type.MultBonusAt(level));
            _bus.Publish(new StoneSplit(socket, chain, stone, newPieces, type.SplitHitsCountForMastery));
            return new PegHitResult(PegOutcome.Split, newPieces);
        }

        private PegHitResult Bounce(int socket, PegType type, int level, GameId hitter, ChainId chain)
        {
            // A ball outside an open chain (the end-of-night sweep) has nothing to score into.
            if (!_chains.IsOpen(chain) || !_bounced.Add((socket, hitter))) return PegHitResult.None;
            float bonus = type.MultBonusAt(level);
            float chainMult = _chains.AddPegMult(chain, socket, hitter, bonus);
            _bus.Publish(new PegBounced(socket, hitter, chain, bonus, chainMult));
            return new PegHitResult(PegOutcome.Bounced);
        }

        // A new piece is born at its needle, touching it: it must not split there again on its first contact.
        private void OnPieceLaunched(StonePieceLaunched e) => _split.Add((e.Socket, e.Piece));

        // A removed ball or stone can't hit anything again: forget it, so the sets don't grow all night.
        private void OnRobotRemoved(RobotRemoved e) => _bounced.RemoveWhere(b => b.hitter == e.Robot);
        private void OnThrowableRemoved(ThrowableRemoved e)
        {
            _split.RemoveWhere(s => s.stone == e.Throwable);
            _bounced.RemoveWhere(b => b.hitter == e.Throwable);
        }
    }
}
