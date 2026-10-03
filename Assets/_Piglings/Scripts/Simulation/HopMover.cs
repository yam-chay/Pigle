using System;
using System.Collections.Generic;
using UnityEngine;

namespace Piglings.Simulation
{
    /// <summary>
    /// The one hop every pile uses: an object flies in an arc from where it is to a target Transform that may be
    /// moving (the pig's animated hand, a climbing thief), then lands. A straight line to the target plus a
    /// parabola bump at the middle, so every hop in the game reads the same.
    ///
    /// Plain class, owned by the pile that started the hops. Call Update from LateUpdate: targets like the hand are
    /// posed by the Animator, so LateUpdate is when they're where they're drawn.
    /// If a target is destroyed mid-hop, onLost gets the object (the owner decides: usually destroy it).
    /// </summary>
    public sealed class HopMover
    {
        private sealed class Hop
        {
            public Transform Item; public Vector3 From; public Transform Target; public Vector3 Offset;
            public bool Fixed;   // true: Offset is a world position (a slot), there's no Target to lose
            public float Seconds; public float T; public Action<Transform> OnLand;
        }

        private readonly List<Hop> _hops = new List<Hop>();
        private readonly Action<Transform> _onLost;

        public float Height { get; set; }

        public HopMover(Action<Transform> onLost) { _onLost = onLost; }

        /// <summary>
        /// Starts a hop of <paramref name="item"/> to <paramref name="target"/> (+ a world offset), taking
        /// <paramref name="seconds"/> (0 = lands at once). onLand runs on landing (e.g. parent it to the target).
        /// Restarting a hop for an item already hopping replaces the old one.
        /// </summary>
        public void Start(Transform item, Transform target, Vector3 offset, float seconds, Action<Transform> onLand = null)
        {
            Cancel(item);
            _hops.Add(new Hop { Item = item, From = item.position, Target = target, Offset = offset, Seconds = seconds, T = 0f, OnLand = onLand });
        }

        /// <summary>A hop to a fixed world position (e.g. back into a pile slot). Nothing to lose mid-hop.</summary>
        public void StartTo(Transform item, Vector3 position, float seconds, Action<Transform> onLand = null)
        {
            Cancel(item);
            _hops.Add(new Hop { Item = item, From = item.position, Offset = position, Fixed = true, Seconds = seconds, T = 0f, OnLand = onLand });
        }

        public bool IsHopping(Transform item)
        {
            foreach (var h in _hops) if (h.Item == item) return true;
            return false;
        }

        /// <summary>Stops a hop where it is (the item stays put, no onLand).</summary>
        public void Cancel(Transform item) => _hops.RemoveAll(h => h.Item == item);

        /// <summary>Every hop going to this target ends now: each item goes to onLost (e.g. the target is leaving).</summary>
        public void LoseTarget(Transform target)
        {
            for (int i = _hops.Count - 1; i >= 0; i--)
            {
                if (_hops[i].Target != target) continue;
                var item = _hops[i].Item;
                _hops.RemoveAt(i);
                if (item != null) _onLost?.Invoke(item);
            }
        }

        public void Update(float deltaTime)
        {
            for (int i = _hops.Count - 1; i >= 0; i--)
            {
                var h = _hops[i];
                if (h.Item == null) { _hops.RemoveAt(i); continue; }
                if (!h.Fixed && h.Target == null) { _hops.RemoveAt(i); _onLost?.Invoke(h.Item); continue; }

                h.T = h.Seconds <= 0f ? 1f : Mathf.Min(1f, h.T + deltaTime / h.Seconds);
                var to = h.Fixed ? h.Offset : h.Target.position + h.Offset;
                h.Item.position = Arc(h.From, to, h.T, Height);
                if (h.T < 1f) continue;

                _hops.RemoveAt(i);
                h.OnLand?.Invoke(h.Item);
            }
        }

        /// <summary>The hop shape: from → to in a straight line, plus a bump of <paramref name="height"/> at the middle.</summary>
        public static Vector3 Arc(Vector3 from, Vector3 to, float t, float height) =>
            Vector3.Lerp(from, to, t) + Vector3.up * (height * 4f * t * (1f - t));
    }
}
