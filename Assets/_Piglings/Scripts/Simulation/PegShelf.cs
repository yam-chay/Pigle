using System.Collections.Generic;
using Piglings.Definitions;
using Piglings.Events;
using Piglings.Runtime;
using UnityEngine;

namespace Piglings.Simulation
{
    /// <summary>
    /// The peg shelf above the pig: one small pile per peg type, mirroring NightState.Shelf (written only by the
    /// Rules) with real peg objects — the same pile pieces as the stone pile (ItemPile, HopMover, PileLayout).
    ///
    /// During a placement round (with throws left):
    ///  - the next peg on the shelf hops to the pig's hand by itself;
    ///  - SwapNext (right mouse, PegThrower) hops it back to its pile and brings the next type's top peg;
    ///  - Pick (left-click on a shelf peg) does the same for the clicked pile.
    /// When the round ends, a peg still in the hand hops back to its pile. PegThrower takes the peg out of the hand to
    /// throw it (ReleaseHeld) and gives it back if the Rules refuse it (Return).
    ///
    /// Lives in Simulation because it hands out the real peg objects. Put it on the peg_shelf object; its position
    /// is the centre of the shelf, and the piles sit side by side along it.
    /// </summary>
    public sealed class PegShelf : MonoBehaviour
    {
        [SerializeField] private NightSession session;
        [Tooltip("Where the peg waits to be thrown: ThrowOrigin, the same point the stone uses. The peg follows it.")]
        [SerializeField] private Transform hand;

        [Header("Layout")]
        [Tooltip("Distance between the piles (one per peg type) along the shelf.")]
        [SerializeField] private float pileSpacing = 0.55f;
        [Tooltip("Where the piles sit relative to this object (the shelf sprite's pivot is at its bottom).")]
        [SerializeField] private Vector3 pileOffset = new Vector3(0f, 0.08f, 0f);
        [SerializeField, Min(1)] private int bottomRow = 3;
        [SerializeField] private float spacing = 0.14f;
        [SerializeField] private float rowHeight = 0.12f;
        [Tooltip("Peg size on the shelf and in the hand (1 = the size it has on a Hold).")]
        [SerializeField, Min(0.05f)] private float pegScale = 0.6f;
        [Tooltip("How close to a shelf peg a click must be to pick it (world units).")]
        [SerializeField, Min(0.01f)] private float pickRadius = 0.2f;

        [Header("Drawing")]
        [Tooltip("Sorting layer of the shelf pegs — the same as the stone so a peg draws over the pig's arm.")]
        [SerializeField] private string sortingLayer = "Pig";
        [SerializeField] private int sortingOrder = 20;
        [Tooltip("Used for a peg type whose PegDefinition has no sprite.")]
        [SerializeField] private Sprite fallbackSprite;

        [Header("Motion")]
        [SerializeField, Min(0.05f)] private float hopSeconds = 0.25f;
        [SerializeField] private float hopHeight = 0.35f;

        private sealed class Pile
        {
            public string Id;
            public PegDefinition Def;
            public ItemPile<SpriteRenderer> Items;
        }

        private readonly List<Pile> _piles = new List<Pile>();
        private HopMover _hops;
        private SpriteRenderer _held;   // in the hand, or hopping there
        private Pile _heldPile;
        // A thrown peg hasn't landed yet: don't bring the next one up (with the round's last throw in the air it would
        // only hop up and straight back).
        private bool _inFlight;

        /// <summary>The type of the peg in the hand (also while it's still hopping there); null if none.</summary>
        public string HeldPegId => _held != null ? _heldPile.Id : null;
        /// <summary>A peg is waiting in the hand, ready to throw.</summary>
        public bool HasPegInHand => _held != null && !_hops.IsHopping(_held.transform);
        public SpriteRenderer HeldPeg => _held;
        public float PegScale => pegScale;
        public float PickRadius => pickRadius;

        /// <summary>The pegs sitting on the shelf (not the one in the hand), for views.</summary>
        public IEnumerable<SpriteRenderer> ShelfPegs
        {
            get
            {
                foreach (var pile in _piles)
                    foreach (var peg in pile.Items.Items)
                        if (peg != null) yield return peg;
            }
        }

        // A round with throws left: the shelf is live.
        private bool Active => session.State.Phase == NightPhase.PegPlacement && session.State.PegThrowsLeft > 0;

        private void Awake()
        {
            _hops = new HopMover(t => Destroy(t.gameObject));
        }

