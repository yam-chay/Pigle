namespace Piglings.Definitions
{
    /// <summary>
    /// Where hour n of a night falls in a palette (M10.E): the palette is spread over the night's length, first colour →
    /// last, so hour 1 is always the first colour and the last hour the last one (dawn gold) — a 5-hour night and a 7-hour
    /// night both end on gold. Between two entries the colours blend. Engine-free (CoreCheck runs it); the palette asset
    /// does the colour part.
    /// </summary>
    public static class PaletteSampling
    {
        /// <summary>
        /// A position in the colour list (0 = the first entry, colours − 1 = the last): the whole part is the entry, the
        /// fraction how far toward the next. One hour (or one colour) → 0.
        /// </summary>
        public static float Position(int hour, int hours, int colours)
        {
            if (colours <= 1 || hours <= 1) return 0f;
            int h = hour < 1 ? 1 : hour > hours ? hours : hour;
            return (h - 1) / (float)(hours - 1) * (colours - 1);
        }
    }
}
