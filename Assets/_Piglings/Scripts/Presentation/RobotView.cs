using Piglings.Simulation;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// Visual side of a wolf-bot: reacts to RobotController state, never changes it.
    /// Rig hierarchy comes from wolfbot_rig.psd (PSD Importer).
    /// </summary>
    public sealed class RobotView : MonoBehaviour
    {
        [SerializeField] private RobotController robot;
        [SerializeField] private Animator animator;          // clips: Climb (default loop), Break
        [SerializeField] private GameObject eyeOn;
        [SerializeField] private GameObject eyeRed;
        [SerializeField] private GameObject eyeX;
        [SerializeField] private GameObject[] detachables;   // arms, legs, antenna, tail roots

        private static readonly int BreakTrigger = Animator.StringToHash("Break");

        private void OnEnable() => robot.StateChanged += OnState;
        private void OnDisable() => robot.StateChanged -= OnState;

        private void OnState(RobotController r, RobotState s)
        {
            switch (s)
            {
                case RobotState.Climbing: break;
                case RobotState.LosingGrip:
                    if (animator) animator.SetTrigger(BreakTrigger);   // the clip itself swaps eyes to X at the crack
                    break;
                case RobotState.Falling:
                    foreach (var d in detachables) if (d) d.SetActive(false);
                    break;
            }
        }
    }
}
