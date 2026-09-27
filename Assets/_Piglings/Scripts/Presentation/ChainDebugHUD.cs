using Piglings.Events;
using Piglings.Simulation;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>Throwaway on-screen readout so chains are visible from the first playtest.</summary>
    public sealed class ChainDebugHUD : MonoBehaviour
    {
        [SerializeField] private NightSession session;
        private string _last = "—";

        private void Start() => session.Bus.Subscribe<ChainClosed>(OnChain);
        private void OnDestroy() { if (session != null && session.Bus != null) session.Bus.Unsubscribe<ChainClosed>(OnChain); }
        // Misses close a chain too (0 robots); skip them so "Last chain" keeps the last real one.
        private void OnChain(ChainClosed e) { if (e.RobotsDropped > 0) _last = $"{e.RobotsDropped} robots, depth {e.MaxDepth}"; }

        // Score ÷ stones actually thrown: how well each stone was spent. Stones lost to breaches
        // don't count — they were never thrown. Display only; nothing reads it back.
        private static string ScorePerStone(int score, int throwsUsed) =>
            throwsUsed > 0 ? (score / (float)throwsUsed).ToString("0.0") : "—";

        private static string PhaseText(Piglings.Runtime.NightState s)
        {
            switch (s.Phase)
            {
                case NightPhase.Running: return s.StonesAtTarget >= 0 ? "TARGET REACHED — letting the wall settle" : null;
                case NightPhase.ChoicePending: return "Choose: stay or leave";
                case NightPhase.Overtime: return "OVERTIME — chain points ×2";
                default: return null;
            }
        }

        private void OnGUI()
        {
            var s = session.State;
            var night = session.Night;
            string breachLimit = night.MaxBreaches > 0 ? $"/{night.MaxBreaches}" : "";
            GUILayout.BeginArea(new Rect(10, 10, 320, 220), GUI.skin.box);
            GUILayout.Label($"Score {s.Score} / {night.TargetScore}   Stones {s.StonesLeft}");
            GUILayout.Label($"Per stone {ScorePerStone(s.Score, s.ThrowsUsed)}");
            GUILayout.Label($"Dropped {s.RobotsDropped}   Reached top {s.RobotsReachedTop}{breachLimit}");
            GUILayout.Label($"Last chain: {_last}");
            GUILayout.Label($"Best chain: {s.LongestChain} robots, depth {s.DeepestChain}");
            // Where the night is. The end screen itself is NightEndView.
            string phase = PhaseText(s);
            if (phase != null) GUILayout.Label(phase);
            GUILayout.EndArea();
        }
    }
}
