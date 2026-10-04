using Piglings.Simulation;
using TMPro;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// The day phase's night sign (M10.F, screen UI, top-left): "NIGHT 2 · 6 HOURS" — which night the player is about to
    /// start, and how long it is. Shown in the barn room and the tower view only. Reads only.
    /// </summary>
    public sealed class NightSignView : MonoBehaviour
    {
        [SerializeField] private NightSession session;
        [SerializeField] private NightFlow flow;
        [SerializeField] private TMP_Text label;
        [Tooltip("What to show and hide with the sign (its panel). Must not contain this component. Empty = only the label hides.")]
        [SerializeField] private GameObject panel;

        private void Start()
        {
            if (label != null) label.text = $"NIGHT {session.NightIndex + 1} · {session.ThresholdCount} HOURS";
            // Switching off the object this sits on would also stop this component: then only the label hides.
            if (panel != null && (panel == gameObject || transform.IsChildOf(panel.transform)))
            {
                Debug.LogWarning("NightSignView: Panel contains this component, so it can't switch it off — put NightSignView " +
                                 "on another object (e.g. the Canvas). Only the label hides for now.", this);
                panel = null;
            }
        }

        private void LateUpdate()
        {
            if (flow == null) return;
            bool day = flow.State == FlowState.Boot || flow.State == FlowState.BarnRoom || flow.State == FlowState.Tower;
            if (panel != null) { if (panel.activeSelf != day) panel.SetActive(day); }
            else if (label != null) label.enabled = day;
        }
    }
}
