using Piglings.Definitions;
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
    ///  - Bouncy: every hit makes the peg pop (squash-stretch); a stone or ball that gives its chain the bonus also gets a small gold ring;
    ///  - Splitter: when it splits a stone, the needle flashes and sparkles burst out of it;
    ///  - Bomb: going off = a star and an orange ring as wide as its radius; then the spent sprite until it recharges,
    ///    when a few sparkles fizz on the fuse and the sprite swaps back;
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

        // R4c: each effect's own look (the Bouncy pop and ring, the Splitter flash and burst, the Bomb star, ring, spent tint
        // and recharge fizz) lives on its PegDefinition's effect class — tuned per peg, played here.

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
            public BouncyEffect Popping;    // whose pop it is (its seconds and amount)
            public float FlashAge = -1f;    // seconds since the last split; < 0 = not flashing
            public SplitterEffect Flashing; // whose flash it is (its colour and seconds)
        }

        private Socket[] _sockets = new Socket[0];

        // The effect settings of the peg placed in socket i, if it's that effect (else null).
        private T EffectAt<T>(int socket) where T : PegEffectSettings
        {
            var state = session.State.Sockets[socket];
            return state.IsEmpty ? null : session.FindPeg(state.PegId)?.Settings as T;
        }
        private FxSprites _fx;

        private void Awake() => _fx = new FxSprites(transform);

        // Start, not Awake: the session's bus and state are created in its Awake.
        private void Start()
        {
            BindSockets();
            board.Rebuilt += BindSockets;
            session.Bus.Subscribe<PegPlaced>(OnPegPlaced);
            session.Bus.Subscribe<PegMerged>(OnPegMerged);
            session.Bus.Subscribe<PegSocketSet>(OnPegSocketSet);
            session.Bus.Subscribe<PegHit>(OnPegHit);
            session.Bus.Subscribe<PegBounced>(OnPegBounced);
            session.Bus.Subscribe<StoneSplit>(OnStoneSplit);
            session.Bus.Subscribe<BombExploded>(OnBombExploded);
            session.Bus.Subscribe<PegRecharged>(OnPegRecharged);
        }

        // One overlay set per Hold. Again after a rebuild (a slice swapped in the day phase): the old Holds are gone with
        // their overlays, and nothing is placed yet before the night begins.
        private void BindSockets()
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
        }

        private void OnDestroy()
        {
            if (board != null) board.Rebuilt -= BindSockets;
            if (session == null || session.Bus == null) return;
            session.Bus.Unsubscribe<PegPlaced>(OnPegPlaced);
            session.Bus.Unsubscribe<PegMerged>(OnPegMerged);
            session.Bus.Unsubscribe<PegSocketSet>(OnPegSocketSet);
            session.Bus.Unsubscribe<PegHit>(OnPegHit);
            session.Bus.Unsubscribe<PegBounced>(OnPegBounced);
            session.Bus.Unsubscribe<StoneSplit>(OnStoneSplit);
            session.Bus.Unsubscribe<BombExploded>(OnBombExploded);
            session.Bus.Unsubscribe<PegRecharged>(OnPegRecharged);
        }

        private void OnPegPlaced(PegPlaced e) => Refresh(e.Socket);
        private void OnPegMerged(PegMerged e) => Refresh(e.Socket);
        private void OnPegSocketSet(PegSocketSet e) => Refresh(e.Socket);

        // Every hit on a Bouncy peg pops it (the bonus — stones and balls, M10.S — is once per peg per hitter: the ring).
        private void OnPegHit(PegHit e)
        {
            if (e.Effect != PegEffect.Bouncy || e.Socket >= _sockets.Length || _sockets[e.Socket].Peg == null) return;
            var bouncy = EffectAt<BouncyEffect>(e.Socket);
            if (bouncy == null) return;
            _sockets[e.Socket].Popping = bouncy;
            _sockets[e.Socket].PopAge = 0f;
        }

        private void OnPegBounced(PegBounced e)
        {
            if (e.Socket >= _sockets.Length || _sockets[e.Socket].Peg == null) return;
            var peg = _sockets[e.Socket].Peg;
            var bouncy = EffectAt<BouncyEffect>(e.Socket);
            if (bouncy == null) return;
            _fx.Spawn(bouncy.bonusRing, peg.transform.position, session.Visuals.Gold, bouncy.bonusRingMotion, peg.sortingLayerID, peg.sortingOrder + 3);
        }

        private void OnBombExploded(BombExploded e)
        {
            if (e.Socket >= _sockets.Length || _sockets[e.Socket].Peg == null) return;
            var peg = _sockets[e.Socket].Peg;
            var at = peg.transform.position;
            var def = session.FindPeg(e.PegId);
            var bomb = def != null ? def.Settings as BombEffect : null;
            if (bomb != null)
            {
                _fx.Spawn(bomb.star, at, bomb.colour, bomb.starMotion, peg.sortingLayerID, peg.sortingOrder + 3);
                if (bomb.ring != null && bomb.ring.bounds.size.x > 0f)
                {
                    var ring = bomb.ringMotion;
                    ring.endScale = 2f * def.BombRadiusAt(e.Level) / bomb.ring.bounds.size.x;   // the ring's outer edge ends on the radius
                    _fx.Spawn(bomb.ring, at, bomb.colour, ring, peg.sortingLayerID, peg.sortingOrder + 3);
                }
            }
            Refresh(e.Socket);   // spent look
        }

        private void OnPegRecharged(PegRecharged e)
        {
            if (e.Socket >= _sockets.Length || _sockets[e.Socket].Peg == null) return;
            var peg = _sockets[e.Socket].Peg;
            var bomb = EffectAt<BombEffect>(e.Socket);
            if (bomb != null)
            {
                var fuse = peg.transform.position + (Vector3)bomb.fuseOffset;
                for (int i = 0; i < bomb.rechargeSparkles; i++)
                    _fx.Spawn(bomb.rechargeSparkle, fuse, bomb.colour, bomb.rechargeSparkleMotion, peg.sortingLayerID, peg.sortingOrder + 3,
                              Random.insideUnitCircle * 0.08f);
            }
            Refresh(e.Socket);
        }

        private void OnStoneSplit(StoneSplit e)
        {
            if (e.Socket >= _sockets.Length || _sockets[e.Socket].Peg == null) return;
            var s = _sockets[e.Socket];
            var splitter = EffectAt<SplitterEffect>(e.Socket);
            if (splitter == null) return;
            s.Flashing = splitter;
            s.FlashAge = 0f;
            // Evenly round the needle, with a little jitter so two bursts never look the same.
            float start = Random.Range(0f, 360f);
            for (int i = 0; i < splitter.sparkles; i++)
            {
                float angle = (start + 360f * i / splitter.sparkles) * Mathf.Deg2Rad;
                var drift = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (splitter.sparkleSpread * Random.Range(0.7f, 1.1f));
                _fx.Spawn(splitter.sparkle, s.Peg.transform.position, splitter.sparkleColour, splitter.sparkleMotion,
                          s.Peg.sortingLayerID, s.Peg.sortingOrder + 3, drift);
            }
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
                // A spent bomb: its spent art, or its usual art greyed when there's none.
                if (state.IsSpent && def != null)
                {
                    if (def.SpentSprite != null) s.Peg.sprite = def.SpentSprite;
                    else if (def.Settings is BombEffect bomb) s.BaseColor *= bomb.spentTint;
                }
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
                s.Peg.color = Flash(s, faded);
                s.Level.color = new Color(1f, 1f, 1f, alpha);

                Pop(s);
            }
            _fx.Update(Time.deltaTime);
        }

        // The split flash: the needle jumps to the flash colour and fades back to what it was.
        private Color Flash(Socket s, Color normal)
        {
            if (s.FlashAge < 0f || s.Flashing == null) return normal;
            s.FlashAge += Time.deltaTime;
            float t = Mathf.Clamp01(s.FlashAge / s.Flashing.flashSeconds);
            if (t >= 1f) s.FlashAge = -1f;
            return Color.Lerp(s.Flashing.flashColour, normal, t);
        }

        // Squash, then stretch, settling: wide-and-short first (the impact), then tall-and-thin, fading out.
        private void Pop(Socket s)
        {
            if (s.PopAge < 0f || s.Popping == null) return;
            s.PopAge += Time.deltaTime;
            float t = Mathf.Clamp01(s.PopAge / s.Popping.popSeconds);
            float wobble = Mathf.Sin(t * 2f * Mathf.PI) * (1f - t) * s.Popping.popAmount;
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
