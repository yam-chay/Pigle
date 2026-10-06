using System.Collections.Generic;

namespace Piglings.Meta
{
    /// <summary>One stone evolution: from this many stones the stone is this level (its place in the list + 1), the
    /// pile gets this many stones back at each hour's placement round, and each throw starts its chain's score with this
    /// base (M10.S: 10 / 20 / 40 / 80).</summary>
    public readonly struct StoneEvolution
    {
        public readonly int StonesNeeded;
        public readonly int Refill;
        public readonly int BaseScore;

        public StoneEvolution(int stonesNeeded, int refill, int baseScore = 10)
        {
            StonesNeeded = stonesNeeded; Refill = refill < 0 ? 0 : refill; BaseScore = baseScore < 0 ? 0 : baseScore;
        }
    }

    /// <summary>Where the stone stands, all derived from its saved direct hits (StoneProgression.For).</summary>
    public readonly struct StoneStatus
    {
        public readonly int Stones;              // on the pile when a night starts
        public readonly int Level;               // 1, 2, 3…: its look, radius and trail (the evolution reached)
        public readonly int Refill;              // stones added at each hour's placement round
        public readonly int NextThreshold;       // total hits for the next +1 stone; -1 at the cap
        public readonly int PreviousThreshold;   // total hits of the last +1 earned (0 before the first): where the bar starts
        public readonly int NextEvolutionStones; // stones the next evolution needs; -1 = none left (or past the cap)

        public StoneStatus(int stones, int level, int refill, int nextThreshold, int previousThreshold, int nextEvolutionStones)
        {
            Stones = stones; Level = level < 1 ? 1 : level; Refill = refill; NextThreshold = nextThreshold;
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
    /// The stone's progression rule. Hits are the saved cause; everything else is derived here:
    /// start with StartStones; each threshold of total direct hits reached gives +1 stone, up to MaxStones. The stone's
    /// level and its hourly refill come from the stone count through the evolutions (in order: entry 0 = level 1): the
    /// level is how many entries' StonesNeeded the count reaches (at least 1), the refill that entry's.
    /// Thresholds are cumulative and strictly rising (MasteryLevels.Problem checks them; Problem below checks the
    /// evolutions); only the first MaxStones − StartStones of them can ever count.
    /// </summary>
    public sealed class StoneProgression
    {
        private readonly int[] _thresholds;
        private readonly StoneEvolution[] _evolutions;

        /// <summary>The default evolutions: 10 → lv1 +1 · 15 → lv2 +2 · 20 → lv3 +3 · 25 → lv4 +4; base 10 / 20 / 40 / 80.</summary>
        public static readonly StoneEvolution[] DefaultEvolutions =
        {
            new StoneEvolution(10, 1, 10), new StoneEvolution(15, 2, 20), new StoneEvolution(20, 3, 40), new StoneEvolution(25, 4, 80),
        };

        public int StartStones { get; }
        public int MaxStones { get; }
        public IReadOnlyList<int> Thresholds => _thresholds;
        public IReadOnlyList<StoneEvolution> Evolutions => _evolutions;

        /// <param name="evolutions">Null = the defaults; empty = one level, refill 1.</param>
        public StoneProgression(IReadOnlyList<int> thresholds, int startStones = 10, int maxStones = 25,
                                IReadOnlyList<StoneEvolution> evolutions = null)
        {
            _thresholds = new int[thresholds?.Count ?? 0];
            for (int i = 0; i < _thresholds.Length; i++) _thresholds[i] = thresholds[i];
            StartStones = startStones < 0 ? 0 : startStones;
            MaxStones = maxStones < StartStones ? StartStones : maxStones;
            if (evolutions == null) _evolutions = (StoneEvolution[])DefaultEvolutions.Clone();
            else if (evolutions.Count == 0) _evolutions = new[] { new StoneEvolution(0, 1) };
            else
            {
                _evolutions = new StoneEvolution[evolutions.Count];
                for (int i = 0; i < _evolutions.Length; i++) _evolutions[i] = evolutions[i];
            }
        }

        /// <summary>A throw's base score at this level (M10.S): the evolution's Base Score; past the list → the last.</summary>
        public int BaseScoreFor(int level) => _evolutions[level < 1 ? 0 : level > _evolutions.Length ? _evolutions.Length - 1 : level - 1].BaseScore;

        /// <summary>
        /// Where a stone count sits on the evolution track, 0..1 (the post-run's evolution bar): the evolutions are evenly
        /// spaced slots (first = 0, last = 1), and between two slots the bar moves by the stones between their Stones Needed.
        /// Below the first → 0, past the last → 1; one evolution → 1 once reached.
        /// </summary>
        public float TrackPosition(float stones)
        {
            int n = _evolutions.Length;
            if (n <= 1) return n == 1 && stones >= _evolutions[0].StonesNeeded ? 1f : 0f;
            if (stones <= _evolutions[0].StonesNeeded) return 0f;
            for (int i = 0; i < n - 1; i++)
            {
                int from = _evolutions[i].StonesNeeded, to = _evolutions[i + 1].StonesNeeded;
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

        /// <summary>The level a stone count gives: entries reached in order (a gap stops it), at least 1.</summary>
        public int LevelFor(int stones)
        {
            int level = 0;
            while (level < _evolutions.Length && stones >= _evolutions[level].StonesNeeded) level++;
            return level < 1 ? 1 : level;
        }

        public StoneStatus For(int hits)
        {
            int reached = MasteryLevels.LevelFor(hits, _thresholds) - 1;
            int stones = StartStones + reached;
            if (stones > MaxStones) { reached -= stones - MaxStones; stones = MaxStones; }
            int level = LevelFor(stones);
            int refill = _evolutions[level - 1].Refill;
            int next = stones < MaxStones && reached < _thresholds.Length ? _thresholds[reached] : -1;
            int previous = reached >= 1 ? _thresholds[reached - 1] : 0;
            // The next evolution, if one is left and the cap lets the stone get there.
            int nextEvolution = level < _evolutions.Length && _evolutions[level].StonesNeeded <= MaxStones ? _evolutions[level].StonesNeeded : -1;
            return new StoneStatus(stones, level, refill, next, previous, nextEvolution);
        }

        /// <summary>What's wrong with an evolution list (for an Inspector warning), or null: Stones Needed must rise.</summary>
        public static string Problem(IReadOnlyList<StoneEvolution> evolutions)
        {
            if (evolutions == null) return null;
            for (int i = 1; i < evolutions.Count; i++)
                if (evolutions[i].StonesNeeded <= evolutions[i - 1].StonesNeeded)
                    return $"evolution {i + 1} needs {evolutions[i].StonesNeeded} stones, not more than evolution {i}'s {evolutions[i - 1].StonesNeeded}";
            return null;
        }
    }
}
