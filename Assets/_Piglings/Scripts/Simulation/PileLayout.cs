using System;

namespace Piglings.Simulation
{
    /// <summary>
    /// Where the n-th item of a pile sits, as an offset from the pile's anchor: a pyramid, bottom row first.
    /// The bottom row holds bottomRow items, each row above one fewer, every row centred on the anchor.
    /// bottomRow 1 = a single column (a stack).
    ///
    /// Engine-free (plain floats) so Tools/CoreCheck can check it. Shared by every pile that mirrors a count with
    /// real objects — the stone pile today, the peg shelf and per-weapon ammo containers next.
    /// </summary>
    public static class PileLayout
    {
        public static void Pyramid(int index, int bottomRow, float spacing, float rowHeight, out float x, out float y)
        {
            int row = 0, inRow = Math.Max(1, bottomRow);
            index = Math.Max(0, index);
            while (index >= inRow)
            {
                index -= inRow;
                row++;
                inRow = Math.Max(1, bottomRow - row);
            }
            x = (index - (inRow - 1) * 0.5f) * spacing;
            y = row * rowHeight;
        }
    }
}
