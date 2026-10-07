using System.Collections.Generic;
using Piglings.Definitions;
using UnityEngine;

namespace Piglings.Simulation
{
    /// <summary>
    /// Builds the night's tower from its slices (campaign scene). The bottom piece, the spawn line and the ground stay put
    /// (the tower grows upward); slices stack every Slice Height above First Slice Y, each with a Hold at its layout
    /// points; then the tower top (Barn_Top with the pig, the perch + TopZone + breach slots, DangerZone, the shelf…, all
    /// under one Tower Top parent) moves to sit on the top slice, and the side walls grow to the new height. Last, the
    /// PegBoard re-reads its Holds — the sockets are the built holds, bottom slice first.
    ///
    /// Every position is absolute (computed from the slice count, never nudged from where things were), so building
    /// twice — or previewing in edit mode and then playing — always lands in the same place.
    /// Called by NightSession in Awake (before the sockets are counted) and, in the day phase, when a slice changes.
    /// "Preview tower" (context menu) builds a night's tower in edit mode; preview objects are never saved in the scene.
    /// </summary>
    public sealed class TowerBuilder : MonoBehaviour
    {
        [Tooltip("Where the built slices go (e.g. Barn). Its built children are replaced on every build.")]
        [SerializeField] private Transform container;
        [Tooltip("A slice's look: a SpriteRenderer (sorting as the original slices). Its sprite is replaced by the slice's.")]
        [SerializeField] private SpriteRenderer slicePrefab;
        [Tooltip("One hold: a Hold with its collider (layer Holds) and sprite, as in the original slices.")]
        [SerializeField] private Hold holdPrefab;
        [Tooltip("Re-reads the built holds as sockets after each build.")]
        [SerializeField] private PegBoard board;

        [Header("Layout (world units)")]
        [Tooltip("The first slice's position (its pivot), just above the bottom piece. (3.0)")]
        [SerializeField] private float firstSliceY = 3f;
        [SerializeField, Min(0.1f)] private float sliceHeight = 1.6f;

        [Header("What moves with the height")]
        [Tooltip("Parent of everything on top of the tower: Barn_Top (with the pig), TopZone (perch, breach slots, stone pile), " +
                 "DangerZone, the peg shelf, ChainPopupAnchor… It's placed at First Slice Y + slices × Slice Height + Top Offset.")]
        [SerializeField] private Transform towerTop;
        [Tooltip("Tower Top's y above the top slice's top edge (0 when its pivot sits where Barn_Top was: 6.2 with 2 slices).")]
        [SerializeField] private float topOffset = 0f;
        [Tooltip("The side walls (BoxCollider2D, layer BarnWalls). Their bottom stays put; their top follows the tower.")]
        [SerializeField] private BoxCollider2D[] walls = new BoxCollider2D[0];
        [Tooltip("The walls' bottom edge (world y), e.g. -0.25.")]
        [SerializeField] private float wallBottom = -0.25f;
        [Tooltip("How far the walls reach above Tower Top's position, e.g. 1.25.")]
        [SerializeField] private float wallAboveTop = 1.25f;

        [Header("Edit-mode preview")]
        [SerializeField] private NightDefinition previewNight;

        private readonly List<GameObject> _built = new List<GameObject>();
        private readonly List<SpriteRenderer> _looks = new List<SpriteRenderer>();   // slice i's look (null for a missing slice)

        /// <summary>The top of the built tower (Tower Top's y): the Tower camera frame and the views use it.</summary>
        public float TopY { get; private set; }
        public int SliceCount { get; private set; }

        /// <summary>Slice i's look (its holds are children). Null if out of range. The day phase jiggles and drags it.</summary>
        public SpriteRenderer SliceLook(int index) => index >= 0 && index < _looks.Count ? _looks[index] : null;

        /// <summary>Where slice i's look belongs (world): the day phase brings a jiggled or dragged slice back here.</summary>
        public Vector3 SliceHome(int index) =>
            new Vector3(CentreX, firstSliceY + index * sliceHeight, container != null ? container.position.z : 0f);

        /// <summary>Puts slice i's look back exactly where the build put it (position and rotation).</summary>
        public void ResetSlice(int index)
        {
            var look = SliceLook(index);
            if (look == null) return;
            look.transform.position = SliceHome(index);
            look.transform.localRotation = slicePrefab != null ? slicePrefab.transform.localRotation : Quaternion.identity;
        }

