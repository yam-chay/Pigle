using System.Collections.Generic;

namespace Piglings.Meta
{
    /// <summary>One stone evolution (R2): unlocked at this stone level (1 = from the start), the pile gets this many stones
    /// back at each hour's placement round, and each throw starts its chain's score with this base (M10.S: 10 / 20 / 40 /
    /// 80). Its place in the list + 1 is the evolution number (the look, radius and trail).</summary>
    public readonly struct StoneEvolution
    {
        public readonly int AtLevel;
        public readonly int Refill;
        public readonly int BaseScore;

        public StoneEvolution(int atLevel, int refill, int baseScore = 10)
        {
            AtLevel = atLevel < 1 ? 1 : atLevel; Refill = refill < 0 ? 0 : refill; BaseScore = baseScore < 0 ? 0 : baseScore;
        }
    }

    /// <summary>Where the stone stands, all derived from its saved direct hits (StoneProgression.For).</summary>
    public readonly struct StoneStatus
    {
        public readonly int Stones;              // on the pile when a night starts
        public readonly int StoneLevel;          // 1 + the Levels entries reached (each one = +1 stone); what evolutions key on
        public readonly int Level;               // 1, 2, 3…: the EVOLUTION reached — its look, radius and trail
        public readonly int Refill;              // stones added at each hour's placement round
        public readonly int NextThreshold;       // total hits for the next +1 stone; -1 at the cap
        public readonly int PreviousThreshold;   // total hits of the last +1 earned (0 before the first): where the bar starts
        public readonly int NextEvolutionStones; // stones the next evolution needs; -1 = none left (or past the cap)

        public StoneStatus(int stones, int stoneLevel, int level, int refill, int nextThreshold, int previousThreshold,
                           int nextEvolutionStones)
        {
            Stones = stones; StoneLevel = stoneLevel < 1 ? 1 : stoneLevel; Level = level < 1 ? 1 : level; Refill = refill; NextThreshold = nextThreshold;
            PreviousThreshold = previousThreshold; NextEvolutionStones = nextEvolutionStones;
        }

        public bool AtCap => NextThreshold < 0;

        /// <summary>The next evolution's level (Level + 1), or 0 when there's none left.</summary>
        public int NextEvolutionLevel => NextEvolutionStones < 0 ? 0 : Level + 1;

        /// <summary>0..1 from the last +1 toward the next (1 at the cap).</summary>
        public float Progress(int hits) =>
            AtCap ? 1f : Clamp01((hits - PreviousThreshold) / (float)(NextThreshold - PreviousThreshold));

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }

    /// <summary>
    /// The stone's progression rule (R2). Hits are the saved cause; everything else is derived here:
    /// the Levels list (Thresholds) holds the total direct hits for each stone level after the first; each one reached is
    /// +1 stone on top of StartStones. The last entry is the cap (MaxStones = StartStones + entries) — no separate maximum.
    /// The evolutions are keyed by the stone level they unlock at (in order: entry 0 = evolution 1); the evolution is how
    /// many entries the stone level reaches (at least 1), the refill that entry's.
    /// Thresholds are cumulative and strictly rising (MasteryLevels.Problem checks them; Problem below checks the
    /// evolutions).
    /// </summary>
    public sealed class StoneProgression
    {
        private readonly int[] _thresholds;
        private readonly StoneEvolution[] _evolutions;

        /// <summary>The default evolutions: at stone levels 1 / 6 / 11 / 16 (10 / 15 / 20 / 25 stones from a start of 10),
        /// refill +1 / +2 / +3 / +4, base 10 / 20 / 40 / 80.</summary>
        public static readonly StoneEvolution[] DefaultEvolutions =
        {
            new StoneEvolution(1, 1, 10), new StoneEvolution(6, 2, 20), new StoneEvolution(11, 3, 40), new StoneEvolution(16, 4, 80),
        };

        public int StartStones { get; }
        /// <summary>The cap: StartStones + one per Levels entry.</summary>
        public int MaxStones => StartStones + _thresholds.Length;
        /// <summary>The highest stone level: 1 + the Levels entries.</summary>
        public int MaxStoneLevel => 1 + _thresholds.Length;
        public IReadOnlyList<int> Thresholds => _thresholds;
        public IReadOnlyList<StoneEvolution> Evolutions => _evolutions;

