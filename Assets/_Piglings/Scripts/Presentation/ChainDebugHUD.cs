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
                case NightPhase.Running:
                    if (s.Dawn) return "DAWN — letting the wall settle";
                    return s.PendingPegRounds > 0 ? $"HOUR {s.Hour} — letting the wall settle" : null;
                case NightPhase.PegPlacement:
                    return s.PegThrowsLeft > 0 ? $"HOUR {s.Hour} — place pegs ({s.PegThrowsLeft} left)" : $"HOUR {s.Hour} — refill";
                default: return null;
            }
        }

        private void OnGUI()
        {
            var s = session.State;
            GUILayout.BeginArea(new Rect(10, 10, 320, 240), GUI.skin.box);
            // The HUD always shows the next threshold; the hour and its multiplier say what a chain is worth now.
            string next = s.Dawn ? "dawn" : $"next {session.NextThreshold}";
            GUILayout.Label($"Score {s.Score} ({next})   Stones {s.StonesLeft}");
            GUILayout.Label($"Hour {System.Math.Min(s.Hour, session.ThresholdCount)} / {session.ThresholdCount}   ×{session.HourMultiplier:0.##}");
            GUILayout.Label($"Per stone {ScorePerStone(s.Score, s.ThrowsUsed)}");
            GUILayout.Label($"Dropped {s.RobotsDropped}   Reached top {s.RobotsReachedTop}");
            GUILayout.Label($"Last chain: {_last}");
            GUILayout.Label($"Best chain: {s.LongestChain} robots, depth {s.DeepestChain}");
            // Where the night is. The end screen itself is NightEndView.
            string phase = PhaseText(s);
            if (phase != null) GUILayout.Label(phase);
            GUILayout.EndArea();
        }
    }
}
