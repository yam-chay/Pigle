using System.Collections.Generic;
using Piglings.Events;
using Piglings.Simulation;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// Makes the peg shelf say "you can click these" during a placement round, and only then:
    ///  - every shelf peg pulses gently (scale + a glow toward Glow Color);
    ///  - the peg under the pointer grows and glows fully, and the cursor changes (if a Hover Cursor is set);
    ///  - the peg in the hand is drawn plain.
    /// The rest of the night the shelf sits still. Reads PegShelf / PegThrower; never changes the game.
    /// </summary>
    public sealed class PegShelfView : MonoBehaviour
    {
        [SerializeField] private NightSession session;
        [SerializeField] private PegShelf shelf;
        [SerializeField] private PegThrower thrower;

        [Header("Pulse (placement round)")]
        [Tooltip("How much the pegs grow at the top of a pulse (0.08 = +8%).")]
        [SerializeField, Min(0f)] private float pulseAmount = 0.08f;
        [Tooltip("Pulses per second.")]
        [SerializeField, Min(0f)] private float pulseSpeed = 1.2f;
        [SerializeField] private Color glowColor = new Color(1f, 0.95f, 0.6f, 1f);
        [Tooltip("How far toward Glow Color the pulse goes (0..1).")]
        [SerializeField, Range(0f, 1f)] private float glowAmount = 0.5f;

        [Header("Hover")]
        [SerializeField, Min(1f)] private float hoverScale = 1.25f;
        [Tooltip("Optional cursor over a clickable shelf peg. Empty = the cursor doesn't change.")]
        [SerializeField] private Texture2D hoverCursor;
        [SerializeField] private Vector2 cursorHotspot;

        // Each peg's own colour (a peg without art is tinted), so the glow always starts from it.
        private readonly Dictionary<SpriteRenderer, Color> _baseColors = new Dictionary<SpriteRenderer, Color>();
        private bool _cursorSet;

        private void LateUpdate()
        {
            bool live = session.State.Phase == NightPhase.PegPlacement && session.State.PegThrowsLeft > 0;
            var hovered = live && thrower != null ? thrower.HoveredShelfPeg : null;
            float pulse01 = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed * 2f * Mathf.PI);

            foreach (var peg in shelf.ShelfPegs)
            {
                var baseColor = BaseColor(peg);
                bool isHovered = peg == hovered;
                float scale = shelf.PegScale * (live ? 1f + pulseAmount * pulse01 : 1f) * (isHovered ? hoverScale : 1f);
                float glow = !live ? 0f : isHovered ? 1f : glowAmount * pulse01;
                peg.transform.localScale = Vector3.one * scale;
                var glowTarget = new Color(glowColor.r, glowColor.g, glowColor.b, baseColor.a);
                peg.color = Color.Lerp(baseColor, glowTarget, glow);
            }

            var held = shelf.HeldPeg;
            if (held != null)
            {
                held.transform.localScale = Vector3.one * shelf.PegScale;
                held.color = BaseColor(held);
            }

            SetCursor(hovered != null);
        }

        private Color BaseColor(SpriteRenderer peg)
        {
            if (!_baseColors.TryGetValue(peg, out var c)) _baseColors[peg] = c = peg.color;
            return c;
        }

        private void SetCursor(bool overPeg)
        {
            if (hoverCursor == null || overPeg == _cursorSet) return;
            _cursorSet = overPeg;
            Cursor.SetCursor(overPeg ? hoverCursor : null, cursorHotspot, CursorMode.Auto);
        }

        private void OnDisable()
        {
            if (_cursorSet) Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            _cursorSet = false;
        }
    }
}
