using Piglings.Events;

namespace Piglings.Meta
{
    /// <summary>
    /// Owns the player's progress (PlayerProfile) and the rules for applying a banked night to it.
    /// Subscribes to NightBanked, which NightReferee publishes once per mastery target just before NightEnded — so by
    /// NightEnded the profile is complete, and NightSession saves it then.
    ///
    /// Barn (the banked score) isn't kept: it's a result, not a cause, and nothing reads it across nights yet.
    /// Adding it later is a new field in the save, not a format break.
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
}
