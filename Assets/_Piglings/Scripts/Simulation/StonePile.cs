using System.Collections.Generic;
using Piglings.Events;
using UnityEngine;

namespace Piglings.Simulation
{
    /// <summary>
    /// The pig's ammo as real stones on the perch: what you see is exactly what you have.
    ///
    /// It MIRRORS the count, it never decides it. NightState.StonesLeft (written only by NightReferee) is the
    /// one truth; this listens to StonesChanged — Thrown, Stolen, Added, Forfeited — and makes the stones
    /// match. So theft, refills and "empty" all come from the Rules, and CoreCheck can test them.
    ///
    /// - Stones sit in fixed pyramid slots on the rag, no physics, no collider (Throwable.Park).
    ///   Top stone = the last slot filled.
    /// - When the hand is empty and the pig may throw, the top stone hops in an arc to the hand and waits.
    ///   ThrowController takes it on release (ReleaseHeld) and it becomes a normal dynamic Throwable.
    ///   The hop is shorter than the throw cooldown, so the next stone is in the hand when you can throw.
    /// - A robot that breaches takes the top stone: it's parented to the robot, stays parked (never a weapon)
    ///   and leaves with it.
    /// - Added stones drop onto the next free slots.
    ///
    /// Lives in Simulation because it hands out the real throwables. Put it on the perch piece; its position
    /// is the centre of the bottom row.
    /// </summary>
    public sealed class StonePile : MonoBehaviour
    {
        [SerializeField] private NightSession session;
        [Tooltip("The stone prefab — the same one ThrowController throws.")]
        [SerializeField] private Throwable stonePrefab;
        [Tooltip("Where the stone waits to be thrown: ThrowOrigin, placed as a child of the pig's throwing-hand bone " +
                 "so it moves with the animation. The stone follows it (it isn't parented to it), so the rig's scale never touches the stone.")]
        [SerializeField] private Transform hand;
        [Tooltip("To find the robot that steals a stone.")]
        [SerializeField] private RobotSpawner spawner;

        [Header("Layout (pyramid, bottom row first)")]
        [SerializeField, Min(1)] private int bottomRow = 8;
        [SerializeField] private float spacing = 0.17f;     // between stone centres in a row
        [SerializeField] private float rowHeight = 0.14f;

        [Header("Motion")]
        [Tooltip("Pile → hand. Keep it shorter than ThrowController's cooldown, or it reads as input lag.")]
        [SerializeField, Min(0.05f)] private float hopSeconds = 0.25f;
        [SerializeField] private float hopHeight = 0.45f;
        [Tooltip("Added stones fall into their slot from this high.")]
        [SerializeField] private float dropHeight = 0.6f;
        [SerializeField, Min(0.05f)] private float dropSeconds = 0.3f;
        [Tooltip("Where a stolen stone sits on the robot that took it (world offset from the robot's centre).")]
        [SerializeField] private Vector3 stolenOffset = new Vector3(0f, 0.25f, 0f);

        private readonly List<Throwable> _pile = new List<Throwable>();   // index = slot; last = top
        private readonly Dictionary<GameId, Transform> _robots = new Dictionary<GameId, Transform>();

        private Throwable _held;          // in the hand, or on its way there
        private float _hopT = -1f;        // 0..1 while hopping; -1 when not
        private Vector3 _hopFrom;

        // Stones falling into their slots (added mid-run).
        private readonly List<(Throwable stone, Vector3 from, Vector3 to, float t)> _drops = new List<(Throwable, Vector3, Vector3, float)>();

        /// <summary>A stone is waiting in the hand (not still hopping). ThrowController won't aim without one.</summary>
        public bool HasStoneInHand => _held != null && _hopT < 0f;

        private void OnEnable() { if (spawner != null) spawner.Spawned += OnSpawned; }
        private void OnDisable() { if (spawner != null) spawner.Spawned -= OnSpawned; }

        // Start, not Awake: the session's bus and state are created in its Awake.
        private void Start()
        {
            session.Bus.Subscribe<StonesChanged>(OnStonesChanged);
            session.Bus.Subscribe<RobotRemoved>(OnRobotRemoved);
            for (int i = 0; i < session.State.StonesLeft; i++) AddToPile(animate: false);
        }

        private void OnDestroy()
        {
            if (session == null || session.Bus == null) return;
            session.Bus.Unsubscribe<StonesChanged>(OnStonesChanged);
            session.Bus.Unsubscribe<RobotRemoved>(OnRobotRemoved);
        }

        /// <summary>Hands the stone in the hand to ThrowController, which launches it. Null if none is ready.</summary>
        public Throwable ReleaseHeld(Transform newParent)
        {
            if (!HasStoneInHand) return null;
            var stone = _held;
            _held = null;
            stone.transform.SetParent(newParent, worldPositionStays: true);
            return stone;
        }

