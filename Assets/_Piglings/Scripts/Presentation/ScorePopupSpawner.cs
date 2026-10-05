using System.Collections.Generic;
using Piglings.Events;
using Piglings.Simulation;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// Makes chains readable on the board (M10.S, Score × Mult): each gain shows where it happened, as its two parts —
    /// "+10" for SCORE (cream → amber as the gain grows) and "+1 mult" for MULT (its own colour). A wolf knocked loose: its
    /// score at the robot (and "+1 mult" too when it took the chain to a new depth); a plain hold touched: "+1" at the hold;
    /// a special peg triggering: "+1 mult" at the peg. No per-robot points popup any more — the live chain counter shows the
    /// running SCORE × MULT and the result. The chain's total at a fixed spot is optional (off by default).
    ///
    /// Only listens and spawns visuals — never touches game state. ChainGained carries a GameId / socket, not a position
    /// (Rules don't know where things are): robots come from RobotSpawner.Spawned (forgotten on RobotRemoved), holds from
    /// the PegBoard.
    /// </summary>
    public sealed class ScorePopupSpawner : MonoBehaviour
    {
        [SerializeField] private NightSession session;
        [SerializeField] private RobotSpawner spawner;
        [Tooltip("For plain-hold and peg popups (the hold's position). Empty = those popups are skipped.")]
        [SerializeField] private PegBoard board;
        [SerializeField] private ScorePopup popupPrefab;
        [SerializeField] private Transform container;

        [Header("SCORE \"+10\"")]
        [SerializeField] private Vector3 robotOffset = new Vector3(0f, 0.25f, 0f);
        [SerializeField, Min(0.01f)] private float scoreScale = 1f;
        [Tooltip("A small gain's colour (cream) …")]
        [SerializeField] private Color scoreSmallColour = new Color(1f, 0.95f, 0.82f);
        [Tooltip("… and a big one's (amber): the colour slides between them by the gain's size.")]
        [SerializeField] private Color scoreBigColour = new Color(1f, 0.72f, 0.2f);
        [Tooltip("A gain this big (or more) gets the big colour and the full shake / life.")]
        [SerializeField, Min(1)] private int bigScoreGain = 40;
        [Tooltip("A plain hold's \"+1\": this much of the score popup's size (small, they come in showers).")]
        [SerializeField, Range(0.1f, 1f)] private float plainHoldScale = 0.55f;

        [Header("MULT \"+1 mult\"")]
        [SerializeField] private Color multColour = new Color(1f, 0.35f, 0.75f);
        [SerializeField, Min(0.01f)] private float multScale = 1.1f;
        [Tooltip("Where the mult popup sits relative to the score popup (both at once when a wolf reaches a new depth).")]
        [SerializeField] private Vector3 multOffset = new Vector3(0f, 0.3f, 0f);

        [Header("Chain total (optional — the live counter shows it)")]
        [SerializeField] private bool showChainTotal = false;
        [Tooltip("Where the chain result appears — a fixed spot (e.g. above the barn).")]
        [SerializeField] private Transform chainAnchor;
        [SerializeField, Min(0.01f)] private float chainScale = 2f;
        [Tooltip("A chain worth this much (or more) gets the full treatment: longest life, strongest shake.")]
        [SerializeField, Min(1)] private int bigChainTotal = 300;
        [Tooltip("Smallest chain that gets the big popup; 0 = misses too.")]
        [SerializeField, Min(0)] private int minRobotsForChainPopup = 2;

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

        // The bus is synchronous: this runs inside the hit, so the robot / hold is still exactly where it happened.
        private void OnGained(ChainGained e)
        {
            Vector3 at;
            float scale = scoreScale;
            switch (e.Cause)
            {
                case ChainGainCause.Wolf:
                    if (!_robots.TryGetValue(e.Source, out var t) || t == null) return;
                    at = t.position + robotOffset;
                    break;
                case ChainGainCause.PlainPeg:
                case ChainGainCause.Peg:
                    if (board == null || e.Socket < 0 || e.Socket >= board.SocketCount) return;
                    at = board.Position(e.Socket);
                    if (e.Cause == ChainGainCause.PlainPeg) scale *= plainHoldScale;
                    break;
                default:
                    return;   // the stone's base: no place on the board for it — the counter shows it
            }

            if (e.ScoreAdded > 0)
            {
                float size = Mathf.Clamp01(e.ScoreAdded / (float)bigScoreGain);
                var colour = new PopupColor { mode = PopupColorMode.Solid, gradient = PopupColor.Flat(Color.Lerp(scoreSmallColour, scoreBigColour, size)) };
                Spawn(at, $"+{e.ScoreAdded}", colour, scale, size);
            }
            if (e.MultAdded > 0f)
                Spawn(at + (e.ScoreAdded > 0 ? multOffset : Vector3.zero), $"+{e.MultAdded:0.##} mult", _mult, multScale, 0.6f);
        }

        private void OnChainScored(ChainScored e)
        {
            if (!showChainTotal || e.RobotsDropped < minRobotsForChainPopup || chainAnchor == null) return;
            float size = Mathf.Clamp01(e.Total / (float)bigChainTotal);
            Spawn(chainAnchor.position, $"+{e.Total}", _styles.ForQuality(session.ThrowQuality(e.Total, e.Hour)), chainScale, size);
        }

        // Inspector: ⋮ (or right-click the component header) → "Preview popups", in Play mode. A score popup at a few sizes,
        // a mult popup, and a chain total in every quality band, side by side at the chain anchor. Visuals only.
        [ContextMenu("Preview popups (Play mode)")]
        private void PreviewPopups()
        {
            if (!Application.isPlaying || chainAnchor == null || _styles == null)
            {
                Debug.LogWarning("ScorePopupSpawner: Preview popups works in Play mode, with a Chain Anchor set.", this);
                return;
            }

            var origin = chainAnchor.position;
            int[] gains = { 1, 10, 20, bigScoreGain };
            for (int i = 0; i < gains.Length; i++)
            {
                float size = Mathf.Clamp01(gains[i] / (float)bigScoreGain);
                var colour = new PopupColor { mode = PopupColorMode.Solid, gradient = PopupColor.Flat(Color.Lerp(scoreSmallColour, scoreBigColour, size)) };
                Spawn(origin + new Vector3(-2f + i, -1.2f, 0f), $"+{gains[i]}", colour, scoreScale, size);
            }
            Spawn(origin + new Vector3(2f, -1.2f, 0f), "+1 mult", _mult, multScale, 0.6f);
            float[] qualities = { 0.02f, 0.1f, 0.2f, 0.4f, 0.7f, 1.2f };
            int gap = session.HourGap(1);
            for (int i = 0; i < qualities.Length; i++)
            {
                int points = Mathf.Max(1, Mathf.RoundToInt(qualities[i] * gap));
                Spawn(origin + new Vector3(-2.5f + i, 0f, 0f), $"+{points}", _styles.ForQuality(session.ThrowQuality(points, 1)), chainScale * 0.6f, qualities[i]);
            }
        }

        private void Spawn(Vector3 position, string text, PopupColor color, float scale, float intensity)
        {
            var popup = Instantiate(popupPrefab, position, Quaternion.identity, container);
            popup.Show(text, color, 0f, scale, intensity);
        }
    }
}
