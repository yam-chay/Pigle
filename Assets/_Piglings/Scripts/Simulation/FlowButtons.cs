using UnityEngine;
using UnityEngine.UI;

namespace Piglings.Simulation
{
    /// <summary>
    /// The barn room's buttons for the campaign flow (M10.C): Start night, Tower, Back. (The post-run's buttons are
    /// PostRunButtons since M10.E.) Each button shows only where it belongs and can only be pressed when the flow allows
    /// it — never while the camera moves. The clicks are wired here in code, so the buttons need no On Click entries in the Inspector.
    /// Every button is optional. Simulation, because pressing one changes what's running (views only read).
    /// </summary>
    public sealed class FlowButtons : MonoBehaviour
    {
        [SerializeField] private NightFlow flow;

        [Header("Barn room / Tower")]
        [SerializeField] private Button startNight;
        [SerializeField] private Button tower;
        [Tooltip("Tower → back to the barn room.")]
        [SerializeField] private Button backToBarn;

        private void Start()
        {
            Wire(startNight, flow.StartNight);
            Wire(tower, flow.OpenTower);
            Wire(backToBarn, flow.LeaveTower);
            Refresh();
        }

        private void OnDestroy()
        {
            if (flow == null) return;
            Unwire(startNight, flow.StartNight);
            Unwire(tower, flow.OpenTower);
            Unwire(backToBarn, flow.LeaveTower);
        }

        private void LateUpdate() => Refresh();

        // Shown in their state, pressable when the flow says so (greyed while the camera moves).
        private void Refresh()
        {
            var s = flow.State;
            bool day = s == FlowState.BarnRoom || s == FlowState.Tower;
            Show(startNight, day, flow.CanStartNight);
            Show(tower, s == FlowState.BarnRoom, flow.CanOpenTower);
            Show(backToBarn, s == FlowState.Tower, flow.CanLeaveTower);
        }

        private static void Show(Button button, bool visible, bool pressable)
        {
            if (button == null) return;
            if (button.gameObject.activeSelf != visible) button.gameObject.SetActive(visible);
            if (visible) button.interactable = pressable;
        }

        private static void Wire(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null) button.onClick.AddListener(action);
        }

        private static void Unwire(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null) button.onClick.RemoveListener(action);
        }
    }
}
