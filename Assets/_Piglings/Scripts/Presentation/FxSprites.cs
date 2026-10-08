using System.Collections.Generic;
using Piglings.Definitions;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// One-shot effect sprites (a hit star, a rising sparkle, a burst ring): spawned, animated, destroyed. Plain class
    /// owned by a view, like ItemPile — the view keeps the Inspector settings and calls Update each frame.
    /// Spawn with a null sprite does nothing, so a missing piece of art just means no effect.
    /// Visual only: these objects have no collider and nothing reads them.
    /// </summary>
    public sealed class FxSprites
    {
        private struct Live
        {
            public SpriteRenderer Renderer; public FxMotion Motion; public Vector3 From; public Vector3 Drift; public Color Colour; public float Age;
        }

        private readonly Transform _parent;
        private readonly List<Live> _live = new List<Live>();

        public FxSprites(Transform parent) { _parent = parent; }

        /// <param name="drift">World units it travels sideways/outward over its life, on top of Rise (a burst flies apart).</param>
        public void Spawn(Sprite sprite, Vector3 position, Color colour, FxMotion motion, int sortingLayerId, int sortingOrder,
                          Vector2 drift = default)
        {
            if (sprite == null) return;
            var go = new GameObject(sprite.name);
            go.transform.SetParent(_parent, worldPositionStays: false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * motion.startScale;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));   // no two stars alike
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = colour;
            sr.sortingLayerID = sortingLayerId;
            sr.sortingOrder = sortingOrder;
            _live.Add(new Live { Renderer = sr, Motion = motion, From = position, Drift = drift, Colour = colour });
        }

        public void Update(float deltaTime)
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var fx = _live[i];
                if (fx.Renderer == null) { _live.RemoveAt(i); continue; }
                fx.Age += deltaTime;
                float t = Mathf.Clamp01(fx.Age / fx.Motion.seconds);
                var tr = fx.Renderer.transform;
                float eased = 1f - (1f - t) * (1f - t);
                tr.position = fx.From + (Vector3.up * fx.Motion.rise + fx.Drift) * eased;   // eases out as it travels
                tr.localScale = Vector3.one * Mathf.Lerp(fx.Motion.startScale, fx.Motion.endScale, 1f - (1f - t) * (1f - t));
                tr.Rotate(0f, 0f, fx.Motion.spin * deltaTime);
                var c = fx.Colour;
                c.a *= 1f - t * t;   // holds, then fades fast at the end
                fx.Renderer.color = c;
                if (t >= 1f)
                {
                    Object.Destroy(fx.Renderer.gameObject);
                    _live.RemoveAt(i);
                }
                else _live[i] = fx;
            }
        }
    }
}
