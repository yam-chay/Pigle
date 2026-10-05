using Piglings.Events;
using UnityEngine;

namespace Piglings.Simulation
{
    /// <summary>Where the campaign scene is between scene load and the reload (M10.C). Only NightFlow writes it.</summary>
    public enum FlowState
    {
        Boot,         // the camera eases from the Doors (where the last scene left it) into the barn room
        BarnRoom,     // the day loadout: Start night, or go see the tower
        Tower,        // slice placement (the whole tower in view)
        Rising,       // Start night pressed: the camera rises to the Night frame as the moon rises
        Night,        // the night is being played (Running / PegPlacement)
        Settling,     // the night ended: the wolf's climb (a loss) and the sweep landing
        PostRun,      // the post-run over the Night frame (M10.E): Retry / To the barn / Next night
        Descending,   // To the barn / Next night pressed: the camera goes down to the doors, they open
        Leaving,      // the scene is reloading (after the doors, or at once for a fast Retry)
    }

    /// <summary>
    /// The campaign scene's flow from load to reload (M10.C; the post-run moved to the Night frame in M10.E, PROTOTYPE_V2.md):
    ///
    ///   Boot (Doors → Barn room) → BarnRoom ⇄ Tower → Rising (→ Night frame) → Night → Settling → PostRun (Night frame)
    ///   → To the barn / Next night: Descending (→ Doors, they open) → Leaving (save + reload)
    ///   → Retry: Leaving at once (save + reload straight into the night — boot at the Night frame, no barn room)
    ///
    /// Restart = reload: every button saves where the player goes (NightSession.GoToNight / RetryNight) and reloads the
    /// scene, so every night starts clean. A reload that boots in the barn starts at the Doors — where To the barn / Next
    /// night left the camera, so the cut is invisible; a fast Retry boots at the Night frame — where the post-run was.
    /// The night itself waits in Dusk (NightSession Start Immediately off) until the camera reaches the Night frame; then
    /// BeginNight().
    ///
    /// Out of stones (M10.E): the Rules end the night at once and the sweep runs then; the wolf's chimney climb (a hook,
    /// WolfClimbStarts) gets Wolf Seconds before the post-run shows. A dawn shows it as soon as the sweep has landed.
    ///
    /// The camera moves only between phases (CameraDirector). Gameplay input is already off outside the night (Dusk and
    /// Ended can't throw); the flow's own actions are refused while the camera moves (Can… false), so a button pressed
    /// mid-move does nothing. Lives in Simulation: it changes what's running. Views read State / DoorsOpen.
    /// One state machine: transitions only through SetState → EnterState.
    /// </summary>
    public sealed class NightFlow : MonoBehaviour
    {
        [SerializeField] private NightSession session;
        [SerializeField] private CameraDirector director;

        [Header("Camera moves (seconds)")]
        [Tooltip("Boot: from the Doors into the barn room.")]
        [SerializeField, Min(0f)] private float bootSeconds = 1.2f;
        [Tooltip("Barn room ⇄ Tower.")]
        [SerializeField, Min(0f)] private float towerSeconds = 1f;
        [Tooltip("Start night: the barn room up to the Night frame (the moon rises meanwhile).")]
        [SerializeField, Min(0f)] private float riseSeconds = 2.5f;
        [Tooltip("To the barn / Next night: from the Night frame down to the Doors.")]
        [SerializeField, Min(0f)] private float descendSeconds = 1.5f;

        [Header("After the night")]
        [Tooltip("Out of stones: the wolf's chimney climb — how long after the night is lost before the post-run shows (the " +
                 "sweep runs as it starts). Match it to the wolf's sequence once there is one.")]
        [SerializeField, Min(0f)] private float wolfSeconds = 2f;
        [Tooltip("Once the sweep has landed (and the wolf's time is up), wait this long before the post-run — a beat to see how it ended.")]
        [SerializeField, Min(0f)] private float settledHoldSeconds = 0.75f;
        [Tooltip("Safety: show the post-run after this long (on top of the wolf's time) even if something still hasn't landed " +
                 "(logged). Balls time out on their own, so this should never be needed.")]
        [SerializeField, Min(1f)] private float maxSettleSeconds = 10f;

        private float _settlingSince;
        private float _settledSince = -1f;   // Time.time the wall was first seen settled; -1 = not yet
        private float _waitForWolf;          // this Settling's wolf time (0 at dawn)
        private int _leaveTo;                // Descending: the night index the reload goes to
        private string _leaveWhy;

        public FlowState State { get; private set; } = FlowState.Boot;

        /// <summary>The barn's doors: open from the camera's arrival at them until the night begins (the bottom is out of view by then).</summary>
        public bool DoorsOpen { get; private set; } = true;

