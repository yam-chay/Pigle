namespace Piglings.Rules
{
    /// <summary>
    /// What it takes to pass a night, as plain values (Rules can't read NightDefinition).
    /// Built once per night by NightSession from the NightDefinition asset.
    /// There is no breach limit: the pile of stones is the pig's life (see NightReferee).
    /// </summary>
    public sealed class NightGoal
    {
        public int TargetScore { get; }
        public int ThrowsAvailable { get; }   // stones on the pile when the night starts

        public NightGoal(int targetScore = 500, int throwsAvailable = 30)
        {
            // At least 1: with a target of 0 you could never throw, and the night would never end.
            TargetScore = targetScore < 1 ? 1 : targetScore;
            ThrowsAvailable = throwsAvailable;
        }
    }
}
