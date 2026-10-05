using UnityEngine;
using UnityEngine.UI;

namespace Piglings.Simulation
{
    /// <summary>
    /// The post-run's two buttons (M10.E): a primary and a secondary slot, laid out once (ui_button_primary / _secondary).
    /// What each does depends on the night (NightFlow.Primary / Secondary, from PostRunChoices): a loss → Retry night +
    /// To the barn; a dawn → Next night + Retry night (after the last night: To the barn + Retry night). The labels are
    /// the view's (PostRunView). Shown from the post-run until the scene reloads; pressable only while the camera is still.
    /// Clicks are wired here in code — no On Click entries needed. Simulation, because a press changes what's running.
    /// </summary>
    public sealed class PostRunButtons : MonoBehaviour
    {
        [SerializeField] private NightFlow flow;
        [Tooltip("The primary slot (Retry night on a loss, Next night ▸ on a dawn).")]
        [SerializeField] private Button primary;
        [Tooltip("The secondary slot (To the barn on a loss, Retry night on a dawn).")]
        [SerializeField] private Button secondary;

        private void Start()
        {
            if (primary != null) primary.onClick.AddListener(PressPrimary);
            if (secondary != null) secondary.onClick.AddListener(PressSecondary);
            Refresh();
        }

        private void OnDestroy()
        {
            if (primary != null) primary.onClick.RemoveListener(PressPrimary);
            if (secondary != null) secondary.onClick.RemoveListener(PressSecondary);
        }

        // Read at the press, so a button always does what it shows.
        private void PressPrimary() => flow.Choose(flow.Primary);
        private void PressSecondary() => flow.Choose(flow.Secondary);

        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            var s = flow.State;
            bool shown = s == FlowState.PostRun || s == FlowState.Descending || s == FlowState.Leaving;
            Show(primary, shown, flow.CanChoose);
            Show(secondary, shown, flow.CanChoose);
        }

        private static void Show(Button button, bool visible, bool pressable)
        {
            if (button == null) return;
            if (button.gameObject.activeSelf != visible) button.gameObject.SetActive(visible);
            if (visible) button.interactable = pressable;
        }
    }
}