        /// <summary>Start night pressed: the camera starts rising, for this many seconds. The moon-rise hook (no moon art yet).</summary>
        public event System.Action<float> MoonRises;

        /// <summary>
        /// Out of stones: the wolf starts his climb to the chimney, and the post-run waits this many seconds (Wolf Seconds) for
        /// it. The hook for the wolf's sequence (M10.E) — nothing listens yet.
        /// </summary>
        public event System.Action<float> WolfClimbStarts;

        public bool CameraMoving => director != null && director.IsMoving;

        public bool CanStartNight => !CameraMoving && (State == FlowState.BarnRoom || State == FlowState.Tower);
        public bool CanOpenTower => !CameraMoving && State == FlowState.BarnRoom;
        public bool CanLeaveTower => !CameraMoving && State == FlowState.Tower;
        /// <summary>The post-run's buttons can be pressed: it's showing, and the camera is still.</summary>
        public bool CanChoose => !CameraMoving && State == FlowState.PostRun;
        /// <summary>A dawn on this night, and a night after it: Next night is a choice (CampaignPlan.CanGoNext).</summary>
        public bool NextEarned => session.Plan != null && session.Plan.CanGoNext(session.Profile, session.NightIndex);

        /// <summary>The post-run's primary button (PostRunChoices: a loss → Retry; a dawn → Next night, or To the barn after the last night).</summary>
        public PostRunAction Primary { get { Choices(out var primary, out _); return primary; } }
        /// <summary>The post-run's secondary button (a loss → To the barn; a dawn → Retry).</summary>
        public PostRunAction Secondary { get { Choices(out _, out var secondary); return secondary; } }

        private void Choices(out PostRunAction primary, out PostRunAction secondary) =>
            PostRunChoices.For(session.State.Result == NightResult.Won && session.State.Ended, NextEarned, out primary, out secondary);

        // ---------- what the buttons call ----------

        public void StartNight() { if (CanStartNight) SetState(FlowState.Rising); }
        public void OpenTower() { if (CanOpenTower) SetState(FlowState.Tower); }
        public void LeaveTower() { if (CanLeaveTower) SetState(FlowState.BarnRoom); }

        /// <summary>A post-run button: does what it's showing (PostRunButtons passes Primary / Secondary). Refused unless offered.</summary>
        public void Choose(PostRunAction action)
        {
            if (!CanChoose || (action != Primary && action != Secondary) || action == PostRunAction.None)
            {
                Debug.Log($"NightFlow: {action} pressed but refused — state {State}, camera moving {CameraMoving}, " +
                          $"offered {Primary} / {Secondary}.", this);
                return;
            }
            switch (action)
            {
                case PostRunAction.Retry:
                    // Fast: straight back into the night. The reload boots at the Night frame — the view the post-run is on.
                    Debug.Log($"NightFlow: Retry pressed — night {session.NightIndex + 1} again, straight into the night.", this);
                    SetState(FlowState.Leaving);
                    session.RetryNight("Retry");
                    break;
                case PostRunAction.ToBarn:
                    Leave(session.NightIndex, "To the barn");
                    break;
                case PostRunAction.NextNight:
                    Leave(session.NightIndex + 1, "Next night");
                    break;
            }
        }

        public void Retry() => Choose(PostRunAction.Retry);
        public void ToBarn() => Choose(PostRunAction.ToBarn);
        public void NextNight() => Choose(PostRunAction.NextNight);

        // To the barn / Next night: down to the doors first (the reload boots there), then save + reload.
        private void Leave(int index, string why)
        {
            Debug.Log($"NightFlow: {why} pressed — down to the doors, then night {index + 1}'s barn room.", this);
            _leaveTo = index;
            _leaveWhy = why;
            SetState(FlowState.Descending);
        }

        // ---------- the machine ----------

        private void Start()
        {
            if (director == null) Debug.LogWarning("NightFlow: no CameraDirector — the flow runs, but the camera stays put.", this);
            if (!session.IsCampaign) Debug.LogWarning("NightFlow: the NightSession has no Campaign — Retry reloads, Next night never shows.", this);

            // A fast Retry: no day phase — start the night where the post-run left the camera.
            if (session.StartsInNight && session.State.Phase == NightPhase.Dusk)
            {
                if (director != null) director.SnapTo(CameraFrame.Night);
                SetState(FlowState.Night);
                return;
            }

            // The night already running means Start Immediately is on: there's no day phase to play. Go straight to it.
            if (session.State.Phase != NightPhase.Dusk)
            {
                Debug.LogWarning("NightFlow: the night is already running — turn NightSession ▸ Start Immediately off in this scene " +
                                 "(the flow begins the night). Skipping the barn room.", this);
                if (director != null) director.SnapTo(CameraFrame.Night);
                SetState(FlowState.Night);
                return;
            }
            // The first state is entered, not transitioned to (State starts at Boot, so SetState would skip it).
            EnterState(FlowState.Boot);
        }

