using Piglings.Events;
using Piglings.Simulation;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// The pegs on the wall, drawn on their Holds (each Hold already has a SpriteRenderer showing the plain hold):
    ///  - a placed peg shows its PegDefinition sprite; levels 2 and 3 add an overlay on top;
    ///  - during a placement round, the sockets the held peg can go into (empty, or a same-type peg it can merge with)
    ///    get the highlight overlay, pulsing; placed pegs it can't go on fade out — the throw ignores them.
    /// Reads the board from NightState and redraws on PegPlaced / PegMerged. Never writes state.
    /// Put it next to PegBoard (on Barn).
    /// </summary>
    public sealed class PegBoardView : MonoBehaviour
    {
        [SerializeField] private NightSession session;
        [SerializeField] private PegBoard board;
        [SerializeField] private PegShelf shelf;

        [Header("Overlays (same size and pivot as a peg)")]
        [SerializeField] private Sprite level2Overlay;
        [SerializeField] private Sprite level3Overlay;
        [SerializeField] private Sprite socketHighlight;

        [Header("During a placement round")]
        [SerializeField] private Color highlightColor = new Color(1f, 1f, 1f, 0.9f);
        [Tooltip("Highlight pulses per second.")]
        [SerializeField, Min(0f)] private float highlightPulse = 2.5f;
        [Tooltip("Opacity of placed pegs the held peg can't go on (it can be thrown past them).")]
        [SerializeField, Range(0f, 1f)] private float blockedOpacity = 0.3f;

        private sealed class Socket
        {
            public SpriteRenderer Body;     // the Hold's own renderer
            public Sprite PlainSprite;      // what it showed at the start (the plain hold)
            public Color PlainColor;
            public Color BaseColor;         // what Refresh last set (before any fading)
            public SpriteRenderer Level;
            public SpriteRenderer Highlight;
        }

        private Socket[] _sockets = new Socket[0];

        // Start, not Awake: the session's bus and state are created in its Awake.
        private void Start()
        {
            _sockets = new Socket[board.SocketCount];
            for (int i = 0; i < _sockets.Length; i++)
            {
                var hold = board.HoldAt(i);
                var body = hold.GetComponent<SpriteRenderer>();
                var s = new Socket { Body = body };
                if (body != null)
                {
                    s.PlainSprite = body.sprite;
                    s.PlainColor = body.color;
                    s.Level = CreateOverlay(hold.transform, body, 1, "Level");
                    s.Highlight = CreateOverlay(hold.transform, body, 2, "Highlight");
                    s.Highlight.sprite = socketHighlight;
                }
                _sockets[i] = s;
                Refresh(i);
            }
            session.Bus.Subscribe<PegPlaced>(OnPegPlaced);
            session.Bus.Subscribe<PegMerged>(OnPegMerged);
        }

        private void OnDestroy()
        {
            if (session == null || session.Bus == null) return;
            session.Bus.Unsubscribe<PegPlaced>(OnPegPlaced);
            session.Bus.Unsubscribe<PegMerged>(OnPegMerged);
        }

        private void OnPegPlaced(PegPlaced e) => Refresh(e.Socket);
        private void OnPegMerged(PegMerged e) => Refresh(e.Socket);

        private void Refresh(int i)
        {
            var s = _sockets[i];
            if (s.Body == null) return;
            var state = session.State.Sockets[i];

            if (state.IsEmpty)
            {
                s.Body.sprite = s.PlainSprite;
                s.BaseColor = s.PlainColor;
            }
            else
            {
                // A peg without art keeps the plain sprite, tinted, so it still reads as placed.
                var def = session.FindPeg(state.PegId);
                bool hasArt = def != null && def.Sprite != null;
                s.Body.sprite = hasArt ? def.Sprite : s.PlainSprite;
                s.BaseColor = hasArt ? Color.white : (def != null ? def.FallbackTint : s.PlainColor);
            }
            s.Body.color = s.BaseColor;
            s.Level.sprite = state.Level >= 3 ? level3Overlay : state.Level == 2 ? level2Overlay : null;
        }

        private void LateUpdate()
        {
            var state = session.State;
            string held = state.Phase == NightPhase.PegPlacement ? shelf.HeldPegId : null;
            float pulse = 0.6f + 0.4f * Mathf.Sin(Time.time * highlightPulse * 2f * Mathf.PI);

            for (int i = 0; i < _sockets.Length; i++)
            {
                var s = _sockets[i];
                if (s.Body == null) continue;
                bool valid = held != null && session.IsValidPegTarget(i, held);
                bool blocked = held != null && !valid && !state.Sockets[i].IsEmpty;

                s.Highlight.enabled = valid && socketHighlight != null;
                if (s.Highlight.enabled) s.Highlight.color = new Color(highlightColor.r, highlightColor.g, highlightColor.b, highlightColor.a * pulse);

                float alpha = blocked ? blockedOpacity : 1f;
                s.Body.color = new Color(s.BaseColor.r, s.BaseColor.g, s.BaseColor.b, s.BaseColor.a * alpha);
                s.Level.color = new Color(1f, 1f, 1f, alpha);
            }
        }

        private static SpriteRenderer CreateOverlay(Transform hold, SpriteRenderer body, int orderAbove, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(hold, worldPositionStays: false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerID = body.sortingLayerID;
            sr.sortingOrder = body.sortingOrder + orderAbove;
            return sr;
        }
    }
}
