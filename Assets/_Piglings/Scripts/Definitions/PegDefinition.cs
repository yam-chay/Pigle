using System.Collections.Generic;
using Piglings.Events;
using UnityEngine;
using UnityEngine.Serialization;

namespace Piglings.Definitions
{
    /// <summary>
    /// One peg type (GDD "שעות הלילה"). Immutable design data. Rules only ever see its id, max level, whether it merges,
    /// its effect and the per-level numbers they need (NightSession turns those into a Rules.PegType); the board stores
    /// the id as a string, never this asset.
    ///
    /// R4: the effect is one class per effect (PegEffectSettings, [SerializeReference]) with only its own fields and its own
    /// levels: pick Effect, see that effect's settings. Callers read it through the getters below, never the classes.
    /// </summary>
    [CreateAssetMenu(menuName = "Piglings/Peg Definition", fileName = "Peg_")]
    public sealed class PegDefinition : ScriptableObject
    {
        [Tooltip("Save key: stored on the board, and peg stats are saved under it. Never rename it once players have a " +
                 "save — their progress would be orphaned (it'd need a migration in ProfileJson). Lowercase, e.g. peg_bomb.")]
        [SerializeField] private string id = "peg_plain";
        [Tooltip("The name players read (post-run, tips). Empty = made from the id (peg_bouncy → Bouncy).")]
        [SerializeField] private string displayName = "";
        [Tooltip("Throwing a peg onto a placed peg of the same type levels it up, up to this level.")]
        [SerializeField, Min(1)] private int maxLevel = 3;
        [Tooltip("Off: a placed peg of this type can't be levelled up — same-type pegs need their own sockets.")]
        [SerializeField] private bool mergeable = true;
        [Tooltip("How it looks on the shelf, in the hand and on a Hold (160×160, pivot Center). Empty = the plain hold sprite, tinted.")]
        [SerializeField] private Sprite sprite;
        [Tooltip("Used only when Sprite is empty, so a peg without art still reads as different from a plain hold.")]
        [SerializeField] private Color fallbackTint = new Color(0.6f, 0.9f, 1f);

        [Header("Effect")]
        [Tooltip("What it does when something hits it. Changing it replaces the settings below with a fresh set for the new " +
                 "effect (the old effect's values are not kept).")]
        [SerializeField] private PegEffect effect = PegEffect.Plain;
        [Tooltip("The picked effect's own settings and levels (entry 0 = level 1; a level past the list uses the last entry).")]
        [SerializeReference] private PegEffectSettings settings;

        [Header("Progression (campaign)")]
        [Tooltip("Peg mastery for each extra copy, cumulative and rising: an unlocked type owns 1 copy, +1 per threshold, up to " +
                 "Max Copies (so Max Copies − 1 entries). Mastery = its effect's triggers × the level's Mastery Weight.")]
        [SerializeField] private int[] copyThresholds = { 10, 30, 60, 100, 150, 210, 280, 360, 450 };
        [Tooltip("The most copies of this type a player can own.")]
        [SerializeField, Min(1)] private int maxCopies = 10;
        [Tooltip("One follow-up throw per this many copies: in an hour's round, placing one of this type then gives that many " +
                 "more throws, each of this same type (3 → 3 copies = 1, 6 = 2, 9 = 3: with 10 copies, 4 in a row). Once per " +
                 "round, never across types. 0 = never.")]
        [FormerlySerializedAs("followUpAtCopies")]
        [SerializeField, Min(0)] private int followUpEveryCopies = 3;

        public string Id => id;