        /// <param name="evolutions">Null = the defaults; empty = one level, refill 1.</param>
        public StoneProgression(IReadOnlyList<int> thresholds, int startStones = 10, IReadOnlyList<StoneEvolution> evolutions = null)
        {
            _thresholds = new int[thresholds?.Count ?? 0];
            for (int i = 0; i < _thresholds.Length; i++) _thresholds[i] = thresholds[i];
            StartStones = startStones < 0 ? 0 : startStones;
            if (evolutions == null) _evolutions = (StoneEvolution[])DefaultEvolutions.Clone();
            else if (evolutions.Count == 0) _evolutions = new[] { new StoneEvolution(1, 1) };
            else
            {
                _evolutions = new StoneEvolution[evolutions.Count];
                for (int i = 0; i < _evolutions.Length; i++) _evolutions[i] = evolutions[i];
            }
        }

        /// <summary>A throw's base score at this level (M10.S): the evolution's Base Score; past the list → the last.</summary>
        public int BaseScoreFor(int level) => _evolutions[level < 1 ? 0 : level > _evolutions.Length ? _evolutions.Length - 1 : level - 1].BaseScore;

        /// <summary>The stones on the pile at this stone level (StartStones at level 1, +1 per level).</summary>
        public int StonesAtLevel(int stoneLevel) => StartStones + (stoneLevel < 1 ? 0 : stoneLevel - 1);

        /// <summary>The stones evolution n (1-based) unlocks at; past the list → the last.</summary>
        public int StonesForEvolution(int evolution) =>
            StonesAtLevel(_evolutions[evolution < 1 ? 0 : evolution > _evolutions.Length ? _evolutions.Length - 1 : evolution - 1].AtLevel);

        /// <summary>
        /// Where a stone count sits on the evolution track, 0..1 (the post-run's evolution bar): the evolutions are evenly
        /// spaced slots (first = 0, last = 1), and between two slots the bar moves by the stones between them.
        /// Below the first → 0, past the last → 1; one evolution → 1 once reached.
        /// </summary>
        public float TrackPosition(float stones)
        {
            int n = _evolutions.Length;
            if (n <= 1) return n == 1 && stones >= StonesForEvolution(1) ? 1f : 0f;
            if (stones <= StonesForEvolution(1)) return 0f;
            for (int i = 0; i < n - 1; i++)
            {
                int from = StonesForEvolution(i + 1), to = StonesForEvolution(i + 2);
                if (stones >= to) continue;
                float within = to > from ? (stones - from) / (float)(to - from) : 1f;
                return (i + within) / (n - 1);
            }
            return 1f;
        }

        /// <summary>
        /// The stone count with its progress toward the next +1 stone as the fraction (14 stones, 40% to the 15th = 14.4) —
        /// so the evolution bar moves a little every night, not only when a whole stone is earned. At the cap: the count.
        /// </summary>
        public float StonesWithProgress(int hits)
        {
            var status = For(hits);
            return status.AtCap ? status.Stones : status.Stones + status.Progress(hits);
        }

        /// <summary>The evolution a stone level gives: entries reached in order (a gap stops it), at least 1.</summary>
        public int EvolutionFor(int stoneLevel)
        {
            int evolution = 0;
            while (evolution < _evolutions.Length && stoneLevel >= _evolutions[evolution].AtLevel) evolution++;
            return evolution < 1 ? 1 : evolution;
        }

        public StoneStatus For(int hits)
        {
            // Levels entries reached; the last entry is the cap, so this never passes MaxStones.
            int reached = MasteryLevels.LevelFor(hits, _thresholds) - 1;
            int stoneLevel = 1 + reached;
            int stones = StonesAtLevel(stoneLevel);
            int evolution = EvolutionFor(stoneLevel);
            int refill = _evolutions[evolution - 1].Refill;
            int next = reached < _thresholds.Length ? _thresholds[reached] : -1;
            int previous = reached >= 1 ? _thresholds[reached - 1] : 0;
            // The next evolution, if one is left and the cap lets the stone get there.
            int nextEvolution = evolution < _evolutions.Length && _evolutions[evolution].AtLevel <= MaxStoneLevel
                ? StonesAtLevel(_evolutions[evolution].AtLevel) : -1;
            return new StoneStatus(stones, stoneLevel, evolution, refill, next, previous, nextEvolution);
        }

        /// <summary>What's wrong with an evolution list (for an Inspector warning), or null: At Level must rise.</summary>
        public static string Problem(IReadOnlyList<StoneEvolution> evolutions)
        {
            if (evolutions == null) return null;
            for (int i = 1; i < evolutions.Count; i++)
                if (evolutions[i].AtLevel <= evolutions[i - 1].AtLevel)
                    return $"evolution {i + 1} unlocks at level {evolutions[i].AtLevel}, not above evolution {i}'s {evolutions[i - 1].AtLevel}";
            return null;
        }
    }
}
