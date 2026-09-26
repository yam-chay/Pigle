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
}