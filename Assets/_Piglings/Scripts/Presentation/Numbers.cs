using System.Globalization;

namespace Piglings.Presentation
{
    /// <summary>
    /// Numbers as players read them on the post-run (post_run_v5): thousands separated, "48,545". Invariant, so a
    /// machine's language settings never turn it into "48.545" or "48 545". Stateless.
    /// </summary>
    public static class Numbers
    {
        public static string Thousands(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

        /// <summary>A multiplier as written after a ×: "2", "2.5", "1.25".</summary>
        public static string Mult(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
