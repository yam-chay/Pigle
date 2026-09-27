using Piglings.Events;
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
        [SerializeField] private Animator animator;          // clips: Climb (default loop), ClimbDanger, Break
        [SerializeField] private GameObject eyeOn;
        [SerializeField] private GameObject eyeRed;
        [SerializeField] private GameObject eyeX;
        [SerializeField] private GameObject[] detachables;   // arms, legs, antenna, tail roots

        private static readonly int BreakTrigger = Animator.StringToHash("Break");
        private static readonly int InDangerBool = Animator.StringToHash("InDanger");

        private EventBus _bus;   // the robot's session bus, known once the robot is initialized

        private void OnEnable() => robot.StateChanged += OnState;

        private void OnDisable()
        {
            robot.StateChanged -= OnState;
            if (_bus != null) _bus.Unsubscribe<RobotEnteredDangerZone>(OnEnteredDanger);
            _bus = null;
        }

        // Every robot hears every danger event; it only reacts to its own. A few dozen a night.
        private void OnEnteredDanger(RobotEnteredDangerZone e)
        {
            if (e.Robot == robot.Id && animator) animator.SetBool(InDangerBool, true);
        }

        private void OnState(RobotController r, RobotState s)
        {
            switch (s)
            {
                case RobotState.Climbing:
                    // Initialize has just given the robot its session — the first moment the bus exists.
                    if (_bus == null && robot.Session != null)
                    {
                        _bus = robot.Session.Bus;
                        _bus.Subscribe<RobotEnteredDangerZone>(OnEnteredDanger);
                    }
                    break;
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