        // The layout, for the day phase's slice picker and its view (TowerLayout does the maths).
        public float SliceHeight => sliceHeight;
        public float CentreX => container != null ? container.position.x : transform.position.x;
        /// <summary>The bottom edge of slice i (world y).</summary>
        public float SliceBottom(int index) => TowerLayout.SliceBottom(index, firstSliceY, sliceHeight);
        /// <summary>The slice under a world point, within <paramref name="halfWidth"/> of the tower's centre; -1 = none.</summary>
        public int SliceAt(Vector2 world, float halfWidth) =>
            TowerLayout.SliceAt(world.x, world.y, CentreX, halfWidth, firstSliceY, sliceHeight, SliceCount);

        public void Build(IReadOnlyList<WallSliceDefinition> slices) => Build(slices, preview: false);

        private void Build(IReadOnlyList<WallSliceDefinition> slices, bool preview)
        {
            Clear();
            if (container == null || slicePrefab == null || holdPrefab == null)
            {
                Debug.LogError("TowerBuilder: set Container, Slice Prefab and Hold Prefab — no tower was built.", this);
                return;
            }

            int expectedHolds = -1;
            for (int i = 0; i < slices.Count; i++)
            {
                var slice = slices[i];
                if (slice == null) { _looks.Add(null); continue; }
                var look = Instantiate(slicePrefab, container);
                _looks.Add(look);
                look.name = $"Slice_{i + 1:00} ({slice.Id})";
                look.transform.position = new Vector3(container.position.x, firstSliceY + i * sliceHeight, container.position.z);
                if (slice.Sprite != null) look.sprite = slice.Sprite;
                Track(look.gameObject, preview);

                foreach (var point in slice.Holds)
                {
                    var hold = Instantiate(holdPrefab, look.transform);
                    hold.transform.localPosition = point;
                    if (slice.HoldMaterial != null && hold.TryGetComponent(out Collider2D collider)) collider.sharedMaterial = slice.HoldMaterial;
                    Track(hold.gameObject, preview);
                }
                if (expectedHolds >= 0 && slice.Holds.Length != expectedHolds)
                    Debug.LogWarning($"{slice.name} has {slice.Holds.Length} holds, the slice below has {expectedHolds}: every slice should " +
                                     "have the same layout (7).", slice);
                expectedHolds = slice.Holds.Length;
            }

            SliceCount = slices.Count;
            TopY = firstSliceY + slices.Count * sliceHeight + topOffset;
            if (towerTop != null) towerTop.position = new Vector3(towerTop.position.x, TopY, towerTop.position.z);
            foreach (var wall in walls) FitWall(wall);

            if (!preview && board != null) board.Rebuild();
        }

        // The wall's bottom stays at Wall Bottom; its top follows the tower. Collider only: the walls are invisible.
        private void FitWall(BoxCollider2D wall)
        {
            if (wall == null) return;
            float top = TopY + wallAboveTop;
            float scaleY = Mathf.Max(0.0001f, wall.transform.lossyScale.y);
            var size = wall.size;
            size.y = (top - wallBottom) / scaleY;
            wall.size = size;
            var offset = wall.offset;
            offset.y = ((top + wallBottom) / 2f - wall.transform.position.y) / scaleY;
            wall.offset = offset;
        }

        // Built objects are owned here and replaced on every build. Unparented before Destroy (which waits for the end of
        // the frame), so the PegBoard re-reading its holds right after never sees the old ones.
        private void Clear()
        {
            foreach (var go in _built)
            {
                if (go == null) continue;
                go.transform.SetParent(null);
                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
            }
            _built.Clear();
            _looks.Clear();

            // A preview left over from before a script reload (this list doesn't survive one): never-saved slices.
            if (container == null) return;
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                var child = container.GetChild(i).gameObject;
                if ((child.hideFlags & HideFlags.DontSave) == 0) continue;
                child.transform.SetParent(null);
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }

        // Only the slices are tracked (their holds go with them); a preview is never saved into the scene.
        private void Track(GameObject go, bool preview)
        {
            if (preview) go.hideFlags = HideFlags.DontSave;
            if (go.GetComponent<Hold>() == null) _built.Add(go);
        }

        [ContextMenu("Preview tower (edit mode)")]
        private void PreviewTower()
        {
            if (Application.isPlaying) { Debug.LogWarning("Preview tower is for edit mode; in play the night builds its own.", this); return; }
            if (previewNight == null) { Debug.LogWarning("Set Preview Night first.", this); return; }
            Build(previewNight.Slices, preview: true);
            Debug.Log($"TowerBuilder: previewing {previewNight.name} — {SliceCount} slices, top at {TopY:0.##}. " +
                      "Tower Top and the walls moved with it (they're scene objects: undo or rebuild to put them back).", this);
        }

        [ContextMenu("Clear preview")]
        private void ClearPreview() => Clear();
    }
}
