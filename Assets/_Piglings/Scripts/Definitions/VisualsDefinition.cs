using System.Collections.Generic;
using UnityEngine;

namespace Piglings.Definitions
{
    /// <summary>How a band of throw quality looks: a plain colour, the colour pulsing, or an animated rainbow.</summary>
    public enum QualityLook { Solid, Pulse, Rainbow }

    /// <summary>
    /// One colour band: from <see cref="minQuality"/> up to the next band's. The same shape serves every band list in Visuals —
    /// throw quality, closeness to a record, a mult — so every number in the game is coloured by one kind of rule.
    /// </summary>
    [System.Serializable]
    public sealed class QualityBand
    {
        [Tooltip("Where this band starts. Quality bands: points ÷ the hour's gap (0.05 = 5% of the hour). Record bands: tonight ÷ " +
                 "your record (1 = a new record). Mult bands: the mult value (2 = +2).")]
        [Min(0f)] public float minQuality;
        public Color colour = Color.white;
        public QualityLook look = QualityLook.Solid;
    }

    /// <summary>
    /// The one visual definition (R2: Score Colours + Hour Palette + the scattered golds and effect numbers, merged): every
    /// colour and effect the views share, read through NightSession.Visuals, so a look is changed in one place.
    /// - Score colours (M10.D): popups by DEPTH, and closed throws by QUALITY (points ÷ the gap of the hour they were thrown
    ///   in — NightGoal.ThrowQuality).
    /// - The night's hour colours, first → last, then the GOLD — dawn's colour, and every "gold" moment (a Bouncy bonus
    ///   ring, the stone pile's upgrade pulse, a broken record).
    /// - Effects: what counts as a big chain (popup size, camera shake), the special peg's "+1 mult" popup.
    /// Unassigned = these defaults.
    /// </summary>
    [CreateAssetMenu(menuName = "Piglings/Visuals", fileName = "Visuals")]
    public sealed class VisualsDefinition : ScriptableObject
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

        [Header("Hours and gold")]
        [Tooltip("The night's hour colours, hour 1 first. Spread over each night's hours with the Gold as the last stop (dawn): " +
                 "hour 1 = the first colour, the hours between blend along the list (8 stops = 7 hours + dawn, one each). " +
                 "A new entry in the Inspector starts transparent — set its alpha. (Was the Hour Palette asset, minus its last colour.)")]
        [SerializeField] private List<Color> hourColours = new List<Color>();
        [Tooltip("THE gold: dawn's colour (scoreboard DAWN, the post-run), a Bouncy peg's bonus ring, the stone pile's upgrade " +
                 "pulse, a broken record.")]
        [SerializeField] private Color gold = new Color(1f, 0.85f, 0.42f, 1f);

        [Header("Effects")]
        [Tooltip("A chain result this big counts as a full-size chain: the chain popup's growth and the camera shake both scale " +
                 "up to it.")]
        [SerializeField, Min(1)] private int bigChainTotal = 300;
        [Tooltip("A special peg's \"+1 mult\" popup: its size (its colour and look come from the Mult Bands).")]
        [SerializeField, Min(0.01f)] private float pegMultScale = 0.7f;

        [Header("Record bands (relative to your best)")]
        [Tooltip("Rising Min Quality = tonight ÷ your record before tonight (1 = matched or beaten). A value takes the last band " +
                 "it reaches. Colours the post-run's wolves dropped against your most-wolves night.")]
        [SerializeField] private List<QualityBand> recordBands = new List<QualityBand>
        {
            new QualityBand { minQuality = 0f,    colour = new Color(1f, 0.95f, 0.82f) },                           // < 50% cream
            new QualityBand { minQuality = 0.5f,  colour = new Color(1f, 0.76f, 0.25f) },                           // amber
            new QualityBand { minQuality = 0.7f,  colour = new Color(1f, 0.52f, 0.15f) },                           // orange
            new QualityBand { minQuality = 0.85f, colour = new Color(1f, 0.25f, 0.2f) },                            // red
            new QualityBand { minQuality = 0.95f, colour = new Color(1f, 0.3f, 0.85f), look = QualityLook.Pulse },  // magenta, pulsing
            new QualityBand { minQuality = 1f,    colour = Color.white, look = QualityLook.Rainbow },               // a new record
        };

