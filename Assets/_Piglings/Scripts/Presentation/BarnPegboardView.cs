using System.Globalization;
using Piglings.Definitions;
using Piglings.Simulation;
using UnityEngine;
using UnityEngine.Serialization;

namespace Piglings.Presentation
{
    /// <summary>
    /// The barn room's wall pegboard (M10.F; stage 2 PR P): a group of slots per peg type of the campaign, in campaign order
    /// (starting types, then each night's unlock: Bouncy, Bomb, Splitter). v2 board: 8 slots per type in two rows of 4;
    /// the second row only shows once the type owns more than 4 copies. An unlocked type hangs one peg per copy owned (empty
    /// slots = copies still to earn); a locked type shows its first row as dim ghosts under icon_lock. No text: the board
    /// itself says how many you own. Built once, at Start (copies don't change by day). Reads only.
    ///
    /// Hole positions: the Holes File (one "x,y" per line, pixels from the PNG's top-left, group order type 1 row 1,
    /// type 1 row 2, type 2 row 1…), else the Hole Pixels list. With a Hole Sprite the code draws a hole at every slot that
    /// shows (use it with board art that has no holes painted); without one the art's own holes are the slots.
    /// </summary>
    public sealed class BarnPegboardView : MonoBehaviour
    {
        [SerializeField] private NightSession session;
        [Tooltip("The pegboard's SpriteRenderer (room_pegboard). Pegs are placed on its holes and drawn above it.")]
        [SerializeField] private SpriteRenderer pegboard;
        [Tooltip("The board's holes file (room_pegboard_holes.txt): one \"x,y\" per line, pixels from the PNG's top-left. " +
                 "Wins over Hole Pixels when set.")]
        [SerializeField] private TextAsset holesFile;
        [Tooltip("Hole centres in pixels from the PNG's top-left, hole 1 first (the v1 board's 12; used without a Holes File). " +
                 "Converted with the sprite's own pivot and pixels per unit, so any import settings work.")]
        [SerializeField] private Vector2[] holePixels =
        {
            new Vector2(44.7f, 88f), new Vector2(102f, 88f), new Vector2(159.3f, 88f), new Vector2(216.7f, 88f),
            new Vector2(274f, 88f), new Vector2(331.3f, 88f), new Vector2(388.7f, 88f), new Vector2(446f, 88f),
            new Vector2(503.3f, 88f), new Vector2(560.7f, 88f), new Vector2(618f, 88f), new Vector2(675.3f, 88f),
        };
        [Tooltip("Slots per peg type, in the holes' order (v2 board: 8; the v1 board: 4).")]
        [FormerlySerializedAs("holesPerGroup")]
        [SerializeField, Min(1)] private int slotsPerType = 8;
        [Tooltip("Slots per row: a row past the first only shows once the type owns more copies than the rows above hold.")]
        [SerializeField, Min(1)] private int slotsPerRow = 4;
        [Tooltip("A hanging peg's scale (1 = the peg sprite's own size).")]
        [SerializeField, Min(0.01f)] private float pegScale = 1f;
        [Tooltip("Offset of a hanging peg from its hole's centre (world units): a peg hangs a little below the hole.")]
        [SerializeField] private Vector2 pegOffset = Vector2.zero;
        [Tooltip("Optional: a hole drawn at every slot that shows (for board art without holes). Empty = the art's own holes.")]
        [SerializeField] private Sprite holeSprite;
        [SerializeField, Min(0.01f)] private float holeScale = 1f;

        [Header("Locked types")]
        [SerializeField] private Sprite lockIcon;   // icon_lock
        [SerializeField, Min(0.01f)] private float lockScale = 1f;
        [Tooltip("A locked type's ghost pegs: this tint (alpha = how faint).")]
        [SerializeField] private Color lockedTint = new Color(0.15f, 0.12f, 0.12f, 0.45f);

        private Vector2[] _holes;

        private void Start()
        {
            if (pegboard == null || pegboard.sprite == null)
            {
                Debug.LogWarning("BarnPegboardView: set Pegboard (a SpriteRenderer with room_pegboard).", this);
                return;
            }
            _holes = holesFile != null ? ParseHoles(holesFile.text) : holePixels;
            var types = session.CampaignPegTypes();
            int groups = Mathf.Min(types.Count, _holes.Length / slotsPerType);
            for (int g = 0; g < groups; g++)
            {
                var peg = types[g];
                bool unlocked = session.IsPegUnlocked(peg);
                int copies = unlocked ? session.CopiesOwned(peg) : 0;
                var firstRowCentre = Vector3.zero;
                int firstRow = Mathf.Min(slotsPerRow, slotsPerType);
                for (int k = 0; k < slotsPerType; k++)
                {
                    int row = k / slotsPerRow;
                    var hole = HoleWorld(g * slotsPerType + k);
                    if (row == 0) firstRowCentre += hole;
                    // A row past the first shows once the rows above are full of copies.
                    bool shows = row == 0 || copies > row * slotsPerRow;
                    if (!shows) continue;
                    if (holeSprite != null) Place(holeSprite, hole, Color.white, holeScale, 1, "Hole");
                    if (unlocked && k < copies) Hang(peg, hole, Color.white, 2);
                    else if (!unlocked && row == 0) Hang(peg, hole, lockedTint, 2);
                }
                if (!unlocked && lockIcon != null) Place(lockIcon, firstRowCentre / firstRow, Color.white, lockScale, 4, "Lock");
            }
            CheckCopies();
            if (types.Count * slotsPerType > _holes.Length)
                Debug.LogWarning($"BarnPegboardView: the campaign has {types.Count} peg types but the board has holes for only " +
                                 $"{_holes.Length / slotsPerType} ({_holes.Length} holes, {slotsPerType} per type) — the rest aren't shown.", this);
        }

        // A copy past Slots Per Type has no hole to hang on, so the board would show fewer copies than the player owns.
        // Warned in the editor (on this component's edit — Max Copies lives on each peg) and once at play start.
        private void OnValidate() => CheckCopies();

        private void CheckCopies()
        {
            if (session == null) return;
            foreach (var peg in session.CampaignPegTypes())
                if (peg.MaxCopies > slotsPerType)
                    Debug.LogWarning($"BarnPegboardView: {peg.name} ▸ Max Copies is {peg.MaxCopies} but the pegboard has {slotsPerType} " +
                                     "slots per type — copies past the slots won't show. Lower Max Copies or raise Slots Per Type.", this);
        }

        // "x,y" per line (spaces, tabs or ';' also separate); blank and unreadable lines are skipped.
        private Vector2[] ParseHoles(string text)
        {
            var holes = new System.Collections.Generic.List<Vector2>();
            foreach (var raw in text.Split('\n'))
            {
                var parts = raw.Trim().Split(new[] { ',', ';', ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;
                if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                    float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
                    holes.Add(new Vector2(x, y));
            }
            if (holes.Count == 0) Debug.LogWarning($"BarnPegboardView: no \"x,y\" lines in {holesFile.name}.", this);
            return holes.ToArray();
        }

        // A hole's world position: pixels from the PNG's top-left → the sprite's local units (its pivot, its PPU) → world.
        private Vector3 HoleWorld(int hole)
        {
            var sprite = pegboard.sprite;
            var px = _holes[hole];
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
