using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Piglings.Simulation
{
    /// <summary>What can be pointed at in the barn room.</summary>
    public enum BarnSpot { None, Materials }

    /// <summary>
    /// The barn room's input (M10.F): hovering the materials pile leans the camera toward it, clicking it opens the
    /// Tower (slice placement). Only in the BarnRoom state, never while the camera moves, never through a UI button.
    /// The hit area is the pile's sprite bounds — no colliders, so nothing in the physics ever meets it.
    /// Views (lift, glow, sparkles) read Hovered; HoverStarted is the sound hook.
    /// </summary>
    public sealed class BarnInteraction : MonoBehaviour
    {
        [SerializeField] private NightFlow flow;
        [SerializeField] private CameraDirector director;
        [SerializeField] private Camera cam;
        [Tooltip("The materials pile's sprite: its bounds are what can be hovered and clicked.")]
        [SerializeField] private Renderer materials;
        [Tooltip("Grows (or shrinks, if negative) the hit area beyond the sprite's bounds, in world units.")]
        [SerializeField] private float hitPadding = 0f;

        [Header("Camera nudge on hover")]
        [Tooltip("How far the camera leans toward the hovered thing: a fraction of the way from the frame's centre to it.")]
        [SerializeField, Range(0f, 0.5f)] private float leanToward = 0.05f;
        [Tooltip("How much closer it zooms (0.05 = 5%).")]
        [SerializeField, Range(0f, 0.3f)] private float zoomIn = 0.05f;

        public BarnSpot Hovered { get; private set; }

        /// <summary>The pointer started hovering a spot (the material sound plays here, later).</summary>
        public event System.Action<BarnSpot> HoverStarted;

        private void Update()
        {
            var was = Hovered;
            Hovered = PointedAt(out var pointer);
            if (Hovered != was)
            {
                if (Hovered != BarnSpot.None) HoverStarted?.Invoke(Hovered);
                Lean(Hovered);
            }
            if (Hovered == BarnSpot.Materials && pointer.press.wasPressedThisFrame)
            {
                Hovered = BarnSpot.None;
                flow.OpenTower();   // the camera's move eases the nudge out
            }
        }

        private BarnSpot PointedAt(out Pointer pointer)
        {
            pointer = Pointer.current;
            if (flow == null || flow.State != FlowState.BarnRoom || flow.CameraMoving || cam == null || pointer == null) return BarnSpot.None;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return BarnSpot.None;
            Vector2 world = cam.ScreenToWorldPoint(pointer.position.ReadValue());
            if (materials != null && Contains(materials.bounds, world)) return BarnSpot.Materials;
            return BarnSpot.None;
        }

        private bool Contains(Bounds bounds, Vector2 point)
        {
            bounds.Expand(hitPadding * 2f);
            return point.x >= bounds.min.x && point.x <= bounds.max.x && point.y >= bounds.min.y && point.y <= bounds.max.y;
        }

        // Lean toward what's hovered (a small pan + zoom); back to the plain frame when nothing is.
        private void Lean(BarnSpot spot)
        {
            if (director == null) return;
            if (spot == BarnSpot.None || materials == null) { director.Nudge(Vector2.zero, 0f); return; }
            Vector2 toward = (Vector2)materials.bounds.center - (Vector2)cam.transform.position;
            director.Nudge(toward * leanToward, zoomIn);
        }
    }
}
