using System.Collections.Generic;
using Piglings.Events;
using Piglings.Runtime;

namespace Piglings.Rules
{
    /// <summary>
    /// Counts what tonight's use is worth to mastery, into NightState (WeaponHits, BallKnocks, KnockedByBall).
    /// NightReferee banks those counts when the night ends; Meta applies them to the saved profile.
    ///
    /// - A weapon hit = RobotLostGrip caused by the thrown weapon itself (Attribution.FromThrowable). One throw can knock
    ///   several robots: each one counts.
    /// - A robot-ball knock (FromRobotBall) never counts for the weapon. It's recorded from both sides, per robot type:
    ///   the knocker's BallKnocks and the knocked robot's KnockedByBall.
    /// - The end-of-night sweep isn't a RobotLostGrip (it's RobotSwept), so it never counts.
    ///
    /// Events carry GameIds; the definition ids come from ThrowReleased.Weapon and RobotSpawned.RobotType, kept here
    /// until the thing leaves play.
    /// </summary>
    public sealed class MasteryTally
    {
        private readonly EventBus _bus;
        private readonly NightState _state;
        private readonly Dictionary<GameId, string> _weapons = new Dictionary<GameId, string>();
        private readonly Dictionary<GameId, string> _robotTypes = new Dictionary<GameId, string>();

        public MasteryTally(EventBus bus, NightState state)
        {
            _bus = bus; _state = state;
            _bus.Subscribe<ThrowReleased>(OnThrow);
            _bus.Subscribe<RobotSpawned>(OnSpawned);
            _bus.Subscribe<RobotLostGrip>(OnLostGrip);
            _bus.Subscribe<ThrowableRemoved>(OnThrowableRemoved);
            _bus.Subscribe<RobotRemoved>(OnRobotRemoved);
        }

        public void Dispose()
        {
            _bus.Unsubscribe<ThrowReleased>(OnThrow);
            _bus.Unsubscribe<RobotSpawned>(OnSpawned);
            _bus.Unsubscribe<RobotLostGrip>(OnLostGrip);
            _bus.Unsubscribe<ThrowableRemoved>(OnThrowableRemoved);
            _bus.Unsubscribe<RobotRemoved>(OnRobotRemoved);
        }

        /// <summary>
        /// Debug (playtesting): adds hits to tonight's tally, as if they were real — so they bank, save and show like
        /// real ones. Ignored once the night has ended (it's already banked).
        /// </summary>
        public void AddWeaponHits(string weapon, int count)
        {
            if (count <= 0 || _state.Ended) return;
            Add(_state.WeaponHits, weapon, count);
        }

        private void OnThrow(ThrowReleased e) => _weapons[e.Throwable] = e.Weapon;
        private void OnSpawned(RobotSpawned e) => _robotTypes[e.Robot] = e.RobotType;
        private void OnThrowableRemoved(ThrowableRemoved e) => _weapons.Remove(e.Throwable);

        // A robot's own removal comes after anything its ball could knock, so its type isn't needed past this point.
        private void OnRobotRemoved(RobotRemoved e) => _robotTypes.Remove(e.Robot);

        private void OnLostGrip(RobotLostGrip e)
        {
            // Banking happened when the night ended: anything counted after it would never be saved, and NightState
            // would disagree with what was banked.
            if (_state.Ended) return;

            if (e.Cause.Kind == CauseKind.Throwable)
            {
                if (_weapons.TryGetValue(e.Cause.Source, out var weapon)) Add(_state.WeaponHits, weapon, 1);
            }
            else if (e.Cause.Kind == CauseKind.RobotBall)
            {
                if (_robotTypes.TryGetValue(e.Cause.Source, out var knocker)) Add(_state.BallKnocks, knocker, 1);
                if (_robotTypes.TryGetValue(e.Robot, out var knocked)) Add(_state.KnockedByBall, knocked, 1);
            }
        }

        // An id we don't have (no definition id) can't be saved against anything, so it isn't counted.
        private static void Add(SortedDictionary<string, int> counts, string id, int amount)
        {
            if (string.IsNullOrEmpty(id)) return;
            counts.TryGetValue(id, out int now);
            counts[id] = ScoreMath.AddClamped(now, amount);
        }
    }
}
