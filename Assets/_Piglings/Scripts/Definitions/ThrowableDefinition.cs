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

        [Tooltip("The stone's evolutions, in order (entry 0 = level 1): from Stones Needed stones the stone is that level — its " +
                 "look, radius, trail and hourly Refill. Stones Needed must rise. Empty = one level that looks like its prefab, refill 1. " +
                 "Default plan: 10 → lv1 +1 · 15 → lv2 +2 · 20 → lv3 +3 · 25 → lv4 +4.")]
        [SerializeField] private List<ThrowableLevel> levels = new List<ThrowableLevel>();

        [Header("Stone progression (mastery from use)")]
        [Tooltip("Total direct hits (saved, across nights) for each +1 stone, cumulative and strictly rising. With Start 10 and " +
                 "Max 20, ten entries take the stone all the way. Derived from the saved hits, never stored: retune any time.")]
        [SerializeField] private int[] stoneThresholds = { 10, 25, 45, 70, 100, 140, 190, 250, 320, 400 };
        [Tooltip("Stones on the pile at the start of a campaign night, before any threshold.")]
        [SerializeField, Min(0)] private int startStones = 10;
        [Tooltip("No more stones past this, whatever the hits.")]
        [SerializeField, Min(1)] private int maxStones = 25;

        public string Id => id;
        public float Radius => radius;
        public float Mass => mass;
        public float Lifetime => lifetime;
        public PhysicsMaterial2D Material => material;

        public int LevelCount => Mathf.Max(1, levels.Count);

        public IReadOnlyList<int> StoneThresholds => stoneThresholds;
        public int StartStones => startStones;
        public int MaxStones => maxStones;
        /// <summary>The evolutions (entry 0 = level 1): look + Stones Needed + Refill. NightSession turns them into Meta's rule.</summary>
        public IReadOnlyList<ThrowableLevel> Levels => levels;

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

    /// <summary>One evolution of a weapon: from how many stones it's this level, how it looks, and its hourly refill.</summary>
    [System.Serializable]
    public sealed class ThrowableLevel
    {
        [Tooltip("From this many stones on the pile the stone is this level. Must rise from entry to entry.")]
        [Min(0)] public int stonesNeeded = 10;
        [Tooltip("Stones added at each hour's placement round at this level (campaign).")]
        [Min(0)] public int refill = 1;
        [Tooltip("The weapon's sprite at this level (the stone should fill the canvas). Empty = the level before's.")]
        public Sprite sprite;
        [Tooltip("× Radius. Collider and sprite scale together. The pile's spacing grows with it too.")]
        [Min(0.1f)] public float radiusScale = 1f;
        [Tooltip("The flight trail's colour at this level (M9.3).")]
        public Color trailColour = Color.white;
    }
}
