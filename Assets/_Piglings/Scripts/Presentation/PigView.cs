using Piglings.Simulation;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>Mirrors the ThrowController into the pig's Animator: arm up while aiming, snap on release.
    /// Reads only — never changes game state.</summary>
    public sealed class PigView : MonoBehaviour
    {
        [SerializeField] private ThrowController thrower;
        [SerializeField] private Animator animator;           // clips: Idle (default loop), Aim (loop), Throw

        private static readonly int ThrowTrigger = Animator.StringToHash("Throw");
        private static readonly int AimingBool = Animator.StringToHash("Aiming");

        private void OnEnable() => thrower.Thrown += OnThrown;
        private void OnDisable() => thrower.Thrown -= OnThrown;

        // Polled, not evented: aiming is a state that lasts while the button is held, not a moment.
        // The trajectory line reads the same flag, so the arm and the line can never disagree.
        private void Update()
        {
            if (animator) animator.SetBool(AimingBool, thrower.CurrentAim.IsAiming);
        }

        private void OnThrown(Vector2 dir) { if (animator) animator.SetTrigger(ThrowTrigger); }
    }
}