        private void SetState(FlowState next)
        {
            if (State == next) return;
            State = next;
            EnterState(next);
        }

        private void EnterState(FlowState s)
        {
            switch (s)
            {
                case FlowState.Boot:
                    DoorsOpen = true;
                    if (director != null) { director.SnapTo(CameraFrame.Doors); director.MoveTo(CameraFrame.BarnRoom, bootSeconds); }
                    break;
                case FlowState.BarnRoom:
                    // From Boot the camera is already there; from the Tower it comes back.
                    if (director != null && director.Target != CameraFrame.BarnRoom) director.MoveTo(CameraFrame.BarnRoom, towerSeconds);
                    break;
                case FlowState.Tower:
                    if (director != null) director.MoveTo(CameraFrame.Tower, towerSeconds);
                    break;
                case FlowState.Rising:
                    if (director != null) director.MoveTo(CameraFrame.Night, riseSeconds);
                    MoonRises?.Invoke(riseSeconds);
                    break;
                case FlowState.Night:
                    // The doors close out of view (the Night frame doesn't reach the barn's bottom).
                    DoorsOpen = false;
                    if (session.State.Phase == NightPhase.Dusk) session.BeginNight();
                    break;
                case FlowState.Settling:
                    _settlingSince = Time.time;
                    _settledSince = -1f;
                    // Out of stones: the wolf's climb starts now (the sweep already ran, as the night ended).
                    _waitForWolf = session.State.Result == NightResult.Lost ? wolfSeconds : 0f;
                    if (session.State.Result == NightResult.Lost) WolfClimbStarts?.Invoke(_waitForWolf);
                    break;
                case FlowState.PostRun:
                    // Over the Night frame: the camera doesn't move until a button says where to go.
                    LogPostRun();
                    break;
                case FlowState.Descending:
                    if (director != null) director.MoveTo(CameraFrame.Doors, descendSeconds);
                    break;
            }
        }

        private void Update()
        {
            switch (State)
            {
                case FlowState.Boot:
                    if (!CameraMoving) SetState(FlowState.BarnRoom);
                    break;
                case FlowState.Rising:
                    if (!CameraMoving) SetState(FlowState.Night);
                    break;
                case FlowState.Night:
                    if (session.State.Ended) SetState(FlowState.Settling);
                    break;
                case FlowState.Settling:
                    UpdateSettling();
                    break;
                case FlowState.Descending:
                    if (CameraMoving) break;
                    // At the doors: they open, and the reloaded scene boots here (Doors → barn room).
                    DoorsOpen = true;
                    SetState(FlowState.Leaving);
                    session.GoToNight(_leaveTo, _leaveWhy);
                    break;
            }
        }

        // One line on what the post-run offers and why, so "Next night didn't show" can be read straight off the console.
        private void LogPostRun()
        {
            var plan = session.Plan;
            if (plan == null) { Debug.Log("NightFlow: post-run — not a campaign: Retry only.", this); return; }
            int index = session.NightIndex;
            string id = plan.NightId(index);
            int dawns = session.Profile.DawnsOn(id);
            string next = NextEarned ? $"Next night offered (to night {index + 2})"
                : index + 1 >= plan.NightCount ? "no Next night: this is the last night"
                : "no Next night: no dawn saved on this night yet";
            Debug.Log($"NightFlow: post-run — night {index + 1}/{plan.NightCount} ({id}), {session.State.Result} " +
                      $"({session.State.EndReason}), dawns saved on it: {dawns}. Buttons: {Primary} / {Secondary}; {next}.", this);
        }

        // The wolf's time is up (a loss) and the sweep has landed (no robot off the wall, no chain open) and stayed so for a
        // beat → the post-run, right here over the Night frame.
        private void UpdateSettling()
        {
            float since = Time.time - _settlingSince;
            if (session.WallSettled)
            {
                if (_settledSince < 0f) _settledSince = Time.time;
                if (since >= _waitForWolf && Time.time - _settledSince >= settledHoldSeconds) SetState(FlowState.PostRun);
                return;
            }
            _settledSince = -1f;
            if (since >= _waitForWolf + maxSettleSeconds)
            {
                Debug.LogWarning($"NightFlow: the wall hasn't settled after {maxSettleSeconds:0.#}s (something still falling or a chain " +
                                 "still open) — showing the post-run anyway.", this);
                SetState(FlowState.PostRun);
            }
        }
    }
}
