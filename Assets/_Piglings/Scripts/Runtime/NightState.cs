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

        // Written by NightReferee. Simulation reads CanThrow; Presentation reads the rest.
        public int StonesLeft;     // throws left; a robot reaching the top costs stones too
        public bool CanThrow;      // false once out of stones, target reached, or night over
        public bool Ended;
        public NightResult Result;
        public NightEndReason EndReason;
    }
}
