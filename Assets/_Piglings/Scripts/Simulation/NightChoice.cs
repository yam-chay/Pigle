using Piglings.Events;
using UnityEngine;

namespace Piglings.Simulation
{
    /// <summary>
    /// The end-of-night choice: Stay (overtime) or Leave. While the night is in ChoicePending it shows two
    /// placeholder buttons and hands the answer to the NightSession (the referee decides what it means).
    ///
    /// Why Simulation: choosing changes the game. A real uGUI panel later (M6.2) calls Stay() / Leave()
    /// from its buttons' OnClick and this OnGUI placeholder goes.
    /// </summary>
    public sealed class NightChoice : MonoBehaviour
    {
        [SerializeField] private NightSession session;

        public void Stay() => session.ChooseStay();
        public void Leave() => session.ChooseLeave();

        private void OnGUI()
        {
            var s = session.State;
            if (s.Phase != NightPhase.ChoicePending) return;

            var box = new Rect(Screen.width / 2f - 190f, Screen.height / 2f - 90f, 380f, 180f);
            GUILayout.BeginArea(box, GUI.skin.box);
            GUILayout.Label($"Target reached! Score {s.Score}. Stones left: {s.StonesLeft}");
            GUILayout.Space(6f);
            if (GUILayout.Button($"STAY — overtime: every point ×2 to the barn\n(the weapon gets nothing; a robot at the line ends it)", GUILayout.Height(52f)))
                Stay();
            if (GUILayout.Button($"LEAVE — bank {s.StonesLeft} stone(s) to the weapon\n(score above target goes to the barn)", GUILayout.Height(52f)))
                Leave();
            GUILayout.EndArea();
        }
    }
}
