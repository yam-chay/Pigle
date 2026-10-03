using System.Collections.Generic;
using Piglings.Events;
using UnityEngine;

namespace Piglings.Simulation
{
    /// <summary>
    /// The pig's ammo as real stones on the perch: what you see is exactly what you have.
    ///
    /// It MIRRORS the count, it never decides it. NightState.StonesLeft (written only by NightReferee) is the
    /// one truth; this listens to StonesChanged — Thrown, Stolen, Added — and makes the stones
    /// match. So theft, refills and "empty" all come from the Rules, and CoreCheck can test them.
    ///
    /// - Stones sit in fixed pyramid slots on the rag, no physics, no collider (Throwable.Park).
    ///   Top stone = the last slot filled.
    /// - When the hand is empty and the pig may throw, the top stone hops in an arc to the hand and waits.
    ///   ThrowController takes it on release (ReleaseHeld) and it becomes a normal dynamic Throwable.
    ///   The hop is shorter than the throw cooldown, so the next stone is in the hand when you can throw.
    /// - A robot that breaches takes the top stone: the stone hops from the pile to the robot (the same arc as the
    ///   hop to the hand), timed to land at BreachTiming.StoneLands of the robot's BreachSeconds, so the robot
    ///   always visibly holds it before it jumps off. Then it's parented to the robot, stays parked (never a
    ///   weapon) and leaves with it.
    /// - Added stones drop onto the next free slots.
    /// - Mastery level: every stone looks and is sized for the night's level (NightSession.WeaponLevel), and the slots
    ///   spread by that level's Radius Scale so bigger stones don't overlap. When the night ends (banked), the pile
    ///   switches to the level the next night will play at — a level earned tonight shows here, never mid-night.
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
        [Tooltip("Where a stolen stone sits on the robot that took it (world offset from the robot's centre). " +
                 "Its hop there is timed from the robot's Breach Seconds, and arcs as high as Hop Height.")]
        [SerializeField] private Vector3 stolenOffset = new Vector3(0f, 0.25f, 0f);

        // The shared pile pieces (ItemPile, HopMover, PileLayout); the peg shelf is built from the same ones.
        private ItemPile<Throwable> _pile;
        private HopMover _hops;
        private readonly Dictionary<GameId, RobotController> _robots = new Dictionary<GameId, RobotController>();

        private Throwable _held;          // in the hand, or hopping there
        private int _shownLevel = 1;      // the mastery level the stones look like (the night's; the next night's once banked)

        /// <summary>The stones sitting on the pile (bottom row first), for views (glints, the gold pulse). Read only.</summary>
        public IReadOnlyList<Throwable> Stones => _pile.Items;

        /// <summary>The stone in the hand or hopping there; null when none.</summary>
        public Throwable Held => _held;

        /// <summary>A stone is waiting in the hand (not still hopping). ThrowController won't aim without one.</summary>
        public bool HasStoneInHand => _held != null && !_hops.IsHopping(_held.transform);

        private void Awake()
        {
            _pile = new ItemPile<Throwable>(CreateStone, s => Destroy(s.gameObject), SlotPosition);
            // A stone whose target is gone mid-hop (a thief destroyed early) just goes.
            _hops = new HopMover(t => Destroy(t.gameObject));
            ApplyTuning();
        }

        private void OnEnable() { if (spawner != null) spawner.Spawned += OnSpawned; }
        private void OnDisable() { if (spawner != null) spawner.Spawned -= OnSpawned; }

        // Start, not Awake: the session's bus and state are created in its Awake.
        private void Start()
        {
            session.Bus.Subscribe<StonesChanged>(OnStonesChanged);
            session.Bus.Subscribe<RobotRemoved>(OnRobotRemoved);
            session.Bus.Subscribe<NightEnded>(OnNightEnded);
            _shownLevel = session.WeaponLevel;
            for (int i = 0; i < session.State.StonesLeft; i++) _pile.Add(animate: false);
        }

