using System.Collections.Generic;
using UnityEngine;

namespace Piglings.Definitions
{
    /// <summary>
    /// One colour per hour of the night (index 0 = hour 1), night → dawn: the scoreboard's hour, the hour dots, the
    /// tutorial card. A night with more hours than colours logs a warning (and reuses the last colour).
    /// </summary>
    [CreateAssetMenu(menuName = "Piglings/Hour Palette", fileName = "HourPalette")]
    public sealed class HourPaletteDefinition : ScriptableObject
    {
        [Tooltip("Hour 1 first. 7 entries cover the longest night. A new entry in the Inspector starts transparent — set its alpha.")]
        [SerializeField] private List<Color> colours = new List<Color>();

        public int Count => colours.Count;

        /// <summary>Hour n's colour (1-based); past the list → the last; an empty palette → white.</summary>
        public Color ColourFor(int hour)
        {
            if (colours.Count == 0) return Color.white;
            return colours[Mathf.Clamp(hour - 1, 0, colours.Count - 1)];
        }
    }
}
