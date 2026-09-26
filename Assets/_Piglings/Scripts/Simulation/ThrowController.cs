using UnityEngine;
using UnityEngine.InputSystem;

namespace Piglings.Simulation
{
    /// <summary>
    /// Minimal click-to-throw so the chain loop is testable on day one.
    /// TASK: replace the aiming/trajectory with the CCTD ThrowController + ThrowSolver port.
    /// </summary>
    public sealed class ThrowController : MonoBehaviour
    {
        [SerializeField] private NightSession session;
        [SerializeField] private Throwable throwablePrefab;
        [SerializeField] private Transform origin;       // the pig's hand / barn hole
        [SerializeField] private Transform container;
        [SerializeField] private Camera cam;
        [SerializeField, Min(0.1f)] private float throwSpeed = 7f;

        public event System.Action<Vector2> Thrown;       // Presentation hooks the pig's throw anim here

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            if (session.State.ThrowsUsed >= session.Night.ThrowsAvailable) return;

            Vector2 target = cam.ScreenToWorldPoint(mouse.position.ReadValue());
            Vector2 dir = (target - (Vector2)origin.position).normalized;
            var t = Instantiate(throwablePrefab, origin.position, Quaternion.identity, container);
            t.Launch(session, session.Night.Throwable, dir * throwSpeed);
            Thrown?.Invoke(dir);
        }
    }
}
