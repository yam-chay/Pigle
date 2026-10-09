using System;

namespace Piglings.Meta
{
    /// <summary>
    /// Debug edits to a save (M11.T2, the F2 panel and scenario presets). The save keeps causes only, so a result the
    /// developer asks for ("12 stones", "3 Bouncy copies", "nights 1–2 won") is written as the smallest cause that gives
    /// it: the hits of that stone's threshold, the triggers of that copy's threshold, one dawn per night. Everything
    /// derived (level, refill, copies, follow-ups, unlocks) then follows from the rules, as for a real save — and lowering
    /// a value (reverting an evolution) works the same way. Never used by the game itself.
    /// </summary>
    public static class ProfileEdits
    {
        /// <summary>
        /// The fewest total hits that give this many stones (StartStones or less = 0; past the cap — the last Levels entry —
        /// = the last that counts).
        /// </summary>
        public static int HitsForStones(StoneProgression stone, int stones)
        {
            int extra = Math.Min(stones, stone.MaxStones) - stone.StartStones;
            if (extra <= 0) return 0;
            var t = stone.Thresholds;
            if (t.Count == 0) return 0;
            return t[Math.Min(extra, t.Count) - 1];
        }

        /// <summary>The stones an evolution needs (evolution 1 or below = the start, or its own level if later; past the list =
        /// the last).</summary>
        public static int StonesForLevel(StoneProgression stone, int level)
        {
            if (stone.Evolutions.Count == 0) return stone.StartStones;
            return Math.Max(stone.StartStones, stone.StonesForEvolution(level <= 1 ? 1 : level));
        }

        /// <summary>Set the weapon's saved hits so a night starts with this many stones (the level and refill follow).</summary>
        public static void SetStones(PlayerProfile profile, string weaponId, StoneProgression stone, int stones)
        {
            if (profile == null || string.IsNullOrEmpty(weaponId) || stone == null) return;
            profile.Weapon(weaponId).DirectHits = HitsForStones(stone, stones);
        }

        /// <summary>
        /// The fewest level-1 triggers that give this many copies (StartCopies or less = 0). A level-1 weight of 0 can't earn
        /// anything: 0 then too.
        /// </summary>
        public static int TriggersForCopies(PegProgression peg, int copies)
        {
            int reached = Math.Min(copies, peg.MaxCopies) - peg.StartCopies;
            var t = peg.Thresholds;
            if (reached <= 0 || t.Count == 0) return 0;
            float needed = t[Math.Min(reached, t.Count) - 1];
            float weight = peg.WeightAt(1);
            return weight > 0f ? (int)Math.Ceiling(needed / weight) : 0;
        }

        /// <summary>Replace a peg type's saved triggers with the fewest that give this many copies (all at level 1).</summary>
        public static void SetPegCopies(PlayerProfile profile, string pegId, PegProgression peg, int copies)
        {
            if (profile == null || string.IsNullOrEmpty(pegId) || peg == null) return;
            var record = profile.Peg(pegId);
            record.Triggers.Clear();
            int triggers = TriggersForCopies(peg, copies);
            if (triggers > 0) record.Triggers.Add(triggers);
        }

        /// <summary>
        /// The campaign's first <paramref name="count"/> nights won (one dawn each), the rest not: so nights 1..count+1 are
        /// open and their peg unlocks follow. Dawns on ids outside the campaign are left alone.
        /// </summary>
        public static void SetNightsWon(PlayerProfile profile, CampaignPlan plan, int count)
        {
            if (profile == null || plan == null) return;
            for (int i = 0; i < plan.NightCount; i++)
            {
                string id = plan.NightId(i);
                if (string.IsNullOrEmpty(id)) continue;
                if (i < count) { if (profile.DawnsOn(id) < 1) profile.Dawns[id] = 1; }
                else profile.Dawns.Remove(id);
            }
        }

        /// <summary>How many of the campaign's nights, from the first, have a dawn (stops at the first without).</summary>
        public static int NightsWon(PlayerProfile profile, CampaignPlan plan)
        {
            if (profile == null || plan == null) return 0;
            int n = 0;
            while (n < plan.NightCount && plan.HasDawn(profile, n)) n++;
            return n;
        }

        /// <summary>Where the campaign goes on the next load (0-based, kept inside the campaign).</summary>
        public static void SetCurrentNight(PlayerProfile profile, CampaignPlan plan, int index)
        {
            if (profile == null) return;
            int last = plan != null && plan.NightCount > 0 ? plan.NightCount - 1 : 0;
            profile.CurrentNight = index < 0 ? 0 : index > last ? last : index;
        }
    }
}
