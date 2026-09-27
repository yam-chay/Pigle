using UnityEngine;

namespace Piglings.Presentation
{
    public enum PopupColorMode
    {
        Solid,         // one colour, picked from the gradient at a "key" (e.g. by depth)
        OverLifetime,  // walks the gradient from appear (left) to gone (right)
        Cycle,         // the whole text loops through the gradient over time
        PerLetter      // each letter sits further along the gradient, and it scrolls: animated rainbow
    }

    /// <summary>
    /// How a popup is coloured, edited in the Inspector: a mode plus Unity's own gradient editor.
    /// Why one type for every popup colour: the robot "+N", the overtime tag and the chain result all
    /// get the same options (solid / gradient / animated), so a style is swapped by editing, not by code.
    /// </summary>
    [System.Serializable]
    public sealed class PopupColor
    {
        public PopupColorMode mode = PopupColorMode.Solid;
        public Gradient gradient = Flat(Color.white);

        [Tooltip("Cycle / PerLetter: trips through the gradient per second.")]
        [Min(0f)] public float speed = 1f;

        [Tooltip("PerLetter: how much of the gradient one word spans. 1 = the whole rainbow across the text.")]
        [Min(0f)] public float spread = 1f;

        public bool IsPerLetter => mode == PopupColorMode.PerLetter;

        /// <param name="key">0..1 chosen by the caller (robots: depth). Used by Solid, and offsets Cycle.</param>
        /// <param name="life">0..1 through the popup's life.</param>
        /// <param name="letter">0..1 position of the letter in the text (PerLetter only).</param>
        public Color Evaluate(float key, float life, float time, float letter = 0f)
        {
            switch (mode)
            {
                case PopupColorMode.OverLifetime: return gradient.Evaluate(Mathf.Clamp01(life));
                case PopupColorMode.Cycle: return gradient.Evaluate(Mathf.Repeat(key + time * speed, 1f));
                case PopupColorMode.PerLetter: return gradient.Evaluate(Mathf.Repeat(letter * spread - time * speed, 1f));
                default: return gradient.Evaluate(Mathf.Clamp01(key));
            }
        }

        // ---- defaults for fresh components (tune in the Inspector afterwards) ----

        public static Gradient Flat(Color c) => TwoColor(c, c);

        public static Gradient TwoColor(Color from, Color to)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        // Loops seamlessly: ends on the colour it starts with.
        public static Gradient Rainbow()
        {
            var g = new Gradient();
            g.SetKeys(new[]
                {
                    new GradientColorKey(new Color(1f, 0.25f, 0.25f), 0f),
                    new GradientColorKey(new Color(1f, 0.85f, 0.2f), 0.17f),
                    new GradientColorKey(new Color(0.35f, 1f, 0.35f), 0.33f),
                    new GradientColorKey(new Color(0.3f, 0.9f, 1f), 0.5f),
                    new GradientColorKey(new Color(0.45f, 0.45f, 1f), 0.67f),
                    new GradientColorKey(new Color(1f, 0.4f, 1f), 0.83f),
                    new GradientColorKey(new Color(1f, 0.25f, 0.25f), 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }
    }
}
