using UnityEngine;

namespace Piglings.Definitions
{
    /// <summary>
    /// How one effect sprite moves over its life: grows from StartScale to EndScale, rises Rise units, fades out.
    /// In Definitions (R4c) so a peg's effect class can own its own effects' motion; Presentation's FxSprites plays it.
    /// </summary>
    [System.Serializable]
    public struct FxMotion
    {
        [Min(0.01f)] public float seconds;
        [Min(0f)] public float startScale;
        [Min(0f)] public float endScale;
        [Tooltip("World units it drifts up over its life.")]
        public float rise;
        [Tooltip("Degrees per second (a little spin reads as sparkle).")]
        public float spin;
    }
}
