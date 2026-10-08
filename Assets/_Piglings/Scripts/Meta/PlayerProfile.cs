using System;
using System.Collections.Generic;
using System.Text;

namespace Piglings.Meta
{
    /// <summary>
    /// The player's progress across nights and app restarts — the save. Stores CAUSES only (what was done), never results
    /// (levels, unlocks): those are derived from the causes by rules, so a rule can change without breaking a save.
    /// Keyed by definition id ("stone", "wolfbot_basic"), which is why those ids must never be renamed.
    /// Written only by Progression (banking) and ProfileJson (loading).
    /// </summary>
    public sealed class PlayerProfile
    {
        /// <summary>The save format this build writes and reads. Bump it (and add a migration) when the format changes meaning.</summary>
        public const int CurrentVersion = 1;

        public readonly SortedDictionary<string, WeaponRecord> Weapons = new SortedDictionary<string, WeaponRecord>(StringComparer.Ordinal);
        public readonly SortedDictionary<string, RobotRecord> Robots = new SortedDictionary<string, RobotRecord>(StringComparer.Ordinal);
        public readonly SortedDictionary<string, PegRecord> Pegs = new SortedDictionary<string, PegRecord>(StringComparer.Ordinal);

        // The campaign (v2 scene). Night index: where the player is (navigation, not a result). Dawns: the cause behind
        // unlocks — how many times each night (by NightDefinition id) was won. Tower (R3): THE player's tower, bottom → top,
        // by slice id — one tower that carries over from night to night (a taller night adds slots on top; a shorter one
        // uses the bottom part and keeps the rest). A choice, like CurrentNight, not a result.
        public int CurrentNight;
        public readonly SortedDictionary<string, int> Dawns = new SortedDictionary<string, int>(StringComparer.Ordinal);
        public readonly List<string> Tower = new List<string>();

        // Fast Retry (M10.E): the next scene load starts straight in the night — no barn room. Navigation like CurrentNight,
        // one-shot: the scene that reads it clears it (and saves) as it boots.
        public bool StartInNight;

        // Where this save came from (M11.T3), for the balance log: "" = normal flow; set by the debug panel ("scenario X",
        // "debug edit"). A label, not a cause — nothing in the game reads it. A new save (reset) starts as normal flow.
        public string Origin = "";

        // The all-time records (M10.E). Results, not causes — the exception to the rule above: they can't be derived from
        // anything saved (no per-night history is kept). A scoring retune doesn't rewrite them.
        public readonly NightRecords Records = new NightRecords();

        /// <summary>Robots knocked off the wall over every banked night, all types together (the post-run's all-time total).</summary>
        public int TotalDropped()
        {
            long sum = 0;
            foreach (var r in Robots) sum += r.Value.Dropped;
            return sum > int.MaxValue ? int.MaxValue : (int)sum;
        }

        /// <summary>How many times this night was won (reached dawn). 0 = never.</summary>
        public int DawnsOn(string nightId) => nightId != null && Dawns.TryGetValue(nightId, out int n) ? n : 0;

        /// <summary>Total robots this weapon knocked loose itself, over every banked night. 0 for a weapon never used.</summary>
        public int DirectHits(string weaponId) =>
            weaponId != null && Weapons.TryGetValue(weaponId, out var w) ? w.DirectHits : 0;

        public WeaponRecord Weapon(string weaponId)
        {
            if (!Weapons.TryGetValue(weaponId, out var w)) Weapons[weaponId] = w = new WeaponRecord();
            return w;
        }

        public RobotRecord Robot(string robotType)
        {
            if (!Robots.TryGetValue(robotType, out var r)) Robots[robotType] = r = new RobotRecord();
            return r;
        }

        public PegRecord Peg(string pegId)
        {
            if (!Pegs.TryGetValue(pegId, out var p)) Pegs[pegId] = p = new PegRecord();
            return p;
        }

        /// <summary>One line for the save/load log: "stone 137 hits · wolfbot_basic 412 ball knocks / 300 knocked by a ball".</summary>
        public string Describe()
        {
            var parts = new List<string>();
            foreach (var w in Weapons) parts.Add($"{w.Key} {w.Value.DirectHits} hits");
            foreach (var r in Robots) parts.Add($"{r.Key} {r.Value.BallKnocks} ball knocks / {r.Value.KnockedByBall} knocked by a ball / {r.Value.Dropped} dropped ({r.Value.Swept} swept)");
            foreach (var p in Pegs) parts.Add($"{p.Key} {p.Value.Knocks} knocks, triggers [{string.Join(", ", p.Value.Triggers)}]");
            if (CurrentNight > 0 || Dawns.Count > 0)
            {
                var dawns = new List<string>();
                foreach (var d in Dawns) dawns.Add($"{d.Key}×{d.Value}");
                parts.Add($"campaign night {CurrentNight + 1}, dawns {(dawns.Count == 0 ? "none" : string.Join(" ", dawns))}");
            }
            if (!string.IsNullOrEmpty(Origin)) parts.Add($"origin: {Origin}");
            if (Records.Any) parts.Add($"records: throw {Records.BestThrow}, chain {Records.LongestChain} wolves, depth {Records.DeepestChain}, night {Records.BestNightScore}");
            return parts.Count == 0 ? "empty (no banked nights)" : string.Join(" · ", parts);
        }
    }

    /// <summary>One weapon's saved use.</summary>
    public sealed class WeaponRecord
    {
        public int DirectHits;   // robots this weapon knocked loose itself (not through a ball)
    }

    /// <summary>One peg type's saved stats. Recorded for later (peg mastery?); nothing reads them yet.</summary>
    public sealed class PegRecord
    {
        public int Knocks;   // robots this peg type knocked loose itself (a Bomb's explosion), whatever set it off

        // Times its effect fired, per merged level (index 0 = level 1). Peg mastery = these weighted by level (PegProgression),
        // derived, so the weights can be retuned.
        public readonly List<int> Triggers = new List<int>();

        public int TriggersAt(int level) => level >= 1 && level <= Triggers.Count ? Triggers[level - 1] : 0;
    }

    /// <summary>One robot type's saved ball stats. Recorded for future wolf-lineage mastery; nothing reads them yet.</summary>
    public sealed class RobotRecord
    {
        public int BallKnocks;      // robots this type's ball knocked loose
        public int KnockedByBall;   // times this type was knocked loose by a ball
        public int Dropped;         // robots of this type knocked off the wall, whatever did it (the sweep too) — M10.E
        public int Swept;           // the part of Dropped the end-of-night sweep took

        /// <summary>Knocked loose by play (a stone, a ball, a bomb): Dropped without the sweep's share.</summary>
        public int KnockedInPlay => Dropped > Swept ? Dropped - Swept : 0;
    }

    /// <summary>The all-time records (M10.E): the best of every banked night. Written only by Progression.RecordNight.</summary>
    public sealed class NightRecords
    {
        public int BestThrow;        // the most points one throw (a closed chain) scored
        public int LongestChain;     // the most robots one throw dropped
        public int DeepestChain;     // the deepest depth one chain reached
        public int BestNightScore;   // the highest score a night ended with (the live score: dawn with its sweep)
        public int MostWolves;       // the most wolves one night dropped (any cause, the sweep too — the post-run's "+N")

        public bool Any => BestThrow > 0 || LongestChain > 0 || DeepestChain > 0 || BestNightScore > 0 || MostWolves > 0;

        public NightRecords Copy() => new NightRecords
        {
            BestThrow = BestThrow, LongestChain = LongestChain, DeepestChain = DeepestChain, BestNightScore = BestNightScore,
            MostWolves = MostWolves,
        };
    }
}
