using System;
using System.Collections.Generic;

namespace Piglings.Meta
{
    /// <summary>
    /// One post-run bar (M10.E): where it stood BEFORE tonight (drawn dim) and where tonight took it (the bright gain), both
    /// 0..1 of the same span — the one the player was working on when the night began. Ready = tonight completed it (the
    /// bar shows full and a READY tag); what it earned waits in the barn.
    /// </summary>
    public readonly struct ProgressBar
    {
        public readonly float Before;
        public readonly float After;
        public readonly bool Ready;

        public ProgressBar(float before, float after, bool ready)
        {
            Before = Clamp01(before);
            After = ready ? 1f : Clamp01(after) < Before ? Before : Clamp01(after);   // a bar never runs backwards
            Ready = ready;
        }

        public float Gain => After - Before;

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }

    /// <summary>The stone's row: tonight's hits, the stones before → after, the bar to the next +1 stone.</summary>
    public sealed class StoneProgressRow
    {
        public int HitsBefore, HitsAfter;
        public StoneStatus Before, After;
        public ProgressBar Bar;
        /// <summary>Along the evolution track (StoneProgression.TrackPosition): before tonight → now; READY = a new evolution tonight.</summary>
        public ProgressBar EvolutionBar;
        public int HitsGained => HitsAfter - HitsBefore;
        public bool EvolvedTonight;
    }

    /// <summary>A peg type's row — only when it triggered tonight, or tonight's dawn unlocked it.</summary>
    public sealed class PegProgressRow
    {
        public string PegId;
        public bool UnlockedTonight;
        public int UnlockedByNight;    // 1-based: "unlocked by dawn on night n" (0 = a starting type)
        public int TriggersTonight;
        public int CopiesBefore;       // 0 when it was locked before tonight
        public PegStatus Before, After;
        public ProgressBar Bar;

        public int CopiesAfter => After.Copies;

        /// <summary>The next follow-up step: at NextFollowUpAt copies, a throw of it gives this many in a row (1 + its follow-ups).
        /// 0 = no step left.</summary>
        public int InARowAtNext => After.NextFollowUpAt < 0 ? 0 : After.FollowUps + 2;
    }

    /// <summary>The all-time records before and after tonight, tonight's own values, and which broke (NEW RECORD).</summary>
    public sealed class RecordsReport
    {
        public NightRecords Before, After;
        public NightRecords Tonight = new NightRecords();   // tonight's bests (M10.S: the rows' "how close you got" bars)
        public RecordsBroken New;

        /// <summary>
        /// How close tonight came to the record (after tonight): tonight ÷ record, 0..1 — 1 when broken or tied. A record of 0
        /// (nothing yet) reads 1 if tonight had any, else 0.
        /// </summary>
        public static float Closeness(int tonight, int record)
        {
            if (tonight <= 0) return 0f;
            if (record <= 0) return 1f;
            float f = tonight / (float)record;
            return f > 1f ? 1f : f;
        }
    }

    /// <summary>What the post-run's PROGRESS and ALL-TIME RECORDS panels show (PostRunProgress.Build).</summary>
    public sealed class PostRunReport
    {
        public StoneProgressRow Stone;   // null without a stone rule
        public readonly List<PegProgressRow> Pegs = new List<PegProgressRow>();
        public int WolvesTonight;
        public int WolvesTotal;
        public RecordsReport Records;
    }

