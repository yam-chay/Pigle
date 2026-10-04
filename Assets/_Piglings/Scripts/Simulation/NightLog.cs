using System.Collections.Generic;
using Piglings.Events;
using UnityEngine;

namespace Piglings.Simulation
{
    /// <summary>
    /// The one "[Night]" line per night, for tuning (M5.2): paste a few into the playtest log and compare.
    /// Per hour: how long it lasted, what the chains thrown in it scored, how many robots breached in it — so a slow
    /// hour, or an hour that bled stones, stands out.
    ///
    /// A plain class owned by NightSession (like ItemPile), so every scene with a night logs it — it used to live in
    /// NightEndView's OnGUI end screen, which the campaign scene doesn't have. Reads only.
    /// </summary>
    public sealed class NightLog
    {
        private readonly NightSession _session;
        private readonly List<float> _hourTimes = new List<float>();   // Time.time when each threshold was crossed
        private float _beganAt;   // Time.time the night began (scene start, or leaving Dusk in the campaign scene)

        public NightLog(NightSession session)
        {
            _session = session;
            _beganAt = Time.time;
            session.Bus.Subscribe<NightPhaseChanged>(OnPhaseChanged);
            session.Bus.Subscribe<HourReached>(OnHourReached);
            session.Bus.Subscribe<DawnReached>(OnDawnReached);
            session.Bus.Subscribe<NightEnded>(OnNightEnded);
        }

        public void Dispose()
        {
            _session.Bus.Unsubscribe<NightPhaseChanged>(OnPhaseChanged);
            _session.Bus.Unsubscribe<HourReached>(OnHourReached);
            _session.Bus.Unsubscribe<DawnReached>(OnDawnReached);
            _session.Bus.Unsubscribe<NightEnded>(OnNightEnded);
        }

        private void OnPhaseChanged(NightPhaseChanged e)
        {
            if (e.From == NightPhase.Dusk) _beganAt = Time.time;   // the clock starts when the night does, not in the day phase
        }

        // HourReached comes when the hour's round starts; DawnReached at the crossing. Both close the hour before.
        private void OnHourReached(HourReached e) => _hourTimes.Add(Time.time);
        private void OnDawnReached(DawnReached e) => _hourTimes.Add(Time.time);

        private void OnNightEnded(NightEnded e)
        {
            var s = _session.State;
            var hours = new List<string>();
            int played = Mathf.Max(s.Hours.Count, _hourTimes.Count + (e.Reason == NightEndReason.Dawn ? 0 : 1));
            for (int h = 1; h <= played; h++)
            {
                float from = h >= 2 && h - 2 < _hourTimes.Count ? _hourTimes[h - 2] : _beganAt;
                float to = h - 1 < _hourTimes.Count ? _hourTimes[h - 1] : Time.time;
                var stats = h - 1 < s.Hours.Count ? s.Hours[h - 1] : null;
                hours.Add($"h{h} {to - from:0}s {(stats != null ? stats.Score : 0)}pts {(stats != null ? stats.Breaches : 0)}br");
            }
            Debug.Log($"[Night] {e.Result} ({e.Reason}) · hours {e.HoursReached}/{_session.ThresholdCount} · {string.Join(" | ", hours)} · " +
                      $"breaches {e.Breaches} · stones left {e.StonesLeft} · thrown {e.ThrowsUsed} · " +
                      $"best chain {s.LongestChain} robots / depth {s.DeepestChain} · best throw {s.BestThrowPoints} (h{s.BestThrowHour}) · " +
                      $"score {e.Score} · banked {e.BankedScore}");
        }
    }
}
