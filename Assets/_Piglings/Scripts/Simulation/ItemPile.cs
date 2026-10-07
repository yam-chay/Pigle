using System;
using System.Collections.Generic;
using UnityEngine;

namespace Piglings.Simulation
{
    /// <summary>
    /// Real objects sitting in a pile's slots: the part every "count mirrored on screen" shares (the stone pile, the
    /// peg shelf's piles, future ammo containers). It holds and places the objects; it never decides how many there
    /// should be — its owner mirrors a count from the Rules and calls Add / TakeTop, then Reconcile to be sure.
    ///
    /// Plain class, not a component: the owner (e.g. StonePile) keeps the serialized settings and the event wiring,
    /// and creates/destroys the objects through the callbacks, so this never needs to know what they are.
    /// Top = the last slot filled. Added objects can fall into their slot from above (call UpdateDrops each frame).
    /// </summary>
    public sealed class ItemPile<T> where T : Component
    {
        private readonly Func<T> _create;
        private readonly Action<T> _destroy;
        private readonly Func<int, Vector3> _slotPosition;   // world position of slot i
        private readonly List<T> _items = new List<T>();      // index = slot; last = top

        private struct Drop { public T Item; public Vector3 From; public Vector3 To; public float Progress; }
        private readonly List<Drop> _drops = new List<Drop>();

        public float DropHeight { get; set; }
        public float DropSeconds { get; set; }

        public ItemPile(Func<T> create, Action<T> destroy, Func<int, Vector3> slotPosition)
        {
            _create = create; _destroy = destroy; _slotPosition = slotPosition;
        }

        public int Count => _items.Count;
        public IReadOnlyList<T> Items => _items;

        /// <summary>Creates one object in the next free slot; animated = it falls in from DropHeight above.</summary>
        public T Add(bool animate)
        {
            var slot = _slotPosition(_items.Count);
            var item = _create();
            item.transform.position = slot;
            _items.Add(item);
            if (animate && DropSeconds > 0f)
            {
                var from = slot + Vector3.up * DropHeight;
                item.transform.position = from;
                _drops.Add(new Drop { Item = item, From = from, To = slot, Progress = 0f });
            }
            return item;
        }

        /// <summary>Takes the top object out of the pile (it's the caller's now). Null when empty.</summary>
        public T TakeTop()
        {
            if (_items.Count == 0) return null;
            var top = _items[_items.Count - 1];
            _items.RemoveAt(_items.Count - 1);
            _drops.RemoveAll(d => d.Item == top);
            return top;
        }

        /// <summary>Puts an object back as the new top (e.g. a peg swapped out of the hand). Returns its slot position.</summary>
        public Vector3 PutBack(T item)
        {
            var slot = _slotPosition(_items.Count);
            _items.Add(item);
            return slot;
        }

        /// <summary>
        /// Makes what's on screen match the count: <paramref name="outside"/> objects the owner holds elsewhere (e.g. one
        /// in the pig's hand) count too. Removes from the top, adds without animation. Normally a no-op.
        /// </summary>
        public void Reconcile(int count, int outside = 0)
        {
            while (_items.Count > 0 && _items.Count + outside > count) _destroy(TakeTop());
            while (_items.Count + outside < count) Add(animate: false);
        }

        /// <summary>
        /// Puts every object straight onto its slot again — after the slots moved (e.g. the stones grew a level, so the
        /// spacing did too). Falls in progress land at once.
        /// </summary>
        public void Relayout()
        {
            _drops.Clear();
            for (int i = 0; i < _items.Count; i++)
                if (_items[i] != null) _items[i].transform.position = _slotPosition(i);
        }

        /// <summary>Moves the falling objects; call every frame.</summary>
        public void UpdateDrops(float deltaTime)
        {
            for (int i = _drops.Count - 1; i >= 0; i--)
            {
                var d = _drops[i];
                if (d.Item == null) { _drops.RemoveAt(i); continue; }
                d.Progress = Mathf.Min(1f, d.Progress + deltaTime / DropSeconds);
                d.Item.transform.position = Vector3.Lerp(d.From, d.To, d.Progress * d.Progress);   // accelerate like a fall
                if (d.Progress >= 1f) _drops.RemoveAt(i); else _drops[i] = d;
            }
        }
    }
}
