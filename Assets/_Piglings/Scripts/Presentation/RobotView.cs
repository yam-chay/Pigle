using Piglings.Events;
using Piglings.Simulation;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// Visual side of a wolf-bot: reacts to RobotController state, never changes it.
    /// Rig hierarchy comes from wolfbot_rig.psd (PSD Importer).
    /// </summary>
    public sealed class RobotView : MonoBehaviour
    {
        [SerializeField] private RobotController robot;
        [SerializeField] private Animator animator;          // clips: Climb (default loop), ClimbDanger, Break
        [SerializeField] private GameObject[] detachables;   // arms, legs, antenna, tail roots
        [Tooltip("Breaching = out of play: the robot is drawn at this opacity, so nobody aims at it. Its stolen stone stays fully opaque.")]
        [SerializeField, Range(0f, 1f)] private float breachOpacity = 0.7f;
        [Tooltip("During a placement round every robot fades to this (× its breach opacity if breaching), so the pegs read first.")]
        [SerializeField, Range(0f, 1f)] private float placementOpacity = 0.4f;

        private static readonly int BreakTrigger = Animator.StringToHash("Break");
        private static readonly int InDangerBool = Animator.StringToHash("InDanger");

        private EventBus _bus;   // the robot's session bus, known once the robot is initialized
        private SpriteRenderer[] _renderers;
        private Color[] _breachColors;   // each part's colour when the breach started, already faded; null until then
        private Color[] _placementColors; // each part's colour when a placement round started; null outside a round

        // Cached before anything is parented to the robot, so a stolen stone riding on it is never faded.
        private void Awake() => _renderers = GetComponentsInChildren<SpriteRenderer>(includeInactive: true);

        private void OnEnable() => robot.StateChanged += OnState;

        private void OnDisable()
        {
            robot.StateChanged -= OnState;
            if (_bus != null) _bus.Unsubscribe<RobotEnteredDangerZone>(OnEnteredDanger);
            _bus = null;
        }

        // The wall freezes for peg placement: freeze the climb animation with it,
        // so frozen robots don't keep pedalling in place. A breaching robot isn't paused (it finishes its sequence).
        private void Update()
        {
            if (animator == null || robot.Session == null) return;
            bool paused = !robot.Session.WallMoving && robot.State == RobotState.Climbing;
            animator.speed = paused ? 0f : 1f;
        }

        // After the Animator: the climb clips animate every part's colour (alpha, red eyes), so a fade set once would
        // be overwritten next frame. Re-applied each frame instead, from the colours the parts had when the breach
        // started (so parts hidden at that moment stay hidden). A future breach clip should move bones, not colours.
        // The placement fade works the same way: the colours the parts had when the round started, faded, re-applied
        // each frame; the climb animation is paused then anyway. After the round the parts get those colours back
        // (animated ones are rewritten by the Animator next frame regardless).
        private void LateUpdate()
        {
            bool placing = robot.Session != null && robot.Session.State.Phase == NightPhase.PegPlacement;
            if (placing && _placementColors == null)
            {
                _placementColors = new Color[_renderers.Length];
                for (int i = 0; i < _renderers.Length; i++) if (_renderers[i]) _placementColors[i] = _renderers[i].color;
            }
            else if (!placing && _placementColors != null)
            {
                if (_breachColors == null)
                    for (int i = 0; i < _renderers.Length; i++) if (_renderers[i]) _renderers[i].color = _placementColors[i];
                _placementColors = null;
            }

            var colors = _breachColors ?? _placementColors;
            if (colors == null) return;
            float fade = placing ? placementOpacity : 1f;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (!_renderers[i]) continue;
                var c = colors[i];
                c.a *= fade;
                _renderers[i].color = c;
            }
        }

        // Every robot hears every danger event; it only reacts to its own. A few dozen a night.
        private void OnEnteredDanger(RobotEnteredDangerZone e)
        {
            if (e.Robot == robot.Id && animator) animator.SetBool(InDangerBool, true);
        }

        private void OnState(RobotController r, RobotState s)
        {
            switch (s)
            {
                case RobotState.Climbing:
                    // Initialize has just given the robot its session — the first moment the bus exists.
                    if (_bus == null && robot.Session != null)
                    {
                        _bus = robot.Session.Bus;
                        _bus.Subscribe<RobotEnteredDangerZone>(OnEnteredDanger);
                    }
                    break;
                case RobotState.LosingGrip:
                    if (animator) animator.SetTrigger(BreakTrigger);   // the clip itself swaps eyes to X at the crack
                    break;
                case RobotState.Falling:
                    foreach (var d in detachables) if (d) d.SetActive(false);
                    break;
                case RobotState.Breaching:
                    // "Not a target": the robot is already out of play (no collider); fading it says so.
                    _breachColors = new Color[_renderers.Length];
                    for (int i = 0; i < _renderers.Length; i++)
                    {
                        if (!_renderers[i]) continue;
                        var c = _renderers[i].color;
                        c.a *= breachOpacity;   // multiplied, so parts already hidden (alpha 0) stay hidden
                        _breachColors[i] = c;
                    }
                    break;
            }
        }
    }
}
