using Piglings.Events;
using Piglings.Simulation;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// The pegs on the wall, drawn on their Holds (each Hold already has a SpriteRenderer showing the plain hold):
    ///  - a placed peg shows its PegDefinition sprite on a child renderer (the Hold's own renderer is hidden under it);
    ///    levels 2 and 3 add an overlay on top. A child, so the peg can squash and stretch without touching the Hold's
    ///    collider, which lives on the Hold's own object;
    ///  - Bouncy: every hit makes the peg pop (squash-stretch); a ball that gets the bonus also gets a small gold ring;
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

        [Header("Bouncy")]
        [Tooltip("How long the squash-stretch lasts after a hit.")]
        [SerializeField, Min(0.01f)] private float popSeconds = 0.3f;
        [Tooltip("How far it squashes: 0.3 = 30% wider, then 30% taller.")]
        [SerializeField, Range(0f, 1f)] private float popAmount = 0.3f;
        [Tooltip("fx_burst_ring: the small gold ring when a ball gets the bonus. Empty = no ring.")]
        [SerializeField] private Sprite bonusRing;
        [SerializeField] private Color bonusRingColour = new Color(1f, 0.8f, 0.25f, 1f);
        [SerializeField] private FxMotion bonusRingMotion = new FxMotion { seconds = 0.35f, startScale = 0.2f, endScale = 0.9f };

        private sealed class Socket
        {
            public SpriteRenderer Body;     // the Hold's own renderer
            public Sprite PlainSprite;      // what it showed at the start (the plain hold)
            public Color PlainColor;
            public Color BaseColor;         // what Refresh last set (before any fading)
            public SpriteRenderer Peg;      // the placed peg, on a child (it pops without moving the collider)
            public SpriteRenderer Level;    // child of Peg: pops with it
            public SpriteRenderer Highlight;
            public float PopAge = -1f;      // seconds since the last Bouncy hit; < 0 = not popping
        }

        private Socket[] _sockets = new Socket[0];
        private FxSprites _fx;

        private void Awake() => _fx = new FxSprites(transform);

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
                    s.Peg = CreateOverlay(hold.transform, body, 1, "Peg");
                    s.Level = CreateOverlay(s.Peg.transform, body, 2, "Level");
                    s.Highlight = CreateOverlay(hold.transform, body, 3, "Highlight");
                    s.Highlight.sprite = socketHighlight;
                }
                _sockets[i] = s;
                Refresh(i);
            }
            session.Bus.Subscribe<PegPlaced>(OnPegPlaced);
            session.Bus.Subscribe<PegMerged>(OnPegMerged);
            session.Bus.Subscribe<PegHit>(OnPegHit);
            session.Bus.Subscribe<PegBounced>(OnPegBounced);
        }

        private void OnDestroy()
        {
            if (session == null || session.Bus == null) return;
            session.Bus.Unsubscribe<PegPlaced>(OnPegPlaced);
            session.Bus.Unsubscribe<PegMerged>(OnPegMerged);
            session.Bus.Unsubscribe<PegHit>(OnPegHit);
            session.Bus.Unsubscribe<PegBounced>(OnPegBounced);
        }

        private void OnPegPlaced(PegPlaced e) => Refresh(e.Socket);
        private void OnPegMerged(PegMerged e) => Refresh(e.Socket);

        // Every hit on a Bouncy peg pops it — stones too: they bounce off physically, they just get no bonus.
        private void OnPegHit(PegHit e)
        {
            if (e.Effect == PegEffect.Bouncy && e.Socket < _sockets.Length && _sockets[e.Socket].Peg != null) _sockets[e.Socket].PopAge = 0f;
        }

        private void OnPegBounced(PegBounced e)
        {
            if (e.Socket >= _sockets.Length || _sockets[e.Socket].Peg == null) return;
            var peg = _sockets[e.Socket].Peg;
            _fx.Spawn(bonusRing, peg.transform.position, bonusRingColour, bonusRingMotion, peg.sortingLayerID, peg.sortingOrder + 3);
        }

        private void Refresh(int i)
        {
            var s = _sockets[i];
            if (s.Body == null) return;
            var state = session.State.Sockets[i];

            // Empty: the Hold's own renderer shows the plain hold. Placed: it hides under the peg's child renderer.
            s.Body.enabled = state.IsEmpty;
            s.Peg.enabled = !state.IsEmpty;
            if (state.IsEmpty) s.BaseColor = s.PlainColor;
            else
            {
                // A peg without art keeps the plain sprite, tinted, so it still reads as placed.
                var def = session.FindPeg(state.PegId);
                bool hasArt = def != null && def.Sprite != null;
                s.Peg.sprite = hasArt ? def.Sprite : s.PlainSprite;
                s.BaseColor = hasArt ? Color.white : (def != null ? def.FallbackTint : s.PlainColor);
            }
            s.Body.color = s.BaseColor;
            s.Peg.color = s.BaseColor;
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
                var faded = new Color(s.BaseColor.r, s.BaseColor.g, s.BaseColor.b, s.BaseColor.a * alpha);
                s.Body.color = faded;
                s.Peg.color = faded;
                s.Level.color = new Color(1f, 1f, 1f, alpha);

                Pop(s);
            }
            _fx.Update(Time.deltaTime);
        }

        // Squash, then stretch, settling: wide-and-short first (the impact), then tall-and-thin, fading out.
        private void Pop(Socket s)
        {
            if (s.PopAge < 0f) return;
            s.PopAge += Time.deltaTime;
            float t = Mathf.Clamp01(s.PopAge / popSeconds);
            float wobble = Mathf.Sin(t * 2f * Mathf.PI) * (1f - t) * popAmount;
            s.Peg.transform.localScale = new Vector3(1f + wobble, 1f - wobble, 1f);
            if (t >= 1f) { s.PopAge = -1f; s.Peg.transform.localScale = Vector3.one; }
        }

        private static SpriteRenderer CreateOverlay(Transform parent, SpriteRenderer body, int orderAbove, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, worldPositionStays: false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerID = body.sortingLayerID;
            sr.sortingOrder = body.sortingOrder + orderAbove;
            return sr;
        }
    }
}
