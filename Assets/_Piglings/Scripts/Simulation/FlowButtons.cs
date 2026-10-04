using UnityEngine;
using UnityEngine.UI;

namespace Piglings.Simulation
{
    /// <summary>
    /// Placeholder buttons for the campaign flow (M10.C) until the real screens: the post-run (PR E) and the barn room
    /// (PR F). Each button shows only where it belongs and can only be pressed when the flow allows it — never while the
    /// camera moves. The clicks are wired here in code, so the buttons need no On Click entries in the Inspector.
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

        [Header("Post-run (at the doors)")]
        [SerializeField] private Button retry;
        [Tooltip("Only after a dawn on this night, and never after the last night.")]
        [SerializeField] private Button nextNight;

        private void Start()
        {
            Wire(startNight, flow.StartNight);
            Wire(tower, flow.OpenTower);
            Wire(backToBarn, flow.LeaveTower);
            Wire(retry, flow.Retry);
            Wire(nextNight, flow.NextNight);
            Refresh();
        }

        private void OnDestroy()
        {
            if (flow == null) return;
            Unwire(startNight, flow.StartNight);
            Unwire(tower, flow.OpenTower);
            Unwire(backToBarn, flow.LeaveTower);
            Unwire(retry, flow.Retry);
            Unwire(nextNight, flow.NextNight);
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
            Show(retry, s == FlowState.PostRun, flow.CanRetry);
            // Next night is hidden (not greyed) when it isn't earned: it isn't a choice the player has.
            Show(nextNight, s == FlowState.PostRun && flow.NextEarned, flow.CanGoNext);
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
