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

    public enum RemovalReason { HitGround, EnteredBarn }

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

    public enum NightResult { Won, Lost }
    public enum NightEndReason { TargetReached, OutOfStones, BarnBreached }

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
