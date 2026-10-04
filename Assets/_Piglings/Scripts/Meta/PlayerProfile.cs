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
        // unlocks — how many times each night (by NightDefinition id) was won. Towers: the slices chosen per night.
        public int CurrentNight;
        public readonly SortedDictionary<string, int> Dawns = new SortedDictionary<string, int>(StringComparer.Ordinal);
        public readonly SortedDictionary<string, List<string>> Towers = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);

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
            foreach (var r in Robots) parts.Add($"{r.Key} {r.Value.BallKnocks} ball knocks / {r.Value.KnockedByBall} knocked by a ball");
            foreach (var p in Pegs) parts.Add($"{p.Key} {p.Value.Knocks} knocks, triggers [{string.Join(", ", p.Value.Triggers)}]");
            if (CurrentNight > 0 || Dawns.Count > 0)
            {
                var dawns = new List<string>();
                foreach (var d in Dawns) dawns.Add($"{d.Key}×{d.Value}");
                parts.Add($"campaign night {CurrentNight + 1}, dawns {(dawns.Count == 0 ? "none" : string.Join(" ", dawns))}");
            }
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
    }
}
