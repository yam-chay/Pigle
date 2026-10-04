using System.Collections.Generic;
using Piglings.Events;
using Piglings.Simulation;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// Makes chains readable (M10.D look, both scenes): where each robot loses grip, its points only ("+60"), coloured by
    /// its DEPTH (cream, amber, orange, red, magenta…); and when a chain closes, its total ("+620") at a fixed spot,
    /// coloured by the throw's QUALITY (its points ÷ the gap of the hour it was thrown in). No depth or multiplier tags:
    /// the colour says how deep, the scoreboard says the rest. Colours come from NightSession.ScoreColours.
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
        [Tooltip("Depth at which a robot popup gets the full shake and extra life. (Its colour is the depth's, from Score Colours.)")]
        [SerializeField, Min(1)] private int deepAtDepth = 3;


        [Header("Chain result")]
        [Tooltip("Where the chain result appears — a fixed spot (e.g. above the barn) so it's always read in the same place.")]
        [SerializeField] private Transform chainAnchor;
        [SerializeField, Min(0.01f)] private float chainScale = 2f;
        [Tooltip("A chain worth this much (or more) gets the full treatment: longest life, strongest shake.")]
        [SerializeField, Min(1)] private int bigChainTotal = 300;
        [Tooltip("Smallest chain that gets the big popup. 1-robot chains already got their \"+N\"; 0 = misses.")]
        [SerializeField, Min(0)] private int minRobotsForChainPopup = 2;

        private readonly Dictionary<GameId, Transform> _robots = new Dictionary<GameId, Transform>();
        private ScoreStyles _styles;

        private void OnEnable() => spawner.Spawned += OnSpawned;
        private void OnDisable() => spawner.Spawned -= OnSpawned;

        // Start, not Awake/OnEnable: same as ChainDebugHUD — the session's bus is created in its Awake.
        private void Start()
        {
            _styles = new ScoreStyles(session.ScoreColours);
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

            // How deep, 0..1: how much it shakes and lingers. The colour is the depth's own.
            float depthT = Mathf.Clamp01(e.Depth / (float)deepAtDepth);
            float scale = robotScale * (1f + scalePerDepth * e.Depth);
            Spawn(t.position + robotOffset, $"+{e.Total}", _styles.ForDepth(e.Depth), scale, depthT);
        }

        private void OnChainScored(ChainScored e)
        {
            if (e.RobotsDropped < minRobotsForChainPopup || chainAnchor == null) return;
            float size = Mathf.Clamp01(e.Total / (float)bigChainTotal);
            Spawn(chainAnchor.position, $"+{e.Total}", _styles.ForQuality(session.ThrowQuality(e.Total, e.Hour)), chainScale, size);
        }

        // Inspector: ⋮ (or right-click the component header) → "Preview popups", in Play mode.
        // Spawns one of every look side by side at the chain anchor — a robot popup per depth 0..4, then a chain total in
        // every quality band — so the colours, shake and life can be compared in seconds. Visuals only: nothing is scored.
        [ContextMenu("Preview popups (Play mode)")]
        private void PreviewPopups()
        {
            if (!Application.isPlaying || chainAnchor == null || _styles == null)
            {
                Debug.LogWarning("ScorePopupSpawner: Preview popups works in Play mode, with a Chain Anchor set.", this);
                return;
            }

            var origin = chainAnchor.position;
            for (int depth = 0; depth <= 4; depth++)
            {
                var pos = origin + new Vector3(-2f + depth, -1.2f, 0f);
                Spawn(pos, $"+{10 * (depth + 1)}", _styles.ForDepth(depth), robotScale * (1f + scalePerDepth * depth),
                      Mathf.Clamp01(depth / (float)deepAtDepth));
            }
            // One chain total per band, at that band's quality (hour 1's gap): cream … rainbow.
            float[] qualities = { 0.02f, 0.1f, 0.2f, 0.4f, 0.7f, 1.2f };
            int gap = session.HourGap(1);
            for (int i = 0; i < qualities.Length; i++)
            {
                int points = Mathf.Max(1, Mathf.RoundToInt(qualities[i] * gap));
                var pos = origin + new Vector3(-2.5f + i, 0f, 0f);
                Spawn(pos, $"+{points}", _styles.ForQuality(session.ThrowQuality(points, 1)), chainScale * 0.6f, qualities[i]);
            }
        }

        private void Spawn(Vector3 position, string text, PopupColor color, float scale, float intensity)
        {
            var popup = Instantiate(popupPrefab, position, Quaternion.identity, container);
            popup.Show(text, color, 0f, scale, intensity);
        }
    }
}
