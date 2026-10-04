using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Piglings.Simulation
{
    /// <summary>
    /// Slice placement in the day phase (M10.F, the Tower frame): hover a slice of the built tower, click to swap it for
    /// the next of the campaign's slices (right-click: the previous). The tower rebuilds at once and the choice is saved
    /// for this night (NightSession.CycleSlice). Only in the Tower state, never while the camera moves, never through a UI
    /// button (Start night / Back sit on top of the tower). Views read HoveredSlice and SliceChanged.
    /// </summary>
    public sealed class SlicePicker : MonoBehaviour
    {
        [SerializeField] private NightSession session;
        [SerializeField] private NightFlow flow;
        [SerializeField] private Camera cam;
        [Tooltip("How far from the tower's centre a slice can be clicked (the barn is 6 wide; its walls stick out a little).")]
        [SerializeField, Min(0.1f)] private float slotHalfWidth = 2.6f;

        /// <summary>The slice under the pointer (index, bottom = 0), -1 = none or not placing now.</summary>
        public int HoveredSlice { get; private set; } = -1;

        /// <summary>A slice was swapped (its index): a view's pop, a sound.</summary>
        public event System.Action<int> SliceChanged;

        private void Update()
        {
            HoveredSlice = -1;
            if (flow == null || flow.State != FlowState.Tower || flow.CameraMoving || !session.CanEditTower || cam == null) return;
            var pointer = Pointer.current;
            if (pointer == null) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;   // a button is on top

            Vector2 world = cam.ScreenToWorldPoint(pointer.position.ReadValue());
            HoveredSlice = session.Tower.SliceAt(world, slotHalfWidth);
            if (HoveredSlice < 0) return;

            int step = 0;
            if (pointer.press.wasPressedThisFrame) step = 1;
            else if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) step = -1;
            if (step != 0 && session.CycleSlice(HoveredSlice, step)) SliceChanged?.Invoke(HoveredSlice);
        }
    }
}
