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

    // TimedOut: a falling ball that never reached the ground (e.g. resting on a hold) — removed so its chain can close.
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
    /// Total = Received × Multiplier (rounded); Carries = what this robot's own ball now carries.
    /// Order is its place in the chain (1 = first to lose grip). Presentation shows "Received ×Multiplier", then "+Total".
    /// </summary>
    public readonly struct RobotScored
    {
        public readonly GameId Robot; public readonly ChainId Chain;
        public readonly int Order; public readonly int Depth;
        public readonly int Received; public readonly float Multiplier; public readonly int Total; public readonly int Carries;
        public RobotScored(GameId robot, ChainId chain, int order, int depth, int received, float multiplier, int total, int carries)
        {
            Robot = robot; Chain = chain; Order = order; Depth = depth;
            Received = received; Multiplier = multiplier; Total = total; Carries = carries;
        }
    }

    /// <summary>
    /// Published by the Rules layer right after ChainClosed: the chain's value, which is the sum of
    /// its RobotScored totals (nothing extra is added at close). Also published for misses, with zeros.
    /// </summary>
    public readonly struct ChainScored
    {
        public readonly ChainId Chain; public readonly int RobotsDropped; public readonly int MaxDepth; public readonly int Total;
        public ChainScored(ChainId chain, int robotsDropped, int maxDepth, int total)
        {
            Chain = chain; RobotsDropped = robotsDropped; MaxDepth = maxDepth; Total = total;
        }
    }

    /// <summary>
    /// Running → ChoicePending → Overtime → Ended, or ChoicePending → Ended (Leave), or Running → Ended (lost).
    /// Reaching the target ends the danger, not the night: from ChoicePending on, the night can't be lost.
    /// </summary>
    public enum NightPhase { Running, ChoicePending, Overtime, Ended }
    // Named StayOrLeave, not NightChoice: Simulation has a NightChoice component (the buttons), and code that
    // uses both namespaces (every Presentation view) would find the name ambiguous.
    public enum StayOrLeave { None, Stay, Leave }
    public enum NightResult { Won, Lost }
    // OutOfStones: lost in Running, or overtime used up its stones (a win). DangerLine: overtime ended by a robot at the line.
    public enum NightEndReason { OutOfStones, BarnBreached, Left, DangerLine }
    public enum MasteryDestination { Barn, Weapon }

    /// <summary>Published by NightReferee on every phase change. Simulation pauses, resumes and sweeps the wall off this.</summary>
    public readonly struct NightPhaseChanged
    {
        public readonly NightPhase From; public readonly NightPhase To;
        public NightPhaseChanged(NightPhase from, NightPhase to) { From = from; To = to; }
    }

    /// <summary>The score first reached the target (chains may still be falling). Once per night.</summary>
    public readonly struct NightTargetReached
    {
        public readonly int Score; public readonly int StonesLeft;
        public NightTargetReached(int score, int stonesLeft) { Score = score; StonesLeft = stonesLeft; }
    }

    /// <summary>The player chose what their leftover stones are for.</summary>
    public readonly struct NightChoiceMade
    {
        public readonly StayOrLeave Choice;
        public NightChoiceMade(StayOrLeave choice) { Choice = choice; }
    }

    /// <summary>
    /// A robot still on the wall was knocked off by the end-of-night sweep (published by Simulation while it
    /// sweeps). Not part of any chain: NightReferee scores it flat with the robot-value function.
    /// </summary>
    public readonly struct RobotSwept
    {
        public readonly GameId Robot;
        public RobotSwept(GameId robot) { Robot = robot; }
    }

    /// <summary>
    /// Where points went when the night ended: Amount × Multiplier to Barn or Weapon mastery.
    /// Published by NightReferee just before NightEnded, only for non-zero amounts. Meta subscribes later.
    /// </summary>
    public readonly struct NightBanked
    {
        public readonly MasteryDestination Destination; public readonly int Amount; public readonly int Multiplier;
        public NightBanked(MasteryDestination destination, int amount, int multiplier) { Destination = destination; Amount = amount; Multiplier = multiplier; }
    }

    /// <summary>
    /// Published once by the Rules layer (NightReferee) when the night is decided.
    /// All values are as of that moment. Leftover stones are recorded for future rewards;
    /// ThrowsUsed counts stones actually thrown (not ones lost to breaches), for score per stone.
    /// </summary>
    public readonly struct NightEnded
    {
        public readonly NightResult Result; public readonly NightEndReason Reason;
        public readonly int Score; public readonly int TargetScore; public readonly int StonesLeft; public readonly int Breaches;
        public readonly int ThrowsUsed;
        public NightEnded(NightResult result, NightEndReason reason, int score, int targetScore, int stonesLeft, int breaches, int throwsUsed)
        {
            Result = result; Reason = reason; Score = score; TargetScore = targetScore; StonesLeft = stonesLeft; Breaches = breaches;
            ThrowsUsed = throwsUsed;
        }
    }
}
