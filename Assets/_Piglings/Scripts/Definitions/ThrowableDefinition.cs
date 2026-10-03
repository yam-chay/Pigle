using System.Collections.Generic;
using UnityEngine;

namespace Piglings.Definitions
{
    [CreateAssetMenu(menuName = "Piglings/Throwable Definition", fileName = "Throwable_")]
    public sealed class ThrowableDefinition : ScriptableObject
    {
        [Tooltip("Save key: the player's mastery for this weapon is saved under this id. Never rename it once players " +
                 "have a save — their progress would be orphaned (it'd need a migration in ProfileJson).")]
        [SerializeField] private string id = "stone";
        [Tooltip("World radius at level 1. A level's Radius Scale multiplies it; the sprite is sized to match.")]
        [SerializeField, Min(0.01f)] private float radius = 0.08f;
        [SerializeField, Min(0.01f)] private float mass = 1f;
        [SerializeField, Min(0f)] private float lifetime = 6f;             // safety cleanup
        [SerializeField] private PhysicsMaterial2D material;

        [Tooltip("Mastery levels, from use. Entry 0 = level 1 (its Hits Required is ignored). Hits Required are cumulative " +
                 "totals and must rise strictly (e.g. 50, 150). The level is derived from the saved hits, never stored, so " +
                 "these can be retuned any time. Empty = the weapon has one level and looks like its prefab.")]
        [SerializeField] private List<ThrowableLevel> levels = new List<ThrowableLevel>();

        public string Id => id;
        public float Radius => radius;
        public float Mass => mass;
        public float Lifetime => lifetime;
        public PhysicsMaterial2D Material => material;

        public int LevelCount => Mathf.Max(1, levels.Count);

        /// <summary>The hits each level after the first needs (cumulative), for Meta's MasteryLevels. Empty = one level.</summary>
        public int[] Thresholds()
        {
            var t = new int[Mathf.Max(0, levels.Count - 1)];
            for (int i = 0; i < t.Length; i++) t[i] = levels[i + 1] != null ? levels[i + 1].hitsRequired : 0;
            return t;
        }

        /// <summary>This level's sprite; a level without one uses the one before. Null = keep the prefab's sprite.</summary>
        public Sprite SpriteFor(int level)
        {
            for (int i = Index(level); i >= 0; i--)
                if (levels[i] != null && levels[i].sprite != null) return levels[i].sprite;
            return null;
        }

        /// <summary>World radius at this level: Radius × the level's Radius Scale (1 when there's no entry).</summary>
        public float RadiusAt(int level)
        {
            var entry = Entry(level);
            return radius * (entry != null ? Mathf.Max(0.1f, entry.radiusScale) : 1f);
        }

        public Color TrailColourFor(int level)
        {
            var entry = Entry(level);
            return entry != null ? entry.trailColour : Color.white;
        }

        // A level past the list (thresholds changed, or a missing entry) falls back to the last entry there is.
        private ThrowableLevel Entry(int level)
        {
            int i = Index(level);
            return i >= 0 ? levels[i] : null;
        }

        private int Index(int level) => levels.Count == 0 ? -1 : Mathf.Clamp(level - 1, 0, levels.Count - 1);
    }

    /// <summary>One mastery level of a weapon: how many total hits it needs, and how it looks and how big it is.</summary>
    [System.Serializable]
    public sealed class ThrowableLevel
    {
        [Tooltip("Total direct hits (saved, across nights) to reach this level. Ignored for the first entry (level 1).")]
        [Min(0)] public int hitsRequired;
        [Tooltip("The weapon's sprite at this level (the stone should fill the canvas). Empty = the level before's.")]
        public Sprite sprite;
        [Tooltip("× Radius. Collider and sprite scale together. The pile's spacing grows with it too.")]
        [Min(0.1f)] public float radiusScale = 1f;
        [Tooltip("The flight trail's colour at this level (M9.3).")]
        public Color trailColour = Color.white;
    }
}
