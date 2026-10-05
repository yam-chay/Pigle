using UnityEngine;

namespace Piglings.Simulation
{
    /// <summary>The named places the camera can be (PROTOTYPE_V2.md, "Camera frames").</summary>
    public enum CameraFrame
    {
        Night,      // tonight's own frame (NightDefinition Camera Y / Size)
        Doors,      // the barn's doors, from outside: the post-run
        BarnRoom,   // inside the open doorway: the day loadout
        Tower,      // the whole built tower, wide: slice placement
    }

    /// <summary>
    /// Moves the camera between named frames (M10.C). Put it on the camera. It only moves when told to (NightFlow, between
    /// phases), with an ease in-out; nothing in play ever moves it, so the aim never fights it.
    ///
    /// Order with CameraShake: the shake puts the camera back early in Update (-900), this sets the pose (-800), gameplay
    /// reads the camera after (ThrowController, default order), and the shake adds its offset in LateUpdate on top of
    /// whatever pose this left. So a shake during a move still shakes around the moving camera.
    ///
    /// On its own (no NightFlow) it starts on the night's frame, so per-night frames apply in any scene that has it.
    /// x never changes: every frame is centred where the camera stands in the scene.
    /// </summary>
    [DefaultExecutionOrder(-800)]
    public sealed class CameraDirector : MonoBehaviour
    {
        [SerializeField] private NightSession session;
        [Tooltip("The camera this moves. Empty = the Camera on this object.")]
        [SerializeField] private Camera cam;
        [Tooltip("The built tower: the Night and Tower frames fit it. Empty (or nothing built) = the night's own Camera Y / Size.")]
        [SerializeField] private TowerBuilder tower;

        [Header("Fixed frames (centre y, orthographic size)")]
        [SerializeField] private float doorsY = 2f;
        [SerializeField, Min(0.1f)] private float doorsSize = 2f;
        [SerializeField] private float barnRoomY = 1.16f;
        [SerializeField, Min(0.1f)] private float barnRoomSize = 1.15f;

        [Header("Night frame (fitted to the built tower)")]
        [Tooltip("The bottom of the Night frame (world y), the same on every night: a taller tower zooms out upward. " +
                 "Night 1's tuned frame (y 6, size 3.5) has its bottom at 2.5. A night's Camera Y / Size above 0 overrides.")]
        [SerializeField] private float nightBottomY = 2.5f;

        [Header("Tower frame (fitted to the built tower)")]
        [Tooltip("The lowest thing to show: the ground under the barn (world y).")]
        [SerializeField] private float towerBottomY = 0f;
        [Tooltip("The roof: how far Barn_Top and the pig reach above the tower's top (TowerBuilder.TopY). The Night frame's top too.")]
        [SerializeField] private float towerAboveTop = 1.5f;
        [Tooltip("Half the barn's width (the barn is 6 units wide): the frame is never narrower than this + the margin.")]
        [SerializeField, Min(0f)] private float towerHalfWidth = 3f;
        [Tooltip("Space kept around the tower on every side.")]
        [SerializeField, Min(0f)] private float towerMargin = 0.3f;

        [Header("Nudge (hovering something in the barn)")]
        [Tooltip("Seconds to ease into a nudge and back out of it.")]
        [SerializeField, Min(0.01f)] private float nudgeSeconds = 0.25f;

        private float _x;
        private CameraPose _rest;        // the frame's own pose, without the nudge
        private Vector2 _nudge, _nudgeTarget, _nudgeVelocity;   // a small pan on top of the frame
        private float _zoom, _zoomTarget, _zoomVelocity;        // a small zoom-in (0.05 = 5% closer)
        private bool _placed;            // a frame was set (by NightFlow, or Start's default)
        private CameraPose _from, _to;
        private float _seconds, _elapsed;
        private CameraEase _ease;
        private bool _warnedNoTower;

