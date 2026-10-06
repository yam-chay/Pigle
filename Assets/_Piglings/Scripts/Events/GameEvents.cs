namespace Piglings.Events
{
    // Every gameplay fact the rest of the game may care about is one of these.
    // Events are immutable facts ("this happened"), never commands ("do this").

    /// <summary>
    /// The pig released a throw: a new chain starts. Weapon = the thrown thing's definition id (ThrowableDefinition.Id,
    /// "stone"), so the Rules can credit its hits to the right weapon without ever seeing a definition.
    /// </summary>
    public readonly struct ThrowReleased
    {
        public readonly ChainId Chain; public readonly GameId Throwable; public readonly string Weapon;
        public ThrowReleased(ChainId chain, GameId throwable, string weapon) { Chain = chain; Throwable = throwable; Weapon = weapon; }
    }

    /// <summary>A robot joined the wall. RobotType = its definition id (RobotDefinition.Id), for per-type stats.</summary>
    public readonly struct RobotSpawned
    {
        public readonly GameId Robot; public readonly string RobotType;
        public RobotSpawned(GameId robot, string robotType) { Robot = robot; RobotType = robotType; }
    }

    public readonly struct RobotLostGrip
    {
        public readonly GameId Robot; public readonly ChainId Chain; public readonly Attribution Cause;
        public RobotLostGrip(GameId robot, ChainId chain, Attribution cause) { Robot = robot; Chain = chain; Cause = cause; }
    }

    /// <summary>
    /// A climbing robot crossed the danger line just below the roof: it's about to breach.
    /// Published once per robot, by the DangerZone. A warning for views today; later it's also
    /// the line that ends overtime. Changes nothing in the Rules on its own.
    /// </summary>
    public readonly struct RobotEnteredDangerZone
    {
        public readonly GameId Robot;
        public RobotEnteredDangerZone(GameId robot) { Robot = robot; }
    }

    /// <summary>
    /// A climbing robot reached the roof and started its breach (Climbing → Breaching). THIS is the breach:
    /// NightReferee counts it here — the stone theft happens at this moment (on an empty pile it takes nothing).
    /// Published by the robot after it has entered Breaching, so the end-of-night sweep (if this breach ends
    /// the night) already sees it as not climbing and leaves it alone.
    /// </summary>
    public readonly struct RobotBreached
    {
        public readonly GameId Robot;
        public RobotBreached(GameId robot) { Robot = robot; }
    }

    // TimedOut: a falling ball that never reached the ground (stuck still on a hold or between pegs, or in the air too
    // long) — removed so its chain can close.
    // EnteredBarn: a breaching robot's sequence is over. Cleanup only — the breach was counted at RobotBreached.
    public enum RemovalReason { HitGround, EnteredBarn, TimedOut }

    public readonly struct RobotRemoved
    {
        public readonly GameId Robot; public readonly ChainId Chain; public readonly RemovalReason Reason;
        public RobotRemoved(GameId robot, ChainId chain, RemovalReason reason) { Robot = robot; Chain = chain; Reason = reason; }
    }

    public readonly struct ThrowableRemoved
    {
        public readonly GameId Throwable; public readonly ChainId Chain;
        public ThrowableRemoved(GameId throwable, ChainId chain) { Throwable = throwable; Chain = chain; }
    }

    /// <summary>Published by the Rules layer when nothing from a chain is still in play.</summary>
    public readonly struct ChainClosed
    {
        public readonly ChainId Chain; public readonly int RobotsDropped; public readonly int MaxDepth;
        public ChainClosed(ChainId chain, int robotsDropped, int maxDepth) { Chain = chain; RobotsDropped = robotsDropped; MaxDepth = maxDepth; }
    }

    /// <summary>What added to a chain (M10.S): the thrown stone's base, a wolf knocked loose, a plain hold touched, a special peg.</summary>
    public enum ChainGainCause { Stone, Wolf, PlainPeg, Peg }

    /// <summary>
    /// A chain grew (M10.S, Score × Mult): published by ChainTracker after it has added ScoreAdded to the night's score
    /// (raw — the mult lands at the close). Score / Mult are the chain's running totals now; HourMultiplier the hour it was
    /// thrown in (×1 in hour 1), applied at the close.
    /// - Stone: the throw's base (Source = the stone). - Wolf: a robot knocked loose (Source = the robot); MultAdded > 0
    ///   when it took the chain to a new depth. - PlainPeg: a plain hold touched (Socket; Source = the stone / ball).
    /// - Peg: a special peg's mult bonus (Socket; Source = the stone / ball).
    /// Views: the board's live row ticks, popups show it. Socket is -1 when no hold is involved.
    /// Depth: how deep the source is — a wolf's own depth; for a hold / peg, its hitter's (a stone 0, a ball its depth) —
    /// so views can colour by depth.
    /// </summary>
    public readonly struct ChainGained
    {
        public readonly ChainId Chain; public readonly ChainGainCause Cause; public readonly GameId Source; public readonly int Socket;
        public readonly int ScoreAdded; public readonly float MultAdded;
        public readonly int Score; public readonly float Mult; public readonly int Hour; public readonly float HourMultiplier;
        public readonly int Depth;
        public ChainGained(ChainId chain, ChainGainCause cause, GameId source, int socket, int scoreAdded, float multAdded,
                           int score, float mult, int hour, float hourMultiplier, int depth = 0)
        {
            Chain = chain; Cause = cause; Source = source; Socket = socket; ScoreAdded = scoreAdded; MultAdded = multAdded;
            Score = score; Mult = mult; Hour = hour; HourMultiplier = hourMultiplier; Depth = depth;
        }
    }

    /// <summary>
    /// Published by the Rules layer right after ChainClosed: the chain's result = Score × Mult × HourMultiplier, rounded once
    /// (M10.S). Score was already added to the night's score as it came (raw); Remainder = Total − Score is added now, just
    /// before this is published. Also published for misses (a miss still has the stone's base). Hour: the hour the throw was
    /// made in, so views can colour it.
    /// </summary>
    public readonly struct ChainScored
    {
        public readonly ChainId Chain; public readonly int RobotsDropped; public readonly int MaxDepth; public readonly int Total;
        public readonly int Hour;
        public readonly int Score; public readonly float Mult; public readonly float HourMultiplier; public readonly int Remainder;
        public ChainScored(ChainId chain, int robotsDropped, int maxDepth, int total, int hour,
                           int score = 0, float mult = 1f, float hourMultiplier = 1f, int remainder = 0)
        {
            Chain = chain; RobotsDropped = robotsDropped; MaxDepth = maxDepth; Total = total; Hour = hour;
            Score = score; Mult = mult; HourMultiplier = hourMultiplier; Remainder = remainder;
        }
    }

    /// <summary>
    /// "Hours until dawn" (GDD "שעות הלילה"): Running(hour) → PegPlacement → Running(hour+1) … → Ended (dawn, won),
    /// or Running → Ended (out of stones, lost). The hour is NightState.Hour; it isn't a phase of its own.
    /// Dusk (campaign scene only): before the night begins — the day phase, the camera rising. Nothing spawns, nothing
    /// can be thrown; NightReferee.Begin() starts the night (Dusk → Running). Last in the list so the others keep their values.
    /// </summary>
    public enum NightPhase { Running, PegPlacement, Ended, Dusk }
    public enum NightResult { Won, Lost }
    // OutOfStones (M10.E): no stone left, nothing in flight and no round waiting to refill — the only way to lose. It took
    // the slot of Caught (a breach on an empty pile), which it replaced. Dawn: the last threshold was reached.
    public enum NightEndReason { OutOfStones, Dawn }

    // Added: a refill (each hour reached, or AddStones).
    public enum StoneChange { Thrown, Stolen, Added }

    /// <summary>
    /// The stone count changed (NightReferee, the one owner of the count). Count is the new total; Delta is
    /// signed. Stolen: Robot is the thief (it takes the top stone). The pile on the perch mirrors this — it
    /// never decides the count.
    /// </summary>
    public readonly struct StonesChanged
    {
        public readonly int Count; public readonly int Delta; public readonly StoneChange Cause; public readonly GameId Robot;
        public StonesChanged(int count, int delta, StoneChange cause, GameId robot)
        {
            Count = count; Delta = delta; Cause = cause; Robot = robot;
        }
    }
    // Where a night's banking goes. Barn: the banked score. Weapon: a weapon's use (Id = the weapon id).
    // Lineage: a robot type's ball stats (Id = the robot type) — recorded for future wolf-lineage mastery, unused for now.
    // Peg: a peg type's stats (Id = the peg id) — recorded, unused for now.
    public enum MasteryDestination { Barn, Weapon, Lineage, Peg }

    // What was counted. Score → Barn. DirectHits → Weapon (a robot knocked loose by the thrown weapon itself).
    // BallKnocks / KnockedByBall → Lineage: this type's ball knocked another robot loose / this type was knocked loose
    // by a ball. Both sides are kept so the lineage design can pick later without losing data.
    // PegKnocks → Peg: robots this peg type knocked loose itself (a Bomb's explosion), whatever set it off.
    // PegTriggers → Peg, per merged level (NightBanked.Level): times its effect fired — a Bouncy bonus granted, a Splitter
    // split, a Bomb explosion. Peg mastery (copies owned) is derived from these, weighted by level.
    // Dropped → Lineage (M10.E): robots of this type knocked off the wall tonight, whatever did it — a stone, a ball, a bomb,
    // or the end-of-night sweep. For the post-run's "wolves dropped" and later lineage progression.
    // Swept → Lineage: the part of Dropped the end-of-night sweep took (knocked in play = Dropped − Swept), kept apart so a
    // later progression rule can choose what counts.
    public enum MasteryStat { Score, DirectHits, BallKnocks, KnockedByBall, PegKnocks, PegTriggers, Dropped, Swept }

    /// <summary>
    /// Published by NightReferee on every phase change. Simulation pauses, resumes and sweeps the wall off this.
    /// PegPlacement → PegPlacement is a real change: one placement round per threshold, played one after the other.
    /// </summary>
    public readonly struct NightPhaseChanged
    {
        public readonly NightPhase From; public readonly NightPhase To;
        public NightPhaseChanged(NightPhase from, NightPhase to) { From = from; To = to; }
    }

    /// <summary>
    /// A new hour starts: published when a threshold's placement round starts (inside the freeze), not at the
    /// crossing — the hour's visuals and juice belong to this moment. Hour is the new hour (2 after the first
    /// threshold), Multiplier what its throws will score with, Threshold the score that was crossed.
    /// </summary>
    public readonly struct HourReached
    {
        public readonly int Hour; public readonly float Multiplier; public readonly int Threshold;
        public HourReached(int hour, float multiplier, int threshold) { Hour = hour; Multiplier = multiplier; Threshold = threshold; }
    }

    /// <summary>
    /// The score crossed the last threshold: dawn, the night is won (it ends once every chain has settled, and can't
    /// be lost from here). Once per night. Meta will turn it into a badge later.
    /// </summary>
    public readonly struct DawnReached
    {
        public readonly int Score;
        public DawnReached(int score) { Score = score; }
    }

    /// <summary>A peg from the shelf was placed in an empty socket (a Hold). Level starts at 1.</summary>
    public readonly struct PegPlaced
    {
        public readonly int Socket; public readonly string PegId; public readonly int Level;
        public PegPlaced(int socket, string pegId, int level) { Socket = socket; PegId = pegId; Level = level; }
    }

    /// <summary>
    /// Debug (M11.T2, the F1 panel's board edit): a socket was set directly — this peg type at this level, or emptied
    /// (PegId null, Level 0). Not a placement: no shelf, no throw, no follow-up. Views redraw the socket.
    /// </summary>
    public readonly struct PegSocketSet
    {
        public readonly int Socket; public readonly string PegId; public readonly int Level;
        public PegSocketSet(int socket, string pegId, int level) { Socket = socket; PegId = pegId; Level = level; }
    }

    /// <summary>A peg from the shelf was thrown onto a peg of the same type, which went up a level (now Level).</summary>
    public readonly struct PegMerged
    {
        public readonly int Socket; public readonly string PegId; public readonly int Level;
        public PegMerged(int socket, string pegId, int level) { Socket = socket; PegId = pegId; Level = level; }
    }

    /// <summary>
    /// A follow-up throw (stage 2): the round gives one more throw, which must be this same peg type — after a peg of a type
    /// with follow-ups was placed (the chain is given once per round), and again after each follow-up while the type still has
    /// some. Remaining = follow-ups still to come after this one. Views can celebrate it; the shelf brings that type up.
    /// </summary>
    public readonly struct PegFollowUpGranted
    {
        public readonly string PegId; public readonly int Remaining;
        public PegFollowUpGranted(string pegId, int remaining) { PegId = pegId; Remaining = remaining; }
    }

    /// <summary>What a placed peg does when something hits it. Plain = nothing (a hold with a look).</summary>
    public enum PegEffect { Plain, Bouncy, Splitter, Bomb }

    /// <summary>What hit a peg: a thrown stone (or a piece of one), or a falling robot ball.</summary>
    public enum PegHitter { Stone, Ball }

    /// <summary>
    /// Something hit a placed peg (published by the Rules from NightSession.HitPeg; empty sockets publish nothing, so
    /// balls bouncing through plain holds don't flood the bus). A fact for views; the effect, if any, has its own event.
    /// </summary>
    public readonly struct PegHit
    {
        public readonly int Socket; public readonly string PegId; public readonly int Level; public readonly PegEffect Effect;
        public readonly PegHitter Hitter; public readonly GameId HitterId; public readonly ChainId Chain;
        public PegHit(int socket, string pegId, int level, PegEffect effect, PegHitter hitter, GameId hitterId, ChainId chain)
        {
            Socket = socket; PegId = pegId; Level = level; Effect = effect; Hitter = hitter; HitterId = hitterId; Chain = chain;
        }
    }

    /// <summary>
    /// A stone or a falling ball bounced off a Bouncy peg and its chain got the peg's mult bonus (M10.S; once per peg per
    /// stone / ball). MultBonus = this peg's (+1 at level 1); ChainMult = the chain's mult now.
    /// </summary>
    public readonly struct PegBounced
    {
        public readonly int Socket; public readonly GameId Hitter; public readonly ChainId Chain;
        public readonly float MultBonus; public readonly float ChainMult;
        public PegBounced(int socket, GameId hitter, ChainId chain, float multBonus, float chainMult)
        {
            Socket = socket; Hitter = hitter; Chain = chain; MultBonus = multBonus; ChainMult = chainMult;
        }
    }

    /// <summary>
    /// A thrown stone (or a piece of one) hit a Splitter needle and splits: it keeps flying, and NewPieces more stones
    /// launch from it, fanned out, in the same chain (their hits add to it like any hit; a piece isn't a throw, so it adds
    /// no stone base). Published by the Rules (they decided it: once per needle per stone, within the per-throw cap);
    /// each piece then announces itself with StonePieceLaunched once it really exists.
    /// PiecesCountForMastery: the pieces' direct hits count as stone hits (the Splitter's toggle).
    /// </summary>
    public readonly struct StoneSplit
    {
        public readonly int Socket; public readonly ChainId Chain; public readonly GameId Stone;
        public readonly int NewPieces; public readonly bool PiecesCountForMastery;
        public StoneSplit(int socket, ChainId chain, GameId stone, int newPieces, bool piecesCountForMastery)
        {
            Socket = socket; Chain = chain; Stone = stone; NewPieces = newPieces; PiecesCountForMastery = piecesCountForMastery;
        }
    }

    /// <summary>
    /// A piece of a split stone is flying (published by the piece itself, in Simulation). It joins Parent's chain — the
    /// chain closes only once every piece is gone. It came from the throw, not the pile: no
    /// ThrowReleased, the stone count doesn't change. Socket = the needle it split at (it can't split there again).
    /// </summary>
    public readonly struct StonePieceLaunched
    {
        public readonly ChainId Chain; public readonly GameId Piece; public readonly GameId Parent; public readonly int Socket;
        public StonePieceLaunched(ChainId chain, GameId piece, GameId parent, int socket)
        {
            Chain = chain; Piece = piece; Parent = parent; Socket = socket;
        }
    }

    /// <summary>
    /// A Bomb went off (the Rules decided: it was charged, the night Running, the trigger in an open chain). Explosion is
    /// a new hitter in the trigger's chain: the robots it knocks loose add to the chain like any; they are VictimDepth deep (1 from a stone, the ball's depth + 1 from a ball). The Simulation knocks
    /// every climbing robot in the level's radius loose with Attribution.FromPeg(Explosion, VictimDepth).
    /// The bomb is now spent — a plain hold — until PegRecharged.
    /// </summary>
    public readonly struct BombExploded
    {
        public readonly int Socket; public readonly string PegId; public readonly int Level; public readonly ChainId Chain;
        public readonly GameId Explosion; public readonly PegHitter Trigger; public readonly GameId TriggerId; public readonly int VictimDepth;
        public BombExploded(int socket, string pegId, int level, ChainId chain, GameId explosion, PegHitter trigger, GameId triggerId,
                            int victimDepth)
        {
            Socket = socket; PegId = pegId; Level = level; Chain = chain; Explosion = explosion;
            Trigger = trigger; TriggerId = triggerId; VictimDepth = victimDepth;
        }
    }

    /// <summary>A spent Bomb has recharged (its cooldown ran out while the wall was moving): it can go off again.</summary>
    public readonly struct PegRecharged
    {
        public readonly int Socket;
        public PegRecharged(int socket) { Socket = socket; }
    }

    /// <summary>
    /// A robot still on the wall was knocked off by the end-of-night sweep (published by Simulation while it
    /// sweeps). Not part of any chain. Won: NightReferee scores it flat with the wolf value (×1). Lost: visual
    /// only — the robots fall either way, but the bank stays at the last threshold reached.
    /// </summary>
    public readonly struct RobotSwept
    {
        public readonly GameId Robot;
        public RobotSwept(GameId robot) { Robot = robot; }
    }

    /// <summary>
    /// What the night banked: Amount × Multiplier of Stat, to Destination's mastery target Id.
    /// Published by NightReferee just before NightEnded (on both outcomes), one per target, only for non-zero amounts.
    /// - Barn, Score: the banked score (the live score at dawn; the last threshold reached when out of stones). Id is null.
    /// - Weapon, DirectHits: Id = the weapon id ("stone"). Mastery from use, so a lost night banks its hits too.
    /// - Lineage, BallKnocks / KnockedByBall / Dropped / Swept: Id = the robot type.
    /// - Peg, PegKnocks / PegTriggers: Id = the peg id; Level = the merged level the triggers happened at (PegTriggers only).
    /// Meta's Progression applies these to the saved profile; the save itself happens at NightEnded.
    /// </summary>
    public readonly struct NightBanked
    {
        public readonly MasteryDestination Destination; public readonly string Id; public readonly MasteryStat Stat;
        public readonly int Amount; public readonly int Multiplier;
        public readonly int Level;   // PegTriggers: the peg's merged level (1, 2, 3…); 0 = not per level
        public NightBanked(MasteryDestination destination, string id, MasteryStat stat, int amount, int multiplier, int level = 0)
        {
            Destination = destination; Id = id; Stat = stat; Amount = amount; Multiplier = multiplier; Level = level;
        }
    }

    /// <summary>
    /// Published once by the Rules layer (NightReferee) when the night is decided. All values are as of that moment.
    /// Score is the live score; BankedScore is what the night keeps (= Score at dawn, the last threshold reached
    /// when out of stones). HoursReached = thresholds crossed (= the threshold count at dawn).
    /// ThrowsUsed counts stones actually thrown (not ones lost to breaches, not pegs), for score per stone.
    /// </summary>
    public readonly struct NightEnded
    {
        public readonly NightResult Result; public readonly NightEndReason Reason;
        public readonly int Score; public readonly int BankedScore; public readonly int HoursReached;
        public readonly int StonesLeft; public readonly int Breaches; public readonly int ThrowsUsed;
        public NightEnded(NightResult result, NightEndReason reason, int score, int bankedScore, int hoursReached,
                          int stonesLeft, int breaches, int throwsUsed)
        {
            Result = result; Reason = reason; Score = score; BankedScore = bankedScore; HoursReached = hoursReached;
            StonesLeft = stonesLeft; Breaches = breaches; ThrowsUsed = throwsUsed;
        }
    }
}
