namespace Piglings.Rules
{
    /// <summary>
    /// What it takes to pass a night, as plain values (Rules can't read NightDefinition).
    /// Built once per night by NightSession from the NightDefinition asset.
    /// </summary>
    public sealed class NightGoal
    {
        public int TargetScore { get; }
        public int ThrowsAvailable { get; }
        public int StonesLostPerBreach { get; }   // 0 = breaches don't cost stones
        public int MaxBreaches { get; }           // 0 = no breach limit

        public NightGoal(int targetScore = 500, int throwsAvailable = 30, int stonesLostPerBreach = 1, int maxBreaches = 5)
        {
            // At least 1: with a target of 0 you could never throw, and the night would never end.
            TargetScore = targetScore < 1 ? 1 : targetScore;
            ThrowsAvailable = throwsAvailable;
            StonesLostPerBreach = stonesLostPerBreach < 0 ? 0 : stonesLostPerBreach;
            MaxBreaches = maxBreaches < 0 ? 0 : maxBreaches;
        }
    }
}
