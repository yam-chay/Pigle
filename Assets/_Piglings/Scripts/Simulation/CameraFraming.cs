using System;

namespace Piglings.Simulation
{
    /// <summary>A camera frame: the centre height and the orthographic size (half the visible height). x never changes.</summary>
    public readonly struct CameraPose
    {
        public readonly float Y;
        public readonly float Size;

        public CameraPose(float y, float size) { Y = y; Size = size; }

        public override string ToString() => $"y {Y:0.##}, size {Size:0.##}";
    }

    /// <summary>
    /// The camera's maths, engine-free so Tools/CoreCheck can check it (like BreachTiming): the ease of a move between
    /// two frames, and the Tower frame fitted to the built tower. CameraDirector does the moving with it.
    /// </summary>
    public static class CameraFraming
    {
        /// <summary>Ease in-out (smoothstep): slow away from a frame, slow into the next, never past it. t is clamped to 0..1.</summary>
        public static float Ease(float t)
        {
            t = Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>The pose t (0..1, already eased or not) of the way from one frame to another.</summary>
        public static CameraPose Between(CameraPose from, CameraPose to, float t)
        {
            t = Clamp01(t);
            return new CameraPose(from.Y + (to.Y - from.Y) * t, from.Size + (to.Size - from.Size) * t);
        }

        /// <summary>
        /// The whole tower in view: from <paramref name="bottomY"/> (the ground under the barn) to <paramref name="topY"/>
        /// (the top of Barn_Top and the pig), plus <paramref name="margin"/> on every side. Wide enough for the barn too:
        /// on a narrow screen (small aspect) the size grows until <paramref name="halfWidth"/> + margin fits across.
        /// </summary>
        public static CameraPose Tower(float bottomY, float topY, float halfWidth, float margin, float aspect)
        {
            if (topY < bottomY) { float swap = topY; topY = bottomY; bottomY = swap; }
            margin = Math.Max(0f, margin);
            float size = (topY - bottomY) / 2f + margin;
            if (aspect > 0f) size = Math.Max(size, (Math.Max(0f, halfWidth) + margin) / aspect);
            return new CameraPose((topY + bottomY) / 2f, Math.Max(0.01f, size));
        }

        /// <summary>
        /// The Night frame from the built tower: a fixed bottom (<paramref name="bottomY"/>, where the wall's play area
        /// starts — the same on every night) up to <paramref name="topY"/> (the roof: Barn_Top and the pig). A taller
        /// tower zooms out upward; the bottom never moves.
        /// </summary>
        public static CameraPose Night(float bottomY, float topY)
        {
            if (topY < bottomY) { float swap = topY; topY = bottomY; bottomY = swap; }
            return new CameraPose((topY + bottomY) / 2f, Math.Max(0.01f, (topY - bottomY) / 2f));
        }

        /// <summary>
        /// A hand-tuned override on top of a computed frame: a value above 0 replaces the computed one, 0 (or less) keeps
        /// it. Each on its own (NightDefinition Camera Y / Size; 0 = auto).
        /// </summary>
        public static CameraPose Override(CameraPose computed, float y, float size) =>
            new CameraPose(y > 0f ? y : computed.Y, size > 0f ? size : computed.Size);

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
