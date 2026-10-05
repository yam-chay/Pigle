using System.Collections.Generic;
using UnityEngine;

namespace Piglings.Definitions
{
    /// <summary>
    /// The colours of the night, first (dusk) → last (dawn gold): the scoreboard's hour, the hour dots, the tutorial card,
    /// the post-run. Since M10.E they're sampled over the night's length (ColourAt): hour 1 = the first colour, the last
    /// hour = the last, the hours between blend along the list — so any number of colours fits any night.
    /// </summary>
    [CreateAssetMenu(menuName = "Piglings/Hour Palette", fileName = "HourPalette")]
    public sealed class HourPaletteDefinition : ScriptableObject
    {
        [Tooltip("First (hour 1) → last (the last hour, dawn gold), spread over each night's length. A new entry in the Inspector " +
                 "starts transparent — set its alpha.")]
        [SerializeField] private List<Color> colours = new List<Color>();

        public int Count => colours.Count;

        /// <summary>
        /// Hour n's colour on a night of <paramref name="hours"/> hours: the palette sampled first → last over the night
        /// (PaletteSampling), blending between entries. An empty palette → white.
        /// </summary>
        public Color ColourAt(int hour, int hours)
        {
            if (colours.Count == 0) return Color.white;
            float at = PaletteSampling.Position(hour, hours, colours.Count);
            int i = Mathf.Min(Mathf.FloorToInt(at), colours.Count - 1);
            int next = Mathf.Min(i + 1, colours.Count - 1);
            return Color.Lerp(colours[i], colours[next], at - i);
        }

        /// <summary>Entry n of the list (1-based), by index; past the list → the last; an empty palette → white.</summary>
        public Color ColourFor(int hour)
        {
            if (colours.Count == 0) return Color.white;
            return colours[Mathf.Clamp(hour - 1, 0, colours.Count - 1)];
        }
    }
}
