using Piglings.Events;
using Piglings.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Piglings.Presentation
{
    /// <summary>
    /// Throwaway on-screen readout so chains are visible from the first playtest. Hidden by default since the scoreboard
    /// (M10.D): the toggle key shows it. The key only changes what this view draws — nothing in play reads it.
    /// </summary>
    public sealed class ChainDebugHUD : MonoBehaviour
    {
        [SerializeField] private NightSession session;
        [Tooltip("Shows / hides this HUD in play.")]
        // Renamed on purpose WITHOUT FormerlySerializedAs (2026-10-09): the scene's F2 must not survive — F2 is the debug
        // panel's now, so this starts at its new default. None = no key (the readout never shows).
        [Tooltip("Shows / hides the readout. F2 is taken by the debug panel.")]
        [SerializeField] private Key hudToggleKey = Key.F3;
        [SerializeField] private bool visibleAtStart = false;
        private string _last = "—";
        private bool _visible;

        private void Awake() => _visible = visibleAtStart;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && hudToggleKey != Key.None && keyboard[hudToggleKey].wasPressedThisFrame) _visible = !_visible;
        }

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
                    // Play goes on: the pause comes when the chain that crossed the line has landed.
                    return s.PendingPegRounds > 0 ? $"THRESHOLD! Hour {s.Hour + 1} starts when the chain lands" : null;
                case NightPhase.PegPlacement:
                    return s.PegThrowsLeft > 0 ? $"HOUR {s.Hour} — place pegs ({s.PegThrowsLeft} left)" : $"HOUR {s.Hour} — refill";
                default: return null;
            }
        }

        private void OnGUI()
        {
            if (!_visible) return;
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
            // Where the night is.
            string phase = PhaseText(s);
            if (phase != null) GUILayout.Label(phase);
            GUILayout.EndArea();
        }
    }
}