        /// <summary>True while easing between frames. NightFlow keeps its buttons off meanwhile.</summary>
        public bool IsMoving { get; private set; }
        /// <summary>The frame the camera is at, or heading to.</summary>
        public CameraFrame Target { get; private set; }
        /// <summary>0..1 through the current move (1 when still), before the ease. For views that follow a move (the moon).</summary>
        public float Progress => IsMoving && _seconds > 0f ? Mathf.Clamp01(_elapsed / _seconds) : 1f;

        private void Awake()
        {
            if (cam == null) TryGetComponent(out cam);
            _x = transform.position.x;
        }

        private void Start()
        {
            if (cam == null) { Debug.LogWarning("CameraDirector: no Camera on this object and none assigned — it can't move anything.", this); return; }
            // NightFlow (default order, so its Start runs after this one) snaps to the Doors right after: same frame, never drawn.
            if (!_placed) SnapTo(CameraFrame.Night);
        }

        /// <summary>The pose of a frame right now (Night and Tower from the built tower; a night's Camera Y / Size override).</summary>
        public CameraPose PoseOf(CameraFrame frame)
        {
            switch (frame)
            {
                case CameraFrame.Doors: return new CameraPose(doorsY, doorsSize);
                case CameraFrame.BarnRoom: return new CameraPose(barnRoomY, barnRoomSize);
                case CameraFrame.Tower: return TowerPose();
                default: return NightPose();
            }
        }

        // Fixed bottom, top at the roof of tonight's tower; the night's own Camera Y / Size (when above 0) win.
        // Without a built tower there's nothing to fit: the night's values, or where the camera already is.
        private CameraPose NightPose()
        {
            var night = session.Night;
            var computed = tower != null && tower.SliceCount > 0
                ? CameraFraming.Night(nightBottomY, tower.TopY + towerAboveTop)
                : cam != null ? Current() : new CameraPose(transform.position.y, 5f);
            return CameraFraming.Override(computed, night.CameraY, night.CameraSize);
        }

        /// <summary>How far (world units, up / down) the camera is from a frame right now — for timing a move by its speed.</summary>
        public float DistanceTo(CameraFrame frame) => cam != null ? Mathf.Abs(PoseOf(frame).Y - Current().Y) : 0f;

        /// <summary>Jump to a frame, no move (the boot after a reload: the same view as before it, so the cut is invisible).</summary>
        public void SnapTo(CameraFrame frame)
        {
            _placed = true;
            Target = frame;
            IsMoving = false;
            _rest = PoseOf(frame);
            _nudge = _nudgeTarget = _nudgeVelocity = Vector2.zero;
            _zoom = _zoomTarget = _zoomVelocity = 0f;
            Apply(_rest);
        }

        /// <summary>
        /// A small, eased pan (<paramref name="pan"/>, world units) and zoom-in (<paramref name="zoom"/>, 0.05 = 5% closer)
        /// on top of the frame the camera rests on — hovering the materials pile leans the camera toward it. (0, 0) eases
        /// back. Ignored while moving: a move always lands on the plain frame.
        /// </summary>
        public void Nudge(Vector2 pan, float zoom)
        {
            _nudgeTarget = pan;
            _zoomTarget = Mathf.Clamp(zoom, -0.5f, 0.5f);
        }

        /// <summary>
        /// Ease from wherever the camera is now (mid-move too) to a frame over <paramref name="seconds"/>.
        /// The target pose is read now: a frame that changes later (a rebuilt tower) needs another MoveTo.
        /// </summary>
        public void MoveTo(CameraFrame frame, float seconds) => MoveTo(frame, seconds, CameraEase.InOut);

        /// <summary>A move with a given ease shape (M10.S: In / Out let two moves join at full speed across a reload).</summary>
        public void MoveTo(CameraFrame frame, float seconds, CameraEase ease)
        {
            _ease = ease;
            if (seconds <= 0f || cam == null) { SnapTo(frame); return; }
            _placed = true;
            Target = frame;
            _from = Current();   // the frame under any nudge; the nudge itself eases out during the move
            _nudgeTarget = Vector2.zero;
            _zoomTarget = 0f;
            _to = PoseOf(frame);
            _seconds = seconds;
            _elapsed = 0f;
            IsMoving = true;
        }

