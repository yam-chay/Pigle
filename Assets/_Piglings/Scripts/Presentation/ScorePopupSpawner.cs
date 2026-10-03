using System.Collections.Generic;
using Piglings.Events;
using Piglings.Simulation;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// Makes chains readable: where each robot loses grip, "received ×mult" resolving into "+total"
    /// (RobotScored); and a big "robots · depth / +total" when a chain closes (ChainScored).
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
        [Tooltip("Robot popups. Solid mode picks the colour by depth: left of the gradient = hit by the stone, " +
                 "right = 'Deep At Depth' or deeper.")]
        [SerializeField] private PopupColor robotColor = new PopupColor
            { mode = PopupColorMode.Solid, gradient = PopupColor.TwoColor(Color.white, new Color(1f, 0.75f, 0.2f)) };
        [Tooltip("Depth at which a robot popup is fully 'deep': right end of the gradient, full shake and extra life.")]
        [SerializeField, Min(1)] private int deepAtDepth = 3;


        [Header("Chain result")]
        [Tooltip("Where the chain result appears — a fixed spot (e.g. above the barn) so it's always read in the same place.")]
        [SerializeField] private Transform chainAnchor;
        [SerializeField, Min(0.01f)] private float chainScale = 2f;
        [SerializeField] private PopupColor chainColor = new PopupColor
            { mode = PopupColorMode.Solid, gradient = PopupColor.Flat(new Color(1f, 0.85f, 0.3f)) };
        [Tooltip("A chain worth this much (or more) gets the full treatment: longest life, strongest shake.")]
        [SerializeField, Min(1)] private int bigChainTotal = 300;
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

            // How deep, 0..1: picks the colour (Solid mode) and how much it shakes and lingers.
            float depthT = Mathf.Clamp01(e.Depth / (float)deepAtDepth);
            float scale = robotScale * (1f + scalePerDepth * e.Depth);
            string total = $"+{e.Total}";

            // Show the sum only when something multiplies: a plain ×1 robot in hour 1 goes straight to "+10".
            // The hour's multiplier gets its own tag ("H2×1.5"), so the player sees which part the hour added.
            // (Each hour's own colours come with the hour palettes.)
            bool hourBonus = e.HourMultiplier > 1f;
            string why = e.Multiplier > 1f ? $"{e.Received} ×{e.Multiplier:0.##}" : $"{e.Received}";
            if (hourBonus) why += $"  H{e.Hour}×{e.HourMultiplier:0.##}";

            if (e.Multiplier > 1f || hourBonus) Spawn(t.position + robotOffset, why, robotColor, depthT, scale, depthT, total);
            else Spawn(t.position + robotOffset, total, robotColor, depthT, scale, depthT);
        }

        private void OnChainScored(ChainScored e)
        {
            if (e.RobotsDropped < minRobotsForChainPopup || chainAnchor == null) return;

            string robots = e.RobotsDropped == 1 ? "1 robot" : $"{e.RobotsDropped} robots";
            float size = Mathf.Clamp01(e.Total / (float)bigChainTotal);
            Spawn(chainAnchor.position, $"{robots} · depth {e.MaxDepth}\n+{e.Total}", chainColor, 0f, chainScale, size);
        }

        // Inspector: ⋮ (or right-click the component header) → "Preview popups", in Play mode.
        // Spawns one of every kind side by side at the chain anchor — depth 0..3, an hour bonus, chain result —
        // so colour modes, shake and life can be compared in seconds instead of playing into a later hour.
        // Visuals only: nothing is scored.
        [ContextMenu("Preview popups (Play mode)")]
        private void PreviewPopups()
        {
            if (!Application.isPlaying || chainAnchor == null)
            {
                Debug.LogWarning("ScorePopupSpawner: Preview popups works in Play mode, with a Chain Anchor set.", this);
                return;
            }

            var origin = chainAnchor.position;
            for (int depth = 0; depth <= 3; depth++)
            {
                float depthT = Mathf.Clamp01(depth / (float)deepAtDepth);
                float mult = 1f + 0.5f * depth;
                var pos = origin + new Vector3(-1.5f + depth, -1.2f, 0f);
                Spawn(pos, $"{10 * (depth + 1)} ×{mult:0.##}", robotColor, depthT, robotScale * (1f + scalePerDepth * depth), depthT,
                      $"+{Mathf.RoundToInt(10 * (depth + 1) * mult)}");
            }
            Spawn(origin + new Vector3(0f, -2.2f, 0f), "20 ×1.5  H3×2", robotColor, 0.33f, robotScale * 1.2f, 0.33f, "+60");
            Spawn(origin, "5 robots · depth 3\n+300", chainColor, 0f, chainScale, 1f);
        }

        private void Spawn(Vector3 position, string text, PopupColor color, float colorKey, float scale, float intensity,
                           string resolvedText = null)
        {
            var popup = Instantiate(popupPrefab, position, Quaternion.identity, container);
            popup.Show(text, color, colorKey, scale, intensity, resolvedText);
        }
    }
}
