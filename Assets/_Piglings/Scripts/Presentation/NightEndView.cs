using Piglings.Events;
using Piglings.Simulation;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// Everything shown once the night reaches Ended (placeholder OnGUI until the real panel, M6.2):
    ///  - the sweep as ONE running total in a fixed spot, ticking up fast (no per-robot popups);
    ///  - the end screen: result, final score vs target, barn / weapon mastery gained, "Click to play again"
    ///    (PlayAgain, Simulation, does the reload).
    /// Also logs one line at Ended for playtest tuning. Reads only.
    /// </summary>
    public sealed class NightEndView : MonoBehaviour
    {
        [SerializeField] private NightSession session;

        [Tooltip("The sweep counter always gets through its total in about this many seconds, however big it is.")]
        [SerializeField, Min(0.1f)] private float sweepCountSeconds = 1.2f;

        private float _shownSweep;
        private float _targetTime = -1f;   // seconds into the night when the score first reached the target

        private GUIStyle _big;

        private void Start()
        {
            session.Bus.Subscribe<NightTargetReached>(OnTargetReached);
            session.Bus.Subscribe<NightEnded>(OnNightEnded);
        }

        private void OnDestroy()
        {
            if (session == null || session.Bus == null) return;
            session.Bus.Unsubscribe<NightTargetReached>(OnTargetReached);
            session.Bus.Unsubscribe<NightEnded>(OnNightEnded);
        }

        private void OnTargetReached(NightTargetReached e) => _targetTime = Time.timeSinceLevelLoad;

        // One line per night, for tuning: paste a few into the playtest log and compare.
        private void OnNightEnded(NightEnded e)
        {
            var s = session.State;
            string timeToTarget = _targetTime < 0f ? "—" : $"{_targetTime:0.0}s";
            string stonesAtTarget = s.StonesAtTarget < 0 ? "—" : s.StonesAtTarget.ToString();
            string overtimeEnd = s.Choice == NightChoice.Stay ? e.Reason.ToString() : "—";
            Debug.Log($"[Night] {e.Result} · time-to-target {timeToTarget} · stones-left-at-target {stonesAtTarget} · " +
                      $"choice {s.Choice} · overtime-end {overtimeEnd} · breaches {e.Breaches} · " +
                      $"best chain {s.LongestChain} robots / depth {s.DeepestChain} · final score {e.Score}");
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

            var night = session.Night;
            var box = new Rect(Screen.width / 2f - 170f, Screen.height / 2f - 80f, 340f, 190f);
            GUILayout.BeginArea(box, GUI.skin.box);
            GUILayout.Label(s.Result == NightResult.Won ? $"NIGHT CLEARED — {WonText(s)}" : $"NIGHT LOST — {LostText(s.EndReason)}");
            GUILayout.Label($"Score {s.Score} / {night.TargetScore}");
            GUILayout.Label($"Barn mastery +{s.BarnMastery}");
            GUILayout.Label($"Weapon mastery +{s.WeaponMastery}");
            GUILayout.Space(8f);
            GUILayout.Label("Click to play again");
            GUILayout.EndArea();
        }

        private static string WonText(Runtime.NightState s)
        {
            if (s.Choice == NightChoice.Leave) return "you left";
            return s.EndReason == NightEndReason.DangerLine ? "overtime ended at the danger line" : "overtime used every stone";
        }

        private static string LostText(NightEndReason r) =>
            r == NightEndReason.BarnBreached ? "the barn was breached" : "out of stones";
    }
}
