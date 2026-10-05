using Piglings.Simulation;
using UnityEngine;

namespace Piglings.Presentation
{
    public sealed class BarnDoorsView : MonoBehaviour
    {
        [SerializeField] private NightFlow flow;

        [Header("Door Renderers")]
        [SerializeField] private SpriteRenderer openRenderer;
        [SerializeField] private SpriteRenderer closedRenderer;

        private bool? _shownOpen;

        private void LateUpdate()
        {
            if (flow == null || openRenderer == null || closedRenderer == null)
                return;

            bool isOpen = flow.DoorsOpen;

            if (_shownOpen == isOpen)
                return;

            _shownOpen = isOpen;

            openRenderer.enabled = isOpen;
            closedRenderer.enabled = !isOpen;
        }
    }
}