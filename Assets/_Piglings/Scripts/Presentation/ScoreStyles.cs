using System.Collections.Generic;
using Piglings.Definitions;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// Turns the shared score colours (VisualsDefinition) into popup styles (PopupColor): a depth → a solid colour,
    /// a throw's quality → its band's look (solid, pulsing, animated rainbow). A plain class owned by a view (like
    /// FxSprites), built once from the asset and cached, so a popup or a board row never allocates a style.
    /// Read at creation: a colour changed in play mode shows after a restart.
    /// </summary>
    public sealed class ScoreStyles
    {
        private readonly VisualsDefinition _colours;
        private readonly List<PopupColor> _depth = new List<PopupColor>();
        private readonly Dictionary<int, PopupColor> _bands = new Dictionary<int, PopupColor>();
        private readonly PopupColor _plain = new PopupColor();

        public ScoreStyles(VisualsDefinition colours)
        {
            _colours = colours;
            int count = colours != null ? Mathf.Max(1, colours.DepthCount) : 1;
            for (int d = 0; d < count; d++)
                _depth.Add(new PopupColor { mode = PopupColorMode.Solid, gradient = PopupColor.Flat(colours != null ? colours.DepthColour(d) : Color.white) });
        }

        /// <summary>Depth d's style (deeper than the list → the last).</summary>
        public PopupColor ForDepth(int depth) => _depth[Mathf.Clamp(depth, 0, _depth.Count - 1)];

        /// <summary>
        /// Depth d's style with the advanced looks (M10.S): the quality bands taken in order, one per depth — the same palette
        /// as the depth colours (cream, amber, orange, red, magenta…) but with the band's look, so the deepest ones pulse and
        /// the last is the rainbow. Deeper than the list → the last band.
        /// </summary>
        public PopupColor ForDepthBand(int depth)
        {
            if (_colours == null || _colours.BandCount == 0) return ForDepth(depth);
            int i = Mathf.Clamp(depth, 0, _colours.BandCount - 1);
            if (_depthBands.TryGetValue(i, out var style)) return style;
            var band = _colours.BandAt(i);
            style = Build(band.look, band.colour);
            _depthBands[i] = style;
            return style;
        }
        private readonly Dictionary<int, PopupColor> _depthBands = new Dictionary<int, PopupColor>();

        /// <summary>A throw's style from its quality (points ÷ its hour's gap).</summary>
        public PopupColor ForQuality(float quality)
        {
            if (_colours == null) return _plain;
            int band = _colours.BandIndex(quality);
            if (band < 0) return _plain;
            if (_bands.TryGetValue(band, out var style)) return style;
            var b = _colours.BandFor(quality);
            style = Build(b.look, b.colour);
            _bands[band] = style;
            return style;
        }

        /// <summary>The animated rainbow, whatever the quality (the post-run's best throw of the night). Same speed / spread as the bands'.</summary>
        public PopupColor Rainbow
        {
            get
            {
                if (_rainbow == null)
                    _rainbow = new PopupColor { mode = PopupColorMode.PerLetter, gradient = PopupColor.Rainbow(),
                                                speed = _colours != null ? _colours.RainbowSpeed : 1f,
                                                spread = _colours != null ? _colours.RainbowSpread : 1f };
                return _rainbow;
            }
        }
        private PopupColor _rainbow;

        private PopupColor Build(QualityLook look, Color colour)
        {
            switch (look)
            {
                // A pulse is a cycle colour → toward white → colour: Cycle loops it, so it breathes.
                case QualityLook.Pulse:
                    var light = Color.Lerp(colour, Color.white, _colours != null ? _colours.PulseAmount : 0.5f);
                    var g = new Gradient();
                    g.SetKeys(new[] { new GradientColorKey(colour, 0f), new GradientColorKey(light, 0.5f), new GradientColorKey(colour, 1f) },
                              new[] { new GradientAlphaKey(colour.a, 0f), new GradientAlphaKey(colour.a, 1f) });
                    return new PopupColor { mode = PopupColorMode.Cycle, gradient = g, speed = _colours != null ? _colours.PulsesPerSecond : 1f };
                case QualityLook.Rainbow:
                    return Rainbow;
                default:
                    return new PopupColor { mode = PopupColorMode.Solid, gradient = PopupColor.Flat(colour) };
            }
        }
    }
}
