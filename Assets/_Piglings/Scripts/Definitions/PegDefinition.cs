using System.Collections.Generic;
using Piglings.Events;
using UnityEngine;

namespace Piglings.Definitions
{
    /// <summary>
    /// One peg type (GDD "שעות הלילה"). Immutable design data. Rules only ever see its id, max level, whether it merges,
    /// its effect and the per-level numbers they need (NightSession turns those into a Rules.PegType); the board stores
    /// the id as a string, never this asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Piglings/Peg Definition", fileName = "Peg_")]
    public sealed class PegDefinition : ScriptableObject
    {
        [Tooltip("Save key: stored on the board, and peg stats are saved under it. Never rename it once players have a " +
                 "save — their progress would be orphaned (it'd need a migration in ProfileJson). Lowercase, e.g. peg_bomb.")]
        [SerializeField] private string id = "peg_plain";
        [Tooltip("Throwing a peg onto a placed peg of the same type levels it up, up to this level.")]
        [SerializeField, Min(1)] private int maxLevel = 3;
        [Tooltip("Off: a placed peg of this type can't be levelled up — same-type pegs need their own sockets.")]
        [SerializeField] private bool mergeable = true;
        [Tooltip("How it looks on the shelf, in the hand and on a Hold (160×160, pivot Center). Empty = the plain hold sprite, tinted.")]
        [SerializeField] private Sprite sprite;
        [Tooltip("Used only when Sprite is empty, so a peg without art still reads as different from a plain hold.")]
        [SerializeField] private Color fallbackTint = new Color(0.6f, 0.9f, 1f);

        [Tooltip("What it does when something hits it. Plain = nothing (a hold with a look).")]
        [SerializeField] private PegEffect effect = PegEffect.Plain;
        [Tooltip("Strength per level (merges level a peg up). Entry 0 = level 1; a level past the list uses the last entry. " +
                 "Each effect reads only its own fields. A new first entry starts all zeros in the Inspector — fill every field.")]
        [SerializeField] private List<PegLevel> levels = new List<PegLevel>();

        [Header("Splitter (whatever the level)")]
        [Tooltip("Most stones one throw can have flying at once, the original included. Splits beyond it are cut short.")]
        [SerializeField, Min(1)] private int maxStonesPerThrow = 4;
        [Tooltip("A piece's size (collider and sprite) × the stone's. Gameplay: smaller pieces are harder to hit with.")]
        [SerializeField, Range(0.3f, 1f)] private float pieceScale = 0.8f;
        [Tooltip("On: robots a piece knocks loose itself count as stone hits for mastery, like the stone's own.")]
        [SerializeField] private bool countSplitHitsForMastery = true;

        public string Id => id;
        public int MaxLevel => maxLevel;
        public bool Mergeable => mergeable;
        public Sprite Sprite => sprite;
        public Color FallbackTint => fallbackTint;
        public PegEffect Effect => effect;
        public int LevelCount => levels.Count;

        /// <summary>This level's numbers; a level past the list uses the last entry. Null when the list is empty.</summary>
        public PegLevel Level(int level) => levels.Count == 0 ? null : levels[Mathf.Clamp(level - 1, 0, levels.Count - 1)];

        /// <summary>Bouncy: × what a ball's victims score after bouncing off it, per level (1 = no bonus).</summary>
        public float ScoreMultiplierAt(int level) => Level(level)?.scoreMultiplier ?? 1f;

        /// <summary>Bouncy: the peg's physical bounciness at this level (0 = the hold's own material).</summary>
        public float BouncinessAt(int level) => Level(level)?.bounciness ?? 0f;

        /// <summary>Splitter: how many stones a stone becomes at this level, itself included (2 = one new piece).</summary>
        public int PiecesAt(int level) => Level(level)?.pieces ?? 1;

        /// <summary>Splitter: degrees between the outermost pieces' directions.</summary>
        public float FanAngleAt(int level) => Level(level)?.fanAngle ?? 0f;

        /// <summary>Splitter: × the stone's value each stone carries after the split (1 = each carries it in full).</summary>
        public float ValueShareAt(int level) => Level(level)?.valueShare ?? 1f;

        public int MaxStonesPerThrow => maxStonesPerThrow;
        public float PieceScale => pieceScale;
        public bool CountSplitHitsForMastery => countSplitHitsForMastery;
    }

    /// <summary>
    /// One level of a peg's effect. Each effect reads only its own fields; the rest are ignored.
    /// Numbers are placeholders — balance comes later.
    /// </summary>
    [System.Serializable]
    public sealed class PegLevel
    {
        [Header("Bouncy")]
        [Tooltip("× what a falling ball's victims score after it bounces off this peg (2 = double). Not what they carry on. " +
                 "Several Bouncy pegs add their extra part: two ×2 pegs = ×3.")]
        [Min(1f)] public float scoreMultiplier = 2f;
        [Tooltip("Physical bounciness of the peg (0..1+). Balls and stones visibly pop off it. 0 = the hold's own material.")]
        [Min(0f)] public float bounciness = 0.8f;

        [Header("Splitter")]
        [Tooltip("A thrown stone that hits the needle becomes this many stones, itself included (2, 3…). Robot balls never split.")]
        [Min(1)] public int pieces = 2;
        [Tooltip("Degrees between the outermost pieces, fanned around the stone's direction after the bounce. Same speed.")]
        [Range(0f, 180f)] public float fanAngle = 30f;
        [Tooltip("Each stone (the original too) carries the stone's value × this. 1 = every piece carries it in full.")]
        [Min(0f)] public float valueShare = 1f;
    }
}
