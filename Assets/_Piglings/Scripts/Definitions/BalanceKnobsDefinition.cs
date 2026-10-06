using UnityEngine;

namespace Piglings.Definitions
{
    /// <summary>
    /// Global tuning knobs over every night (M11.T3): one place to make the whole game faster or slower while balancing,
    /// without editing each night. 1 = as the nights are authored. Every balance-log row records the values in force, so
    /// runs at different settings stay comparable. On NightSession ▸ Balance Knobs; unassigned = 1 / 1.
    /// </summary>
    [CreateAssetMenu(menuName = "Piglings/Balance Knobs", fileName = "BalanceKnobs")]
    public sealed class BalanceKnobsDefinition : ScriptableObject
    {
        [Tooltip("Every wolf climbs this much faster (2 = twice as fast), on top of the night's robot and wall.")]
        [SerializeField, Min(0.05f)] private float climbSpeedMultiplier = 1f;
        [Tooltip("Wolves spawn this much more often (2 = twice as many: half the night's spawn interval).")]
        [SerializeField, Min(0.05f)] private float spawnRateMultiplier = 1f;

        public float ClimbSpeedMultiplier => climbSpeedMultiplier;
        public float SpawnRateMultiplier => spawnRateMultiplier;
    }
}
