using System.Collections.Generic;
using Piglings.Events;
using Piglings.Simulation;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// Everything shown once the night reaches Ended (placeholder OnGUI until the real panel, M6.2):
    ///  - the sweep as ONE running total in a fixed spot, ticking up fast (no per-robot popups) — on a win only:
    ///    on a loss the robots still fall, but the sweep scores nothing;
    ///  - the end screen: dawn or caught, hours reached, final score, what the night banked, "Click to play again"
    ///    (PlayAgain, Simulation, does the reload).
    /// Also logs one line at Ended for playtest tuning. Reads only.
    /// </summary>
    public sealed class NightEndView : MonoBehaviour
    {
        [SerializeField] private NightSession session;

        [Tooltip("The sweep counter always gets through its total in about this many seconds, however big it is.")]
        [SerializeField, Min(0.1f)] private float sweepCountSeconds = 1.2f;

        private float _shownSweep;
        private readonly List<float> _hourTimes = new List<float>();   // Time.time when each threshold was crossed
        private float _beganAt;   // Time.time the night began (scene start, or leaving Dusk in the campaign scene)

        private GUIStyle _big;

        private void Start()
        {
            session.Bus.Subscribe<HourReached>(OnHourReached);
            session.Bus.Subscribe<DawnReached>(OnDawnReached);
            session.Bus.Subscribe<NightEnded>(OnNightEnded);
            session.Bus.Subscribe<NightPhaseChanged>(OnPhaseChanged);
            _beganAt = Time.time;
        }

        private void OnDestroy()
        {
            if (session == null || session.Bus == null) return;
            session.Bus.Unsubscribe<HourReached>(OnHourReached);
            session.Bus.Unsubscribe<DawnReached>(OnDawnReached);
            session.Bus.Unsubscribe<NightEnded>(OnNightEnded);
            session.Bus.Unsubscribe<NightPhaseChanged>(OnPhaseChanged);
        }

        private void OnPhaseChanged(NightPhaseChanged e)
        {
            if (e.From == NightPhase.Dusk) _beganAt = Time.time;   // the clock starts when the night does, not in the day phase
        }

        // HourReached comes when the hour's round starts; DawnReached at the crossing. Both close the hour before.
        private void OnHourReached(HourReached e) => _hourTimes.Add(Time.time);
        private void OnDawnReached(DawnReached e) => _hourTimes.Add(Time.time);

        // One line per night, for tuning (M5.2): paste a few into the playtest log and compare.
        // Per hour: how long it lasted, what the chains thrown in it scored, how many robots breached in it —
        // so a slow hour, or an hour that bled stones, stands out.
        private void OnNightEnded(NightEnded e)
        {
            var s = session.State;
            var hours = new List<string>();
            int played = Mathf.Max(s.Hours.Count, _hourTimes.Count + (e.Reason == NightEndReason.Dawn ? 0 : 1));
            for (int h = 1; h <= played; h++)
            {
                float from = h >= 2 && h - 2 < _hourTimes.Count ? _hourTimes[h - 2] : _beganAt;
                float to = h - 1 < _hourTimes.Count ? _hourTimes[h - 1] : Time.time;
                var stats = h - 1 < s.Hours.Count ? s.Hours[h - 1] : null;
                hours.Add($"h{h} {to - from:0}s {(stats != null ? stats.Score : 0)}pts {(stats != null ? stats.Breaches : 0)}br");
            }
            Debug.Log($"[Night] {e.Result} ({e.Reason}) · hours {e.HoursReached}/{session.ThresholdCount} · {string.Join(" | ", hours)} · " +
                      $"breaches {e.Breaches} · stones left {e.StonesLeft} · thrown {e.ThrowsUsed} · " +
                      $"best chain {s.LongestChain} robots / depth {s.DeepestChain} · best throw {s.BestThrowPoints} (h{s.BestThrowHour}) · " +
                      $"score {e.Score} · banked {e.BankedScore}");
        }

        private void Update()
        {
            var s = session.State;
            if (!s.Ended) return;
            // Constant rate sized to the total, so a big sweep is as quick to read as a small one.
            float rate = Mathf.Max(1f, s.SweepScore / sweepCountSeconds);
            _shownSweep = Mathf.MoveTowards(_shownSweep, s.SweepScore, rate * Time.deltaTime);
        }

        private void OnGUI()
        {
            var s = session.State;
            if (!s.Ended) return;
            if (_big == null) _big = new GUIStyle(GUI.skin.label) { fontSize = 36, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };

            if (s.SweepScore > 0)
                GUI.Label(new Rect(0f, Screen.height * 0.18f, Screen.width, 50f), $"SWEEP +{Mathf.RoundToInt(_shownSweep)}", _big);

            var box = new Rect(Screen.width / 2f - 170f, Screen.height / 2f - 80f, 340f, 190f);
            GUILayout.BeginArea(box, GUI.skin.box);
            GUILayout.Label(s.Result == NightResult.Won ? "DAWN — the night is won" : "CAUGHT — the wolves got the pigs");
            GUILayout.Label($"Hours {s.ThresholdsReached} / {session.ThresholdCount}   Score {s.Score}");
            GUILayout.Label(s.Result == NightResult.Won
                ? $"Banked {s.BankedScore}"
                : $"Banked {s.BankedScore} (the last hour you finished)");
            GUILayout.Space(8f);
            GUILayout.Label("Click to play again");
            GUILayout.EndArea();
        }
    }
}
