using System.Collections.Generic;
using Piglings.Events;
using Piglings.Simulation;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// The live chain counters (M10.S, PROTOTYPE_V2.md ▸ PR S), screen space: one per chain in flight, ticking up with each
    /// gain (ChainGained: SCORE × MULT × H). The newest sits on top (where the template is laid out); older ones step
    /// down and shrink; at most Max Visible show. When its chain closes (ChainScored) a counter shows the result, holds a
    /// beat, then flies to the score (Fly Target) and fades out; the others close the gap.
    /// Yam lays out one ChainCounter as the template (it's hidden and cloned). Reads only.
    /// </summary>
    public sealed class ChainCounterView : MonoBehaviour
    {
        [SerializeField] private NightSession session;
        [Tooltip("One counter, laid out where the newest should sit (under the night track). Hidden and cloned per chain.")]
        [SerializeField] private ChainCounter template;
        [Tooltip("Where a resolved counter flies (the score). Empty = it fades where it is.")]
        [SerializeField] private RectTransform flyTarget;

        [Header("Stack")]
        [SerializeField, Min(1)] private int maxVisible = 3;
        [Tooltip("Each older counter sits this far from the one above (canvas units; negative y = down).")]
        [SerializeField] private Vector2 step = new Vector2(0f, -70f);
        [Tooltip("Each older counter is this much smaller than the one above.")]
        [SerializeField, Range(0.3f, 1f)] private float olderScale = 0.8f;
        [Tooltip("How fast counters glide to their place (higher = snappier).")]
        [SerializeField, Min(0.1f)] private float glide = 12f;

        [Header("Resolve")]
        [Tooltip("Seconds the result shows before it flies.")]
        [SerializeField, Min(0f)] private float resultHoldSeconds = 0.45f;
        [Tooltip("Seconds to fly to the score (fading out on the way).")]
        [SerializeField, Min(0.01f)] private float flySeconds = 0.35f;

        private sealed class Live
        {
            public ChainCounter Counter;
            public float ResolvedAt;
            public Vector3 FlyFrom;
            public bool Flying;
        }

        private readonly Dictionary<GameId, Live> _byChain = new Dictionary<GameId, Live>();
        private readonly List<Live> _stack = new List<Live>();       // open chains, newest first
        private readonly List<Live> _leaving = new List<Live>();     // resolved: showing the result, then flying
        private Vector2 _topPosition;
        private Vector3 _topScale;

        private void Start()
        {
            if (template == null) { Debug.LogWarning("ChainCounterView: no template counter — nothing to show.", this); enabled = false; return; }
            _topPosition = template.Rect.anchoredPosition;
            _topScale = template.Rect.localScale;
            template.gameObject.SetActive(false);
            session.Bus.Subscribe<ChainGained>(OnGained);
            session.Bus.Subscribe<ChainScored>(OnScored);
        }

        private void OnDestroy()
        {
            if (session == null || session.Bus == null) return;
            session.Bus.Unsubscribe<ChainGained>(OnGained);
            session.Bus.Unsubscribe<ChainScored>(OnScored);
        }

        private void OnGained(ChainGained e)
        {
            if (!_byChain.TryGetValue(e.Chain.Id, out var live))
            {
                var counter = Instantiate(template, template.transform.parent);
                counter.name = $"{template.name} (chain)";
                counter.gameObject.SetActive(true);
                counter.Rect.anchoredPosition = _topPosition;
                counter.Rect.localScale = _topScale;
                live = new Live { Counter = counter };
                _byChain[e.Chain.Id] = live;
                _stack.Insert(0, live);   // the newest on top
            }
            live.Counter.Show(e.Score, e.Mult, e.Hour);
        }

        private void OnScored(ChainScored e)
        {
            if (!_byChain.TryGetValue(e.Chain.Id, out var live)) return;
            _byChain.Remove(e.Chain.Id);
            _stack.Remove(live);
            live.Counter.Resolve(e.Total);
            live.ResolvedAt = Time.time;
            _leaving.Add(live);
        }

        private void Update()
        {
            float k = 1f - Mathf.Exp(-glide * Time.deltaTime);
            for (int i = 0; i < _stack.Count; i++)
            {
                var rect = _stack[i].Counter.Rect;
                var goalPos = _topPosition + step * i;
                var goalScale = _topScale * Mathf.Pow(olderScale, i);
                rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, goalPos, k);
                rect.localScale = Vector3.Lerp(rect.localScale, goalScale, k);
                float alpha = i < maxVisible ? 1f : 0f;
                var group = _stack[i].Counter.Group;
                group.alpha = Mathf.Lerp(group.alpha, alpha, k);
            }

            for (int i = _leaving.Count - 1; i >= 0; i--)
            {
                var live = _leaving[i];
                float since = Time.time - live.ResolvedAt;
                if (since < resultHoldSeconds) continue;
                if (!live.Flying) { live.Flying = true; live.FlyFrom = live.Counter.transform.position; }
                float t = Mathf.Clamp01((since - resultHoldSeconds) / flySeconds);
                float eased = t * t;   // accelerating into the score
                if (flyTarget != null) live.Counter.transform.position = Vector3.Lerp(live.FlyFrom, flyTarget.position, eased);
                live.Counter.Rect.localScale = Vector3.Lerp(live.Counter.Rect.localScale, _topScale * 0.6f, k);
                live.Counter.Group.alpha = 1f - t;
                if (t < 1f) continue;
                Destroy(live.Counter.gameObject);
                _leaving.RemoveAt(i);
            }
        }
    }
}
