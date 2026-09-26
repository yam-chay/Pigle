using Piglings.Simulation;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>Plays the pig's throw when the ThrowController fires. Idle loops by default.</summary>
    public sealed class PigView : MonoBehaviour
    {
        [SerializeField] private ThrowController thrower;
        [SerializeField] private Animator animator;           // clips: Idle (default loop), Throw
        private static readonly int ThrowTrigger = Animator.StringToHash("Throw");

        private void OnEnable() => thrower.Thrown += OnThrown;
        private void OnDisable() => thrower.Thrown -= OnThrown;
        private void OnThrown(Vector2 dir) { if (animator) animator.SetTrigger(ThrowTrigger); }
    }
}
