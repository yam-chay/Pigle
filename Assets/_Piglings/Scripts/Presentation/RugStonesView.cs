using Piglings.Simulation;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// The barn room's rug (M10.F, PROTOTYPE_V2.md §5): tonight's stones as a real pile — exactly as many as the night
    /// starts with, in the same pyramid as the perch's pile (PileLayout), at the stone's level and size — plus ONE stone set
    /// apart with a badge: the hourly refill (Refill Badges: +1, +2, +3, +4 — the evolutions raise it). Every stone shows the
    /// level's sprite. No text: the pile is the number. Built once, at Start. Reads only.
    /// </summary>
    public sealed class RugStonesView : MonoBehaviour
    {
        [SerializeField] private NightSession session;
        [Tooltip("Where the pile's bottom row is centred (on the rug). Stones are drawn under this object.")]
        [SerializeField] private Transform anchor;
        [Tooltip("Sorting for the stones (the badge goes one above).")]
        [SerializeField] private string sortingLayer = "Default";
        [SerializeField] private int sortingOrder = 10;

        [Header("Pile (multiples of a stone's diameter, so an evolved stone spreads it out)")]
        [SerializeField, Min(1)] private int bottomRow = 6;
        [SerializeField, Min(0.1f)] private float spacing = 0.95f;
        [SerializeField, Min(0.1f)] private float rowHeight = 0.8f;
        [Tooltip("A stone's size on the rug × its size in play (1 = the same).")]
        [SerializeField, Min(0.1f)] private float stoneScale = 1f;

        [Header("The refill stone, set apart")]
        [Tooltip("Where it sits, from the anchor (world units).")]
        [SerializeField] private Vector2 apartOffset = new Vector2(0.6f, 0f);
        [Tooltip("The refill badge per refill amount: entry 0 = +1, entry 1 = +2… A refill past the list uses the last badge.")]
        [SerializeField] private Sprite[] refillBadges = new Sprite[0];
        [Tooltip("The badge's offset from the refill stone (world units) and its scale.")]
        [SerializeField] private Vector2 badgeOffset = new Vector2(0.06f, 0.06f);
        [SerializeField, Min(0.01f)] private float badgeScale = 1f;

        private void Start()
        {
            if (anchor == null) { Debug.LogWarning("RugStonesView: set Anchor (where the pile sits on the rug).", this); return; }
            var throwable = session.Night.Throwable;
            var stone = session.StoneAtStart;
            var sprite = throwable.SpriteFor(stone.Level);
            if (sprite == null) { Debug.LogWarning($"RugStonesView: {throwable.name} has no sprite for level {stone.Level}.", this); return; }

            float diameter = 2f * throwable.RadiusAt(stone.Level) * stoneScale;
            for (int i = 0; i < stone.Stones; i++)
            {
                PileLayout.Pyramid(i, bottomRow, spacing * diameter, rowHeight * diameter, out float x, out float y);
                Place(sprite, anchor.position + new Vector3(x, y + diameter / 2f, 0f), diameter, sortingOrder + i, "Stone");
            }

            var apart = anchor.position + (Vector3)apartOffset + new Vector3(0f, diameter / 2f, 0f);
            Place(sprite, apart, diameter, sortingOrder, "Refill stone");
            var badge = refillBadges.Length > 0 ? refillBadges[Mathf.Clamp(stone.Refill - 1, 0, refillBadges.Length - 1)] : null;
            if (stone.Refill > 0 && badge != null)
                PlaceScaled(badge, apart + (Vector3)badgeOffset, badgeScale, sortingOrder + stone.Stones + 1, "Refill badge");
        }

        // A stone sprite exactly `diameter` wide in the world (like Throwable.ApplyLevel: the sprite matches the stone's size).
        private void Place(Sprite sprite, Vector3 position, float diameter, int order, string name)
        {
            float width = Mathf.Max(0.0001f, sprite.bounds.size.x);
            PlaceScaled(sprite, position, diameter / width, order, name);
        }

        private void PlaceScaled(Sprite sprite, Vector3 position, float worldScale, int order, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(anchor, worldPositionStays: false);
            go.transform.position = position;
            float parent = Mathf.Max(0.0001f, Mathf.Abs(anchor.lossyScale.x));
            go.transform.localScale = Vector3.one * (worldScale / parent);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = sortingLayer;
            sr.sortingOrder = order;
        }
    }
}
