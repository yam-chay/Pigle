using UnityEngine;

namespace Piglings.Definitions
{
    /// <summary>
    /// One peg type (GDD "שעות הלילה"). Immutable design data. Rules only ever see its id, max level and whether it
    /// merges (NightSession turns those into a Rules.PegType); the board stores the id as a string, never this asset.
    /// The behaviour (Bouncy first) comes with M8.5.
    /// </summary>
    [CreateAssetMenu(menuName = "Piglings/Peg Definition", fileName = "Peg_")]
    public sealed class PegDefinition : ScriptableObject
    {
        [Tooltip("Stable id, stored on the board and in saves. Never rename it once nights use it.")]
        [SerializeField] private string id = "peg_plain";
        [Tooltip("Throwing a peg onto a placed peg of the same type levels it up, up to this level.")]
        [SerializeField, Min(1)] private int maxLevel = 3;
        [Tooltip("Off: a placed peg of this type can't be levelled up — same-type pegs need their own sockets.")]
        [SerializeField] private bool mergeable = true;
        [Tooltip("How it looks on the shelf, in the hand and on a Hold (160×160, pivot Center). Empty = the plain hold sprite, tinted.")]
        [SerializeField] private Sprite sprite;
        [Tooltip("Used only when Sprite is empty, so a peg without art still reads as different from a plain hold.")]
        [SerializeField] private Color fallbackTint = new Color(0.6f, 0.9f, 1f);

        public string Id => id;
        public int MaxLevel => maxLevel;
        public bool Mergeable => mergeable;
        public Sprite Sprite => sprite;
        public Color FallbackTint => fallbackTint;
    }
}