        /// <summary>The name players read: Display Name, or the id without "peg_", capitalised.</summary>
        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(displayName)) return displayName;
                string bare = id != null && id.StartsWith("peg_", System.StringComparison.Ordinal) ? id.Substring(4) : id ?? "";
                return bare.Length == 0 ? name : char.ToUpperInvariant(bare[0]) + bare.Substring(1).Replace('_', ' ');
            }
        }

        public int MaxLevel => maxLevel;
        public bool Mergeable => mergeable;
        public Sprite Sprite => sprite;
        public Color FallbackTint => fallbackTint;
        public PegEffect Effect => Settings.Kind;
        public int LevelCount => Settings.LevelCount;

        /// <summary>
        /// The effect's settings. Never null: an asset whose settings are missing or don't match its Effect (edited outside the
        /// Inspector) plays with a fresh set for its Effect — the editor writes one in on the next validate.
        /// </summary>
        public PegEffectSettings Settings
        {
            get
            {
                if (settings != null && settings.Kind == effect) return settings;
                if (_fallback == null || _fallback.Kind != effect) _fallback = PegEffectSettings.Create(effect);
                return _fallback;
            }
        }
        [System.NonSerialized] private PegEffectSettings _fallback;

        /// <summary>Bomb: how it looks while spent (after going off, until it recharges). Null for other effects.</summary>
        public Sprite SpentSprite => (Settings as BombEffect)?.spentSprite;

        /// <summary>What it adds to its chain's mult when it triggers at this level (M10.S; 0 = none — always for Plain).</summary>
        public float MultBonusAt(int level) => (Settings.LevelAt(level) as SpecialPegLevel)?.multBonus ?? 0f;

        /// <summary>Its SCORE value at this level (M10.S): Plain per contact, special per trigger; -1 = the default (see PegEffectLevel).</summary>
        public int ScoreValueAt(int level) => Settings.LevelAt(level)?.scoreValue ?? -1;

        /// <summary>Bouncy: the peg's physical bounciness at this level (0 = the hold's own material, and for other effects).</summary>
        public float BouncinessAt(int level) => (Settings as BouncyEffect)?.Level(level)?.bounciness ?? 0f;

        /// <summary>Splitter: how many stones a stone becomes at this level, itself included (2 = one new piece; 1 = no split).</summary>
        public int PiecesAt(int level) => (Settings as SplitterEffect)?.Level(level)?.pieces ?? 1;

        /// <summary>Splitter: degrees between the outermost pieces' directions.</summary>
        public float FanAngleAt(int level) => (Settings as SplitterEffect)?.Level(level)?.fanAngle ?? 0f;

        /// <summary>Bomb: climbing robots within this many world units of it are knocked loose.</summary>
        public float BombRadiusAt(int level) => (Settings as BombEffect)?.Level(level)?.bombRadius ?? 0f;

        /// <summary>Bomb: seconds it stays spent after going off (counted only while the wall moves).</summary>
        public float CooldownAt(int level) => (Settings as BombEffect)?.Level(level)?.cooldownSeconds ?? 0f;

        /// <summary>Bomb: the push given to falling balls and flying stones in its radius (0 = none).</summary>
        public float ImpulseAt(int level) => (Settings as BombEffect)?.Level(level)?.impulse ?? 0f;

        public IReadOnlyList<int> CopyThresholds => copyThresholds;
        public int MaxCopies => maxCopies;
        public int FollowUpEveryCopies => followUpEveryCopies;

        /// <summary>Peg mastery weight of one trigger at this level: the entry's Mastery Weight, or the level itself when it's 0.</summary>
        public float MasteryWeightAt(int level)
        {
            var entry = level >= 1 && level <= LevelCount ? Settings.LevelAt(level) : null;
            return entry != null && entry.masteryWeight > 0f ? entry.masteryWeight : level;
        }

        /// <summary>Splitter: most stones one throw can have flying at once (4 for other effects — never read).</summary>
        public int MaxStonesPerThrow => (Settings as SplitterEffect)?.maxStonesPerThrow ?? 4;
        /// <summary>Splitter: a piece's size × the stone's (1 for other effects).</summary>
        public float PieceScale => (Settings as SplitterEffect)?.pieceScale ?? 1f;
        /// <summary>Splitter: pieces' direct hits count for the stone's mastery (true for other effects — never read).</summary>
        public bool CountSplitHitsForMastery => (Settings as SplitterEffect)?.countSplitHitsForMastery ?? true;
        /// <summary>Plain: seconds before the same hitter scores on the same hold again (0.2 for other effects — never read).</summary>
        public float ContactCooldown => (Settings as PlainEffect)?.contactCooldown ?? 0.2f;

#if UNITY_EDITOR
        // Editor only: a new asset, or a changed Effect, gets a fresh settings class for it (one sensible level). SetDirty so
        // the next Save Project keeps it.
        private void OnValidate()
        {
            if (settings != null && settings.Kind == effect) return;
            settings = PegEffectSettings.Create(effect);
            _fallback = null;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
