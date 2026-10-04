using System;

namespace Piglings.Simulation
{
    /// <summary>What dropping a dragged slice does.</summary>
    public enum SliceDropKind
    {
        None,      // dropped off the tower, or back on its own slot: it slides home, nothing changes
        Replace,   // a slice from the tray dropped on a slot: that slot becomes it
        Swap,      // a tower slice dropped on another slot: the two trade places
    }

    /// <summary>
    /// The day phase's drag-and-drop rules and the hover jiggle, as plain numbers (engine-free, so CoreCheck checks them).
    /// The tower is always whole: a slice dragged out comes back unless it lands on another slot, and a tray slice only
    /// ever replaces one — so there's never a gap to fill before the night.
    /// </summary>
    public static class SliceDrag
    {
        /// <param name="fromTray">The dragged slice came from the tray (else from the tower, slot <paramref name="fromSlot"/>).</param>
        /// <param name="targetSlot">The tower slot under the pointer on release, -1 = none.</param>
        public static SliceDropKind Resolve(bool fromTray, int fromSlot, int targetSlot)
        {
            if (targetSlot < 0) return SliceDropKind.None;
            if (fromTray) return SliceDropKind.Replace;
            return targetSlot == fromSlot ? SliceDropKind.None : SliceDropKind.Swap;
        }

        /// <summary>A press becomes a drag once the pointer has moved this far (world units) — a plain click never drags.</summary>
        public static bool IsDrag(float dx, float dy, float threshold) => dx * dx + dy * dy > threshold * threshold;

        /// <summary>The hover jiggle's angle (degrees) at time t: a wobble of ± amplitude, frequency times a second.</summary>
        public static float Jiggle(float t, float amplitude, float frequency) =>
            amplitude * (float)Math.Sin(t * frequency * 2.0 * Math.PI);
    }
}
