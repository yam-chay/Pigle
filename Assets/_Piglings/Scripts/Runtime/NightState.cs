using System.Collections.Generic;
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
        public int StonesLeft;     // throws left; a robot that breaches takes one
        public bool CanThrow;      // false during peg placement, once out of stones, after dawn, or night over
        public NightResult Result;
        public NightEndReason EndReason;

        // The hours (GDD "שעות הלילה").
        public int ThresholdsReached;    // thresholds crossed so far; the hour being played is this + 1
        public bool Dawn;                // the last threshold was crossed: won, ends once every chain settles
        public int PendingPegRounds;     // thresholds crossed whose placement round hasn't been played yet
        public int PegThrowsLeft;        // in the current placement round

        // The pegs. By string id, never a ScriptableObject: saves can store them, and Rules can't see Definitions.
        public PegSocket[] Sockets = new PegSocket[0];        // index = socket id (one per Hold); max pegs = this length
        public List<PegStack> Shelf = new List<PegStack>();   // in loadout order; up to PegSetup.ShelfCapacity types

        public int SweepScore;           // what the end-of-night sweep added (0 on a loss: there it's visual only)
        public int BankedScore;          // what the night keeps: the live score at dawn, the last threshold reached when caught
        public int BarnMastery;          // Σ Amount × Multiplier banked to the barn

        // Derived, not stored: Phase is the source of truth.
        public bool Ended => Phase == NightPhase.Ended;
        public int Hour => ThresholdsReached + 1;

        // Robots climb and the spawner runs while the night is Running — including between a threshold crossing and
        // its round (the player keeps throwing then). The wall freezes only for the placement round itself.
        public bool WallMoving => Phase == NightPhase.Running;
    }

    /// <summary>One socket on the wall (a Hold). Empty = a plain hold.</summary>
    [System.Serializable]
    public sealed class PegSocket
    {
        public string PegId;   // null or "" = empty
        public int Level;      // 0 when empty; 1 when placed; +1 per merge, up to the type's max level
        public bool IsEmpty => string.IsNullOrEmpty(PegId);
    }

    /// <summary>How many pegs of one type are left on the shelf.</summary>
    [System.Serializable]
    public sealed class PegStack
    {
        public string PegId;
        public int Count;
    }
}
