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

        [Tooltip("How each level looks: entry 0 = level 1, entry 1 = level 2 (the evolved stone). The level comes from the stone " +
                 "progression below. Empty = one level that looks like its prefab.")]
        [SerializeField] private List<ThrowableLevel> levels = new List<ThrowableLevel>();

        [Header("Stone progression (mastery from use)")]
        [Tooltip("Total direct hits (saved, across nights) for each +1 stone, cumulative and strictly rising. With Start 10 and " +
                 "Max 20, ten entries take the stone all the way. Derived from the saved hits, never stored: retune any time.")]
        [SerializeField] private int[] stoneThresholds = { 10, 25, 45, 70, 100, 140, 190, 250, 320, 400 };
        [Tooltip("Stones on the pile at the start of a campaign night, before any threshold.")]
        [SerializeField, Min(0)] private int startStones = 10;
        [Tooltip("At this many stones the stone evolves: level 2 (its look and radius), and the hourly refill goes up.")]
        [SerializeField, Min(1)] private int evolveAtStones = 15;
        [Tooltip("No more stones past this, whatever the hits.")]
        [SerializeField, Min(1)] private int maxStones = 20;
        [Tooltip("Stones added at each hour's placement round, before / after evolving (campaign).")]
        [SerializeField, Min(0)] private int refill = 1;
        [SerializeField, Min(0)] private int evolvedRefill = 2;

        public string Id => id;
        public float Radius => radius;
        public float Mass => mass;
        public float Lifetime => lifetime;
        public PhysicsMaterial2D Material => material;

        public int LevelCount => Mathf.Max(1, levels.Count);

        public IReadOnlyList<int> StoneThresholds => stoneThresholds;
        public int StartStones => startStones;
        public int EvolveAtStones => evolveAtStones;
        public int MaxStones => maxStones;
        public int Refill => refill;
        public int EvolvedRefill => evolvedRefill;

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

    /// <summary>One level of a weapon: how it looks and how big it is (the level itself comes from the stone progression).</summary>
    [System.Serializable]
    public sealed class ThrowableLevel
    {
        [Tooltip("The weapon's sprite at this level (the stone should fill the canvas). Empty = the level before's.")]
        public Sprite sprite;
        [Tooltip("× Radius. Collider and sprite scale together. The pile's spacing grows with it too.")]
        [Min(0.1f)] public float radiusScale = 1f;
        [Tooltip("The flight trail's colour at this level (M9.3).")]
        public Color trailColour = Color.white;
    }
}
