using System.Collections.Generic;
using Piglings.Events;

namespace Piglings.Meta
{
    /// <summary>
    /// Owns the player's progress (PlayerProfile) and the rules for applying a banked night to it.
    /// Subscribes to NightBanked, which NightReferee publishes once per mastery target just before NightEnded — so by
    /// NightEnded the profile is complete, and NightSession saves it then.
    ///
    /// Barn (the banked score) isn't kept: it's a result, not a cause. The best night's score is, as one of the all-time
    /// records (RecordNight, called by the scene with tonight's bests — a record is the one kind of result the save keeps).
    /// </summary>
    public sealed class Progression
    {
        private readonly EventBus _bus;

        public PlayerProfile Profile { get; private set; }

        public Progression(EventBus bus, PlayerProfile profile)
        {
            _bus = bus;
            Profile = profile ?? new PlayerProfile();
            _bus.Subscribe<NightBanked>(Apply);
        }

        public void Dispose() => _bus.Unsubscribe<NightBanked>(Apply);

        /// <summary>Debug (playtesting): forget everything. The caller saves.</summary>
        public void Reset() => Profile = new PlayerProfile();

        // ---------- the campaign (called by the scene's flow; plain writes, nothing derived is stored) ----------

        /// <summary>A night ended: a dawn is the cause behind unlocks. A lost night records nothing here (its hits still bank).</summary>
        public void RecordNightResult(string nightId, bool dawn)
        {
            if (!dawn || string.IsNullOrEmpty(nightId)) return;
            Profile.Dawns.TryGetValue(nightId, out int n);
            Profile.Dawns[nightId] = AddClamped(n, 1);
        }

        /// <summary>
        /// Tonight's bests go into the all-time records (each only if it beats the record). Returns which broke — the
        /// post-run's NEW RECORD tags. Called by the scene once the night is banked, before it saves.
        /// </summary>
        public RecordsBroken RecordNight(int bestThrow, int longestChain, int deepestChain, int nightScore)
        {
            var r = Profile.Records;
            var broken = new RecordsBroken(bestThrow > r.BestThrow, longestChain > r.LongestChain, deepestChain > r.DeepestChain,
                                           nightScore > r.BestNightScore);
            if (broken.BestThrow) r.BestThrow = bestThrow;
            if (broken.LongestChain) r.LongestChain = longestChain;
            if (broken.DeepestChain) r.DeepestChain = deepestChain;
            if (broken.BestNightScore) r.BestNightScore = nightScore;
            return broken;
        }

        /// <summary>Fast Retry: the next scene load starts straight in the night (set before saving + reloading).</summary>
        public void SetStartInNight(bool on) => Profile.StartInNight = on;

        /// <summary>Where the player is in the campaign (0 = the first night). Retry keeps it; Next moves it on.</summary>
        public void SetCurrentNight(int index) => Profile.CurrentNight = index < 0 ? 0 : index;

        /// <summary>The slices the player chose for a night's tower, bottom → top. Null or empty = the night's own slices.</summary>
        public void SetTower(string nightId, IReadOnlyList<string> slices)
        {
            if (string.IsNullOrEmpty(nightId)) return;
            if (slices == null || slices.Count == 0) { Profile.Towers.Remove(nightId); return; }
            Profile.Towers[nightId] = new List<string>(slices);
        }

        private void Apply(NightBanked e)
        {
            if (string.IsNullOrEmpty(e.Id) || e.Amount <= 0) return;
            int gained = (int)System.Math.Min(int.MaxValue, (long)e.Amount * System.Math.Max(0, e.Multiplier));

            switch (e.Stat)
            {
                case MasteryStat.DirectHits:
                    var w = Profile.Weapon(e.Id);
                    w.DirectHits = AddClamped(w.DirectHits, gained);
                    break;
                case MasteryStat.BallKnocks:
                    var knocker = Profile.Robot(e.Id);
                    knocker.BallKnocks = AddClamped(knocker.BallKnocks, gained);
                    break;
                case MasteryStat.KnockedByBall:
                    var knocked = Profile.Robot(e.Id);
                    knocked.KnockedByBall = AddClamped(knocked.KnockedByBall, gained);
                    break;
                case MasteryStat.PegTriggers:
                    var triggers = Profile.Peg(e.Id).Triggers;
                    int i = e.Level < 1 ? 0 : e.Level - 1;
                    while (triggers.Count <= i) triggers.Add(0);
                    triggers[i] = AddClamped(triggers[i], gained);
                    break;
                case MasteryStat.Dropped:
                    var robot = Profile.Robot(e.Id);
                    robot.Dropped = AddClamped(robot.Dropped, gained);
                    break;
                case MasteryStat.Swept:
                    var sweptRobot = Profile.Robot(e.Id);
                    sweptRobot.Swept = AddClamped(sweptRobot.Swept, gained);
                    break;
                case MasteryStat.PegKnocks:
                    var peg = Profile.Peg(e.Id);
                    peg.Knocks = AddClamped(peg.Knocks, gained);
                    break;
                // Score (Barn): not kept, see above.
            }
        }

        // Saturate rather than wrap into a negative count (which the loader would then reject as corrupt).
        private static int AddClamped(int a, int b) => (int)System.Math.Min(int.MaxValue, (long)a + b);
    }

    /// <summary>Which all-time records a night broke (it beat them; a tie doesn't count).</summary>
    public readonly struct RecordsBroken
    {
        public readonly bool BestThrow, LongestChain, DeepestChain, BestNightScore;

        public RecordsBroken(bool bestThrow, bool longestChain, bool deepestChain, bool bestNightScore)
        {
            BestThrow = bestThrow; LongestChain = longestChain; DeepestChain = deepestChain; BestNightScore = bestNightScore;
        }

        public bool Any => BestThrow || LongestChain || DeepestChain || BestNightScore;
    }
}