        private int Total => _pile.Count + (_held != null ? 1 : 0);

        private void OnStonesChanged(StonesChanged e)
        {
            switch (e.Cause)
            {
                case StoneChange.Stolen:
                    GiveTopStoneTo(e.Robot);
                    break;
                case StoneChange.Added:
                    for (int i = 0; i < e.Delta; i++) AddToPile(animate: true);
                    break;
                case StoneChange.Forfeited:
                    while (Total > 0) Destroy(TakeTop().gameObject);
                    break;
                // Thrown: ThrowController already took the stone out of the hand (ReleaseHeld).
            }
            Reconcile(e.Count);
        }

        // Whatever happened, the stones on screen must equal the count. Normally a no-op.
        private void Reconcile(int count)
        {
            while (Total > count) Destroy(TakeTop().gameObject);
            while (Total < count) AddToPile(animate: false);
        }

        private void GiveTopStoneTo(GameId robot)
        {
            if (Total == 0) return;
            var stone = TakeTop();
            if (_robots.TryGetValue(robot, out var thief) && thief != null)
            {
                // Parked (no collider, no physics): it leaves with the robot and can never start a chain.
                // worldPositionStays keeps the stone's own size — the robot is scaled 0.3.
                stone.transform.SetParent(thief, worldPositionStays: true);
                stone.transform.position = thief.position + stolenOffset;
            }
            else Destroy(stone.gameObject);
        }

        // The top of the pile; the stone in the hand only when the pile itself is empty.
        private Throwable TakeTop()
        {
            if (_pile.Count > 0)
            {
                var top = _pile[_pile.Count - 1];
                _pile.RemoveAt(_pile.Count - 1);
                _drops.RemoveAll(d => d.stone == top);
                return top;
            }
            var held = _held;
            _held = null;
            _hopT = -1f;
            return held;
        }

        private void AddToPile(bool animate)
        {
            var slot = SlotPosition(_pile.Count);
            var stone = Instantiate(stonePrefab, slot, Quaternion.identity, transform);
            stone.Park();
            _pile.Add(stone);
            if (animate)
            {
                var from = slot + Vector3.up * dropHeight;
                stone.transform.position = from;
                _drops.Add((stone, from, slot, 0f));
            }
        }

        // Pyramid: the bottom row has bottomRow stones, each row above one fewer, centred on this transform.
        private Vector3 SlotPosition(int index)
        {
            int row = 0, inRow = Mathf.Max(1, bottomRow);
            while (index >= inRow)
            {
                index -= inRow;
                row++;
                inRow = Mathf.Max(1, bottomRow - row);
            }
            float x = (index - (inRow - 1) * 0.5f) * spacing;
            return transform.position + new Vector3(x, row * rowHeight, 0f);
        }

        private void Update()
        {
            // An empty hand and the pig allowed to throw: the next stone comes up.
            if (_held == null && _pile.Count > 0 && session.State.CanThrow && hand != null)
            {
                _held = TakeTop();
                _hopFrom = _held.transform.position;
                _hopT = 0f;
            }

            for (int i = _drops.Count - 1; i >= 0; i--)
            {
                var d = _drops[i];
                if (d.stone == null) { _drops.RemoveAt(i); continue; }
                d.t = Mathf.Min(1f, d.t + Time.deltaTime / dropSeconds);
                d.stone.transform.position = Vector3.Lerp(d.from, d.to, d.t * d.t);   // accelerate like a fall
                if (d.t >= 1f) _drops.RemoveAt(i); else _drops[i] = d;
            }
        }

        // LateUpdate: runs after the Animator has posed the pig this frame, so the hand is where it's drawn.
        // The stone FOLLOWS the hand instead of becoming its child — parented to the rig it would inherit the
        // pig's scale (0.26) and every bone's scale. Following keeps its own size and still tracks the hand
        // through idle, aim and throw.
        private void LateUpdate()
        {
            if (_held == null || hand == null) return;

            if (_hopT >= 0f)
            {
                _hopT = Mathf.Min(1f, _hopT + Time.deltaTime / hopSeconds);
                // Arc: straight line to the (moving) hand plus a parabola bump at the middle.
                _held.transform.position = Vector3.Lerp(_hopFrom, hand.position, _hopT)
                                           + Vector3.up * (hopHeight * 4f * _hopT * (1f - _hopT));
                if (_hopT >= 1f) _hopT = -1f;   // arrived: from now on it just follows the hand
                return;
            }

            _held.transform.position = hand.position;
        }

        private void OnSpawned(RobotController robot) => _robots[robot.Id] = robot.transform;

        // After the referee has handled the breach (it subscribed first), so a thief is still in the map
        // when its StonesChanged arrives.
        private void OnRobotRemoved(RobotRemoved e) => _robots.Remove(e.Robot);
    }
}
