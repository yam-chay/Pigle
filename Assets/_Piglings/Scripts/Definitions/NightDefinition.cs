using System.Collections.Generic;
using UnityEngine;

namespace Piglings.Definitions
{
    /// <summary>
    /// One night's content (R3): its robots and spawn, its wall, the hours until dawn (score targets + hour multipliers) and
    /// its tower's height. What the player has to play it with — slices, pegs, the weapon, peg throws — is the Campaign's
    /// (progression); the stones come from the weapon's progression. NightSession turns it into the Rules' plain values.
    /// </summary>
    [CreateAssetMenu(menuName = "Piglings/Night Definition", fileName = "Night_")]
    public sealed class NightDefinition : ScriptableObject
    {
        [Tooltip("Save key (campaign): dawns and tower choices are saved by this id. Never rename it once players have a save.")]
        [SerializeField] private string id = "night_01";
        [SerializeField] private RobotDefinition robot;
        [SerializeField] private WallMaterialDefinition wall;
        [SerializeField, Min(0.1f)] private float spawnInterval = 1.6f;

        [Header("Hours until dawn")]
        [Tooltip("Score thresholds, rising (5 = five hours). Crossing one starts the next hour — a higher score multiplier, " +
                 "a stone refill and a peg-placement round. Crossing the last one is dawn: the night is won. " +
                 "A value not above the one before is raised to it + 1.")]
        [SerializeField] private int[] thresholds = { 500, 1500, 3000, 5000, 8000 };
        [Tooltip("HOUR: what chains thrown in hour 1 are multiplied by (at least 1). Balanced against the thresholds above — " +
                 "a later night can start higher (e.g. 1.5). Fixed when the stone is thrown; never applied to the dawn sweep.")]
        [SerializeField, Min(1f)] private float firstHourMultiplier = 1f;
        [Tooltip("HOUR: each hour after the first adds this: 1 / 0.5 → hour 1 ×1, hour 2 ×1.5, hour 3 ×2… " +
                 "(Moved here from the Scoring asset, R2.)")]
        [SerializeField, Min(0f)] private float hourMultiplierStep = 0.5f;
        [Tooltip("How long a placement round lasts when nothing can be placed (empty shelf, no valid socket): " +
                 "long enough to watch the refill land on the pile.")]
        [SerializeField, Min(0f)] private float refillPauseSeconds = 1f;

        [Header("Tower (TowerBuilder)")]
        [Tooltip("How many slices tall tonight's tower is (one per 1.6 units above the bottom piece): more = a longer climb. " +
                 "Which slices fill it is the player's choice in the day phase, from what the Campaign has unlocked.")]
        [SerializeField, Min(1)] private int towerHeight = 2;

        public string Id => id;
        public int TowerHeight => towerHeight;
        public RobotDefinition Robot => robot;
        public WallMaterialDefinition Wall => wall;
        public float SpawnInterval => spawnInterval;
        public IReadOnlyList<int> Thresholds => thresholds;
        public float FirstHourMultiplier => firstHourMultiplier;
        public float HourMultiplierStep => hourMultiplierStep;
        public float RefillPauseSeconds => refillPauseSeconds;
    }
}
