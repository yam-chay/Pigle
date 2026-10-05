using System.Collections.Generic;

namespace Piglings.Simulation
{
    /// <summary>One dot of the post-run's hour row: an hour (1..hours) or dawn (Hour = hours + 1, IsDawn), lit or dimmed.</summary>
    public readonly struct HourDot
    {
        public readonly int Hour;
        public readonly bool IsDawn;
        public readonly bool Lit;

        public HourDot(int hour, bool isDawn, bool lit) { Hour = hour; IsDawn = isDawn; Lit = lit; }
    }

    /// <summary>
    /// The post-run's hour dots (M10.E fix): exactly one per hour of the night, then one for dawn. Hours up to the one
    /// reached are lit (in their colour), the rest dimmed; the dawn dot is lit (gold — the palette's last colour) only on a
    /// dawn. Engine-free, so CoreCheck pins the count — the bug was 7 + the reached hours again (a second template list
    /// cloning the same dot).
    /// </summary>
    public static class HourDots
    {
        public static List<HourDot> For(int hours, int reached, bool dawn)
        {
            if (hours < 1) hours = 1;
            if (dawn) reached = hours;
            reached = reached < 0 ? 0 : reached > hours ? hours : reached;
            var dots = new List<HourDot>(hours + 1);
            for (int h = 1; h <= hours; h++) dots.Add(new HourDot(h, false, h <= reached));
            dots.Add(new HourDot(hours + 1, true, dawn));
            return dots;
        }
    }
}
