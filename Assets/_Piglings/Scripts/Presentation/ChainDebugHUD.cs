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
        private void OnChain(ChainClosed e) => _last = $"{e.RobotsDropped} robots, depth {e.MaxDepth}";

        private void OnGUI()
        {
            var s = session.State;
            GUILayout.BeginArea(new Rect(10, 10, 320, 160), GUI.skin.box);
            GUILayout.Label($"Score {s.Score}   Throws {s.ThrowsUsed}/{session.Night.ThrowsAvailable}");
            GUILayout.Label($"Dropped {s.RobotsDropped}   Reached top {s.RobotsReachedTop}");
            GUILayout.Label($"Last chain: {_last}");
            GUILayout.Label($"Best chain: {s.LongestChain} robots, depth {s.DeepestChain}");
            GUILayout.EndArea();
        }
    }
}
