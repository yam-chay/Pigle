using System;

namespace Piglings.Simulation
{
    /// <summary>
    /// Where the built tower's slices are, as plain numbers (engine-free, like BreachTiming, so CoreCheck checks it):
    /// slice i spans [firstY + i × height, firstY + (i + 1) × height) — a slice's pivot is its bottom edge. TowerBuilder
    /// stacks them this way; the day phase's slice picker asks which slice a point is on.
    /// </summary>
    public static class TowerLayout
    {
        /// <summary>The bottom edge of slice i.</summary>
        public static float SliceBottom(int index, float firstY, float height) => firstY + index * height;

        /// <summary>
        /// The slice under (x, y): within <paramref name="halfWidth"/> of the tower's centre x and inside one of the
        /// <paramref name="count"/> slices. -1 = none (beside the tower, below the first slice, above the last).
        /// </summary>
        public static int SliceAt(float x, float y, float centreX, float halfWidth, float firstY, float height, int count)
        {
            if (count <= 0 || height <= 0f || Math.Abs(x - centreX) > halfWidth) return -1;
            float rel = (y - firstY) / height;
            if (rel < 0f) return -1;
            int index = (int)Math.Floor(rel);
            return index < count ? index : -1;
        }
    }
}
