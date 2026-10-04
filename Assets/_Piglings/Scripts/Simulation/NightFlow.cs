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
        Settling,     // the night ended: waiting for the sweep to land
        Descending,   // the camera goes down to the doors
        PostRun,      // at the doors, open: Retry / Next night
        Leaving,      // Retry / Next pressed: the scene is reloading
    }

    /// <summary>
    /// The campaign scene's flow from load to reload (M10.C, PROTOTYPE_V2.md):
    ///
    ///   Boot (Doors → Barn room) → BarnRoom ⇄ Tower → Rising (→ Night frame) → Night → Settling → Descending (→ Doors)
    ///   → PostRun → Leaving (save + reload)
    ///
    /// Restart = reload: Retry / Next night save where the player goes (NightSession.GoToNight) and reload the scene,
    /// which boots at the Doors — the same view the post-run showed, so the cut is invisible — and every night starts
    /// clean. The night itself waits in Dusk (NightSession Start Immediately off) until the camera reaches the Night
    /// frame; then BeginNight().
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
        [Tooltip("After the night: down to the Doors.")]
        [SerializeField, Min(0f)] private float descendSeconds = 1.5f;

        [Header("After the night")]
        [Tooltip("Once the sweep has landed, wait this long before the camera goes down — a beat to see how it ended.")]
        [SerializeField, Min(0f)] private float settledHoldSeconds = 0.75f;
        [Tooltip("Safety: go down after this long even if something still hasn't landed (logged). Balls time out on their own, " +
                 "so this should never be needed.")]
        [SerializeField, Min(1f)] private float maxSettleSeconds = 10f;

        private float _settlingSince;
        private float _settledSince = -1f;   // Time.time the wall was first seen settled; -1 = not yet

        public FlowState State { get; private set; } = FlowState.Boot;

        /// <summary>The barn's doors: open from the post-run until the night begins (the bottom is out of view by then).</summary>
        public bool DoorsOpen { get; private set; } = true;

        /// <summary>Start night pressed: the camera starts rising, for this many seconds. The moon-rise hook (no moon art yet).</summary>
        public event System.Action<float> MoonRises;

        public bool CameraMoving => director != null && director.IsMoving;

        public bool CanStartNight => !CameraMoving && (State == FlowState.BarnRoom || State == FlowState.Tower);
        public bool CanOpenTower => !CameraMoving && State == FlowState.BarnRoom;
        public bool CanLeaveTower => !CameraMoving && State == FlowState.Tower;
        public bool CanRetry => !CameraMoving && State == FlowState.PostRun;
        /// <summary>A dawn on this night, and a night after it: Next night is a choice (CampaignPlan.CanGoNext).</summary>
        public bool NextEarned => session.Plan != null && session.Plan.CanGoNext(session.Profile, session.NightIndex);
        public bool CanGoNext => CanRetry && NextEarned;

        // ---------- what the buttons call ----------

        public void StartNight() { if (CanStartNight) SetState(FlowState.Rising); }
        public void OpenTower() { if (CanOpenTower) SetState(FlowState.Tower); }
        public void LeaveTower() { if (CanLeaveTower) SetState(FlowState.BarnRoom); }

        public void Retry()
        {
            if (!CanRetry) return;
            SetState(FlowState.Leaving);
            session.GoToNight(session.NightIndex, "Retry");
        }

        public void NextNight()
        {
            if (!CanGoNext) return;
            SetState(FlowState.Leaving);
            session.GoToNight(session.NightIndex + 1, "Next night");
        }

        // ---------- the machine ----------

        private void Start()
        {
            if (director == null) Debug.LogWarning("NightFlow: no CameraDirector — the flow runs, but the camera stays put.", this);
            if (!session.IsCampaign) Debug.LogWarning("NightFlow: the NightSession has no Campaign — Retry reloads, Next night never shows.", this);

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
                    break;
                case FlowState.Descending:
                    if (director != null) director.MoveTo(CameraFrame.Doors, descendSeconds);
                    break;
                case FlowState.PostRun:
                    // The camera has reached the Doors: they open on the post-run.
                    DoorsOpen = true;
                    LogPostRun();
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
                    if (!CameraMoving) SetState(FlowState.PostRun);
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
            Debug.Log($"NightFlow: post-run — night {index + 1}/{plan.NightCount} ({id}), {session.State.Result}, " +
                      $"dawns saved on it: {dawns}. {next}.", this);
        }

        // The sweep has landed (no robot off the wall, no chain open) and stayed so for a beat → down to the doors.
        private void UpdateSettling()
        {
            if (session.WallSettled)
            {
                if (_settledSince < 0f) _settledSince = Time.time;
                if (Time.time - _settledSince >= settledHoldSeconds) SetState(FlowState.Descending);
                return;
            }
            _settledSince = -1f;
            if (Time.time - _settlingSince >= maxSettleSeconds)
            {
                Debug.LogWarning($"NightFlow: the wall hasn't settled after {maxSettleSeconds:0.#}s (something still falling or a chain " +
                                 "still open) — going to the doors anyway.", this);
                SetState(FlowState.Descending);
            }
        }
    }
}
