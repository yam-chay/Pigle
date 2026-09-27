using Piglings.Events;

namespace Piglings.Runtime
{
    /// <summary>
    /// Plain serializable state of the current night. No behaviour, no engine types.
    /// Rules write to it; Presentation only reads it.
    /// </summary>
    [System.Serializable]
    public sealed class NightState
    {
        public int NightNumber = 1;
        public int ThrowsUsed;
        public int RobotsDropped;
        public int RobotsReachedTop;
        public int Score;
        public int LongestChain;   // most robots dropped by a single throw
        public int DeepestChain;   // highest depth reached in any chain

        // Written by NightReferee. Simulation reads Phase and CanThrow; Presentation reads the rest.
        public NightPhase Phase;   // the one source of truth for where the night is
        public int StonesLeft;     // throws left; a robot reaching the top (before the target) costs stones too
        public bool CanThrow;      // false while paused, once out of stones, after the target (until Stay), or night over
        public NightResult Result;
        public NightEndReason EndReason;
        public StayOrLeave Choice;

        public int StonesAtTarget = -1;  // stones left when the score first reached the target; -1 = not yet
        public int ScoreAtChoice;        // score when the choice appeared (target + overshoot)
        public int OvertimeScore;        // everything scored after Stay, sweep included (banks ×2 to the barn)
        public int SweepScore;           // what the end-of-night sweep added
        public int BarnMastery;          // Σ Amount × Multiplier banked to the barn
        public int WeaponMastery;        // Σ Amount × Multiplier banked to the weapon

        // Derived, not stored: Phase is the source of truth.
        public bool Ended => Phase == NightPhase.Ended;
    }
}
