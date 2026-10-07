using System.Collections.Generic;
using UnityEngine;

namespace Piglings.Definitions
{
    /// <summary>
    /// The colours of the night, first (dusk) → last (dawn gold): the scoreboard's hour, the hour dots, the tutorial card,
    /// the post-run. Since M10.E they're sampled over the night's length (ColourAt): hour 1 = the first colour, DAWN = the
    /// last (DawnColour), the hours between blend along the list — so any number of colours fits any night.
    /// </summary>
    [CreateAssetMenu(menuName = "Piglings/Hour Palette", fileName = "HourPalette")]
    public sealed class HourPaletteDefinition : ScriptableObject
    {
        [Tooltip("First (hour 1) → last (dawn's gold), spread over each night's hours + dawn. A new entry in the Inspector " +
                 "starts transparent — set its alpha.")]
        [SerializeField] private List<Color> colours = new List<Color>();

        public int Count => colours.Count;

        /// <summary>
        /// Hour n's colour on a night of <paramref name="hours"/> hours: the palette sampled first → last over the night
        /// and its dawn (PaletteSampling), blending between entries. An empty palette → white.
        /// </summary>
        public Color ColourAt(int hour, int hours) => Sample(PaletteSampling.Position(hour, hours, colours.Count));

        /// <summary>Dawn's colour: the palette's last (the gold). An empty palette → white.</summary>
        public Color DawnColour => Sample(PaletteSampling.Dawn(colours.Count));

        private Color Sample(float at)
        {
            if (colours.Count == 0) return Color.white;
            int i = Mathf.Min(Mathf.FloorToInt(at), colours.Count - 1);
            int next = Mathf.Min(i + 1, colours.Count - 1);
            return Color.Lerp(colours[i], colours[next], at - i);
        }
    }
}
