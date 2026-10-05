using System.Collections.Generic;
using Piglings.Events;
using Piglings.Simulation;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// Makes chains readable on the board (M10.S, Score × Mult):
    ///  - A robot knocked loose: ONE number at the robot — the chain so far, "789 ×5" (its SCORE × its MULT at that moment; no
    ///    hour, no result). The numbers grow along the chain.
    ///  - A special peg triggering: a small "+1 mult" at the peg. Plain holds and the stone's base: no popup.
    ///  - The chain's close: above the pig (Chain Anchor = ChainPopupAnchor), "9 wolves · depth 2" over
    ///    "120 ×3 ×H1 = 360" — the only place the result shows on the board (with the camera shake / big-hit juice).
    /// Colours: the HOUR PALETTE — the colour of the hour the chain was thrown in — with the quality styles on top for big
    /// throws (pulse, rainbow; ScoreStyles.ForHour). The quality is the chain's worth so far ÷ its hour's gap.
    ///
    /// Only listens and spawns visuals — never touches game state. ChainGained carries a GameId / socket, not a position
    /// (Rules don't know where things are): robots come from RobotSpawner.Spawned (forgotten on RobotRemoved), pegs from
    /// the PegBoard.
    /// </summary>
    public sealed class ScorePopupSpawner : MonoBehaviour
    {
        [SerializeField] private NightSession session;
        [SerializeField] private RobotSpawner spawner;
        [Tooltip("For the special pegs' \"+1 mult\" (the peg's position). Empty = those popups are skipped.")]
        [SerializeField] private PegBoard board;
        [SerializeField] private ScorePopup popupPrefab;
        [SerializeField] private Transform container;

        [Header("Robot \"789 ×5\"")]
        [SerializeField] private Vector3 robotOffset = new Vector3(0f, 0.25f, 0f);
        [SerializeField, Min(0.01f)] private float robotScale = 1f;
        [Tooltip("Each depth step makes the popup this much bigger (0.2 = +20%), so deeper hits read as bigger.")]
        [SerializeField, Min(0f)] private float scalePerDepth = 0.15f;

        [Header("Special peg \"+1 mult\"")]
        [SerializeField] private Color multColour = new Color(1f, 0.35f, 0.75f);
        [SerializeField, Min(0.01f)] private float multScale = 0.7f;

        [Header("Chain close (above the pig)")]
        [SerializeField] private bool showChainResult = true;
        [Tooltip("ChainPopupAnchor, above the pig: where the result appears, always in the same place.")]
        [SerializeField] private Transform chainAnchor;
        [SerializeField, Min(0.01f)] private float chainScale = 2f;
        [Tooltip("A chain worth this much (or more) gets the full treatment: longest life, strongest shake.")]
        [SerializeField, Min(1)] private int bigChainTotal = 300;
        [Tooltip("Smallest chain that gets the result popup (robots dropped); 0 = misses too.")]
        [SerializeField, Min(0)] private int minRobotsForChainPopup = 1;

        private readonly Dictionary<GameId, Transform> _robots = new Dictionary<GameId, Transform>();
        private ScoreStyles _styles;
        private PopupColor _mult;

        private void OnEnable() => spawner.Spawned += OnSpawned;
        private void OnDisable() => spawner.Spawned -= OnSpawned;

        // Start, not Awake/OnEnable: the session's bus is created in its Awake.
        private void Start()
        {
            _styles = new ScoreStyles(session.ScoreColours);
            _mult = new PopupColor { mode = PopupColorMode.Solid, gradient = PopupColor.Flat(multColour) };
            session.Bus.Subscribe<ChainGained>(OnGained);
            session.Bus.Subscribe<ChainScored>(OnChainScored);
            session.Bus.Subscribe<RobotRemoved>(OnRobotRemoved);
        }

        private void OnDestroy()
        {
            if (session == null || session.Bus == null) return;
            session.Bus.Unsubscribe<ChainGained>(OnGained);
            session.Bus.Unsubscribe<ChainScored>(OnChainScored);
            session.Bus.Unsubscribe<RobotRemoved>(OnRobotRemoved);
        }

        private void OnSpawned(RobotController robot) => _robots[robot.Id] = robot.transform;
        private void OnRobotRemoved(RobotRemoved e) => _robots.Remove(e.Robot);

        /// <summary>A score's look: the hour's palette colour, with the quality style for its size.</summary>
        private PopupColor StyleFor(int points, int hour) => _styles.ForHour(session.HourColour(hour), session.ThrowQuality(points, hour));

        // The bus is synchronous: this runs inside the hit, so the robot / peg is still exactly where it happened.
        private void OnGained(ChainGained e)
        {
            if (e.Cause == ChainGainCause.Wolf)
            {
                if (!_robots.TryGetValue(e.Source, out var t) || t == null) return;
                // The chain's worth so far decides the look (what it would score if it closed now).
                int worth = Mathf.RoundToInt(e.Score * e.Mult * e.HourMultiplier);
                float quality = session.ThrowQuality(worth, e.Hour);
                float scale = robotScale * (1f + scalePerDepth * Mathf.Max(0f, e.Mult - 1f));
                Spawn(t.position + robotOffset, $"{Numbers.Thousands(e.Score)} ×{e.Mult:0.##}", StyleFor(worth, e.Hour), scale, Mathf.Clamp01(quality));
            }
            else if (e.Cause == ChainGainCause.Peg && e.MultAdded > 0f)
            {
                if (board == null || e.Socket < 0 || e.Socket >= board.SocketCount) return;
                Spawn(board.Position(e.Socket), $"+{e.MultAdded:0.##} mult", _mult, multScale, 0.4f);
            }
            // Plain holds and the stone's base: no popup (the board's live row shows them).
        }

        private void OnChainScored(ChainScored e)
        {
            if (!showChainResult || e.RobotsDropped < minRobotsForChainPopup || chainAnchor == null) return;
            string wolves = e.RobotsDropped == 1 ? "1 wolf" : $"{e.RobotsDropped} wolves";
            string head = e.MaxDepth >= 1 ? $"{wolves} · depth {e.MaxDepth}" : wolves;
            string sum = $"{Numbers.Thousands(e.Score)} ×{e.Mult:0.##} ×H{e.Hour} = {Numbers.Thousands(e.Total)}";
            float size = Mathf.Clamp01(e.Total / (float)bigChainTotal);
            Spawn(chainAnchor.position, $"{head}\n{sum}", StyleFor(e.Total, e.Hour), chainScale, size);
        }

        // Inspector: ⋮ (or right-click the component header) → "Preview popups", in Play mode: a robot popup and a chain result
        // in every quality band, in hour 1's colour, side by side at the chain anchor. Visuals only.
        [ContextMenu("Preview popups (Play mode)")]
        private void PreviewPopups()
        {
            if (!Application.isPlaying || chainAnchor == null || _styles == null)
            {
                Debug.LogWarning("ScorePopupSpawner: Preview popups works in Play mode, with a Chain Anchor set.", this);
                return;
            }
            var origin = chainAnchor.position;
            float[] qualities = { 0.02f, 0.1f, 0.2f, 0.4f, 0.7f, 1.2f };
            int gap = session.HourGap(1);
            for (int i = 0; i < qualities.Length; i++)
            {
                int points = Mathf.Max(1, Mathf.RoundToInt(qualities[i] * gap));
                Spawn(origin + new Vector3(-2.5f + i, -1.2f, 0f), $"{points} ×2", StyleFor(points, 1), robotScale, qualities[i]);
                Spawn(origin + new Vector3(-2.5f + i, 0f, 0f), $"= {points}", StyleFor(points, 1), chainScale * 0.6f, qualities[i]);
            }
            Spawn(origin + new Vector3(3.5f, -1.2f, 0f), "+1 mult", _mult, multScale, 0.4f);
        }

        private void Spawn(Vector3 position, string text, PopupColor color, float scale, float intensity)
        {
            var popup = Instantiate(popupPrefab, position, Quaternion.identity, container);
            popup.Show(text, color, 0f, scale, intensity);
        }
    }
}