        private void Update()
        {
            bool nudging = _nudge.sqrMagnitude > 1e-8f || _nudgeTarget.sqrMagnitude > 1e-8f
                           || Mathf.Abs(_zoom) > 1e-5f || Mathf.Abs(_zoomTarget) > 1e-5f;
            if (!IsMoving && !nudging) return;

            var pose = _rest;
            if (IsMoving)
            {
                _nudgeTarget = Vector2.zero;
                _zoomTarget = 0f;
                _elapsed += Time.deltaTime;
                float t = _elapsed / _seconds;
                pose = CameraFraming.Between(_from, _to, CameraFraming.Ease(t, _ease));
                if (t >= 1f) { IsMoving = false; _rest = _to; }
            }
            _nudge = Vector2.SmoothDamp(_nudge, _nudgeTarget, ref _nudgeVelocity, nudgeSeconds);
            _zoom = Mathf.SmoothDamp(_zoom, _zoomTarget, ref _zoomVelocity, nudgeSeconds);
            Apply(pose);
        }

        private CameraPose TowerPose()
        {
            if (tower != null && tower.SliceCount > 0)
                return CameraFraming.Tower(towerBottomY, tower.TopY + towerAboveTop, towerHalfWidth, towerMargin,
                                           cam != null ? cam.aspect : 16f / 9f);
            if (!_warnedNoTower)
            {
                _warnedNoTower = true;
                Debug.LogWarning("CameraDirector: no built tower (Tower unset, or the night has no slices) — the Tower frame is the night's frame.", this);
            }
            return PoseOf(CameraFrame.Night);
        }

        // The pose without the nudge (what a move starts from).
        private CameraPose Current() => new CameraPose(transform.position.y - _nudge.y, cam.orthographicSize / Mathf.Max(0.01f, 1f - _zoom));

        // The frame's pose, with the nudge on top (zero unless something in the barn is hovered).
        private void Apply(CameraPose pose)
        {
            if (cam == null) return;
            transform.position = new Vector3(_x + _nudge.x, pose.Y + _nudge.y, transform.position.z);
            cam.orthographicSize = pose.Size * (1f - _zoom);
        }

        // Tuning in play mode: right-click the component's header, change a frame's numbers, show it again.
        [ContextMenu("Show frame/Night")] private void ShowNight() => DebugShow(CameraFrame.Night);
        [ContextMenu("Show frame/Doors")] private void ShowDoors() => DebugShow(CameraFrame.Doors);
        [ContextMenu("Show frame/Barn room")] private void ShowBarnRoom() => DebugShow(CameraFrame.BarnRoom);
        [ContextMenu("Show frame/Tower")] private void ShowTower() => DebugShow(CameraFrame.Tower);

        private void DebugShow(CameraFrame frame)
        {
            if (!Application.isPlaying || session == null || session.State == null)
            {
                Debug.LogWarning("Show frame works in play mode only (the Night and Tower frames need tonight's night and tower).", this);
                return;
            }
            SnapTo(frame);
            Debug.Log($"CameraDirector: {frame} — {PoseOf(frame)}", this);
        }

        // The two fixed frames as boxes in the Scene view (select the camera), at the camera's aspect.
        private void OnDrawGizmosSelected()
        {
            var c = cam != null ? cam : GetComponent<Camera>();
            float aspect = c != null ? c.aspect : 16f / 9f;
            float x = Application.isPlaying ? _x : transform.position.x;
            DrawFrame(x, new CameraPose(doorsY, doorsSize), aspect, new Color(1f, 0.8f, 0.2f));
            DrawFrame(x, new CameraPose(barnRoomY, barnRoomSize), aspect, new Color(0.4f, 0.9f, 1f));
        }

        private static void DrawFrame(float x, CameraPose pose, float aspect, Color colour)
        {
            Gizmos.color = colour;
            Gizmos.DrawWireCube(new Vector3(x, pose.Y, 0f), new Vector3(pose.Size * 2f * aspect, pose.Size * 2f, 0f));
        }
    }
}
