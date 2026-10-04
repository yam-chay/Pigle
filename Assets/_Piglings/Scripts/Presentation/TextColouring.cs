using TMPro;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// Paints a TMP text with a PopupColor style — the whole text, or letter by letter for the animated rainbow.
    /// Shared by the score popups and the scoreboard, so a style looks the same everywhere. Stateless.
    /// </summary>
    public static class TextColouring
    {
        /// <param name="key">0..1 for the style (Solid picks its colour there; Cycle starts there).</param>
        /// <param name="life">0..1 through the text's life (OverLifetime).</param>
        /// <param name="alpha">Multiplies the style's alpha (fades, dimmed rows).</param>
        public static void Apply(TMP_Text label, PopupColor style, float key, float life, float alpha)
        {
            if (label == null || style == null) return;
            if (!style.IsPerLetter)
            {
                var c = style.Evaluate(key, life, Time.time);
                label.color = new Color(c.r, c.g, c.b, c.a * alpha);
                return;
            }

            // Per letter: rebuild the mesh (the text may just have changed), then colour each visible character's four
            // vertices by its position along the text.
            label.color = Color.white;
            label.ForceMeshUpdate();
            var info = label.textInfo;
            int count = info.characterCount;
            for (int i = 0; i < count; i++)
            {
                var ch = info.characterInfo[i];
                if (!ch.isVisible) continue;
                var c = style.Evaluate(key, life, Time.time, count > 1 ? i / (float)(count - 1) : 0f);
                Color32 c32 = new Color(c.r, c.g, c.b, c.a * alpha);
                var colors = info.meshInfo[ch.materialReferenceIndex].colors32;
                int v = ch.vertexIndex;
                colors[v] = colors[v + 1] = colors[v + 2] = colors[v + 3] = c32;
            }
            label.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }
    }
}
