using System.Collections.Generic;
using Piglings.Definitions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Piglings.Simulation
{
    /// <summary>
    /// Slice placement in the day phase (M10.F, the Tower frame), by hand:
    ///  - hover a slice of the tower: it jiggles;
    ///  - drag it out and drop it on another slot: the two trade places;
    ///  - drag a slice from the tray beside the tower onto a slot: that slot becomes it;
    ///  - drop anywhere else: it slides back. The tower is always whole (SliceDrag.Resolve), so there's never a gap.
    /// Every change rebuilds the tower and saves the night's choice (NightSession.SetSlice / SwapSlices). Only in the Tower
    /// state, never while the camera moves, never through a UI button.
    ///
    /// The slices that move are the tower's own looks (TowerBuilder.SliceLook, holds included) — before the night begins
    /// nothing physical runs, and every look is put back exactly (TowerBuilder.ResetSlice) when placement ends. The tray
    /// and the dragged ghost are this component's own sprites. Simulation: it's the input and it moves scene objects.
    /// </summary>
    public sealed class SlicePicker : MonoBehaviour
    {
        [SerializeField] private NightSession session;
        [SerializeField] private NightFlow flow;
        [SerializeField] private Camera cam;
        [Tooltip("How far from the tower's centre a slice can be grabbed or dropped on (the barn is 6 wide).")]
        [SerializeField, Min(0.1f)] private float slotHalfWidth = 2.6f;

        [Header("Hover jiggle")]
        [SerializeField, Min(0f)] private float jiggleDegrees = 2.5f;
        [SerializeField, Min(0f)] private float jiggleHz = 4f;
        [Tooltip("Seconds to ease into and out of the jiggle.")]
        [SerializeField, Min(0.01f)] private float jiggleEase = 0.1f;
        [Tooltip("A dropped slice wobbles harder for this long.")]
        [SerializeField, Min(0.01f)] private float landSeconds = 0.4f;
        [SerializeField, Min(0f)] private float landDegrees = 6f;

        [Header("Drag")]
        [Tooltip("World units the pointer moves before a press becomes a drag.")]
        [SerializeField, Min(0f)] private float dragThreshold = 0.1f;
        [Tooltip("Degrees of tilt per unit/second of sideways speed while dragging (it leans into the motion).")]
        [SerializeField, Min(0f)] private float dragTilt = 2f;
        [SerializeField, Min(0.01f)] private float returnSeconds = 0.18f;

        [Header("Tray (the slices to choose from, beside the tower)")]
        [Tooltip("The tray's x from the tower's centre (world units): + = right of the tower.")]
        [SerializeField] private float trayOffsetX = 5f;
        [Tooltip("A tray slice's size × the tower's.")]
        [SerializeField, Range(0.1f, 1f)] private float trayScale = 0.45f;
        [Tooltip("Space between tray slices, × a tray slice's height.")]
        [SerializeField, Min(1f)] private float traySpacing = 1.2f;
        [Tooltip("A hovered tray slice grows by this (0.08 = 8%).")]
        [SerializeField, Range(0f, 0.3f)] private float trayHoverGrow = 0.08f;

        /// <summary>The tower slot under the pointer (bottom = 0), -1 = none or not placing now.</summary>
        public int HoveredSlice { get; private set; } = -1;

        /// <summary>A slot changed (its index): the sound hook.</summary>
        public event System.Action<int> SliceChanged;

        private sealed class TrayItem { public WallSliceDefinition Slice; public SpriteRenderer Renderer; public float Hover; }

        private readonly List<TrayItem> _tray = new List<TrayItem>();
        private Transform _trayRoot;
        private SpriteRenderer _ghost;           // the tray slice being dragged
        private float[] _jiggle = new float[0];  // 0..1 per slot, eased
        private float[] _landedAt = new float[0];

        private bool _pending, _dragging, _fromTray;
        private int _fromSlot = -1, _trayIndex = -1, _target = -1;
        private Vector2 _pressPoint;
        private Vector3 _grabOffset;
        private float _tilt, _lastX;
        private SortingGroup _lifted;            // raises the dragged slice (and its holds) above the others

        private int _returning = -1;             // a slot sliding home after a drop that changed nothing
        private Vector3 _returnFrom;
        private Quaternion _returnFromRotation;
        private float _returnT;

        private bool _posed;                     // something was moved, and must be put back when placement ends

        private void Update()
        {
            var tower = session != null ? session.Tower : null;
            bool active = flow != null && flow.State == FlowState.Tower && !flow.CameraMoving && session.CanEditTower && !session.GameplayInputBlocked
                          && cam != null && tower != null;
            if (!active) { StopPlacing(tower); return; }

            EnsureSlots(tower);
            ShowTray(true);

            var pointer = Pointer.current;
            if (pointer == null) return;
            Vector2 world = cam.ScreenToWorldPoint(pointer.position.ReadValue());
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            HoveredSlice = overUi ? -1 : tower.SliceAt(world, slotHalfWidth);
            int trayHover = overUi || _dragging ? -1 : TrayAt(world);

            // Press: a tray slice starts dragging at once (it's a copy); a tower slice waits for the pointer to move.
            if (!_pending && !_dragging && pointer.press.wasPressedThisFrame && !overUi)
            {
                if (trayHover >= 0) BeginTrayDrag(trayHover, world);
                else if (HoveredSlice >= 0)
                {
                    _pending = true;
                    _fromSlot = HoveredSlice;
                    _pressPoint = world;
                    _grabOffset = tower.SliceLook(_fromSlot).transform.position - (Vector3)world;
                }
            }
            if (_pending)
            {
                if (!pointer.press.isPressed) { _pending = false; Land(_fromSlot); }   // a click: a little wobble, nothing moves
                else if (SliceDrag.IsDrag(world.x - _pressPoint.x, world.y - _pressPoint.y, dragThreshold)) BeginTowerDrag(tower, world);
            }
            if (_dragging)
            {
                _target = HoveredSlice;
                Drag(tower, world);
                if (!pointer.press.isPressed) Drop(tower);
            }

            Animate(tower, trayHover);
        }

        // ---------- dragging ----------

        private void BeginTrayDrag(int item, Vector2 world)
        {
            _dragging = true;
            _fromTray = true;
            _trayIndex = item;
            _fromSlot = -1;
            var source = _tray[item].Renderer;
            if (_ghost == null)
            {
                _ghost = new GameObject("Dragged slice").AddComponent<SpriteRenderer>();
                _ghost.transform.SetParent(transform, worldPositionStays: false);
            }
            _ghost.sprite = source.sprite;
            _ghost.sortingLayerID = source.sortingLayerID;
            _ghost.sortingOrder = source.sortingOrder + 100;
            _ghost.transform.localScale = Vector3.one * BaseScale() / Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
            _ghost.gameObject.SetActive(true);
            // Held by its middle: a slice's pivot is its bottom edge.
            _grabOffset = new Vector3(0f, -session.Tower.SliceHeight / 2f, 0f);
            _ghost.transform.position = (Vector3)world + _grabOffset;
            _lastX = world.x;
            _posed = true;
        }

        private void BeginTowerDrag(TowerBuilder tower, Vector2 world)
        {
            _pending = false;
            _dragging = true;
            _fromTray = false;
            // A slice still sliding home lands now: one lifted slice at a time.
            if (_returning >= 0) { tower.ResetSlice(_returning); _returning = -1; }
            Unlift();
            var look = tower.SliceLook(_fromSlot);
            _lifted = look.gameObject.AddComponent<SortingGroup>();
            _lifted.sortingLayerID = look.sortingLayerID;
            _lifted.sortingOrder = look.sortingOrder + 100;
            _lastX = world.x;
            _posed = true;
        }

        private void Drag(TowerBuilder tower, Vector2 world)
        {
            // Lean into the motion: tilt follows the sideways speed, settling when the pointer stops.
            float speed = Time.deltaTime > 0f ? (world.x - _lastX) / Time.deltaTime : 0f;
            _lastX = world.x;
            _tilt = Mathf.Lerp(_tilt, Mathf.Clamp(-speed * dragTilt, -12f, 12f), 1f - Mathf.Exp(-12f * Time.deltaTime));
            Transform moved = null;
            if (_fromTray) { if (_ghost != null) moved = _ghost.transform; }
            else { var look = tower.SliceLook(_fromSlot); if (look != null) moved = look.transform; }
            if (moved == null) return;
            moved.position = (Vector3)world + _grabOffset;
            moved.rotation = Quaternion.Euler(0f, 0f, _tilt);
        }

        private void Drop(TowerBuilder tower)
        {
            _dragging = false;
            _tilt = 0f;
            var kind = SliceDrag.Resolve(_fromTray, _fromSlot, _target);
            bool changed = false;
            if (kind == SliceDropKind.Replace && _trayIndex >= 0) changed = session.SetSlice(_target, _tray[_trayIndex].Slice);
            else if (kind == SliceDropKind.Swap) changed = session.SwapSlices(_fromSlot, _target);

            if (_ghost != null) _ghost.gameObject.SetActive(false);
            if (changed)
            {
                // The tower was rebuilt: new looks at home (the dragged one, and its SortingGroup, went with the old ones).
                _lifted = null;
                Land(_target);
                if (kind == SliceDropKind.Swap) Land(_fromSlot);
                SliceChanged?.Invoke(_target);
                if (kind == SliceDropKind.Swap) SliceChanged?.Invoke(_fromSlot);
            }
            else if (!_fromTray) StartReturn(tower, _fromSlot);
            _fromSlot = _trayIndex = _target = -1;
        }

        private void StartReturn(TowerBuilder tower, int slot)
        {
            var look = tower.SliceLook(slot);
            if (look == null) return;
            _returning = slot;
            _returnFrom = look.transform.position;
            _returnFromRotation = look.transform.rotation;
            _returnT = 0f;
        }

        private void Land(int slot)
        {
            if (slot >= 0 && slot < _landedAt.Length) _landedAt[slot] = Time.time;
        }

        // ---------- every frame: jiggles, the slide home, the tray ----------

        private void Animate(TowerBuilder tower, int trayHover)
        {
            float ease = 1f - Mathf.Exp(-Time.deltaTime / jiggleEase);
            for (int i = 0; i < _jiggle.Length; i++)
            {
                bool dragged = _dragging && !_fromTray && i == _fromSlot;
                if (dragged) continue;
                var look = tower.SliceLook(i);
                if (look == null) continue;

                if (i == _returning)
                {
                    _returnT += Time.deltaTime / returnSeconds;
                    float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_returnT));
                    look.transform.position = Vector3.Lerp(_returnFrom, tower.SliceHome(i), t);
                    look.transform.rotation = Quaternion.Slerp(_returnFromRotation, Quaternion.identity, t);
                    if (_returnT >= 1f) { _returning = -1; Unlift(); tower.ResetSlice(i); Land(i); }
                    continue;
                }

                // Hovered (or the slot a dragged slice would land on) jiggles; a fresh landing wobbles harder.
                bool wanted = _dragging ? i == _target : i == HoveredSlice;
                _jiggle[i] = Mathf.Lerp(_jiggle[i], wanted ? 1f : 0f, ease);
                float landed = Mathf.Clamp01(1f - (Time.time - _landedAt[i]) / landSeconds);
                float angle = SliceDrag.Jiggle(Time.time + i * 0.37f, jiggleDegrees, jiggleHz) * _jiggle[i]
                              + SliceDrag.Jiggle(Time.time, landDegrees, jiggleHz * 1.5f) * landed * landed;
                Pose(tower, i, angle);
            }

            for (int k = 0; k < _tray.Count; k++)
            {
                var item = _tray[k];
                item.Hover = Mathf.Lerp(item.Hover, k == trayHover ? 1f : 0f, ease);
                item.Renderer.transform.localScale = Vector3.one * TrayItemScale() * (1f + trayHoverGrow * item.Hover);
            }
        }

        // Rotates slice i about its centre (its pivot is the bottom edge), so the jiggle wobbles in place.
        private void Pose(TowerBuilder tower, int i, float angle)
        {
            _posed = true;
            var look = tower.SliceLook(i);
            var home = tower.SliceHome(i);
            var centre = home + new Vector3(0f, tower.SliceHeight / 2f, 0f);
            var rotation = Quaternion.Euler(0f, 0f, angle);
            look.transform.SetPositionAndRotation(centre + rotation * (home - centre), rotation);
        }

        // ---------- the tray ----------

        private void EnsureSlots(TowerBuilder tower)
        {
            int n = session.TowerSlices.Count;
            if (_jiggle.Length != n)
            {
                _jiggle = new float[n];
                _landedAt = new float[n];
                for (int i = 0; i < n; i++) _landedAt[i] = float.NegativeInfinity;
            }
            if (_trayRoot != null) return;

            _trayRoot = new GameObject("Slice tray").transform;
            _trayRoot.SetParent(transform, worldPositionStays: false);
            var first = tower.SliceLook(0);
            var choices = session.SlicesToChoose();
            float itemHeight = tower.SliceHeight * trayScale;
            float step = itemHeight * traySpacing;
            float middle = (tower.SliceBottom(0) + tower.SliceBottom(n)) / 2f;
            for (int k = 0; k < choices.Count; k++)
            {
                var slice = choices[k];
                var sr = new GameObject($"Tray {slice.Id}").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(_trayRoot, worldPositionStays: false);
                sr.sprite = slice.Sprite != null ? slice.Sprite : (first != null ? first.sprite : null);
                if (first != null) { sr.sortingLayerID = first.sortingLayerID; sr.sortingOrder = first.sortingOrder + 10; }
                // Centred on the tower's middle, first at the top; a slice's pivot is its bottom edge.
                float centreY = middle + ((choices.Count - 1) / 2f - k) * step;
                sr.transform.position = new Vector3(tower.CentreX + trayOffsetX, centreY - itemHeight / 2f, 0f);
                sr.transform.localScale = Vector3.one * TrayItemScale();
                _tray.Add(new TrayItem { Slice = slice, Renderer = sr });
            }
        }

        // The tower slices' world scale (the slice prefab's), so tray and ghost match them.
        private float BaseScale()
        {
            var first = session.Tower != null ? session.Tower.SliceLook(0) : null;
            return first != null ? Mathf.Abs(first.transform.lossyScale.x) : 1f;
        }

        private float TrayItemScale() => BaseScale() * trayScale / Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));

        private int TrayAt(Vector2 world)
        {
            for (int k = 0; k < _tray.Count; k++)
            {
                var b = _tray[k].Renderer.bounds;
                if (world.x >= b.min.x && world.x <= b.max.x && world.y >= b.min.y && world.y <= b.max.y) return k;
            }
            return -1;
        }

        private void ShowTray(bool show)
        {
            if (_trayRoot != null && _trayRoot.gameObject.activeSelf != show) _trayRoot.gameObject.SetActive(show);
        }

        // ---------- leaving placement: everything back exactly ----------

        private void StopPlacing(TowerBuilder tower)
        {
            HoveredSlice = -1;
            ShowTray(false);
            if (!_posed && !_dragging && !_pending) return;
            _pending = _dragging = false;
            _returning = -1;
            _fromSlot = _trayIndex = _target = -1;
            if (_ghost != null) _ghost.gameObject.SetActive(false);
            Unlift();
            if (tower != null) for (int i = 0; i < session.TowerSlices.Count; i++) tower.ResetSlice(i);
            for (int i = 0; i < _jiggle.Length; i++) _jiggle[i] = 0f;
            _posed = false;
        }

        private void Unlift()
        {
            if (_lifted != null) Destroy(_lifted);
            _lifted = null;
        }
    }
}