        private void OnDestroy()
        {
            if (session == null || session.Bus == null) return;
            session.Bus.Unsubscribe<StonesChanged>(OnStonesChanged);
            session.Bus.Unsubscribe<RobotRemoved>(OnRobotRemoved);
            session.Bus.Unsubscribe<NightEnded>(OnNightEnded);
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
                    for (int i = 0; i < e.Delta; i++) _pile.Add(animate: true);
                    break;
                // Thrown: ThrowController already took the stone out of the hand (ReleaseHeld).
            }
            Reconcile(e.Count);
        }

        // Whatever happened, the stones on screen must equal the count. Normally a no-op. Not ItemPile.Reconcile:
        // the stone in the hand counts too, and goes last (only once the pile itself is empty).
        private void Reconcile(int count)
        {
            while (Total > count) Destroy(TakeTop().gameObject);
            while (Total < count) _pile.Add(animate: false);
        }

        private void GiveTopStoneTo(GameId robot)
        {
            if (Total == 0) return;
            var stone = TakeTop();
            // Parked (no collider, no physics) the whole way: it can never start a chain. It lands at
            // BreachTiming.StoneLands of the thief's breach, then rides on it: parented, so it follows the jump off.
            // worldPositionStays keeps the stone's own size — the robot is scaled 0.3.
            if (_robots.TryGetValue(robot, out var thief) && thief != null)
                _hops.Start(stone.transform, thief.transform, stolenOffset, BreachTiming.StoneHopSeconds(thief.BreachSeconds),
                            landed => landed.SetParent(thief.transform, worldPositionStays: true));
            else Destroy(stone.gameObject);
        }

        // The top of the pile; the stone in the hand only when the pile itself is empty.
        private Throwable TakeTop()
        {
            if (_pile.Count > 0) return _pile.TakeTop();
            var held = _held;
            _held = null;
            _hops.Cancel(held.transform);
            return held;
        }

        private Throwable CreateStone()
        {
            var stone = Instantiate(stonePrefab, transform.position, Quaternion.identity, transform);
            stone.Park();
            stone.ApplyLevel(session.Night.Throwable, _shownLevel);
            return stone;
        }

        // The night is banked: if it earned a level, the stones take on the next night's look now (the reward moment).
        // Only the look — no stone can be thrown after the night ends; the next night's NightSession fixes its own level.
        private void OnNightEnded(NightEnded e)
        {
            int level = session.BankedWeaponLevel;
            if (level == _shownLevel) return;
            _shownLevel = level;
            var def = session.Night.Throwable;
            foreach (var stone in _pile.Items) if (stone != null) stone.ApplyLevel(def, level);
            if (_held != null) _held.ApplyLevel(def, level);
            _pile.Relayout();   // the slots spread with the new size
        }

        // Pyramid: the bottom row has bottomRow stones, each row above one fewer, centred on this transform.
        // Spacing and row height are for level-1 stones; they grow with the shown level's size so stones never overlap.
        private Vector3 SlotPosition(int index)
        {
            var def = session.Night.Throwable;
            float grow = def.RadiusAt(_shownLevel) / def.Radius;
            PileLayout.Pyramid(index, bottomRow, spacing * grow, rowHeight * grow, out float x, out float y);
            return transform.position + new Vector3(x, y, 0f);
        }

        // Read every frame, so the Inspector values can be tuned in play.
        private void ApplyTuning()
        {
            _pile.DropHeight = dropHeight;
            _pile.DropSeconds = dropSeconds;
            _hops.Height = hopHeight;
        }

        private void Update()
        {
            ApplyTuning();

            // An empty hand and the pig allowed to throw: the next stone comes up.
            if (_held == null && _pile.Count > 0 && session.State.CanThrow && hand != null)
            {
                _held = _pile.TakeTop();
                _hops.Start(_held.transform, hand, Vector3.zero, hopSeconds);
            }
            // A placement round needs the hand for pegs: the stone hops back onto the pile, and comes up again after.
            else if (_held != null && session.State.Phase == NightPhase.PegPlacement)
            {
                var stone = _held;
                _held = null;
                _hops.StartTo(stone.transform, _pile.PutBack(stone), hopSeconds);
            }

            _pile.UpdateDrops(Time.deltaTime);
        }

        // LateUpdate: runs after the Animator has posed the pig this frame, so the hand is where it's drawn.
        // The stone FOLLOWS the hand instead of becoming its child — parented to the rig it would inherit the
        // pig's scale (0.26) and every bone's scale. Following keeps its own size and still tracks the hand
        // through idle, aim and throw.
        private void LateUpdate()
        {
            _hops.Update(Time.deltaTime);
            if (HasStoneInHand && hand != null) _held.transform.position = hand.position;
        }

        private void OnSpawned(RobotController robot) => _robots[robot.Id] = robot;

        // The theft happens at RobotBreached, while the thief is still alive (and in the map). It's removed at the
        // end of its breach sequence, taking the parented stone with it; a stone still mid-hop goes with it too.
        private void OnRobotRemoved(RobotRemoved e)
        {
            if (_robots.TryGetValue(e.Robot, out var robot) && robot != null) _hops.LoseTarget(robot.transform);
            _robots.Remove(e.Robot);
        }
    }
}
