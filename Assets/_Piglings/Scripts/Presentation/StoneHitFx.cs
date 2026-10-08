using System.Collections.Generic;
using Piglings.Definitions;
using Piglings.Events;
using Piglings.Simulation;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// Weapon mastery, felt on every hit: where the stone itself knocks a robot loose (RobotLostGrip from the throwable —
    /// exactly what MasteryTally counts), a star flashes and a small sparkle rises out of it. Ball knocks get nothing:
    /// they don't count for the stone, so they don't sparkle.
    ///
    /// The event has a GameId, not a position (Rules don't know where things are), so — like ScorePopupSpawner — this
    /// keeps a GameId → Transform map from RobotSpawner.Spawned. The bus is synchronous, so the robot is still where it
    /// was hit. Missing sprites = no effect. Visual only.
    /// </summary>
    public sealed class StoneHitFx : MonoBehaviour
    {
        [SerializeField] private NightSession session;
        [SerializeField] private RobotSpawner spawner;

        [Header("Art (white, tinted here)")]
        [SerializeField] private Sprite hitStar;     // fx_hit_star
        [SerializeField] private Sprite sparkle;     // fx_sparkle

        [Header("Star")]
        [SerializeField] private Color starColour = new Color(1f, 0.95f, 0.7f, 1f);
        [SerializeField] private FxMotion starMotion = new FxMotion { seconds = 0.25f, startScale = 0.6f, endScale = 1.1f, spin = 90f };

        [Header("Sparkle")]
        [SerializeField] private Color sparkleColour = new Color(1f, 0.85f, 0.35f, 1f);
        [SerializeField] private FxMotion sparkleMotion = new FxMotion { seconds = 0.6f, startScale = 0.5f, endScale = 0.3f, rise = 0.45f, spin = 180f };

        [Header("Drawing")]
        [Tooltip("Over the robots: the PopUp layer, like the score popups.")]
        [SerializeField] private string sortingLayer = "PopUp";
        [SerializeField] private int sortingOrder = -1;   // just under the score popups, which say more

        private readonly Dictionary<GameId, Transform> _robots = new Dictionary<GameId, Transform>();
        private FxSprites _fx;
        private int _layerId;

        private void Awake()
        {
            _fx = new FxSprites(transform);
            _layerId = SortingLayer.NameToID(sortingLayer);
        }

        private void OnEnable() { if (spawner != null) spawner.Spawned += OnSpawned; }
        private void OnDisable() { if (spawner != null) spawner.Spawned -= OnSpawned; }

        // Start, not Awake: the session's bus is created in its Awake.
        private void Start()
        {
            session.Bus.Subscribe<RobotLostGrip>(OnLostGrip);
            session.Bus.Subscribe<RobotRemoved>(OnRobotRemoved);
        }

        private void OnDestroy()
        {
            if (session == null || session.Bus == null) return;
            session.Bus.Unsubscribe<RobotLostGrip>(OnLostGrip);
            session.Bus.Unsubscribe<RobotRemoved>(OnRobotRemoved);
        }

        private void OnSpawned(RobotController robot) => _robots[robot.Id] = robot.transform;
        private void OnRobotRemoved(RobotRemoved e) => _robots.Remove(e.Robot);

        private void OnLostGrip(RobotLostGrip e)
        {
            if (e.Cause.Kind != CauseKind.Throwable || session.State.Ended) return;
            if (!_robots.TryGetValue(e.Robot, out var robot) || robot == null) return;
            var at = robot.position;
            _fx.Spawn(hitStar, at, starColour, starMotion, _layerId, sortingOrder);
            _fx.Spawn(sparkle, at, sparkleColour, sparkleMotion, _layerId, sortingOrder + 1);
        }

        private void Update() => _fx.Update(Time.deltaTime);
    }
}
