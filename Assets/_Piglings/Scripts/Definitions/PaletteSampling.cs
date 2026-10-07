namespace Piglings.Definitions
{
    /// <summary>
    /// Where hour n of a night falls in a palette (M10.E): the palette is spread over the night's length, the first
    /// colour → the last, and the LAST colour is dawn's own (post_run_v5: a 6-hour night shows 6 hour dots and a 7th, gold,
    /// for dawn). So hour 1 is always the first colour, dawn always the last, and the hours between blend along the list —
    /// any number of colours fits any night. Engine-free (CoreCheck runs it); VisualsDefinition does the colour part (its hour colours + the gold as the last stop).
    /// </summary>
    public static class PaletteSampling
    {
        /// <summary>
        /// A position in the colour list (0 = the first entry, colours − 1 = the last): the whole part is the entry, the
        /// fraction how far toward the next. Hour 1 → 0; hour <paramref name="hours"/> + 1 = dawn → the last entry.
        /// One colour → 0.
        /// </summary>
        public static float Position(int hour, int hours, int colours)
        {
            if (colours <= 1) return 0f;
            int steps = hours < 1 ? 1 : hours;   // hour 1 … dawn (= hours + 1) are steps + 1 points
            int h = hour < 1 ? 1 : hour > steps + 1 ? steps + 1 : hour;
            return (h - 1) / (float)steps * (colours - 1);
        }

        /// <summary>Dawn's position: always the last colour.</summary>
        public static float Dawn(int colours) => colours <= 1 ? 0f : colours - 1;
    }
}
