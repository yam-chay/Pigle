using System.Collections.Generic;
using Piglings.Definitions;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// Turns the shared score colours (ScoreColoursDefinition) into popup styles (PopupColor): a depth → a solid colour,
    /// a throw's quality → its band's look (solid, pulsing, animated rainbow). A plain class owned by a view (like
    /// FxSprites), built once from the asset and cached, so a popup or a board row never allocates a style.
    /// Read at creation: a colour changed in play mode shows after a restart.
    /// </summary>
    public sealed class ScoreStyles
    {
        private readonly ScoreColoursDefinition _colours;
        private readonly List<PopupColor> _depth = new List<PopupColor>();
        private readonly Dictionary<int, PopupColor> _bands = new Dictionary<int, PopupColor>();
        private readonly PopupColor _plain = new PopupColor();

        public ScoreStyles(ScoreColoursDefinition colours)
        {
            _colours = colours;
            int count = colours != null ? Mathf.Max(1, colours.DepthCount) : 1;
            for (int d = 0; d < count; d++)
                _depth.Add(new PopupColor { mode = PopupColorMode.Solid, gradient = PopupColor.Flat(colours != null ? colours.DepthColour(d) : Color.white) });
        }

        public ScoreColoursDefinition Colours => _colours;

        /// <summary>Depth d's style (deeper than the list → the last).</summary>
        public PopupColor ForDepth(int depth) => _depth[Mathf.Clamp(depth, 0, _depth.Count - 1)];

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

        /// <summary>A throw quality's base colour (for a marker or an icon tint that can't animate letters).</summary>
        public Color QualityColour(float quality) => _colours != null ? _colours.BandFor(quality).colour : Color.white;

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
