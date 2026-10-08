using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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

        [Tooltip("The stone's evolutions, in order (entry 0 = evolution 1): from its At Level on, the stone has that look, " +
                 "radius, trail, base score and hourly Refill. At Level must rise (the first is usually 1). Empty = one evolution " +
                 "that looks like its prefab, refill 1.")]
        // (Was "levels" before R2; Throwable_Stone was re-saved as "evolutions" in R2-b, and "levels" is now the hit list.)
        [SerializeField] private List<ThrowableEvolution> evolutions = new List<ThrowableEvolution>();

        [Header("Stone progression (mastery from use)")]
        [Tooltip("Stones on the pile at stone level 1 — the start of a campaign night before any level is earned.")]
        [SerializeField, Min(0)] private int startStones = 10;
        [Tooltip("The LEVELS: total direct hits (saved, across nights) for each stone level after the first, cumulative and " +
                 "strictly rising. Each level is +1 stone; the last entry is the cap (most stones = Start Stones + entries). " +
                 "Derived from the saved hits, never stored: retune any time.")]
        [FormerlySerializedAs("stoneLevels")]
        [SerializeField] private int[] levels = { 10, 25, 45, 70, 100, 140, 190, 250, 320, 400 };

        public string Id => id;
        public float Radius => radius;
        public float Mass => mass;
        public float Lifetime => lifetime;
        public PhysicsMaterial2D Material => material;

        public int EvolutionCount => Mathf.Max(1, evolutions.Count);

        /// <summary>The Levels: hits for each stone level after the first (the last = the cap).</summary>
        public IReadOnlyList<int> Levels => levels;
        public int StartStones => startStones;
        /// <summary>The evolutions (entry 0 = evolution 1): look + At Level + Refill + Base Score. NightSession turns them into
        /// Meta's rule.</summary>
        public IReadOnlyList<ThrowableEvolution> Evolutions => evolutions;

        // "level" below = the evolution number (1-based) — what the views call the weapon level (NightSession.WeaponLevel).

        /// <summary>This evolution's sprite; one without uses the one before. Null = keep the prefab's sprite.</summary>
        public Sprite SpriteFor(int level)
        {
            for (int i = Index(level); i >= 0; i--)
                if (evolutions[i] != null && evolutions[i].sprite != null) return evolutions[i].sprite;
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

        // An evolution past the list (a missing entry) falls back to the last entry there is.
        private ThrowableEvolution Entry(int level)
        {
            int i = Index(level);
            return i >= 0 ? evolutions[i] : null;
        }

        private int Index(int level) => evolutions.Count == 0 ? -1 : Mathf.Clamp(level - 1, 0, evolutions.Count - 1);
    }

    /// <summary>One evolution of a weapon: the stone level it unlocks at, how it looks, its base score and its hourly refill.
    /// (Was ThrowableLevel, keyed by Stones Needed — R2.)</summary>
    [System.Serializable]
    public sealed class ThrowableEvolution
    {
        [Tooltip("The stone level this evolution unlocks at (1 = from the start; e.g. evolution 2 at level 4). Must rise from " +
                 "entry to entry. Stones at a level = Start Stones + level − 1.")]
        [Min(1)] public int atLevel = 1;
        [Tooltip("Stones added at each hour's placement round at this level (campaign).")]
        [Min(0)] public int refill = 1;
        [Tooltip("M10.S: a throw at this level starts its chain's SCORE with this (10 / 20 / 40 / 80).")]
        [Min(0)] public int baseScore = 10;
        [Tooltip("The weapon's sprite at this level (the stone should fill the canvas). Empty = the level before's.")]
        public Sprite sprite;
        [Tooltip("× Radius. Collider and sprite scale together. The pile's spacing grows with it too.")]
        [Min(0.1f)] public float radiusScale = 1f;
        [Tooltip("The flight trail's colour at this level (M9.3).")]
        public Color trailColour = Color.white;
    }
}