        // Start, not Awake: the session's bus and state are created in its Awake.
        private void Start()
        {
            var shelf = session.State.Shelf;
            for (int i = 0; i < shelf.Count; i++)
            {
                var pile = new Pile { Id = shelf[i].PegId, Def = session.FindPeg(shelf[i].PegId) };
                var anchor = PileAnchor(i, shelf.Count);
                var p = pile;   // captured by the callbacks below
                pile.Items = new ItemPile<SpriteRenderer>(() => CreatePeg(p), s => Destroy(s.gameObject), slot => SlotPosition(anchor, slot));
                _piles.Add(pile);
                pile.Items.Reconcile(shelf[i].Count);
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

        // ---------- for PegThrower ----------

        /// <summary>Right mouse: the peg in the hand goes back, the next type's top peg comes up (cycles).</summary>
        public void SwapNext()
        {
            if (!Active || _held == null || _piles.Count < 2) return;
            int start = _piles.IndexOf(_heldPile);
            for (int step = 1; step < _piles.Count; step++)
            {
                var pile = _piles[(start + step) % _piles.Count];
                if (pile.Items.Count == 0) continue;
                ReturnHeld();
                TakeToHand(pile);
                return;
            }
        }

        /// <summary>Left-click on a shelf peg: its pile's top peg comes to the hand (the held one goes back).</summary>
        public void Pick(SpriteRenderer peg)
        {
            if (!Active || peg == null) return;
            foreach (var pile in _piles)
            {
                if (!Contains(pile, peg)) continue;
                if (pile == _heldPile) return;
                ReturnHeld();
                TakeToHand(pile);
                return;
            }
        }

        /// <summary>The shelf peg nearest to a point within the pick radius (not the one in the hand); null if none.</summary>
        public SpriteRenderer ItemAt(Vector2 point)
        {
            SpriteRenderer best = null;
            float bestSq = pickRadius * pickRadius;
            foreach (var peg in ShelfPegs)
            {
                float sq = ((Vector2)peg.transform.position - point).sqrMagnitude;
                if (sq <= bestSq) { bestSq = sq; best = peg; }
            }
            return best;
        }

        /// <summary>Takes the peg out of the hand for a throw. Null if none is ready.</summary>
        public SpriteRenderer ReleaseHeld(out string pegId)
        {
            pegId = null;
            if (!HasPegInHand) return null;
            var peg = _held;
            pegId = _heldPile.Id;
            _held = null;
            _heldPile = null;
            _inFlight = true;
            return peg;
        }

        /// <summary>A thrown peg the Rules refused: it hops back onto its pile.</summary>
        public void Return(SpriteRenderer peg, string pegId)
        {
            _inFlight = false;
            var pile = _piles.Find(p => p.Id == pegId);
            if (pile == null) { Destroy(peg.gameObject); return; }
            _hops.StartTo(peg.transform, pile.Items.PutBack(peg), hopSeconds);
        }

        // ---------- mirroring the Rules ----------

        // The shelf count dropped (the Rules placed or merged a peg). The thrown peg is already gone; make the piles
        // match — normally a no-op, the one in the hand counts for its pile.
        private void OnPegPlaced(PegPlaced e) => Reconcile();
        private void OnPegMerged(PegMerged e) => Reconcile();

        private void Reconcile()
        {
            _inFlight = false;
            var shelf = session.State.Shelf;
            for (int i = 0; i < _piles.Count && i < shelf.Count; i++)
                _piles[i].Items.Reconcile(shelf[i].Count, _heldPile == _piles[i] ? 1 : 0);
        }

        // ---------- the hand ----------

        private void TakeToHand(Pile pile)
        {
            var peg = pile.Items.TakeTop();
            if (peg == null) return;
            _held = peg;
            _heldPile = pile;
            _hops.Start(peg.transform, hand, Vector3.zero, hopSeconds);
        }

        // The swap always shows the hop back to the slot, so the change is visible.
        private void ReturnHeld()
        {
            if (_held == null) return;
            var peg = _held;
            var pile = _heldPile;
            _held = null;
            _heldPile = null;
            _hops.StartTo(peg.transform, pile.Items.PutBack(peg), hopSeconds);
        }

        private void Update()
        {
            _hops.Height = hopHeight;
            if (hand == null) return;

            if (Active && _held == null && !_inFlight)
            {
                // The next peg comes up by itself: the first pile (in shelf order) that still has one.
                foreach (var pile in _piles)
                    if (pile.Items.Count > 0) { TakeToHand(pile); break; }
            }
            else if (!Active && _held != null)
            {
                ReturnHeld();   // the round is over: the peg goes back on the shelf
            }
        }

        // LateUpdate: after the Animator has posed the pig, so the hand is where it's drawn (same as the stone).
        private void LateUpdate()
        {
            _hops.Update(Time.deltaTime);
            if (HasPegInHand && hand != null) _held.transform.position = hand.position;
        }

        // ---------- building ----------

        private SpriteRenderer CreatePeg(Pile pile)
        {
            var go = new GameObject($"Peg {pile.Id}");
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.localScale = Vector3.one * pegScale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = sortingLayer;
            sr.sortingOrder = sortingOrder;
            if (pile.Def != null && pile.Def.Sprite != null) sr.sprite = pile.Def.Sprite;
            else
            {
                sr.sprite = fallbackSprite;
                if (pile.Def != null) sr.color = pile.Def.FallbackTint;
            }
            return sr;
        }

        // Piles side by side along the shelf, centred on this object.
        private Vector3 PileAnchor(int index, int count) =>
            transform.position + pileOffset + Vector3.right * ((index - (count - 1) * 0.5f) * pileSpacing);

        private Vector3 SlotPosition(Vector3 anchor, int slot)
        {
            PileLayout.Pyramid(slot, bottomRow, spacing, rowHeight, out float x, out float y);
            return anchor + new Vector3(x, y, 0f);
        }

        private static bool Contains(Pile pile, SpriteRenderer peg)
        {
            foreach (var p in pile.Items.Items) if (p == peg) return true;
            return false;
        }
    }
}
