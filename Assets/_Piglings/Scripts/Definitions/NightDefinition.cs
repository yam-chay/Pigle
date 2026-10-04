using System.Collections.Generic;
using UnityEngine;

namespace Piglings.Definitions
{
    /// <summary>
    /// What one night contains: its robots, wall, stones, the hours until dawn and the pegs it brings.
    /// NightSession turns it into the Rules' plain values (NightGoal, PegSetup).
    /// </summary>
    [CreateAssetMenu(menuName = "Piglings/Night Definition", fileName = "Night_")]
    public sealed class NightDefinition : ScriptableObject
    {
        [Tooltip("Save key (campaign): dawns and tower choices are saved by this id. Never rename it once players have a save.")]
        [SerializeField] private string id = "night_01";
        [SerializeField] private RobotDefinition robot;
        [SerializeField] private ThrowableDefinition throwable;
        [SerializeField] private WallMaterialDefinition wall;
        [SerializeField, Min(0.1f)] private float spawnInterval = 1.6f;
        [Tooltip("Stones on the pile when the night starts. They're the pig's life: a robot that breaches takes one, " +
                 "and a robot that breaches when there are none left catches the pigs.")]
        [SerializeField, Min(1)] private int throwsAvailable = 30;

        [Header("Hours until dawn")]
        [Tooltip("Score thresholds, rising (5 = five hours). Crossing one starts the next hour — a higher score multiplier, " +
                 "a stone refill and a peg-placement round. Crossing the last one is dawn: the night is won. " +
                 "A value not above the one before is raised to it + 1.")]
        [SerializeField] private int[] thresholds = { 500, 1500, 3000, 5000, 8000 };
        [Tooltip("Stones added to the pile at each hour's placement round (0 = none). Not at dawn.")]
        [SerializeField, Min(0)] private int stonesPerThreshold = 0;
        [Tooltip("How long a placement round lasts when nothing can be placed (empty shelf, no valid socket): " +
                 "long enough to watch the refill land on the pile.")]
        [SerializeField, Min(0f)] private float refillPauseSeconds = 1f;

        [Header("Tower (campaign scene: TowerBuilder)")]
        [Tooltip("The tower, bottom → top: one slice per 1.6 units above the bottom piece. The player can swap them in the day phase; " +
                 "the count is the night's (more slices = a taller tower, a longer climb).")]
        [SerializeField] private List<WallSliceDefinition> slices = new List<WallSliceDefinition>();
        [Tooltip("Override for the camera during this night: centre height. 0 = auto — CameraDirector fits the Night frame to " +
                 "the built tower (fixed bottom, top at the roof).")]
        [SerializeField] private float cameraY = 0f;
        [Tooltip("Override: orthographic size. 0 = auto (fitted to the tower, see Camera Y).")]
        [SerializeField, Min(0f)] private float cameraSize = 0f;

        [Header("Pegs")]
        [Tooltip("The peg shelf for this night (until the day phase chooses it): up to 5 types, one pile each. " +
                 "Types past the 5th are ignored; the same type twice adds up.")]
        [SerializeField] private List<PegLoadoutEntry> pegLoadout = new List<PegLoadoutEntry>();
        [Tooltip("Pegs the player throws at each threshold. (Future: a skill-tree node adds +1.)")]
        [SerializeField, Min(0)] private int pegThrowsPerThreshold = 1;

        public string Id => id;
        public IReadOnlyList<WallSliceDefinition> Slices => slices;
        public float CameraY => cameraY;
        public float CameraSize => cameraSize;
        public RobotDefinition Robot => robot;
        public ThrowableDefinition Throwable => throwable;
        public WallMaterialDefinition Wall => wall;
        public float SpawnInterval => spawnInterval;
        public int ThrowsAvailable => throwsAvailable;
        public IReadOnlyList<int> Thresholds => thresholds;
        public int StonesPerThreshold => stonesPerThreshold;
        public float RefillPauseSeconds => refillPauseSeconds;
        public IReadOnlyList<PegLoadoutEntry> PegLoadout => pegLoadout;
        public int PegThrowsPerThreshold => pegThrowsPerThreshold;
    }

    /// <summary>One pile on the peg shelf: a peg type and how many of it the night starts with.</summary>
    [System.Serializable]
    public sealed class PegLoadoutEntry
    {
        public PegDefinition peg;
        [Min(1)] public int count = 1;
    }
}
