using System.Collections.Generic;
using UnityEngine;

namespace Piglings.Definitions
{
    /// <summary>How a band of throw quality looks: a plain colour, the colour pulsing, or an animated rainbow.</summary>
    public enum QualityLook { Solid, Pulse, Rainbow }

    /// <summary>One band of throw quality: from <see cref="minQuality"/> up to the next band's.</summary>
    [System.Serializable]
    public sealed class QualityBand
    {
        [Tooltip("Points ÷ the gap of the hour the throw was thrown in, where this band starts (0.05 = 5% of the hour).")]
        [Min(0f)] public float minQuality;
        public Color colour = Color.white;
        public QualityLook look = QualityLook.Solid;
    }

    /// <summary>
    /// The score colours every view shares (M10.D): popups by DEPTH, and closed throws by QUALITY (points ÷ the gap of
    /// the hour they were thrown in — NightGoal.ThrowQuality). The scoreboard, the chain popup and the post-run all read
    /// this one asset (through NightSession), so a colour is changed in one place. Unassigned = these defaults.
    /// </summary>
    [CreateAssetMenu(menuName = "Piglings/Score Colours", fileName = "ScoreColours")]
    public sealed class ScoreColoursDefinition : ScriptableObject
    {
        [Tooltip("Index = depth (0 = hit by the stone). Deeper than the list → the last colour. " +
                 "A new entry in the Inspector starts transparent — set its alpha.")]
        [SerializeField] private List<Color> depthColours = new List<Color>
        {
            new Color(1f, 0.95f, 0.82f),   // 0 cream
            new Color(1f, 0.76f, 0.25f),   // 1 amber
            new Color(1f, 0.52f, 0.15f),   // 2 orange
            new Color(1f, 0.25f, 0.2f),    // 3 red
            new Color(1f, 0.3f, 0.85f),    // 4+ magenta
        };

        [Tooltip("Rising Min Quality. A throw takes the last band it reaches; below the first → the first.")]
        [SerializeField] private List<QualityBand> qualityBands = new List<QualityBand>
        {
            new QualityBand { minQuality = 0f,    colour = new Color(1f, 0.95f, 0.82f) },                           // < 5% cream
            new QualityBand { minQuality = 0.05f, colour = new Color(1f, 0.76f, 0.25f) },                           // amber
            new QualityBand { minQuality = 0.15f, colour = new Color(1f, 0.52f, 0.15f) },                           // orange
            new QualityBand { minQuality = 0.3f,  colour = new Color(1f, 0.25f, 0.2f) },                            // red
            new QualityBand { minQuality = 0.5f,  colour = new Color(1f, 0.3f, 0.85f), look = QualityLook.Pulse },  // magenta, pulsing
            new QualityBand { minQuality = 1f,    colour = Color.white, look = QualityLook.Rainbow },               // ≥ a whole hour
        };

        [Header("Animated looks")]
        [Tooltip("Pulse: how far the colour swings toward white (0..1), and how many pulses per second.")]
        [SerializeField, Range(0f, 1f)] private float pulseAmount = 0.45f;
        [SerializeField, Min(0f)] private float pulsesPerSecond = 2f;
        [Tooltip("Rainbow: trips through the rainbow per second, and how much of it one word spans.")]
        [SerializeField, Min(0f)] private float rainbowSpeed = 0.8f;
        [SerializeField, Min(0f)] private float rainbowSpread = 1f;

        public float PulseAmount => pulseAmount;
        public float PulsesPerSecond => pulsesPerSecond;
        public float RainbowSpeed => rainbowSpeed;
        public float RainbowSpread => rainbowSpread;
        public int DepthCount => depthColours.Count;
        public int BandCount => qualityBands.Count;

        /// <summary>Depth d's colour; past the list → the last; an empty list → white.</summary>
        public Color DepthColour(int depth)
        {
            if (depthColours.Count == 0) return Color.white;
            return depthColours[Mathf.Clamp(depth, 0, depthColours.Count - 1)];
        }

        /// <summary>The band a quality falls in (index into the list), or -1 with no bands.</summary>
        public int BandIndex(float quality)
        {
            int found = qualityBands.Count > 0 ? 0 : -1;
            for (int i = 0; i < qualityBands.Count; i++)
                if (qualityBands[i] != null && quality >= qualityBands[i].minQuality) found = i;
            return found;
        }

        /// <summary>Band i (past the list → the last); a plain white band when there are none.</summary>
        public QualityBand BandAt(int index)
        {
            if (qualityBands.Count == 0) return new QualityBand();
            var band = qualityBands[Mathf.Clamp(index, 0, qualityBands.Count - 1)];
            return band ?? new QualityBand();
        }

        /// <summary>The band for a quality; a plain white band when there are none.</summary>
        public QualityBand BandFor(float quality)
        {
            int i = BandIndex(quality);
            return i >= 0 && qualityBands[i] != null ? qualityBands[i] : new QualityBand();
        }
    }
}
