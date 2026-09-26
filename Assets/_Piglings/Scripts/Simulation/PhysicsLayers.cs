namespace Piglings.Simulation
{
    /// <summary>
    /// Layer names — must match Project Settings > Tags and Layers exactly.
    /// Collision matrix (see SETUP.md): Holds collide ONLY with RobotBall and Throwable,
    /// so climbing robots pass through holds but falling balls bounce off them.
    /// </summary>
    public static class PhysicsLayers
    {
        public const string RobotClimbing = "RobotClimbing";
        public const string RobotBall = "RobotBall";
        public const string Throwable = "Throwable";
        public const string Holds = "Holds";
        public const string BarnWalls = "BarnWalls";
        public const string Ground = "Ground";
        public const string Zones = "Zones";
    }
}
