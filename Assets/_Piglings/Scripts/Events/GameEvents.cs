namespace Piglings.Events
{
    // Every gameplay fact the rest of the game may care about is one of these.
    // Events are immutable facts ("this happened"), never commands ("do this").

    public readonly struct ThrowReleased
    {
        public readonly ChainId Chain; public readonly GameId Throwable;
        public ThrowReleased(ChainId chain, GameId throwable) { Chain = chain; Throwable = throwable; }
    }

    public readonly struct RobotSpawned
    {
        public readonly GameId Robot;
        public RobotSpawned(GameId robot) { Robot = robot; }
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
    /// NightReferee counts it here — the stone theft, or the catch on an empty pile, happens at this moment.
    /// Published by the robot after it has entered Breaching, so the end-of-night sweep (if this breach ends
    /// the night) already sees it as not climbing and leaves it alone.
    /// </summary>
    public readonly struct RobotBreached
    {
        public readonly GameId Robot;
        public RobotBreached(GameId robot) { Robot = robot; }
    }

    // TimedOut: a falling ball that never reached the ground (e.g. resting on a hold) — removed so its chain can close.
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

    /// <summary>
    /// Published by the Rules layer right after RobotLostGrip: what that robot is worth, already
    /// added to the score. Received = the value its hitter (stone or ball) carried into it;
    /// Total = Received × Multiplier × HourMultiplier, rounded once; Carries = what this robot's own ball now carries.
    /// Hour / HourMultiplier: the hour the chain's stone was thrown in, and its multiplier (×1 in hour 1) — fixed at
    /// the throw, the same as ChainScored.Hour, so robot and chain popups always agree.
    /// Order is its place in the chain (1 = first to lose grip).
    /// Presentation shows "Received ×Multiplier", then "+Total".
    /// </summary>
    public readonly struct RobotScored
    {
        public readonly GameId Robot; public readonly ChainId Chain;
        public readonly int Order; public readonly int Depth;
        public readonly int Received; public readonly float Multiplier; public readonly int Hour; public readonly float HourMultiplier;
        public readonly int Total; public readonly int Carries;
        public RobotScored(GameId robot, ChainId chain, int order, int depth, int received, float multiplier, int hour, float hourMultiplier,
                           int total, int carries)
        {
            Robot = robot; Chain = chain; Order = order; Depth = depth;
            Received = received; Multiplier = multiplier; Hour = hour; HourMultiplier = hourMultiplier; Total = total; Carries = carries;
        }
    }

    /// <summary>
    /// Published by the Rules layer right after ChainClosed: the chain's value, which is the sum of
    /// its RobotScored totals (nothing extra is added at close). Also published for misses, with zeros.
    /// Hour: the hour the throw was made in (a chain is never split across hours: throwing stops when a
    /// threshold is crossed, and the next hour starts only once every chain has settled), so views can colour it.
    /// </summary>
    public readonly struct ChainScored
    {
        public readonly ChainId Chain; public readonly int RobotsDropped; public readonly int MaxDepth; public readonly int Total;
        public readonly int Hour;
        public ChainScored(ChainId chain, int robotsDropped, int maxDepth, int total, int hour)
        {
            Chain = chain; RobotsDropped = robotsDropped; MaxDepth = maxDepth; Total = total; Hour = hour;
        }
    }

    /// <summary>
    /// "Hours until dawn" (GDD "שעות הלילה"): Running(hour) → PegPlacement → Running(hour+1) … → Ended (dawn, won),
    /// or Running → Ended (caught, lost). The hour is NightState.ThresholdsReached + 1; it isn't a phase of its own.
    /// </summary>
    public enum NightPhase { Running, PegPlacement, Ended }
    public enum NightResult { Won, Lost }
    // Caught: a robot breached while the pile was empty — the only way to lose. Dawn: the last threshold was reached.
    public enum NightEndReason { Caught, Dawn }

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
    // Weapon: nothing banks to it any more (leftover stones used to). Kept for Meta: weapon mastery will come from use + feats.
    public enum MasteryDestination { Barn, Weapon }

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
    /// The score crossed a threshold before the last one: a new hour starts. Hour is the new hour (2 after the first
    /// threshold), Multiplier what its chains will score with, Threshold the score that was crossed. Throwing stops
    /// now; a peg-placement round follows once every chain has settled.
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

    /// <summary>A peg from the shelf was thrown onto a peg of the same type, which went up a level (now Level).</summary>
    public readonly struct PegMerged
    {
        public readonly int Socket; public readonly string PegId; public readonly int Level;
        public PegMerged(int socket, string pegId, int level) { Socket = socket; PegId = pegId; Level = level; }
    }

    /// <summary>
    /// A robot still on the wall was knocked off by the end-of-night sweep (published by Simulation while it
    /// sweeps). Not part of any chain. Won: NightReferee scores it flat with the robot-value function. Lost: visual
    /// only — the robots fall either way, but the bank stays at the last threshold reached.
    /// </summary>
    public readonly struct RobotSwept
    {
        public readonly GameId Robot;
        public RobotSwept(GameId robot) { Robot = robot; }
    }

    /// <summary>
    /// Where points went when the night ended: Amount × Multiplier to Barn or Weapon mastery.
    /// Published by NightReferee just before NightEnded, only for non-zero amounts. Meta subscribes later.
    /// Today only Barn: the banked score (the live score at dawn; the last threshold reached when caught).
    /// </summary>
    public readonly struct NightBanked
    {
        public readonly MasteryDestination Destination; public readonly int Amount; public readonly int Multiplier;
        public NightBanked(MasteryDestination destination, int amount, int multiplier) { Destination = destination; Amount = amount; Multiplier = multiplier; }
    }

    /// <summary>
    /// Published once by the Rules layer (NightReferee) when the night is decided. All values are as of that moment.
    /// Score is the live score; BankedScore is what the night keeps (= Score at dawn, the last threshold reached
    /// when caught). HoursReached = thresholds crossed (= the threshold count at dawn).
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
