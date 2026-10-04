using Piglings.Definitions;
using Piglings.Simulation;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// The barn room's wall pegboard (M10.F, PROTOTYPE_V2.md §5): room_pegboard.png has 12 holes in one row, in 3 groups
    /// of 4 (cream battens between holes 4→5 and 8→9). Group g belongs to the g-th peg type of the campaign (starting
    /// types, then each night's unlock: Bouncy, Bomb, Splitter). An unlocked type hangs one peg per copy owned on its
    /// group's holes (empty holes = the slots still to earn); a locked type shows its 4 pegs as dim ghosts with icon_lock
    /// over the group. No text: the board itself says how many you own. Built once, at Start (copies don't change by day).
    /// Reads only.
    /// </summary>
    public sealed class BarnPegboardView : MonoBehaviour
    {
        [SerializeField] private NightSession session;
        [Tooltip("The pegboard's SpriteRenderer (room_pegboard). Pegs are placed on its holes and drawn above it.")]
        [SerializeField] private SpriteRenderer pegboard;
        [Tooltip("Hole centres in pixels from the PNG's top-left (room_pegboard_holes.txt), hole 1 first. Converted with the " +
                 "sprite's own pivot and pixels per unit, so any import settings work.")]
        [SerializeField] private Vector2[] holePixels =
        {
            new Vector2(44.7f, 88f), new Vector2(102f, 88f), new Vector2(159.3f, 88f), new Vector2(216.7f, 88f),
            new Vector2(274f, 88f), new Vector2(331.3f, 88f), new Vector2(388.7f, 88f), new Vector2(446f, 88f),
            new Vector2(503.3f, 88f), new Vector2(560.7f, 88f), new Vector2(618f, 88f), new Vector2(675.3f, 88f),
        };
        [Tooltip("Holes per peg type (the battens split the 12 holes into 3 groups of 4).")]
        [SerializeField, Min(1)] private int holesPerGroup = 4;
        [Tooltip("A hanging peg's scale (1 = the peg sprite's own size).")]
        [SerializeField, Min(0.01f)] private float pegScale = 1f;
        [Tooltip("Offset of a hanging peg from its hole's centre (world units): a peg hangs a little below the hole.")]
        [SerializeField] private Vector2 pegOffset = Vector2.zero;

        [Header("Locked types")]
        [SerializeField] private Sprite lockIcon;   // icon_lock
        [SerializeField, Min(0.01f)] private float lockScale = 1f;
        [Tooltip("A locked type's ghost pegs: this tint (alpha = how faint).")]
        [SerializeField] private Color lockedTint = new Color(0.15f, 0.12f, 0.12f, 0.45f);

        private void Start()
        {
            if (pegboard == null || pegboard.sprite == null)
            {
                Debug.LogWarning("BarnPegboardView: set Pegboard (a SpriteRenderer with room_pegboard).", this);
                return;
            }
            var types = session.CampaignPegTypes();
            int groups = Mathf.Min(types.Count, holePixels.Length / holesPerGroup);
            for (int g = 0; g < groups; g++)
            {
                var peg = types[g];
                bool unlocked = session.IsPegUnlocked(peg);
                int copies = unlocked ? session.CopiesOwned(peg) : 0;
                var centre = Vector3.zero;
                for (int k = 0; k < holesPerGroup; k++)
                {
                    var hole = HoleWorld(g * holesPerGroup + k);
                    centre += hole;
                    if (unlocked && k < copies) Hang(peg, hole, Color.white, 1);
                    else if (!unlocked) Hang(peg, hole, lockedTint, 1);
                }
                if (!unlocked && lockIcon != null) Place(lockIcon, centre / holesPerGroup, Color.white, lockScale, 3, "Lock");
            }
            if (types.Count * holesPerGroup > holePixels.Length)
                Debug.LogWarning($"BarnPegboardView: the campaign has {types.Count} peg types but the board only " +
                                 $"{holePixels.Length / holesPerGroup} groups — the rest aren't shown.", this);
        }

        // A hole's world position: pixels from the PNG's top-left → the sprite's local units (its pivot, its PPU) → world.
        private Vector3 HoleWorld(int hole)
        {
            var sprite = pegboard.sprite;
            var px = holePixels[hole];
            var local = new Vector3((px.x - sprite.pivot.x) / sprite.pixelsPerUnit,
                                    (sprite.rect.height - px.y - sprite.pivot.y) / sprite.pixelsPerUnit, 0f);
            return pegboard.transform.TransformPoint(local);
        }

        private void Hang(PegDefinition peg, Vector3 hole, Color tint, int orderAbove) =>
            Place(peg.Sprite, hole + (Vector3)pegOffset, tint, pegScale, orderAbove, peg.Id);

        private void Place(Sprite sprite, Vector3 position, Color tint, float scale, int orderAbove, string name)
        {
            if (sprite == null) return;
            var go = new GameObject(name);
            go.transform.SetParent(pegboard.transform, worldPositionStays: false);
            go.transform.position = position;
            // World size = the sprite's own × scale, whatever the pegboard's own scale is.
            float parent = Mathf.Max(0.0001f, Mathf.Abs(pegboard.transform.lossyScale.x));
            go.transform.localScale = Vector3.one * (scale / parent);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = tint;
            sr.sortingLayerID = pegboard.sortingLayerID;
            sr.sortingOrder = pegboard.sortingOrder + orderAbove;
        }
    }
}
