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
        public int ThresholdsReached;    // thresholds crossed so far (banking, the next threshold, dawn)
        public int Hour = 1;             // the hour being played: +1 when a threshold's placement round STARTS (in the freeze),
                                         // not at the crossing — throws in between still score at the old hour
        public bool Dawn;                // the last threshold was crossed: won, ends once every chain settles
        public int PendingPegRounds;     // thresholds crossed whose placement round hasn't been played yet
        public int PegThrowsLeft;        // in the current placement round
        public string PegFollowUp;       // set = the next throw is a same-type follow-up and must be this peg id; null = any type
        public int PegFollowUpsLeft;     // follow-ups still to come after the pending one (this round's chain)

        // The pegs. By string id, never a ScriptableObject: saves can store them, and Rules can't see Definitions.
        public PegSocket[] Sockets = new PegSocket[0];        // index = socket id (one per Hold); max pegs = this length
        public List<PegStack> Shelf = new List<PegStack>();   // in loadout order; up to PegSetup.ShelfCapacity types

        // Per hour (index 0 = hour 1), written by NightReferee: for the [Night] log and the post-run screen.
        public List<HourStats> Hours = new List<HourStats>();

        // The best closed chain tonight (most points), with the hour it was thrown in (its quality = points ÷ that hour's gap).
        public int BestThrowPoints;
        public int BestThrowHour;        // 0 = no throw has scored yet

        public int SweepScore;           // what the end-of-night sweep added (0 on a loss: there it's visual only)
        public int BankedScore;          // what the night keeps: the live score at dawn, the last threshold reached when caught
        public int BarnMastery;          // Σ Amount × Multiplier banked to the barn

        // This night's use, for mastery. Written only by MasteryTally (Rules); banked by NightReferee when the night ends
        // (both outcomes), then forgotten with the scene — the saved totals live in Meta's PlayerProfile.
        // Keyed by definition id; sorted, so banking (and the save log) always comes out in the same order.
        public SortedDictionary<string, int> WeaponHits = new SortedDictionary<string, int>(System.StringComparer.Ordinal);    // weapon → robots it knocked loose itself
        public SortedDictionary<string, int> BallKnocks = new SortedDictionary<string, int>(System.StringComparer.Ordinal);    // robot type → robots its ball knocked loose
        public SortedDictionary<string, int> KnockedByBall = new SortedDictionary<string, int>(System.StringComparer.Ordinal); // robot type → times it was knocked loose by a ball
        public SortedDictionary<string, int> PegKnocks = new SortedDictionary<string, int>(System.StringComparer.Ordinal);     // peg id → robots its effect knocked loose (Bomb)
        // peg id → times its effect fired, per merged level (index 0 = level 1): Bouncy bonus granted, Splitter split, Bomb explosion.
        public SortedDictionary<string, List<int>> PegTriggers = new SortedDictionary<string, List<int>>(System.StringComparer.Ordinal);

        // Derived, not stored: Phase is the source of truth.
        public bool Ended => Phase == NightPhase.Ended;

        // Robots climb and the spawner runs while the night is Running — including between a threshold crossing and
        // its round (the player keeps throwing then). The wall freezes only for the placement round itself.
        public bool WallMoving => Phase == NightPhase.Running;
    }

    /// <summary>What happened during one hour of the night (the hour a chain was thrown in, for its points).</summary>
    [System.Serializable]
    public sealed class HourStats
    {
        public int Score;      // points from chains thrown in this hour (not the end-of-night sweep)
        public int Breaches;   // robots that reached the roof during this hour
    }

    /// <summary>One socket on the wall (a Hold). Empty = a plain hold.</summary>
    [System.Serializable]
    public sealed class PegSocket
    {
        public string PegId;   // null or "" = empty
        public int Level;      // 0 when empty; 1 when placed; +1 per merge, up to the type's max level
        public float Recharge; // Bomb: seconds until it can go off again; 0 = charged. Counts down only while the night is Running.
        public bool IsEmpty => string.IsNullOrEmpty(PegId);
        public bool IsSpent => Recharge > 0f;
    }

    /// <summary>How many pegs of one type are left on the shelf.</summary>
    [System.Serializable]
    public sealed class PegStack
    {
        public string PegId;
        public int Count;
    }
}
