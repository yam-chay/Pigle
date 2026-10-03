using System.Collections.Generic;
using Piglings.Events;
using Piglings.Simulation;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// During a placement round, every sprite under this object goes semi-transparent, so the wall's pegs and the
    /// shelf read first; after the round each gets its own opacity back. For things that aren't animated (the stone
    /// pile on the perch). Robots fade in RobotView instead, where their Animator is accounted for. Reads only.
    /// </summary>
    public sealed class PlacementFade : MonoBehaviour
    {
        [SerializeField] private NightSession session;
        [SerializeField, Range(0f, 1f)] private float opacity = 0.35f;

        // Each sprite's own alpha before the round, so it's restored exactly (children can come and go mid-round).
        private readonly Dictionary<SpriteRenderer, float> _alphas = new Dictionary<SpriteRenderer, float>();

        private void LateUpdate()
        {
            if (session.State.Phase == NightPhase.PegPlacement)
            {
                foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
                {
                    if (!_alphas.TryGetValue(sr, out float a)) _alphas[sr] = a = sr.color.a;
                    var c = sr.color;
                    c.a = a * opacity;
                    sr.color = c;
                }
            }
            else if (_alphas.Count > 0)
            {
                foreach (var kv in _alphas)
                {
                    if (kv.Key == null) continue;
                    var c = kv.Key.color;
                    c.a = kv.Value;
                    kv.Key.color = c;
                }
                _alphas.Clear();
            }
        }
    }
}
