using Piglings.Simulation;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// The barn's bottom piece: closed doors during the night, open ones from the moment the camera reaches the Doors
    /// (the post-run) until the next night begins (NightFlow.DoorsOpen). A sprite swap for now; an opening animation
    /// can hang off the same flag later. Missing sprites leave the renderer as it is. Reads only.
    /// </summary>
    public sealed class BarnDoorsView : MonoBehaviour
    {
        [SerializeField] private NightFlow flow;
        [Tooltip("The bottom piece's renderer. Empty = the SpriteRenderer on this object.")]
        [SerializeField] private SpriteRenderer target;
        [SerializeField] private Sprite closed;   // barn_bottom_closed
        [SerializeField] private Sprite open;     // barn_bottom_open

        private bool? _shownOpen;

        private void Awake()
        {
            if (target == null) TryGetComponent(out target);
        }

        private void LateUpdate()
        {
            if (flow == null || target == null) return;
            bool isOpen = flow.DoorsOpen;
            if (_shownOpen == isOpen) return;
            _shownOpen = isOpen;
            var sprite = isOpen ? open : closed;
            if (sprite != null) target.sprite = sprite;
        }
    }
}