        [Header("Mult bands")]
        [Tooltip("Rising Min Quality = the mult value a special peg adds (+1, +2…). Colours its \"+N mult\" popup.")]
        [SerializeField] private List<QualityBand> multBands = new List<QualityBand>
        {
            new QualityBand { minQuality = 0f, colour = new Color(1f, 0.35f, 0.75f) },                              // +1 pink
            new QualityBand { minQuality = 2f, colour = new Color(1f, 0.3f, 0.85f), look = QualityLook.Pulse },     // +2 magenta, pulsing
            new QualityBand { minQuality = 3f, colour = Color.white, look = QualityLook.Rainbow },                  // +3 and up
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
        public Color Gold => gold;
        public int BigChainTotal => bigChainTotal;
        public float PegMultScale => pegMultScale;
        public int DepthCount => depthColours.Count;

        /// <summary>Stops the hours are spread over: the hour colours + the gold (dawn).</summary>
        public int HourStops => hourColours.Count + 1;

        /// <summary>
        /// Hour n's colour on a night of <paramref name="hours"/> hours: the hour colours + the gold sampled first → last over
        /// the night and its dawn (PaletteSampling), blending between stops. No hour colours → white (NightSession warns).
        /// </summary>
        public Color ColourAt(int hour, int hours) =>
            hourColours.Count == 0 ? Color.white : Stop(PaletteSampling.Position(hour, hours, HourStops));

        public int HourColourCount => hourColours.Count;

        /// <summary>Dawn's colour: the gold.</summary>
        public Color DawnColour => gold;

        // Stop i of [hour colours…, gold].
        private Color Stop(float at)
        {
            int last = HourStops - 1;
            int i = Mathf.Min(Mathf.FloorToInt(at), last);
            int next = Mathf.Min(i + 1, last);
            return Color.Lerp(StopAt(i), StopAt(next), at - i);
        }

        private Color StopAt(int i) => i < hourColours.Count ? hourColours[i] : gold;
        public int BandCount => qualityBands.Count;
        public int MultBandCount => multBands.Count;

        /// <summary>Depth d's colour; past the list → the last; an empty list → white.</summary>
        public Color DepthColour(int depth)
        {
            if (depthColours.Count == 0) return Color.white;
            return depthColours[Mathf.Clamp(depth, 0, depthColours.Count - 1)];
        }

        /// <summary>The band a quality falls in (index into the list), or -1 with no bands.</summary>
        public int BandIndex(float quality) => IndexIn(qualityBands, quality);

        /// <summary>The record band for tonight ÷ your record before tonight (index), or -1 with none.</summary>
        public int RecordBandIndex(float vsRecord) => IndexIn(recordBands, vsRecord);
        /// <summary>Record band i; a plain white band when there are none.</summary>
        public QualityBand RecordBandAt(int index) => At(recordBands, index);

        /// <summary>The mult band for a mult value (index), or -1 with none.</summary>
        public int MultBandIndex(float mult) => IndexIn(multBands, mult);
        /// <summary>Mult band i; a plain white band when there are none.</summary>
        public QualityBand MultBandAt(int index) => At(multBands, index);

        // Every band list works the same: a value takes the last band whose start it reaches; below the first → the first.
        private static int IndexIn(List<QualityBand> bands, float value)
        {
            int found = bands.Count > 0 ? 0 : -1;
            for (int i = 0; i < bands.Count; i++)
                if (bands[i] != null && value >= bands[i].minQuality) found = i;
            return found;
        }

        private static QualityBand At(List<QualityBand> bands, int index)
        {
            if (index < 0 || bands.Count == 0) return new QualityBand();
            return bands[Mathf.Clamp(index, 0, bands.Count - 1)] ?? new QualityBand();
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