    /// <summary>
    /// Builds the post-run's report from two profiles: as it was BEFORE tonight (a copy taken when the scene loaded) and as
    /// it is AFTER (tonight banked into it). Everything is derived from those, so it's the same rule as the barn's and
    /// checkable outside Unity. "Only what moved": a peg type gets a row only if its saved triggers changed tonight (it
    /// fired) or tonight unlocked it; the stone and the wolves always have one.
    /// </summary>
    public static class PostRunProgress
    {
        /// <param name="pegTypes">Every peg type of the campaign, in its order (rows come out in this order).</param>
        /// <param name="pegRule">A type's progression rule (copy thresholds, weights); null for an unknown type = no row.</param>
        /// <param name="tonight">Tonight's bests (best throw, longest / deepest chain, the night's score); null = none.</param>
        public static PostRunReport Build(PlayerProfile before, PlayerProfile after, string weaponId, StoneProgression stones,
                                          CampaignPlan plan, IReadOnlyList<string> pegTypes, Func<string, PegProgression> pegRule,
                                          NightRecords tonight = null)
        {
            before = before ?? new PlayerProfile();
            after = after ?? new PlayerProfile();
            var report = new PostRunReport();

            if (stones != null) report.Stone = StoneRow(before.DirectHits(weaponId), after.DirectHits(weaponId), stones);

            if (plan != null && pegTypes != null && pegRule != null)
                foreach (var id in pegTypes)
                {
                    var row = PegRow(id, before, after, plan, pegRule(id));
                    if (row != null) report.Pegs.Add(row);
                }

            report.WolvesTotal = after.TotalDropped();
            report.WolvesTonight = Math.Max(0, report.WolvesTotal - before.TotalDropped());

            var b = before.Records; var a = after.Records;
            report.Records = new RecordsReport
            {
                Before = b.Copy(), After = a.Copy(), Tonight = tonight != null ? tonight.Copy() : new NightRecords(),
                New = new RecordsBroken(a.BestThrow > b.BestThrow, a.LongestChain > b.LongestChain, a.DeepestChain > b.DeepestChain,
                                        a.BestNightScore > b.BestNightScore),
            };
            return report;
        }

        /// <summary>The stone's row. The bar is the span to the next +1 stone as it stood before tonight; a stone earned fills it.</summary>
        public static StoneProgressRow StoneRow(int hitsBefore, int hitsAfter, StoneProgression stones)
        {
            var row = new StoneProgressRow
            {
                HitsBefore = hitsBefore, HitsAfter = hitsAfter, Before = stones.For(hitsBefore), After = stones.For(hitsAfter),
            };
            bool earned = row.After.Stones > row.Before.Stones;
            row.Bar = new ProgressBar(row.Before.Progress(hitsBefore), row.Before.Progress(hitsAfter), earned);
            float from = stones.TrackPosition(row.Before.Stones), to = stones.TrackPosition(row.After.Stones);
            // Not forced full when READY: the bar ends where the stones are, on the slot of the evolution reached.
            row.EvolutionBar = new ProgressBar(from, to, false);
            row.EvolvedTonight = row.After.Level > row.Before.Level;
            return row;
        }

        // Null when the type has nothing to show: still locked, or unlocked before and didn't fire tonight.
        private static PegProgressRow PegRow(string id, PlayerProfile before, PlayerProfile after, CampaignPlan plan, PegProgression rule)
        {
            if (rule == null || string.IsNullOrEmpty(id) || !plan.IsUnlocked(after, id)) return null;
            bool unlockedTonight = !plan.IsUnlocked(before, id);
            var triggersBefore = TriggersOf(before, id);
            var triggersAfter = TriggersOf(after, id);
            int tonight = Sum(triggersAfter) - Sum(triggersBefore);
            if (!unlockedTonight && !Differ(triggersBefore, triggersAfter)) return null;

            var row = new PegProgressRow
            {
                PegId = id, UnlockedTonight = unlockedTonight, UnlockedByNight = plan.UnlockNightOf(id),
                TriggersTonight = tonight < 0 ? 0 : tonight,
                Before = rule.For(triggersBefore), After = rule.For(triggersAfter),
            };
            row.CopiesBefore = unlockedTonight ? 0 : row.Before.Copies;   // locked = none owned, whatever the rule's start
            bool earned = row.After.Copies > row.Before.Copies;
            row.Bar = new ProgressBar(row.Before.Progress, row.Before.ProgressAt(row.After.Mastery), earned);
            return row;
        }

        private static IReadOnlyList<int> TriggersOf(PlayerProfile profile, string id) =>
            profile.Pegs.TryGetValue(id, out var record) ? record.Triggers : (IReadOnlyList<int>)Array.Empty<int>();

        private static int Sum(IReadOnlyList<int> list)
        {
            long sum = 0;
            for (int i = 0; i < list.Count; i++) sum += list[i];
            return sum > int.MaxValue ? int.MaxValue : (int)sum;
        }

        // Missing entries read as 0, so [3] and [3, 0] are the same triggers.
        private static bool Differ(IReadOnlyList<int> a, IReadOnlyList<int> b)
        {
            int n = Math.Max(a.Count, b.Count);
            for (int i = 0; i < n; i++)
                if ((i < a.Count ? a[i] : 0) != (i < b.Count ? b[i] : 0)) return true;
            return false;
        }
    }
}
