namespace Piglings.Events
{
    /// <summary>
    /// A chain starts when the pig releases a throw. Everything that falls because of that
    /// throw — directly or through other falling robots — shares the same ChainId.
    /// </summary>
    public readonly struct ChainId
    {
        public readonly GameId Id;
        public ChainId(GameId id) { Id = id; }
        public static readonly ChainId None = new ChainId(GameId.None);
        public bool IsNone => Id.IsNone;
        public override string ToString() => $"Chain{Id}";
    }

    public enum CauseKind
    {
        Throwable,   // the pig's thrown object hit it
        RobotBall,   // another robot's falling ball hit it
        Hazard       // traps, boiling pot, etc. (later)
    }

    /// <summary>
    /// Who made this robot lose its grip, and how deep in the chain it happened.
    /// Depth 0 = hit directly by the throw; depth 1 = hit by a ball that was hit by the throw; ...
    /// </summary>
    public readonly struct Attribution
    {
        public readonly CauseKind Kind;
        public readonly GameId Source;
        public readonly int Depth;

        public Attribution(CauseKind kind, GameId source, int depth)
        {
            Kind = kind; Source = source; Depth = depth;
        }

        public static Attribution FromThrowable(GameId throwable) => new Attribution(CauseKind.Throwable, throwable, 0);

        /// <summary>A falling robot passes its own depth + 1 to the robot it knocks off.</summary>
        public static Attribution FromRobotBall(GameId robot, int robotDepth) => new Attribution(CauseKind.RobotBall, robot, robotDepth + 1);
    }
}
