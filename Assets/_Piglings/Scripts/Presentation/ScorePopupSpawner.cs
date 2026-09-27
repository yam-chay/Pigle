using System.Collections.Generic;
using Piglings.Events;
using Piglings.Simulation;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// Makes chains readable: a small "+N" where each robot loses grip (RobotScored), and a big
    /// "robots · depth / +total ×mult" when a chain closes (ChainScored).
    ///
    /// Only listens and spawns visuals — never touches game state. The numbers come from the Rules
    /// events, so the popups always show exactly what was added to the score.
    ///
    /// RobotScored carries a GameId, not a position (Rules don't know where things are), so this
    /// keeps its own GameId → Transform map from RobotSpawner.Spawned, and forgets robots on
    /// RobotRemoved.
    /// </summary>
    public sealed class ScorePopupSpawner : MonoBehaviour
    {
        [SerializeField] private NightSession session;
        [SerializeField] private RobotSpawner spawner;
        [SerializeField] private ScorePopup popupPrefab;
        [SerializeField] private Transform container;

        [Header("Robot \"+N\"")]
        [SerializeField] private Vector3 robotOffset = new Vector3(0f, 0.25f, 0f);
        [SerializeField, Min(0.01f)] private float robotScale = 1f;
        [Tooltip("Each depth step makes the popup this much bigger (0.2 = +20%), so deeper hits read as bigger.")]
        [SerializeField, Min(0f)] private float scalePerDepth = 0.2f;
        [SerializeField] private Color shallowColor = Color.white;
        [SerializeField] private Color deepColor = new Color(1f, 0.75f, 0.2f);
        [Tooltip("Depth at which the colour is fully 'deep'.")]
        [SerializeField, Min(1)] private int deepAtDepth = 3;

        [Header("Chain result")]
        [Tooltip("Where the chain result appears — a fixed spot (e.g. above the barn) so it's always read in the same place.")]
        [SerializeField] private Transform chainAnchor;
        [SerializeField, Min(0.01f)] private float chainScale = 2f;
        [SerializeField] private Color chainColor = new Color(1f, 0.85f, 0.3f);
        [Tooltip("Smallest chain that gets the big popup. 1-robot chains already got their \"+N\"; 0 = misses.")]
        [SerializeField, Min(0)] private int minRobotsForChainPopup = 2;

        private readonly Dictionary<GameId, Transform> _robots = new Dictionary<GameId, Transform>();

        private void OnEnable() => spawner.Spawned += OnSpawned;
        private void OnDisable() => spawner.Spawned -= OnSpawned;

        // Start, not Awake/OnEnable: same as ChainDebugHUD — the session's bus is created in its Awake.
        private void Start()
        {
            session.Bus.Subscribe<RobotScored>(OnRobotScored);
            session.Bus.Subscribe<ChainScored>(OnChainScored);
            session.Bus.Subscribe<RobotRemoved>(OnRobotRemoved);
        }

        private void OnDestroy()
        {
            if (session == null || session.Bus == null) return;
            session.Bus.Unsubscribe<RobotScored>(OnRobotScored);
            session.Bus.Unsubscribe<ChainScored>(OnChainScored);
            session.Bus.Unsubscribe<RobotRemoved>(OnRobotRemoved);
        }

        private void OnSpawned(RobotController robot) => _robots[robot.Id] = robot.transform;
        private void OnRobotRemoved(RobotRemoved e) => _robots.Remove(e.Robot);

        private void OnRobotScored(RobotScored e)
        {
            // The bus is synchronous: this runs inside RobotController.LoseGrip, so the robot is
            // still exactly where it was hit.
            if (!_robots.TryGetValue(e.Robot, out var t) || t == null) return;

            float depthT = Mathf.Clamp01(e.Depth / (float)deepAtDepth);
            Color color = Color.Lerp(shallowColor, deepColor, depthT);
            float scale = robotScale * (1f + scalePerDepth * e.Depth);
            Spawn(t.position + robotOffset, $"+{e.Points}", color, scale);
        }

        private void OnChainScored(ChainScored e)
        {
            if (e.RobotsDropped < minRobotsForChainPopup || chainAnchor == null) return;

            string robots = e.RobotsDropped == 1 ? "1 robot" : $"{e.RobotsDropped} robots";
            string mult = e.Multiplier > 1f ? $"  ×{e.Multiplier:0.##}" : "";
            Spawn(chainAnchor.position, $"{robots} · depth {e.MaxDepth}\n+{e.Total}{mult}", chainColor, chainScale);
        }

        private void Spawn(Vector3 position, string text, Color color, float scale)
        {
            var popup = Instantiate(popupPrefab, position, Quaternion.identity, container);
            popup.Show(text, color, scale);
        }
    }
}
