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
    ///  - A special peg triggering: a small "+1 mult" at the peg. A plain hold: a small "+1" at the hold, coloured by its
    ///    hitter's depth with the quality bands' looks (ScoreStyles.ForDepthBand).
    ///  - The result fits the Chain Area box (the font shrinks; ScorePopup.FitInto).
    ///  - The chain's close: above the pig (Chain Anchor = ChainPopupAnchor), "120 ×3 ×H1 = 360" — the only place the result
    ///    shows on the board (with the camera shake / big-hit juice).
    /// Colours: the Visuals asset's quality bands (cream → amber → orange → red → magenta pulse → rainbow) by quality —
    /// the chain's worth so far (or its result) ÷ its hour's gap; so a chain's popups warm up as it grows.
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

        [Tooltip("A special peg's \"+N mult\" sits this far above its \"+score\" when it adds both.")]
        [SerializeField] private Vector3 pegMultOffset = new Vector3(0f, 0.3f, 0f);

        [Header("Plain hold \"+1\" (always shown)")]
        [Tooltip("The hold's \"+1\": small (they come in showers).")]
        [SerializeField, Min(0.01f)] private float plainHoldScale = 0.5f;

        [Header("Chain close (above the pig)")]
        [SerializeField] private bool showChainResult = true;
        [Tooltip("ChainPopupAnchor, above the pig: where the result appears, always in the same place.")]
        [SerializeField] private Transform chainAnchor;
        [SerializeField, Min(0.01f)] private float chainScale = 1.2f;
        [Tooltip("The space the result may take (world units, before Chain Scale): the font shrinks to fit it, so long results " +
                 "never break. Drawn as a box at the anchor when this object is selected. (0, 0) = no fitting.")]
        [SerializeField] private Vector2 chainArea = new Vector2(3f, 0.6f);
        [Tooltip("The smallest font the result may shrink to.")]
        [SerializeField, Min(0.1f)] private float chainMinFontSize = 2f;
        [Tooltip("Smallest chain that gets the result popup (robots dropped); 0 = misses too.")]
        [SerializeField, Min(0)] private int minRobotsForChainPopup = 1;

        private readonly Dictionary<GameId, Transform> _robots = new Dictionary<GameId, Transform>();
        private ScoreStyles _styles;

        private void OnEnable() => spawner.Spawned += OnSpawned;
        private void OnDisable() => spawner.Spawned -= OnSpawned;

        // Start, not Awake/OnEnable: the session's bus is created in its Awake.
        private void Start()
        {
            _styles = new ScoreStyles(session.Visuals);
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

        /// <summary>A score's look: its quality band from the Visuals asset (points ÷ its hour's gap).</summary>
        private PopupColor StyleFor(int points, int hour) => _styles.ForQuality(session.ThrowQuality(points, hour));

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
                string text = UiText.Fill(session.Texts.popupRobot, ("score", Numbers.Thousands(e.Score)), ("mult", Numbers.Mult(e.Mult)));
                Spawn(t.position + robotOffset, text, StyleFor(worth, e.Hour), scale, _styles.QualityIntensity(quality));
            }
            else if (e.Cause == ChainGainCause.Peg && (e.ScoreAdded > 0 || e.MultAdded > 0f))
            {
                if (board == null || e.Socket < 0 || e.Socket >= board.SocketCount) return;
                var at = board.Position(e.Socket);
                // What a special peg adds when it triggers: its score — coloured by the peg's LEVEL (Visuals ▸ Peg Level Bands),
                // since the level is what the score comes from — and, above it, its mult bonus (Mult Bands). The bus is
                // synchronous, so the socket still holds the level that scored.
                if (e.ScoreAdded > 0)
                {
                    int level = Mathf.Max(1, session.State.Sockets[e.Socket].Level);
                    Spawn(at, UiText.Fill(session.Texts.popupPegScore, ("score", Numbers.Thousands(e.ScoreAdded))),
                          _styles.ForPegLevel(level), plainHoldScale, _styles.PegLevelIntensity(level));
                }
                if (e.MultAdded > 0f)
                    Spawn(at + (e.ScoreAdded > 0 ? pegMultOffset : Vector3.zero),
                          UiText.Fill(session.Texts.popupPegMult, ("mult", Numbers.Mult(e.MultAdded))), _styles.ForMult(e.MultAdded),
                          session.Visuals.PegMultScale, _styles.MultIntensity(e.MultAdded));
            }
            else if (e.Cause == ChainGainCause.PlainPeg && e.ScoreAdded > 0)
            {
                if (board == null || e.Socket < 0 || e.Socket >= board.SocketCount) return;
                // Coloured by its hitter's depth (the stone 0, a ball its own), with the bands' looks: the deepest pulse / rainbow.
                Spawn(board.Position(e.Socket), UiText.Fill(session.Texts.popupPlainHold, ("score", Numbers.Thousands(e.ScoreAdded))),
                      _styles.ForDepthBand(e.Depth), plainHoldScale, _styles.DepthIntensity(e.Depth));
            }
            // The stone's base: no popup (the board's live row shows it).
        }

        private void OnChainScored(ChainScored e)
        {
            if (!showChainResult || e.RobotsDropped < minRobotsForChainPopup || chainAnchor == null) return;
            string sum = UiText.Fill(session.Texts.popupChain, ("score", Numbers.Thousands(e.Score)), ("mult", Numbers.Mult(e.Mult)),
                                     ("hour", e.Hour.ToString()), ("hourMult", Numbers.Mult(e.HourMultiplier)), ("total", Numbers.Thousands(e.Total)));
            // Its effect follows its colour: the quality band's rank (points ÷ its hour's gap), like every other popup.
            float intensity = _styles.QualityIntensity(session.ThrowQuality(e.Total, e.Hour));
            var popup = Spawn(chainAnchor.position, sum, StyleFor(e.Total, e.Hour), chainScale, intensity);
            popup.FitInto(chainArea, chainMinFontSize);
        }

        // Inspector: ⋮ (or right-click the component header) → "Preview popups", in Play mode: a robot popup and a chain result
        // in every quality band, side by side at the chain anchor. Visuals only.
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
                Spawn(origin + new Vector3(-2.5f + i, -1.2f, 0f), $"{points} ×2", StyleFor(points, 1), robotScale, _styles.QualityIntensity(qualities[i]));
                Spawn(origin + new Vector3(-2.5f + i, 0f, 0f), $"= {points}", StyleFor(points, 1), chainScale * 0.6f, _styles.QualityIntensity(qualities[i]));
            }
            for (int m = 1; m <= 3; m++)
                Spawn(origin + new Vector3(3.5f, -1.2f + (m - 1) * 0.5f, 0f), $"+{m} mult", _styles.ForMult(m), session.Visuals.PegMultScale, _styles.MultIntensity(m));
        }

        private ScorePopup Spawn(Vector3 position, string text, PopupColor color, float scale, float intensity)
        {
            var popup = Instantiate(popupPrefab, position, Quaternion.identity, container);
            popup.Show(text, color, 0f, scale, intensity);
            return popup;
        }

        // The chain result's space, so it can be sized in the Scene view (select this object).
        private void OnDrawGizmosSelected()
        {
            if (chainAnchor == null || chainArea.x <= 0f || chainArea.y <= 0f) return;
            Gizmos.color = new Color(1f, 0.8f, 0.2f);
            Gizmos.DrawWireCube(chainAnchor.position, new Vector3(chainArea.x * chainScale, chainArea.y * chainScale, 0f));
        }
    }
}
