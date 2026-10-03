using Piglings.Simulation;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>Mirrors the ThrowController into the pig's Animator: arm up while aiming, snap on release.
    /// Reads only — never changes game state.</summary>
    public sealed class PigView : MonoBehaviour
    {
        [SerializeField] private ThrowController thrower;
        [Tooltip("Optional: the peg thrower, so the arm also rises and throws for pegs in a placement round.")]
        [SerializeField] private PegThrower pegThrower;
        [SerializeField] private Animator animator;           // clips: Idle (default loop), Aim (loop), Throw

        private static readonly int ThrowTrigger = Animator.StringToHash("Throw");
        private static readonly int AimingBool = Animator.StringToHash("Aiming");

        private void OnEnable()
        {
            thrower.Thrown += OnThrown;
            if (pegThrower != null) pegThrower.Thrown += OnThrown;
        }

        private void OnDisable()
        {
            thrower.Thrown -= OnThrown;
            if (pegThrower != null) pegThrower.Thrown -= OnThrown;
        }

        // Polled, not evented: aiming is a state that lasts while the button is held, not a moment.
        // The trajectory line reads the same flag, so the arm and the line can never disagree.
        private void Update()
        {
            bool aiming = thrower.CurrentAim.IsAiming || (pegThrower != null && pegThrower.CurrentAim.IsAiming);
            if (animator) animator.SetBool(AimingBool, aiming);
        }

        private void OnThrown(Vector2 dir) { if (animator) animator.SetTrigger(ThrowTrigger); }
    }
}