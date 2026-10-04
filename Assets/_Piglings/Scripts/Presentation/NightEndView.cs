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
    /// Night.unity only: the campaign scene has NightFlow and its own buttons instead. (The "[Night]" tuning line moved
    /// to NightLog, owned by NightSession, so both scenes keep it.) Reads only.
    /// </summary>
    public sealed class NightEndView : MonoBehaviour
    {
        [SerializeField] private NightSession session;

        [Tooltip("The sweep counter always gets through its total in about this many seconds, however big it is.")]
        [SerializeField, Min(0.1f)] private float sweepCountSeconds = 1.2f;

        private float _shownSweep;

        private GUIStyle _big;

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
